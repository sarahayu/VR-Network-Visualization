using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VidiGraph;

public class NetworkRenderer : MonoBehaviour
{
    public GameObject nodePrefab;

    [Range(0.001f, 1000f)]
    public float spaceScale = 10f;
    public bool drawVirtualNodes = true;

    List<GameObject> gameObjects = new List<GameObject>();

    void Reset()
    {
        if (Application.isEditor)
        {
            foreach (var obj in gameObjects)
            {
                DestroyImmediate(obj);
            }
        }
        else
        {
            foreach (var obj in gameObjects)
            {
                Destroy(obj);
            }
        }

        gameObjects.Clear();
    }

    public void DrawNetwork()
    {
        Reset();

        var networkData = GetComponent<NetworkDataStructure>();

        foreach (var node in networkData.nodes)
        {
            if (drawVirtualNodes || !node.virtualNode)
            {
                Color? col = node.virtualNode ? (Color?)Color.black : null;
                var nodeObj = NodeRenderer.MakeNode(Instantiate(nodePrefab, transform), node, col);

                gameObjects.Add(nodeObj);
            }
        }


    }
}
