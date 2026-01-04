using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    /// <summary>
    /// Provides procedurally generated meshes for different node shapes
    /// Used for categorical shape encoding in network visualization
    /// </summary>
    public static class NodeShapeMeshes
    {
        private static Dictionary<string, Mesh> _meshCache = new Dictionary<string, Mesh>();

        /// <summary>
        /// Get a mesh for a specific shape type (sphere, cube, or tetrahedron)
        /// Meshes are cached for performance
        /// </summary>
        public static Mesh GetMesh(string shapeName, float size = 0.5f)
        {
            string cacheKey = $"{shapeName}_{size}";

            if (_meshCache.ContainsKey(cacheKey))
            {
                return _meshCache[cacheKey];
            }

            Mesh mesh = shapeName.ToLower() switch
            {
                "sphere" => CreateSphere(size),
                "cube" => CreateCube(size),
                "tetrahedron" => CreateTetrahedron(size),
                _ => CreateSphere(size) // default to sphere
            };

            _meshCache[cacheKey] = mesh;
            return mesh;
        }

        /// <summary>
        /// Create sphere mesh using IcoSphere algorithm
        /// </summary>
        public static Mesh CreateSphere(float radius = 0.5f)
        {
            return IcoSphere.Create(radius, detail: 1);
        }

        /// <summary>
        /// Create cube mesh procedurally
        /// </summary>
        public static Mesh CreateCube(float size = 0.5f)
        {
            Mesh mesh = new Mesh();
            mesh.Clear();

            float s = size;

            // 8 corner vertices (each used by 3 faces, so we need 24 vertices total for proper normals)
            Vector3[] vertices = new Vector3[24]
            {
                // Front face (normal: 0, 0, 1)
                new Vector3(-s, -s, s), new Vector3(s, -s, s), new Vector3(s, s, s), new Vector3(-s, s, s),
                // Back face (normal: 0, 0, -1)
                new Vector3(s, -s, -s), new Vector3(-s, -s, -s), new Vector3(-s, s, -s), new Vector3(s, s, -s),
                // Top face (normal: 0, 1, 0)
                new Vector3(-s, s, s), new Vector3(s, s, s), new Vector3(s, s, -s), new Vector3(-s, s, -s),
                // Bottom face (normal: 0, -1, 0)
                new Vector3(-s, -s, -s), new Vector3(s, -s, -s), new Vector3(s, -s, s), new Vector3(-s, -s, s),
                // Right face (normal: 1, 0, 0)
                new Vector3(s, -s, s), new Vector3(s, -s, -s), new Vector3(s, s, -s), new Vector3(s, s, s),
                // Left face (normal: -1, 0, 0)
                new Vector3(-s, -s, -s), new Vector3(-s, -s, s), new Vector3(-s, s, s), new Vector3(-s, s, -s)
            };

            // 6 faces × 2 triangles per face × 3 vertices per triangle = 36 indices
            int[] triangles = new int[36]
            {
                // Front
                0, 2, 1, 0, 3, 2,
                // Back
                4, 6, 5, 4, 7, 6,
                // Top
                8, 10, 9, 8, 11, 10,
                // Bottom
                12, 14, 13, 12, 15, 14,
                // Right
                16, 18, 17, 16, 19, 18,
                // Left
                20, 22, 21, 20, 23, 22
            };

            // Normals for each face
            Vector3[] normals = new Vector3[24]
            {
                // Front
                Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
                // Back
                Vector3.back, Vector3.back, Vector3.back, Vector3.back,
                // Top
                Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                // Bottom
                Vector3.down, Vector3.down, Vector3.down, Vector3.down,
                // Right
                Vector3.right, Vector3.right, Vector3.right, Vector3.right,
                // Left
                Vector3.left, Vector3.left, Vector3.left, Vector3.left
            };

            // Simple UVs (not critical for solid colors)
            Vector2[] uvs = new Vector2[24];
            for (int i = 0; i < 24; i += 4)
            {
                uvs[i] = new Vector2(0, 0);
                uvs[i + 1] = new Vector2(1, 0);
                uvs[i + 2] = new Vector2(1, 1);
                uvs[i + 3] = new Vector2(0, 1);
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.normals = normals;
            mesh.uv = uvs;

            mesh.RecalculateBounds();
            mesh.Optimize();

            return mesh;
        }

        /// <summary>
        /// Create tetrahedron (4-sided pyramid) mesh procedurally
        /// </summary>
        public static Mesh CreateTetrahedron(float size = 0.5f)
        {
            Mesh mesh = new Mesh();
            mesh.Clear();

            float s = size * 1.5f; // Scale up slightly to match visual size with sphere/cube

            // 4 vertices forming a regular tetrahedron centered at origin
            // Using tetrahedral symmetry for balanced appearance
            float a = s / Mathf.Sqrt(3f);
            Vector3[] baseVertices = new Vector3[4]
            {
                new Vector3(0, a * Mathf.Sqrt(2f/3f), 0),              // Top vertex
                new Vector3(-a / Mathf.Sqrt(3f), -a / (2f * Mathf.Sqrt(3f)), a / 2f),   // Base vertex 1
                new Vector3(-a / Mathf.Sqrt(3f), -a / (2f * Mathf.Sqrt(3f)), -a / 2f),  // Base vertex 2
                new Vector3(a * Mathf.Sqrt(2f/3f), -a / (2f * Mathf.Sqrt(3f)), 0)       // Base vertex 3
            };

            // 12 vertices (3 per face × 4 faces) for proper face normals
            Vector3[] vertices = new Vector3[12]
            {
                // Face 1: Top-Base1-Base2
                baseVertices[0], baseVertices[1], baseVertices[2],
                // Face 2: Top-Base2-Base3
                baseVertices[0], baseVertices[2], baseVertices[3],
                // Face 3: Top-Base3-Base1
                baseVertices[0], baseVertices[3], baseVertices[1],
                // Face 4: Base1-Base3-Base2 (bottom)
                baseVertices[1], baseVertices[3], baseVertices[2]
            };

            // 4 faces × 3 vertices per triangle = 12 indices
            int[] triangles = new int[12]
            {
                0, 1, 2,    // Face 1
                3, 4, 5,    // Face 2
                6, 7, 8,    // Face 3
                9, 10, 11   // Face 4
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.Optimize();

            return mesh;
        }

        /// <summary>
        /// Clear the mesh cache (call if memory becomes an issue)
        /// </summary>
        public static void ClearCache()
        {
            _meshCache.Clear();
        }
    }
}
