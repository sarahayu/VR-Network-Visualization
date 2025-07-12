using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GK;
using UnityEngine;

namespace VidiGraph
{
    public static class MultiLayoutContextUtils
    {
        public static void ComputeProperties(IEnumerable<Node> nodes, Dictionary<int, MultiLayoutContext.Node> nodeContexts,
            out double mass, out Vector3 massCenter, out float size)
        {
            mass = 0;
            massCenter = Vector3.zero;
            size = 0.1f;

            foreach (var node in nodes)
            {
                mass += node.Degree + 0.01;
                massCenter += nodeContexts[node.ID].Position;
                var dist = Vector3.Distance(massCenter, nodeContexts[node.ID].Position);
                size = Math.Max(dist, size);
            }

            massCenter /= nodes.Count();
        }


        public static Mesh GenerateConvexHull(MultiLayoutContext.Community commProps, IEnumerable<MultiLayoutContext.Node> commNodes)
        {
            var calc = new ConvexHullCalculator();
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var normals = new List<Vector3>();

            var points = new List<Vector3>();

            foreach (var node in commNodes)
            {
                var size = node.Size + 0.03f;
                var pos = node.Position - commProps.MassCenter;

                points.Add(pos + Vector3.forward * size);
                points.Add(pos + Vector3.back * size);
                points.Add(pos + Vector3.up * size);
                points.Add(pos + Vector3.down * size);
                points.Add(pos + Vector3.left * size);
                points.Add(pos + Vector3.right * size);
            }

            try
            {
                calc.GenerateHull(points, true, ref verts, ref tris, ref normals);

                var mesh = new Mesh();
                mesh.SetVertices(verts);
                mesh.SetTriangles(tris, 0);
                mesh.SetNormals(normals);

                return mesh;
            }
            catch (ArgumentException)
            {
                return IcoSphere.Create((float)commProps.Size);
            }
        }
    }
}
