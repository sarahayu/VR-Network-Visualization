using System.Collections;
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
        ComputeBuffer _inSplineData;
        ComputeBuffer _inSplineSegmentData;
        ComputeBuffer _inSplineControlPointData;
        ComputeBuffer _outSampleControlPointData;

        /*
         * Data Sources for Compute Shader Buffers
         */
        List<SplineData> _splines;
        List<SplineSegmentData> _splineSegments;
        List<SplineControlPointData> _splineControlPoints;

        ComputeShader _batchComputeShader;
        Material _splineMaterial;

        BundledNetworkRenderer.BundledNetworkSettings _settings;

        public void Initialize(ComputeShader computeShader, Material material, BundledNetworkRenderer.BundledNetworkSettings settings)
        {
            _batchComputeShader = computeShader;
            _splineMaterial = material;
            _settings = settings;

            // Configure the spline compute shader
            computeShader.SetVector("COLOR_HIGHLIGHT", _settings.LinkHighlightColor);
            computeShader.SetVector("COLOR_FOCUS", _settings.LinkFocusColor);
            computeShader.SetFloat("COLOR_MINIMUM_ALPHA", _settings.LinkMinimumAlpha);
            computeShader.SetFloat("COLOR_NORMAL_ALPHA_FACTOR", _settings.LinkNormalAlphaFactor);
            computeShader.SetFloat("COLOR_CONTEXT_ALPHA_FACTOR", _settings.LinkContextAlphaFactor);
            computeShader.SetFloat("COLOR_FOCUS2CONTEXT_ALPHA_FACTOR", _settings.LinkContext2FocusAlphaFactor);
        }

        public void PrepareBuffers(NetworkDataStructure networkData, NetworkContext3D networkContext, Dictionary<int, List<Vector3>> controlPoints)
        {
            // Initialize Compute Shader data
            _splines = new List<SplineData>();
            _splineSegments = new List<SplineSegmentData>();
            _splineControlPoints = new List<SplineControlPointData>();

            uint splineSegmentCount = 0;
            uint splineControlPointCount = 0;
            uint splineSampleCount = 0;

            uint splineIdx = 0;
            foreach (var link in networkData.Links)
            {
                /*
                 * Add Compute Shader data
                 */

                var cp = controlPoints[link.ID];
                int NumSegments = cp.Count + BSplineDegree - 2; //NumControlPoints + Degree - 2 (First/Last Point)
                Color sourceColor = link.SourceNode.ColorParsed;
                Color targetColor = link.TargetNode.ColorParsed;

                Vector3 startPosition = cp[0];
                Vector3 endPosition = cp[cp.Count - 1];
                uint linkState = (uint)networkContext.Links[link.ID].State;
                SplineData spline = new SplineData(splineIdx++, (uint)NumSegments, splineSegmentCount, (uint)(NumSegments * BSplineSamplesPerSegment),
                    splineSampleCount, startPosition, endPosition, sourceColor, targetColor, linkState);
                _splines.Add(spline);

                // Add all segments of this spline
                for (int i = 0; i < NumSegments; i++)
                {
                    _splineSegments.Add(new SplineSegmentData(
                        spline.Idx,
                        (uint)(splineControlPointCount + i),
                        (uint)BSplineSamplesPerSegment,
                        (uint)(splineSegmentCount * BSplineSamplesPerSegment)
                        ));

                    splineSampleCount += (uint)BSplineSamplesPerSegment;
                    splineSegmentCount += 1;
                }

                // Add all control points of this spline
                // We have to add *degree* times the first and last control points to make the spline coincide with its endpoints
                // Remember to add cp0 degree-1 times, because the loop that adds all the points will add the last remaining cp0
                // See: https://web.mit.edu/hyperbook/Patrikalakis-Maekawa-Cho/node17.html
                for (int i = 0; i < BSplineDegree - 1; i++)
                {
                    _splineControlPoints.Add(new SplineControlPointData(cp[0]));
                    splineControlPointCount += 1;
                }
                for (int i = 0; i < cp.Count; i++)
                {
                    _splineControlPoints.Add(new SplineControlPointData(
                            cp[i]
                        ));
                    splineControlPointCount += 1;
                }
                for (int i = 0; i < BSplineDegree - 1; i++)
                {
                    _splineControlPoints.Add(new SplineControlPointData(cp[cp.Count - 1]));
                    splineControlPointCount += 1;
                }
            }

            // Finally, set up buffers and bind them to the shader
            int kernel = _batchComputeShader.FindKernel("CSMain");
            _inSplineData = new ComputeBuffer(_splines.Count, SplineData.size());
            _inSplineControlPointData = new ComputeBuffer(_splineControlPoints.Count, SplineControlPointData.size());
            _inSplineSegmentData = new ComputeBuffer(_splineSegments.Count, SplineSegmentData.size());
            _outSampleControlPointData = new ComputeBuffer((int)splineSampleCount, SplineSamplePointData.size());

            _inSplineData.SetData(_splines);
            _inSplineControlPointData.SetData(_splineControlPoints);
            _inSplineSegmentData.SetData(_splineSegments);

            _batchComputeShader.SetBuffer(kernel, "InSplineData", _inSplineData);
            _batchComputeShader.SetBuffer(kernel, "InSplineControlPointData", _inSplineControlPointData);
            _batchComputeShader.SetBuffer(kernel, "InSplineSegmentData", _inSplineSegmentData);
            _batchComputeShader.SetBuffer(kernel, "OutSamplePointData", _outSampleControlPointData);

            _splineMaterial.SetFloat("_LineWidth", _settings.LinkWidth * networkContext.CurrentTransform.localScale.y);


            // Bind the buffers to the LineRenderer Material
            _splineMaterial.SetBuffer("OutSamplePointData", _outSampleControlPointData);
        }

        public void UpdateBuffers(NetworkDataStructure networkData, NetworkContext3D networkProperties, Dictionary<int, List<Vector3>> controlPoints)
        {
            // Initialize Compute Shader data
            _splineControlPoints = new List<SplineControlPointData>();

            uint splineSegmentCount = 0;
            uint splineControlPointCount = 0;
            uint splineSampleCount = 0;

            int splineIdx = 0;

            foreach (var link in networkData.Links)
            {
                var cp = controlPoints[link.ID];
                int ControlPointCount = cp.Count;
                int NumSegments = ControlPointCount + BSplineDegree - 2; //NumControlPoints + Degree - 2 (First/Last Point)


                /*
                * Add Compute Shader data
                */
                Vector3 startPosition = cp[0];
                Vector3 endPosition = cp[ControlPointCount - 1];
                uint linkState = (uint)networkProperties.Links[link.ID].State;

                // Update spline information, we can preserve colors since their lookup is expensive
                SplineData spline = _splines[splineIdx];
                int OldNumSegments = (int)spline.NumSegments;
                spline.StartPosition = startPosition;
                spline.EndPosition = endPosition;
                spline.LinkState = linkState;
                spline.NumSegments = (uint)NumSegments;
                spline.BeginSplineSegmentIdx = splineSegmentCount;
                spline.NumSamples = (uint)(NumSegments * BSplineSamplesPerSegment);
                spline.BeginSamplePointIdx = splineSampleCount;
                _splines[splineIdx++] = spline;

                // To improve performance, we differentiate between cases where there's the same number of segments and where the number differs
                // For same number of segments, we only update the data without creating now instances
                // For differing number of segments, we delete the old range of segment data and insert the new one in place
                if (NumSegments != OldNumSegments)
                {
                    // Remove old segment data
                    _splineSegments.RemoveRange((int)splineSegmentCount, OldNumSegments);

                    // Add new segment data
                    for (int i = 0; i < NumSegments; i++)
                    {
                        _splineSegments.Insert((int)splineSegmentCount, new SplineSegmentData(
                            spline.Idx,
                            (uint)(splineControlPointCount + i),
                            (uint)BSplineSamplesPerSegment,
                            (uint)(splineSegmentCount * BSplineSamplesPerSegment)
                            ));

                        splineSampleCount += (uint)BSplineSamplesPerSegment;
                        splineSegmentCount += 1;
                    }
                }
                else
                {
                    // Update segment
                    for (int i = 0; i < NumSegments; i++)
                    {
                        SplineSegmentData splineSegment = _splineSegments[(int)splineSegmentCount];
                        splineSegment.SplineIdx = spline.Idx;
                        splineSegment.BeginControlPointIdx = (uint)(splineControlPointCount + i);
                        splineSegment.NumSamples = (uint)BSplineSamplesPerSegment;
                        splineSegment.BeginSamplePointIdx = (uint)(splineSegmentCount * BSplineSamplesPerSegment);
                        _splineSegments[(int)splineSegmentCount] = splineSegment;

                        splineSampleCount += (uint)BSplineSamplesPerSegment;
                        splineSegmentCount += 1;
                    }
                }



                // Add all control points of this spline
                // We have to add *degree* times the first and last control points to make the spline coincide with its endpoints
                // Remember to add cp0 degree-1 times, because the loop that adds all the points will add the last remaining cp0
                // See: https://web.mit.edu/hyperbook/Patrikalakis-Maekawa-Cho/node17.html
                SplineControlPointData[] controlPointsData = new SplineControlPointData[2 * (BSplineDegree - 1) + ControlPointCount];
                for (int i = 0; i < BSplineDegree; i++)
                {
                    controlPointsData[i].Position = cp[0];
                }
                for (int i = 1; i < ControlPointCount - 1; i++)
                {
                    controlPointsData[BSplineDegree - 1 + i].Position = cp[i];
                }
                for (int i = BSplineDegree + (ControlPointCount - 1) - 1; i < controlPointsData.Length; i++)
                {
                    controlPointsData[i].Position = cp[ControlPointCount - 1];
                }
                _splineControlPoints.AddRange(controlPointsData); // AddRange is faster than adding items in a loop
                splineControlPointCount += (uint)controlPointsData.Length;
            }


            // Finally, set up buffers
            _inSplineData.SetData(_splines);
            _inSplineControlPointData.SetData(_splineControlPoints);
            _inSplineSegmentData.SetData(_splineSegments);

            _splineMaterial.SetFloat("_LineWidth", _settings.LinkWidth * networkProperties.CurrentTransform.localScale.y);
        }

        public void Draw()
        {
            // Run the ComputeShader. 1 Thread per segment.
            int kernel = _batchComputeShader.FindKernel("CSMain");
            _batchComputeShader.Dispatch(kernel, _splineSegments.Count / 32, 1, 1);

            Graphics.DrawProcedural(_splineMaterial, new Bounds(Vector3.zero, Vector3.one * 500),
                MeshTopology.Triangles, _outSampleControlPointData.count * 6);
        }
    }

}