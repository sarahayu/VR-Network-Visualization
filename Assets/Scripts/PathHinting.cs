using System.Collections.Generic;
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
    [Range(0, 12)]
    public int cornerVertices;
    [Min(0.01f)]
    public float textureRepeatDistance = 1f;

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

        List<Vector3> centerline = BuildCenterline();

        Vector3[] vertices = new Vector3[centerline.Count * 4];
        Vector2[] uvs = new Vector2[centerline.Count * 4];
        Color[] colors = new Color[centerline.Count * 4];
        int segmentCount = connectEnd ? centerline.Count : centerline.Count - 1;
        int capTriangleCount = connectEnd ? 0 : 12;
        int[] triangles = new int[segmentCount * 24 + capTriangleCount];
        float distanceAlongPath = 0f;

        for (int i = 0; i < centerline.Count; i++)
        {
            if (i > 0)
                distanceAlongPath += Vector3.Distance(centerline[i - 1], centerline[i]);

            Vector3 current = centerline[i];
            Vector3 forward = GetForward(centerline, i);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            Vector3 left = current - right * width * 0.5f;
            Vector3 rightEdge = current + right * width * 0.5f;
            Vector3 depth = Vector3.down * thickness;

            vertices[i * 4] = transform.InverseTransformPoint(left);
            vertices[i * 4 + 1] = transform.InverseTransformPoint(rightEdge);
            vertices[i * 4 + 2] = transform.InverseTransformPoint(left + depth);
            vertices[i * 4 + 3] = transform.InverseTransformPoint(rightEdge + depth);

            float uvY = distanceAlongPath / textureRepeatDistance;
            uvs[i * 4] = new Vector2(0, uvY);
            uvs[i * 4 + 1] = new Vector2(1, uvY);
            uvs[i * 4 + 2] = new Vector2(0, uvY);
            uvs[i * 4 + 3] = new Vector2(1, uvY);

            colors[i * 4] = Color.white;
            colors[i * 4 + 1] = Color.white;
            colors[i * 4 + 2] = Color.white;
            colors[i * 4 + 3] = Color.white;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = colors;
        int t = 0;

        for (int i = 0; i < segmentCount; i++)
            AddSegmentTriangles(i, centerline.Count, triangles, ref t);

        if (!connectEnd)
            AddEndCaps(centerline.Count, triangles, ref t);

        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
    }

    List<Vector3> BuildCenterline()
    {
        List<Vector3> centerline = new List<Vector3>();

        if (cornerVertices <= 0)
        {
            for (int i = 0; i < points.Length; i++)
                centerline.Add(GetAdjustedPoint(points[i].position));

            return centerline;
        }

        if (!connectEnd)
            centerline.Add(GetAdjustedPoint(points[0].position));

        int start = connectEnd ? 0 : 1;
        int end = connectEnd ? points.Length : points.Length - 1;

        for (int i = start; i < end; i++)
        {
            int previousIndex = (i - 1 + points.Length) % points.Length;
            int nextIndex = (i + 1) % points.Length;

            Vector3 previous = points[previousIndex].position;
            Vector3 current = points[i].position;
            Vector3 next = points[nextIndex].position;

            Vector3 toPrevious = previous - current;
            Vector3 toNext = next - current;
            float cornerDistance = Mathf.Min(toPrevious.magnitude, toNext.magnitude) * 0.35f;

            if (cornerDistance <= 0.0001f)
            {
                centerline.Add(GetAdjustedPoint(current));
                continue;
            }

            Vector3 cornerStart = current + toPrevious.normalized * cornerDistance;
            Vector3 cornerEnd = current + toNext.normalized * cornerDistance;

            centerline.Add(GetAdjustedPoint(cornerStart));

            for (int j = 1; j <= cornerVertices; j++)
            {
                float t = j / (cornerVertices + 1f);
                centerline.Add(GetAdjustedPoint(GetQuadraticBezierPoint(cornerStart, current, cornerEnd, t)));
            }

            centerline.Add(GetAdjustedPoint(cornerEnd));
        }

        if (!connectEnd)
            centerline.Add(GetAdjustedPoint(points[points.Length - 1].position));

        return centerline;
    }

    Vector3 GetForward(List<Vector3> centerline, int index)
    {
        Vector3 forward;

        if (connectEnd)
        {
            int previous = (index - 1 + centerline.Count) % centerline.Count;
            int next = (index + 1) % centerline.Count;
            forward = centerline[next] - centerline[previous];
        }
        else if (index == 0)
            forward = centerline[index + 1] - centerline[index];
        else if (index == centerline.Count - 1)
            forward = centerline[index] - centerline[index - 1];
        else
            forward = centerline[index + 1] - centerline[index - 1];

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            return Vector3.forward;

        return forward.normalized;
    }

    Vector3 GetQuadraticBezierPoint(Vector3 start, Vector3 control, Vector3 end, float t)
    {
        float oneMinusT = 1f - t;
        return oneMinusT * oneMinusT * start + 2f * oneMinusT * t * control + t * t * end;
    }

    Vector3 GetAdjustedPoint(Vector3 point)
    {
        return point + Vector3.up * heightOffset + GetSignedPositionOffset(point);
    }

    void AddSegmentTriangles(int segment, int pointCount, int[] triangles, ref int t)
    {
        int topLeft0 = segment * 4;
        int topRight0 = segment * 4 + 1;
        int bottomLeft0 = segment * 4 + 2;
        int bottomRight0 = segment * 4 + 3;
        int next = (segment + 1) % pointCount;
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

    void AddEndCaps(int pointCount, int[] triangles, ref int t)
    {
        int last = (pointCount - 1) * 4;

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
