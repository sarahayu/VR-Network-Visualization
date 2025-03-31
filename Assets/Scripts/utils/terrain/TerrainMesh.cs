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
    public class TerrainMesh
    {
        float _meshSize;
        float _meshHeight;
        float _curvatureRadius;
        bool _useNormalMap; // unused for now

        Mesh _mesh;
        public Mesh Mesh { get { return _mesh; } }

        public TerrainMesh(float meshSize, float heightScale, float curvatureRadius, bool useNormalMap)
        {
            _meshSize = meshSize;
            _meshHeight = heightScale * meshSize;
            _curvatureRadius = curvatureRadius;
            _useNormalMap = useNormalMap;
        }

        public void Generate(MinimapContext networkContext, HeightMap heightMap, FlatMesh flatMesh)
        {
            _mesh = CreateMeshFromTriangles(networkContext, flatMesh.Mesh.Triangles, heightMap, _meshHeight, _meshSize, _curvatureRadius, _useNormalMap);
        }

        // assume origin to be a point at xz-plane origin minus some y
        static Vector3 flatToRoundCoords(Vector3 flatCoord, Vector3 origin)
        {
            var newCoord = flatCoord;
            float origY = flatCoord.y;
            newCoord.y = 0;
            var ray = newCoord - origin;
            ray.Normalize();
            ray *= -origin.y;
            ray *= 1 + origY / -origin.y;
            ray.y += origin.y;

            return ray;
        }

        // public Vector2 LocalToTexPos(Vector3 localPos)
        // {
        //     localPos.y += _radius;
        //     localPos.Normalize();
        //     localPos.x *= _radius / localPos.y;
        //     localPos.z *= _radius / localPos.y;
        //     float minBoundX = (_meshWidth - 1) / -2f, maxBoundX = (_meshWidth - 1) / 2f,
        //         minBoundZ = (_meshSize - 1) / -2f, maxBoundZ = (_meshSize - 1) / 2f;

        //     return new Vector2(
        //         MathUtil.rlerp(localPos.x, minBoundX, maxBoundX),
        //         MathUtil.rlerp(localPos.z, minBoundZ, maxBoundZ)
        //         );
        // }

        static Mesh CreateMeshFromTriangles(MinimapContext networkContext, ICollection<Triangle> triangles, HeightMap heightMap,
            float meshHeight, float meshSize, float curvatureRadius, bool useNormalMap)
        {
            List<Vector3> vertices = new List<Vector3>(triangles.Count * 3);
            List<int> indices = new List<int>(triangles.Count * 3);
            List<Vector2> uvs = new List<Vector2>(triangles.Count * 3);

            var vecOrigin = new Vector3(0, -curvatureRadius, 0);

            int i = 0;
            foreach (var triangle in triangles)
            {
                Vertex p0 = triangle.GetVertex(0), p1 = triangle.GetVertex(1), p2 = triangle.GetVertex(2);
                float x0 = (float)p0.x, y0 = (float)p0.y,
                    x1 = (float)p1.x, y1 = (float)p1.y,
                    x2 = (float)p2.x, y2 = (float)p2.y;

                var vx0 = x0 * meshSize;
                var vx1 = x1 * meshSize;
                var vx2 = x2 * meshSize;

                var vy0 = meshHeight * heightMap.MaxWeightAt(networkContext, x0, y0);
                var vy1 = meshHeight * heightMap.MaxWeightAt(networkContext, x1, y1);
                var vy2 = meshHeight * heightMap.MaxWeightAt(networkContext, x2, y2);

                var vz0 = y0 * meshSize;
                var vz1 = y1 * meshSize;
                var vz2 = y2 * meshSize;

                var vf0 = new Vector3(vx0, vy0, vz0);
                var vf1 = new Vector3(vx1, vy1, vz1);
                var vf2 = new Vector3(vx2, vy2, vz2);

                vertices.Add(flatToRoundCoords(vf0, vecOrigin));
                vertices.Add(flatToRoundCoords(vf1, vecOrigin));
                vertices.Add(flatToRoundCoords(vf2, vecOrigin));

                indices.Add(i * 3);
                indices.Add(i * 3 + 2); // Changes order
                indices.Add(i * 3 + 1);

                uvs.Add(new Vector2((float)(x0 / 2 + 0.5), (float)(y0 / 2 + 0.5)));
                uvs.Add(new Vector2((float)(x1 / 2 + 0.5), (float)(y1 / 2 + 0.5)));
                uvs.Add(new Vector2((float)(x2 / 2 + 0.5), (float)(y2 / 2 + 0.5)));
                i++;
            }

            Mesh mesh = new Mesh();
            mesh.subMeshCount = 1;
            mesh.SetVertices(vertices);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            mesh.SetUVs(0, uvs);

            if (useNormalMap)
                MeshUtil.FlattenNormals(mesh, vecOrigin);
            else
                mesh.RecalculateNormals();
            return mesh;
        }
    }
}