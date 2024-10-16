using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class BasicNetworkRenderer : NetworkRenderer
    {
        public GameObject NodePrefab;
        public GameObject StraightLinkPrefab;

        [Range(0.0f, 0.1f)]
        public float LinkWidth = 0.005f;
        public bool DrawVirtualNodes = true;
        public bool DrawTreeStructure = false;
        public Transform NetworkTransform;

        List<GameObject> _gameObjects = new List<GameObject>();
        NetworkDataStructure _networkData;

        void Reset()
        {
            if (Application.isEditor)
            {
                GameObjectUtils.ChildrenDestroyImmediate(NetworkTransform);
            }
            else
            {
                GameObjectUtils.ChildrenDestroy(NetworkTransform);
            }

            _gameObjects.Clear();
        }

        public override void Initialize()
        {
            Reset();

            _networkData = GetComponentInParent<NetworkDataStructure>();

            CreateNodes();
            CreateLinks();

        }

        public override void UpdateRenderElements()
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
            foreach (var node in _networkData.Nodes)
            {
                if (DrawVirtualNodes || !node.virtualNode)
                {
                    var nodeObj = node.virtualNode
                        ? NodeLinkRenderer.MakeNode(NodePrefab, NetworkTransform, node, Color.black)
                        : NodeLinkRenderer.MakeNode(NodePrefab, NetworkTransform, node);

                    _gameObjects.Add(nodeObj);
                }
            }
        }

        void CreateLinks()
        {
            // This will draw a 'debug' tree structure to emphasize the underlying hierarchy...
            if (DrawTreeStructure)
            {
                foreach (var link in _networkData.TreeLinks)
                {
                    var linkObj = NodeLinkRenderer.MakeStraightLink(StraightLinkPrefab, NetworkTransform, link, LinkWidth);
                    _gameObjects.Add(linkObj);
                }
            }
            // ...whereas this is concerned with the visible links between nodes in the graph

            foreach (var link in _networkData.Links)
            {
                var linkObj = NodeLinkRenderer.MakeStraightLink(StraightLinkPrefab, NetworkTransform, link, LinkWidth);
                _gameObjects.Add(linkObj);
            }
        }
    }

}