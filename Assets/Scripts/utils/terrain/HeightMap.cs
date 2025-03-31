using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TriangleNet.Meshing.Algorithm;
using TriangleNet.Topology;

namespace VidiGraph
{
    public class HeightMap
    {
        float _falloffDistance;
        AnimationCurve _falloffShapeFunc;
        AnimationCurve _peakHeightFunc;
        AnimationCurve _slackFunc;

        int _maxLinkWeight = -1;
        int _maxNodeSize = -1;

        public HeightMap(float falloffDistance, AnimationCurve falloffShapeFunc, AnimationCurve peakHeightFunc, AnimationCurve slackFunc)
        {
            _falloffDistance = falloffDistance;
            _falloffShapeFunc = falloffShapeFunc;
            _peakHeightFunc = peakHeightFunc;
            _slackFunc = slackFunc;
        }

        public void CalcMaxes(MinimapContext networkContext)
        {
            _maxLinkWeight = -1;
            _maxNodeSize = -1;

            foreach (var link in networkContext.Links.Values)
                if (link.Weight > _maxLinkWeight) _maxLinkWeight = link.Weight;

            foreach (var node in networkContext.CommunityNodes.Values)
                if (node.Size > _maxNodeSize) _maxNodeSize = node.Size;
        }

        public Texture2D GenerateTextureHeight(MinimapContext networkContext, FlatMesh flatMesh, int resX, int resY)
        {
            Material glMaterial;
            RenderTexture renderTexture;

            var precalcHeights = new Dictionary<Tuple<double, double>, float>();
            foreach (var point in flatMesh.FlatPoints)
            {
                precalcHeights[new Tuple<double, double>(point.x, point.y)]
                    = MaxWeightAt(networkContext, (float)point.x, (float)point.y);
            }

            // setup

            var texture = new Texture2D(resX, resY);
            texture.wrapMode = TextureWrapMode.Clamp;

            glMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
            glMaterial.hideFlags = HideFlags.HideAndDontSave;
            glMaterial.shader.hideFlags = HideFlags.HideAndDontSave;

            renderTexture = RenderTexture.GetTemporary(resX, resY);

            RenderTexture.active = renderTexture;

            // end setup

            // draw

            GL.Clear(false, true, Color.black);

            glMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, resX, resY, 0);
            GL.Begin(GL.TRIANGLES);
            // GL.wireframe = true;

            foreach (var triangle in flatMesh.Mesh.Triangles)
            {
                Vertex p0 = triangle.GetVertex(0),
                    p1 = triangle.GetVertex(1),
                    p2 = triangle.GetVertex(2);
                double x0 = p0.x, y0 = p0.y,
                    x1 = p1.x, y1 = p1.y,
                    x2 = p2.x, y2 = p2.y;

                GL.Color(Color.Lerp(Color.black, Color.white,
                    precalcHeights[new Tuple<double, double>(x0, y0)]));
                GL.Vertex3((float)((x0 / 2 + 0.5f) * resX), resY - (float)((y0 / 2 + 0.5f) * resY), 0);

                GL.Color(Color.Lerp(Color.black, Color.white,
                    precalcHeights[new Tuple<double, double>(x1, y1)]));
                GL.Vertex3((float)((x1 / 2 + 0.5f) * resX), resY - (float)((y1 / 2 + 0.5f) * resY), 0);

                GL.Color(Color.Lerp(Color.black, Color.white,
                    precalcHeights[new Tuple<double, double>(x1, y1)]));
                GL.Vertex3((float)((x2 / 2 + 0.5f) * resX), resY - (float)((y2 / 2 + 0.5f) * resY), 0);
            }

            GL.End();
            GL.PopMatrix();

            texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            texture.Apply();

            RenderTexture.active = null;

            // end draw

            return texture;
        }

        public Texture2D GenerateTextureLines(MinimapContext networkContext, int resX, int resY, float intensity)
        {
            var colorBit = new float[resX * resY];

            for (int y = 0; y < resY; y++)
                for (int x = 0; x < resX; x++)
                {
                    colorBit[y * resX + x] = 0f;
                }


            foreach (var linkPair in networkContext.Links)
            {
                var linkID = linkPair.Key;
                var link = linkPair.Value;

                var source = linkID.Item1;
                var target = linkID.Item2;

                var weight = link.Weight;

                var first = networkContext.CommunityNodes[source];
                var second = networkContext.CommunityNodes[target];

                int x1 = (int)Mathf.Floor((first.Position.x + 1) / 2 * (resX - 1)),
                    y1 = (int)Mathf.Floor((first.Position.y + 1) / 2 * (resY - 1)),
                    size1 = first.Size,

                    x2 = (int)Mathf.Floor((second.Position.x + 1) / 2 * (resX - 1)),
                    y2 = (int)Mathf.Floor((second.Position.y + 1) / 2 * (resY - 1)),
                    size2 = second.Size;

                int dist_x = Math.Abs(x1 - x2), dist_y = Math.Abs(y1 - y2);
                var dist = Mathf.Sqrt(dist_x * dist_x + dist_y * dist_y);
                float vx = (x2 - x1) / dist, vy = (y2 - y1) / dist;
                // var minheight = Math.Min(size1, size2);
                // var minridge = ridgeFunc(minheight, minheight, weight, 0);

                if (dist_x > dist_y)
                {
                    for (var i = 0; i < dist_x; i++)
                    {
                        int dx = vx > 0 ? i : -i, dy = (int)Mathf.Floor((float)(y2 - y1) / dist_x * i);
                        colorBit[(y1 + dy) * resX + x1 + dx] = intensity;
                    }
                }
                else
                {
                    for (var i = 0; i < dist_y; i++)
                    {
                        int dx = (int)Mathf.Floor((float)(x2 - x1) / dist_y * i), dy = vy > 0 ? i : -i;
                        colorBit[(y1 + dy) * resX + x1 + dx] = intensity;
                    }
                }
            }

            float[] res = new float[resX * resX];

            MathUtils.gaussBlur_4(colorBit, res, resX, resY, 1);

            var colors = new Color[resX * resY];

            for (int y = 0; y < resY; y++)
                for (int x = 0; x < resX; x++)
                {
                    colors[y * resX + x] = Color.Lerp(Color.black, Color.white, res[y * resY + x]);
                }

            var texture = new Texture2D(resX, resY);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels(colors);
            texture.Apply();

            return texture;
        }

