using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TriangleNet.Meshing.Algorithm;
using TriangleNet.Topology;

namespace VidiGraph
{
    public class FlatMesh
    {
        NetworkGlobal _networkGlobal;
        MinimapContext _networkContext;
        float _meshHeight;
        int _subdivideSunflower;
        int _subdivideRidges;

        // x \in [-1, 1], y \in [-1, 1]
        public List<Vertex> FlatPoints;
        public IMesh Mesh;

        public FlatMesh(NetworkGlobal networkGlobal, MinimapContext networkContext,
            int subdivide = -1, int subdivideSunflower = -1, int subdivideRidges = -1)
        {
            _networkGlobal = networkGlobal;
            _networkContext = networkContext;
            FlatPoints = new List<Vertex>();

            if (subdivide == -1)
            {
                _subdivideSunflower = subdivideSunflower;
                _subdivideRidges = subdivideRidges;
            }
            else
            {
                _subdivideSunflower = _subdivideRidges = subdivide;
            }
        }

        public void CalcMeshPoints()
        {
            MeshUtil.PopulateCirclePoints(FlatPoints, 1f, _subdivideSunflower);
            PopulateRidgePoints();

            Mesh = new GenericMesher(new Dwyer()).Triangulate(FlatPoints);
        }

        void PopulateRidgePoints()
        {
            foreach (var node in _networkContext.CommunityNodes.Values)
            {
                FlatPoints.Add(new Vertex(node.Position.x, node.Position.y));
            }

            foreach (var linkPair in _networkContext.Links)
            {
                var linkID = linkPair.Key;
                var link = linkPair.Value;
                var source = linkID.Item1;
                var target = linkID.Item2;
                var weight = link.Weight;

                var first = _networkContext.CommunityNodes[source];
                var second = _networkContext.CommunityNodes[target];
                float x1 = first.Position.x, y1 = first.Position.y, size1 = first.Size,
                    x2 = second.Position.x, y2 = second.Position.y, size2 = second.Size;
                float distX = Mathf.Abs(x2 - x1), distY = Mathf.Abs(y2 - y1);
                float dist = Mathf.Sqrt(distX * distX + distY * distY);

                var dir = new Vertex((x2 - x1) / dist, (y2 - y1) / dist);
                // dir.Normalize();
                // dir = dir / 2;
                float x = x1, y = y1;

                x += (float)dir.x / _subdivideRidges;
                y += (float)dir.y / _subdivideRidges;

                while (Mathf.Abs(x - x1) < distX && Mathf.Abs(y - y1) < distY)
                {
                    FlatPoints.Add(new Vertex(x, y));
                    x += (float)dir.x / _subdivideRidges;
                    y += (float)dir.y / _subdivideRidges;
                }
                // points.Add(new Vertex(x2 * (meshWidth - 1) / graphWidth, y2 * (meshLength - 1) / graphHeight));
            }
        }
    }
}