using System;
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
            }

            massCenter /= nodes.Count();

            foreach (var node in nodes)
            {
                var dist = Vector3.Distance(massCenter, nodeContexts[node.ID].Position);
                size = Math.Max(dist, size);
            }

        }


        public static Mesh GenerateConvexHull(MultiLayoutContext.Community commProps, IEnumerable<MultiLayoutContext.Node> commNodes, float nodeScale)
        {
            var calc = new ConvexHullCalculator();
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var normals = new List<Vector3>();

            var points = new List<Vector3>();

            foreach (var node in commNodes)
            {
                var nodeRadius = node.Size / 2;
                var buffer = 0.3f;
                var pos = node.Position - commProps.MassCenter;

                var mesh = IcoSphere.Create(radius: nodeRadius + buffer * nodeScale);

                points.AddRange(mesh.vertices.Select(v => v + pos));
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


        public static Mesh GenerateConvexHull(MultiLayoutContext mlContext, float nodeScale)
        {
            var calc = new ConvexHullCalculator();
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var normals = new List<Vector3>();

            var points = new List<Vector3>();

            foreach (var (commID, comm) in mlContext.Communities)
            {
                var nodeRadius = (float)comm.Size;
                var buffer = 1f;
                var pos = comm.MassCenter - mlContext.MassCenter;

                var mesh = IcoSphere.Create(radius: nodeRadius + buffer * nodeScale);

                points.AddRange(mesh.vertices.Select(v => v + pos));
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
                return IcoSphere.Create(1f);
            }
        }
    }
}