        float ridgeFunc(float size1, float size2, float weight, float offset)
        {
            float lerpedHeight1 = _peakHeightFunc.Evaluate((size1) / (_maxNodeSize));
            float lerpedHeight2 = _peakHeightFunc.Evaluate((size2) / (_maxNodeSize));
            float relWeight = MathUtils.RelWeight(weight, size1, size2);
            return Mathf.Max(0.01f, Mathf.Lerp(0, 1, MathUtils.clerp(lerpedHeight1, lerpedHeight2, 1 - _slackFunc.Evaluate(relWeight), offset)));
        }

        public float GetRadiusFromNodeSize(float size)
        {
            return size / _maxNodeSize * _falloffDistance;
        }

        // x \in [-1, 1], y \in [-1, 1]
        public float MaxWeightAt(MinimapContext networkContext, float x, float y)
        {
            var maxWeight = 0f;
            foreach (var linkPair in networkContext.Links)
            {
                var linkID = linkPair.Key;
                var link = linkPair.Value;
                var source = linkID.Item1;
                var target = linkID.Item2;
                var weight = link.Weight;
                var first = networkContext.CommunityNodes[source];
                var second = networkContext.CommunityNodes[target];
                float x1 = first.Position.x, y1 = first.Position.y, size1 = first.Size,
                    x2 = second.Position.x, y2 = second.Position.y, size2 = second.Size;
                float len_x = Mathf.Abs(x1 - x2), len_y = Mathf.Abs(y1 - y2);
                var len_sq = len_x * len_x + len_y * len_y;

                var w = MathUtils.proj_factor(x, y, x1, y1, x2, y2);

                var heightAtPoint = Math.Max(0, ridgeFunc(size1, size2, weight, Math.Min(1, Math.Max(0, w))));
                var heightAt1 = ridgeFunc(size1, size2, weight, 0);
                var heightAt2 = ridgeFunc(size1, size2, weight, 1);

                var line_dist = MathUtils.LineDistSq(x, y, x1, y1, x2, y2);
                var param = line_dist.param;
                var distline_sq = line_dist.dist_sq;

                float maxHeightPoint = heightAt1;
                float relWeight;
                // TODO divide by zero with sizes = 1
                if (param < 0)
                {
                    var dist = Mathf.Pow(MathUtils.distSq(x, y, x1, y1), 0.5f);
                    relWeight = _falloffShapeFunc.Evaluate(Math.Max(0, 1 - dist / _falloffDistance * (_maxNodeSize) / (size1))) * heightAt1;
                }
                else if (param > 1)
                {
                    var dist = Mathf.Pow(MathUtils.distSq(x, y, x2, y2), 0.5f);
                    relWeight = _falloffShapeFunc.Evaluate(Math.Max(0, 1 - dist / _falloffDistance * (_maxNodeSize) / (size2))) * heightAt2;
                }
                else
                {
                    var dist = Mathf.Pow(distline_sq, 0.5f);
                    relWeight = _falloffShapeFunc.Evaluate(Math.Max(0, 1 - dist / _falloffDistance * (_maxNodeSize) / (Math.Min(size1, size2)))) * heightAtPoint;
                }

                if (relWeight > maxWeight) maxWeight = relWeight;
            }

            // TODO optimize, only check isolated nodes (nodes without links since they would have been looped above)
            foreach (var node in networkContext.CommunityNodes.Values)
            {
                float x2 = node.Position.x, y2 = node.Position.y, size2 = node.Size;

                var dist = Mathf.Pow(Mathf.Pow(x2 - x, 2) + Mathf.Pow(y2 - y, 2), 0.5f);
                float maxHeightPoint = ridgeFunc(size2, size2, _maxLinkWeight, 0);
                float relWeight = _falloffShapeFunc.Evaluate(Math.Max(0, 1 - dist / _falloffDistance/*  / maxHeightPoint */)) * maxHeightPoint;

                if (relWeight > maxWeight) maxWeight = relWeight;
            }

            return maxWeight;
        }
    }
}