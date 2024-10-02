using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class BasicNetworkRenderer : NetworkRenderer
    {
        public GameObject nodePrefab;
        public GameObject straightLinkPrefab;

        [Range(0.0f, 0.1f)]
        public float linkWidth = 0.005f;
        public bool drawVirtualNodes = true;
        public bool drawTreeStructure = false;
        public Transform networkTransform;

        List<GameObject> gameObjects = new List<GameObject>();

        void Reset()
        {
            if (Application.isEditor)
            {
                GameObjectUtils.ChildrenDestroyImmediate(networkTransform);
            }
            else
            {
                GameObjectUtils.ChildrenDestroy(networkTransform);
            }

            gameObjects.Clear();
        }

        public override void Initialize()
        {
            Reset();

            var networkData = GetComponentInParent<NetworkDataStructure>();

            CreateNodes(networkData);
            CreateLinks(networkData);

        }

        public override void DrawNetwork()
        {
            // nothing to call
        }

        void CreateNodes(NetworkDataStructure networkData)
        {
            foreach (var node in networkData.nodes)
            {
                if (drawVirtualNodes || !node.virtualNode)
                {
                    Color? col = node.virtualNode ? (Color?)Color.black : null;
                    var nodeObj = NodeLinkRenderer.MakeNode(nodePrefab, networkTransform, node, col);
                    gameObjects.Add(nodeObj);
                }
            }
        }

        void CreateLinks(NetworkDataStructure networkData)
        {
            // This will draw a 'debug' tree structure to emphasize the underlying hierarchy...
            if (drawTreeStructure)
            {
                foreach (var link in networkData.treeLinks)
                {
                    var linkObj = NodeLinkRenderer.MakeStraightLink(straightLinkPrefab, networkTransform, link, linkWidth);
                    gameObjects.Add(linkObj);
                }
            }
            // ...whereas this is concerned with the visible links between nodes in the graph

            foreach (var link in networkData.links)
            {
                var linkObj = NodeLinkRenderer.MakeStraightLink(straightLinkPrefab, networkTransform, link, linkWidth);
                gameObjects.Add(linkObj);
            }
        }
    }

}