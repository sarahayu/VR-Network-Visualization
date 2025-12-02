/*
*
* BSplineShaderWrapper is used by BundledNetworkRenderer to simplify shader operations.
*
*/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class BSplineShaderWrapper
    {
        const int BSplineDegree = 3;
        const int BSplineSamplesPerSegment = 10;

        /*
         * Compute Shader Buffers
         */
        ComputeBuffer _inSplineData = null;
        ComputeBuffer _inSplineSegmentData = null;
        ComputeBuffer _inSplineControlPointData = null;
        ComputeBuffer _outSampleControlPointData = null;

        /*
         * Data Sources for Compute Shader Buffers
         */
        List<SplineData> _splines;
        List<SplineSegmentData> _splineSegments;
        List<SplineControlPointData> _splineControlPoints;

        ComputeShader _batchComputeShader;
        Material _splineMaterial;

        MultiLayoutContext.Settings _contextSettings;

        public void Initialize(ComputeShader computeShader, Material material, MultiLayoutContext context)
        {
            _batchComputeShader = computeShader;
            _splineMaterial = material;
            _contextSettings = context.ContextSettings;
        }

        public void PrepareBuffers(NetworkGlobal networkGlobal, MultiLayoutContext networkContext, Dictionary<int, List<Vector3>> linksToCP)
        {
            InitBufferData(
                networkContext: networkContext,
                linksToCP: linksToCP);

            // Finally, set up buffers and bind them to the shader
            _inSplineData = new ComputeBuffer(_splines.Count, SplineData.size());
            _inSplineControlPointData = new ComputeBuffer(_splineControlPoints.Count, SplineControlPointData.size());
            _inSplineSegmentData = new ComputeBuffer(_splineSegments.Count, SplineSegmentData.size());
            _outSampleControlPointData = new ComputeBuffer(_splineSegments.Count * BSplineSamplesPerSegment, SplineSamplePointData.size());

            _inSplineData.SetData(_splines);
            _inSplineControlPointData.SetData(_splineControlPoints);
            _inSplineSegmentData.SetData(_splineSegments);

            _splineMaterial.SetFloat("_LineWidth", _contextSettings.LinkWidth);
            _splineMaterial.SetBuffer("OutSamplePointData", _outSampleControlPointData);
        }

        public void UpdateBuffers(NetworkGlobal networkGlobal, MultiLayoutContext networkContext,
            HashSet<int> selNodes, Dictionary<int, List<Vector3>> controlPoints)
        {
            UpdateBufferData(
                networkGlobal: networkGlobal,
                networkContext: networkContext,
                controlPoints: controlPoints,
                selNodes: selNodes);

            _inSplineData.SetData(_splines);
            _inSplineControlPointData.SetData(_splineControlPoints);
            _inSplineSegmentData.SetData(_splineSegments);

            _splineMaterial.SetFloat("_LineWidth", _contextSettings.LinkWidth);
        }

        public void DeleteBuffers()
        {
            int kernel = _batchComputeShader.FindKernel("CSMain");

            _splines.Clear();
            _splineControlPoints.Clear();
            _splineSegments.Clear();

            _inSplineData.SetData(_splines);
            _inSplineControlPointData.SetData(_splineControlPoints);
            _inSplineSegmentData.SetData(_splineSegments);

            _batchComputeShader.SetBuffer(kernel, "InSplineData", _inSplineData);
            _batchComputeShader.SetBuffer(kernel, "InSplineControlPointData", _inSplineControlPointData);
            _batchComputeShader.SetBuffer(kernel, "InSplineSegmentData", _inSplineSegmentData);
            _batchComputeShader.SetBuffer(kernel, "OutSamplePointData", _outSampleControlPointData);

            // idk if this is how it's done, but redraw with empty data to remove splines from world
            _batchComputeShader.Dispatch(kernel, Math.Max(1, _splineSegments.Count / 32), 1, 1);
        }

        public void Draw()
        {
            int kernel = _batchComputeShader.FindKernel("CSMain");

            if (_inSplineData == null) return;

            _batchComputeShader.SetBuffer(kernel, "InSplineData", _inSplineData);
            _batchComputeShader.SetBuffer(kernel, "InSplineControlPointData", _inSplineControlPointData);
            _batchComputeShader.SetBuffer(kernel, "InSplineSegmentData", _inSplineSegmentData);
            _batchComputeShader.SetBuffer(kernel, "OutSamplePointData", _outSampleControlPointData);

            _batchComputeShader.Dispatch(kernel, Math.Max(1, _splineSegments.Count / 32), 1, 1);

            // TODO don't render ghost links
            Graphics.DrawProcedural(_splineMaterial, new Bounds(Vector3.zero, Vector3.one * 500),
                MeshTopology.Triangles, _splineSegments.Count * BSplineSamplesPerSegment * 6);
        }

        void InitBufferData(MultiLayoutContext networkContext, Dictionary<int, List<Vector3>> linksToCP)
        {
            // Initialize Compute Shader data
            _splines = new List<SplineData>();
            _splineSegments = new List<SplineSegmentData>();
            _splineControlPoints = new List<SplineControlPointData>();

            uint curSegmentIdx = 0;
            uint curControlPointIndex = 0;
            uint curSplineIdx = 0;

            foreach (var (linkID, linkContext) in networkContext.Links)
            {
                var cp = linksToCP[linkID];

                AddSpline(
                    linkContext: linkContext,
                    cp: cp,
                    curSegmentIdx: curSegmentIdx,
                    splineIdx: curSplineIdx);

                AddSegments(
                    numSegments: ControlPointsToSegmentCount(cp),
                    curSegmentIdx: curSegmentIdx,
                    curControlPointIdx: curControlPointIndex,
                    curSplineIdx: curSplineIdx
                );

                UpdateControlPoints(cp);

                curSegmentIdx += (uint)ControlPointsToSegmentCount(cp);
                curControlPointIndex += (uint)(cp.Count + BSplineDegree * 2 - 2);
                curSplineIdx += 1;
            }
        }

        void AddSpline(MultiLayoutContext.Link linkContext, List<Vector3> cp,
            uint curSegmentIdx, uint splineIdx)
        {
            _splines.Add(new SplineData(
                Idx: splineIdx,
                NumSegments: (uint)ControlPointsToSegmentCount(cp),
                BeginSplineSegmentIdx: curSegmentIdx,
                NumSamples: (uint)(ControlPointsToSegmentCount(cp) * BSplineSamplesPerSegment),
                BeginSamplePointIdx: curSegmentIdx * BSplineSamplesPerSegment,
                StartPosition: cp[0],
                EndPosition: cp[cp.Count - 1],
                StartColorRGBA: linkContext.ColorStart,
                EndColorRGBA: linkContext.ColorEnd,
                LinkType: (uint)LinkType.BundledLink
                ));
        }

        void AddSegments(int numSegments, uint curSegmentIdx, uint curControlPointIdx, uint curSplineIdx)
        {
            // Add all segments of this spline
            for (uint i = 0; i < numSegments; i++)
            {
                _splineSegments.Add(new SplineSegmentData(
                    SplineIdx: curSplineIdx,
                    BeginControlPointIdx: curControlPointIdx + i,
                    NumSamples: BSplineSamplesPerSegment,
                    BeginSamplePointIdx: (curSegmentIdx + i) * BSplineSamplesPerSegment
                    ));
            }
        }

        void UpdateBufferData(NetworkGlobal networkGlobal, MultiLayoutContext networkContext, Dictionary<int, List<Vector3>> controlPoints, HashSet<int> selNodes)
        {
            // TODO account for control points (and segments) increasing
            _splineControlPoints = new List<SplineControlPointData>();

            uint curSegmentIdx = 0;
            uint curControlPointIdx = 0;
            uint curSplineIdx = 0;

            foreach (var (linkID, linkContext) in networkContext.Links)
            {
                networkGlobal.Links[linkID].Dirty = false;
                linkContext.Dirty = false;

                var cp = controlPoints[linkID];

                UpdateSpline(
                    curSplineIdx: curSplineIdx,
                    curSegmentIdx: curSegmentIdx,
                    cp: cp,
                    selNodes: selNodes,
                    linkID: linkID,
                    networkContext: networkContext,
                    networkGlobal: networkGlobal);

                UpdateSegments(
                    curSplineIdx: curSplineIdx,
                    curSegmentIdx: curSegmentIdx,
                    curControlPointIdx: curControlPointIdx,
                    cp: cp);

                UpdateControlPoints(cp);

                curSegmentIdx += (uint)ControlPointsToSegmentCount(cp);
                curControlPointIdx += (uint)(cp.Count + BSplineDegree * 2 - 2);
                curSplineIdx += 1;
            }
        }

        void UpdateSpline(uint curSplineIdx, uint curSegmentIdx,
            List<Vector3> cp, HashSet<int> selNodes, int linkID, MultiLayoutContext networkContext, NetworkGlobal networkGlobal)
        {
            var link = networkGlobal.Links[linkID];
            var contextLink = networkContext.Links[linkID];

            /*
            * Add Compute Shader data
            */
            var spline = _splines[(int)curSplineIdx];
            spline.StartPosition = cp[0];
            spline.EndPosition = cp[cp.Count - 1];
            spline.StartColorRGBA = contextLink.ColorStart;
            spline.EndColorRGBA = contextLink.ColorEnd;

            uint linkType = (uint)LinkType.BundledLink;
            spline.StartColorRGBA.a *= contextLink.Alpha;
            spline.EndColorRGBA.a *= contextLink.Alpha;

            bool hovered = networkGlobal.HoveredNode?.ID == link.SourceNodeID || networkGlobal.HoveredNode?.ID == link.TargetNodeID;
            bool selected = contextLink.Selected;

            var hoverCol = networkContext.ContextSettings.LinkHoverColor;
            var selectCol = networkContext.ContextSettings.LinkSelectColor;

            Color finalStartColor = spline.StartColorRGBA;
            Color finalEndColor = spline.EndColorRGBA;

            if (hovered && selected)
            {
                finalStartColor = Color.Lerp(Color.Lerp(hoverCol, finalStartColor, 0.5f), selectCol, 0.67f);
                finalEndColor = Color.Lerp(Color.Lerp(hoverCol, finalEndColor, 0.5f), selectCol, 0.67f);
            }
            else if (hovered)
            {
                finalStartColor = Color.Lerp(hoverCol, finalStartColor, 0.5f);
                finalEndColor = Color.Lerp(hoverCol, finalEndColor, 0.5f);
            }
            else if (selected)
            {
                finalStartColor = Color.Lerp(selectCol, finalStartColor, 0.5f);
                finalEndColor = Color.Lerp(selectCol, finalEndColor, 0.5f);
            }

            spline.StartColorRGBA = finalStartColor;
            spline.EndColorRGBA = finalEndColor;

            int NumSegments = ControlPointsToSegmentCount(cp); //NumControlPoints + Degree - 2 (First/Last Point)

            spline.LinkType = linkType;
            spline.NumSegments = (uint)NumSegments;
            spline.BeginSplineSegmentIdx = curSegmentIdx;
            spline.NumSamples = (uint)(NumSegments * BSplineSamplesPerSegment);
            spline.BeginSamplePointIdx = curSegmentIdx * BSplineSamplesPerSegment;

            _splines[(int)curSplineIdx] = spline;
        }

        void UpdateSegments(uint curSplineIdx, uint curSegmentIdx, uint curControlPointIdx, List<Vector3> cp)
        {
            int NumSegments = ControlPointsToSegmentCount(cp);
            int OldNumSegments = (int)_splines[(int)curSplineIdx].NumSegments;

            // To improve performance, we differentiate between cases where there's the same number of segments and where the number differs
            // For same number of segments, we only update the data without creating now instances
            // For differing number of segments, we delete the old range of segment data and insert the new one in place
            if (NumSegments != OldNumSegments)
            {
                // Remove old segment data
                _splineSegments.RemoveRange((int)curSegmentIdx, OldNumSegments);

                // Add new segment data
                for (int i = 0; i < NumSegments; i++)
                {
                    int curSegment = (int)(curSegmentIdx + i);

                    _splineSegments.Insert(curSegment, new SplineSegmentData(
                        SplineIdx: (uint)curSplineIdx,
                        BeginControlPointIdx: (uint)(curControlPointIdx + i),
                        NumSamples: BSplineSamplesPerSegment,
                        BeginSamplePointIdx: (uint)(curSegment * BSplineSamplesPerSegment)
                        ));
                }
            }
            else
            {
                // Update segment
                for (int i = 0; i < NumSegments; i++)
                {
                    int curSegment = (int)(curSegmentIdx + i);

                    SplineSegmentData splineSegment = _splineSegments[curSegment];

                    splineSegment.SplineIdx = curSplineIdx;
                    splineSegment.BeginControlPointIdx = (uint)(curControlPointIdx + i);
                    splineSegment.NumSamples = BSplineSamplesPerSegment;
                    splineSegment.BeginSamplePointIdx = (uint)(curSegment * BSplineSamplesPerSegment);

                    _splineSegments[curSegment] = splineSegment;
                }
            }
        }

        void UpdateControlPoints(List<Vector3> cp)
        {
            // Add all control points of this spline
            // We have to add *degree* times the first and last control points to make the spline coincide with its endpoints
            // Remember to add cp0 degree-1 times, because the loop that adds all the points will add the last remaining cp0
            // See: https://web.mit.edu/hyperbook/Patrikalakis-Maekawa-Cho/node17.html
            SplineControlPointData[] controlPointsData = new SplineControlPointData[cp.Count + BSplineDegree * 2 - 2];

            int offset = 0;

            for (int i = 0; i < BSplineDegree - 1; i++, offset++)
            {
                controlPointsData[offset].Position = cp[0];
            }

            for (int i = 0; i < cp.Count; i++, offset++)
            {
                controlPointsData[offset].Position = cp[i];
            }

            for (int i = 0; i < BSplineDegree - 1; i++, offset++)
            {
                controlPointsData[offset].Position = cp[cp.Count - 1];
            }

            _splineControlPoints.AddRange(controlPointsData); // AddRange is faster than adding items in a loop
        }

        static int ControlPointsToSegmentCount(List<Vector3> cp)
        {
            return cp.Count + BSplineDegree - 2;    //NumControlPoints + Degree - 2 (First/Last Point)
        }

    }

}