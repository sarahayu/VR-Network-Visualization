using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class BSplineShaderWrapper
    {
        static readonly int BSplineDegree = 3;
        static readonly int BSplineSamplesPerSegment = 10;

        /*
         * Compute Shader Buffers
         */
        ComputeBuffer InSplineData;
        ComputeBuffer InSplineSegmentData;
        ComputeBuffer InSplineControlPointData;
        ComputeBuffer OutSampleControlPointData;

        /*
         * Data Sources for Compute Shader Buffers
         */
        List<SplineData> Splines;
        List<SplineSegmentData> SplineSegments;
        List<SplineControlPointData> SplineControlPoints;

        ComputeShader computeShader;
        Material material;

        public void Initialize(ComputeShader computeShader, Material material)
        {
            this.computeShader = computeShader;
            this.material = material;
        }

        public void PrepareBuffers(NetworkDataStructure networkData)
        {
            // Initialize Compute Shader data
            Splines = new List<SplineData>();
            SplineSegments = new List<SplineSegmentData>();
            SplineControlPoints = new List<SplineControlPointData>();

            uint splineSegmentCount = 0;
            uint splineControlPointCount = 0;
            uint splineSampleCount = 0;

            uint splineIdx = 0;
            foreach (var link in networkData.links)
            {
                /*
                 * Add Compute Shader data
                 */

                int NumSegments = link.straightenPoints.Count + BSplineDegree - 2; //NumControlPoints + Degree - 2 (First/Last Point)
                Color sourceColor = link.sourceNode.colorParsed;
                Color targetColor = link.targetNode.colorParsed;

                Vector3 startPosition = link.straightenPoints[0];
                Vector3 endPosition = link.straightenPoints[link.straightenPoints.Count - 1];
                uint linkState = (uint)link.state.CurState;
                SplineData spline = new SplineData(splineIdx++, (uint)NumSegments, splineSegmentCount, (uint)(NumSegments * BSplineSamplesPerSegment),
                    splineSampleCount, startPosition, endPosition, sourceColor, targetColor, linkState);
                Splines.Add(spline);

                // Add all segments of this spline
                for (int i = 0; i < NumSegments; i++)
                {
                    SplineSegments.Add(new SplineSegmentData(
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
                    SplineControlPoints.Add(new SplineControlPointData(link.straightenPoints[0]));
                    splineControlPointCount += 1;
                }
                for (int i = 0; i < link.straightenPoints.Count; i++)
                {
                    SplineControlPoints.Add(new SplineControlPointData(
                            link.straightenPoints[i]
                        ));
                    splineControlPointCount += 1;
                }
                for (int i = 0; i < BSplineDegree - 1; i++)
                {
                    SplineControlPoints.Add(new SplineControlPointData(link.straightenPoints[link.straightenPoints.Count - 1]));
                    splineControlPointCount += 1;
                }
            }

            // Finally, set up buffers and bind them to the shader
            int kernel = computeShader.FindKernel("CSMain");
            InSplineData = new ComputeBuffer(Splines.Count, SplineData.size());
            InSplineControlPointData = new ComputeBuffer(SplineControlPoints.Count, SplineControlPointData.size());
            InSplineSegmentData = new ComputeBuffer(SplineSegments.Count, SplineSegmentData.size());
            OutSampleControlPointData = new ComputeBuffer((int)splineSampleCount, SplineSamplePointData.size());

            InSplineData.SetData(Splines);
            InSplineControlPointData.SetData(SplineControlPoints);
            InSplineSegmentData.SetData(SplineSegments);

            computeShader.SetBuffer(kernel, "InSplineData", InSplineData);
            computeShader.SetBuffer(kernel, "InSplineControlPointData", InSplineControlPointData);
            computeShader.SetBuffer(kernel, "InSplineSegmentData", InSplineSegmentData);
            computeShader.SetBuffer(kernel, "OutSamplePointData", OutSampleControlPointData);


            // Bind the buffers to the LineRenderer Material
            material.SetBuffer("OutSamplePointData", OutSampleControlPointData);
        }

        public void UpdateBuffers(NetworkDataStructure networkData)
        {
            // Initialize Compute Shader data
            SplineControlPoints = new List<SplineControlPointData>();

            uint splineSegmentCount = 0;
            uint splineControlPointCount = 0;
            uint splineSampleCount = 0;

            int splineIdx = 0;

            foreach (var link in networkData.links)
            {
                int ControlPointCount = link.straightenPoints.Count;
                int NumSegments = ControlPointCount + BSplineDegree - 2; //NumControlPoints + Degree - 2 (First/Last Point)


                /*
                * Add Compute Shader data
                */
                Vector3 startPosition = link.straightenPoints[0];
                Vector3 endPosition = link.straightenPoints[ControlPointCount - 1];
                uint linkState = (uint)link.state.CurState;

                // Update spline information, we can preserve colors since their lookup is expensive
                SplineData spline = Splines[splineIdx];
                int OldNumSegments = (int)spline.NumSegments;
                spline.StartPosition = startPosition;
                spline.EndPosition = endPosition;
                spline.LinkState = linkState;
                spline.NumSegments = (uint)NumSegments;
                spline.BeginSplineSegmentIdx = splineSegmentCount;
                spline.NumSamples = (uint)(NumSegments * BSplineSamplesPerSegment);
                spline.BeginSamplePointIdx = splineSampleCount;
                Splines[splineIdx++] = spline;

                // To improve performance, we differentiate between cases where there's the same number of segments and where the number differs
                // For same number of segments, we only update the data without creating now instances
                // For differing number of segments, we delete the old range of segment data and insert the new one in place
                if (NumSegments != OldNumSegments)
                {
                    // Remove old segment data
                    SplineSegments.RemoveRange((int)splineSegmentCount, OldNumSegments);

                    // Add new segment data
                    for (int i = 0; i < NumSegments; i++)
                    {
                        SplineSegments.Insert((int)splineSegmentCount, new SplineSegmentData(
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
                        SplineSegmentData splineSegment = SplineSegments[(int)splineSegmentCount];
                        splineSegment.SplineIdx = spline.Idx;
                        splineSegment.BeginControlPointIdx = (uint)(splineControlPointCount + i);
                        splineSegment.NumSamples = (uint)BSplineSamplesPerSegment;
                        splineSegment.BeginSamplePointIdx = (uint)(splineSegmentCount * BSplineSamplesPerSegment);
                        SplineSegments[(int)splineSegmentCount] = splineSegment;

                        splineSampleCount += (uint)BSplineSamplesPerSegment;
                        splineSegmentCount += 1;
                    }
                }



                // Add all control points of this spline
                // We have to add *degree* times the first and last control points to make the spline coincide with its endpoints
                // Remember to add cp0 degree-1 times, because the loop that adds all the points will add the last remaining cp0
                // See: https://web.mit.edu/hyperbook/Patrikalakis-Maekawa-Cho/node17.html
                SplineControlPointData[] controlPoints = new SplineControlPointData[2 * (BSplineDegree - 1) + ControlPointCount];
                for (int i = 0; i < BSplineDegree; i++)
                {
                    controlPoints[i].Position = link.straightenPoints[0];
                }
                for (int i = 1; i < ControlPointCount - 1; i++)
                {
                    controlPoints[BSplineDegree - 1 + i].Position = link.straightenPoints[i];
                }
                for (int i = BSplineDegree + (ControlPointCount - 1) - 1; i < controlPoints.Length; i++)
                {
                    controlPoints[i].Position = link.straightenPoints[ControlPointCount - 1];
                }
                SplineControlPoints.AddRange(controlPoints); // AddRange is faster than adding items in a loop
                splineControlPointCount += (uint)controlPoints.Length;
            }


            // Finally, set up buffers
            InSplineData.SetData(Splines);
            InSplineControlPointData.SetData(SplineControlPoints);
            InSplineSegmentData.SetData(SplineSegments);
        }

        public void Draw()
        {
            // Run the ComputeShader. 1 Thread per segment.
            int kernel = computeShader.FindKernel("CSMain");
            computeShader.Dispatch(kernel, SplineSegments.Count / 32, 1, 1);

            Graphics.DrawProcedural(material, new Bounds(Vector3.zero, Vector3.one * 500),
                MeshTopology.Triangles, OutSampleControlPointData.count * 6);
        }
    }

}