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

        NetworkDataStructure NetworkData;

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

            NetworkData = GetComponentInParent<NetworkDataStructure>();

            CreateNodes();
            CreateLinks();

        }

        public override void Update()
        {
            // TODO create updater
            Initialize();
        }

        public override void Draw()
        {
            // nothing to call
        }

        void CreateNodes()
        {
            foreach (var node in NetworkData.nodes)
            {
                if (drawVirtualNodes || !node.virtualNode)
                {
                    var nodeObj = node.virtualNode
                        ? NodeLinkRenderer.MakeNode(nodePrefab, networkTransform, node, Color.black)
                        : NodeLinkRenderer.MakeNode(nodePrefab, networkTransform, node);

                    gameObjects.Add(nodeObj);
                }
            }
        }

        void CreateLinks()
        {
            // This will draw a 'debug' tree structure to emphasize the underlying hierarchy...
            if (drawTreeStructure)
            {
                foreach (var link in NetworkData.treeLinks)
                {
                    var linkObj = NodeLinkRenderer.MakeStraightLink(straightLinkPrefab, networkTransform, link, linkWidth);
                    gameObjects.Add(linkObj);
                }
            }
            // ...whereas this is concerned with the visible links between nodes in the graph

            foreach (var link in NetworkData.links)
            {
                var linkObj = NodeLinkRenderer.MakeStraightLink(straightLinkPrefab, networkTransform, link, linkWidth);
                gameObjects.Add(linkObj);
            }
        }
    }

}