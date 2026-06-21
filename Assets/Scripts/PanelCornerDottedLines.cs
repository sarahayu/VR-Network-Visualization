using UnityEngine;

public class PanelCornerDottedLines : MonoBehaviour
{
    [SerializeField] private Transform anchor;
    [SerializeField] private Transform[] panelCorners;
    [SerializeField] private Material dottedLineMaterial;
    [SerializeField] private float lineWidth = 0.005f;
    [SerializeField] private float textureTilingPerMeter = 12f;

    private LineRenderer[] lines;

    void Awake()
    {
        lines = new LineRenderer[panelCorners.Length];

        for (int i = 0; i < panelCorners.Length; i++)
        {
            GameObject lineObject = new GameObject($"Panel Corner Dotted Line {i}");
            lineObject.transform.SetParent(transform, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.material = dottedLineMaterial;
            line.widthMultiplier = lineWidth;
            line.textureMode = LineTextureMode.Tile;
            line.alignment = LineAlignment.View;

            lines[i] = line;
        }
    }

    void LateUpdate()
    {
        if (anchor == null)
            return;

        for (int i = 0; i < lines.Length; i++)
        {
            if (panelCorners[i] == null)
                continue;

            Vector3 start = anchor.position;
            Vector3 end = panelCorners[i].position;

            lines[i].SetPosition(0, start);
            lines[i].SetPosition(1, end);

            float distance = Vector3.Distance(start, end);
            lines[i].material.mainTextureScale = new Vector2(distance * textureTilingPerMeter, 1f);
        }
    }
}