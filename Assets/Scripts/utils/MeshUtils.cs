/**
 * File Description: Utility functions for general mesh-making
 **/

using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TriangleNet.Meshing.Algorithm;
using TriangleNet.Topology;

namespace VidiGraph
{
    public static class MeshUtil
    {
        public static void FlattenNormals(Mesh mesh, Vector3 origin)
        {
            List<Vector3> normals = new List<Vector3>(mesh.vertices.Length);

            foreach (var vert in mesh.vertices)
            {
                var normal = vert - origin;
                normal.Normalize();
                normals.Add(normal);
            }
            mesh.SetNormals(normals);
        }

        public static void PopulateCirclePoints(List<Vertex> points, float rad, int subdivide)
        {
            var v3Points = new List<Vector3>();
            MathUtils.PopulateSunflowerPoints(v3Points, rad, subdivide, 2);

            foreach (var point in v3Points)
                points.Add(new Vertex(point.x, point.z));
        }
    }
}