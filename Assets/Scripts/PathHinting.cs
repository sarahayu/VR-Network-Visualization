using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PathHinting : MonoBehaviour
{
    public Transform[] points;
    public float width = 0.5f;
    public float thickness = 0.03f;
    public float heightOffset = 0.03f;
    public Vector3 positionOffset;
    public bool connectEnd;

    void Start()
    {
        BuildMesh();
    }

    void BuildMesh()
    {
        if (!HasValidPoints())
        {
            GetComponent<MeshFilter>().mesh = null;
            return;
        }

        Mesh mesh = new Mesh();
        mesh.name = "Path Hint";

        Vector3[] vertices = new Vector3[points.Length * 4];
        Vector2[] uvs = new Vector2[points.Length * 4];
        int segmentCount = connectEnd ? points.Length : points.Length - 1;
        int capTriangleCount = connectEnd ? 0 : 12;
        int[] triangles = new int[segmentCount * 24 + capTriangleCount];

        for (int i = 0; i < points.Length; i++)
        {
            Vector3 current = points[i].position + Vector3.up * heightOffset + GetSignedPositionOffset(points[i].position);

            Vector3 forward;

            if (connectEnd)
            {
                int previous = (i - 1 + points.Length) % points.Length;
                int next = (i + 1) % points.Length;
                forward = points[next].position - points[previous].position;
            }
            else if (i == 0)
                forward = points[i + 1].position - points[i].position;
            else if (i == points.Length - 1)
                forward = points[i].position - points[i - 1].position;
            else
                forward = points[i + 1].position - points[i - 1].position;

            forward.y = 0;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            Vector3 left = current - right * width * 0.5f;
            Vector3 rightEdge = current + right * width * 0.5f;
            Vector3 depth = Vector3.down * thickness;

            vertices[i * 4] = transform.InverseTransformPoint(left);
            vertices[i * 4 + 1] = transform.InverseTransformPoint(rightEdge);
            vertices[i * 4 + 2] = transform.InverseTransformPoint(left + depth);
            vertices[i * 4 + 3] = transform.InverseTransformPoint(rightEdge + depth);

            uvs[i * 4] = new Vector2(0, i);
            uvs[i * 4 + 1] = new Vector2(1, i);
            uvs[i * 4 + 2] = new Vector2(0, i);
            uvs[i * 4 + 3] = new Vector2(1, i);
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        int t = 0;

        for (int i = 0; i < segmentCount; i++)
            AddSegmentTriangles(i, triangles, ref t);

        if (!connectEnd)
            AddEndCaps(triangles, ref t);

        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
    }

    void AddSegmentTriangles(int segment, int[] triangles, ref int t)
    {
        int topLeft0 = segment * 4;
        int topRight0 = segment * 4 + 1;
        int bottomLeft0 = segment * 4 + 2;
        int bottomRight0 = segment * 4 + 3;
        int next = (segment + 1) % points.Length;
        int topLeft1 = next * 4;
        int topRight1 = next * 4 + 1;
        int bottomLeft1 = next * 4 + 2;
        int bottomRight1 = next * 4 + 3;

        AddQuad(topLeft0, topLeft1, topRight0, topRight1, triangles, ref t);
        AddQuad(bottomLeft0, bottomRight0, bottomLeft1, bottomRight1, triangles, ref t);
        AddQuad(topLeft0, bottomLeft0, topLeft1, bottomLeft1, triangles, ref t);
        AddQuad(topRight0, topRight1, bottomRight0, bottomRight1, triangles, ref t);
    }

    Vector3 GetSignedPositionOffset(Vector3 point)
    {
        return new Vector3(
            point.x < 0f ? -Mathf.Abs(positionOffset.x) : Mathf.Abs(positionOffset.x),
            point.y < 0f ? -Mathf.Abs(positionOffset.y) : Mathf.Abs(positionOffset.y),
            point.z < 0f ? -Mathf.Abs(positionOffset.z) : Mathf.Abs(positionOffset.z)
        );
    }

    void AddEndCaps(int[] triangles, ref int t)
    {
        int last = (points.Length - 1) * 4;

        AddQuad(0, 1, 2, 3, triangles, ref t);
        AddQuad(last, last + 2, last + 1, last + 3, triangles, ref t);
    }

    void AddQuad(int topLeft, int bottomLeft, int topRight, int bottomRight, int[] triangles, ref int t)
    {
        triangles[t++] = topLeft;
        triangles[t++] = bottomLeft;
        triangles[t++] = topRight;

        triangles[t++] = topRight;
        triangles[t++] = bottomLeft;
        triangles[t++] = bottomRight;
    }

    bool HasValidPoints()
    {
        if (points == null || points.Length < 2)
        {
            Debug.LogError("PathHinting needs at least 2 points.", this);
            return false;
        }

        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == null)
            {
                Debug.LogError($"PathHinting point {i} is not assigned.", this);
                return false;
            }
        }

        return true;
    }
}
