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

        Dictionary<int, GameObject> _nodeGameObjs = new Dictionary<int, GameObject>();
        Dictionary<int, GameObject> _linkGameObjs = new Dictionary<int, GameObject>();
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

            _nodeGameObjs.Clear();
            _linkGameObjs.Clear();
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
            UpdateNodes();
            UpdateLinks();
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

                    _nodeGameObjs[node.id] = nodeObj;
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
                    _linkGameObjs[link.linkIdx] = linkObj;
                }
            }
            // ...whereas this is concerned with the visible links between nodes in the graph

            foreach (var link in _networkData.Links)
            {
                var linkObj = NodeLinkRenderer.MakeStraightLink(StraightLinkPrefab, NetworkTransform, link, LinkWidth);
                _linkGameObjs[link.linkIdx] = linkObj;
            }
        }

        void UpdateNodes()
        {
            foreach (var node in _networkData.Nodes)
            {
                if (DrawVirtualNodes || !node.virtualNode)
                {
                    _nodeGameObjs[node.id].transform.localPosition = node.Position3D;
                }
            }
        }

        void UpdateLinks()
        {
            // This will draw a 'debug' tree structure to emphasize the underlying hierarchy...
            if (DrawTreeStructure)
            {
                foreach (var link in _networkData.TreeLinks)
                {
                    NodeLinkRenderer.UpdateStraightLink(_linkGameObjs[link.linkIdx], link, LinkWidth);
                }
            }
            // ...whereas this is concerned with the visible links between nodes in the graph

            foreach (var link in _networkData.Links)
            {
                NodeLinkRenderer.UpdateStraightLink(_linkGameObjs[link.linkIdx], link, LinkWidth);
            }
        }
    }

}