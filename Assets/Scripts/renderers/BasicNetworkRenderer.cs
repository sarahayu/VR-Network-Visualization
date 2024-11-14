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
        NetworkContext3D _networkProperties;

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

        public override void Initialize(NetworkContext networkContext)
        {
            Reset();

            _networkData = GameObject.Find("/Network Manager").GetComponent<NetworkDataStructure>();
            _networkProperties = (NetworkContext3D)networkContext;

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
                    var nodeProps = _networkProperties.Nodes[node.id];
                    var nodeObj = node.virtualNode
                        ? NodeLinkRenderUtils.MakeNode(NodePrefab, NetworkTransform, node, nodeProps, Color.black)
                        : NodeLinkRenderUtils.MakeNode(NodePrefab, NetworkTransform, node, nodeProps);

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
                    Vector3 startPos = _networkProperties.Nodes[link.sourceIdx].Position,
                        endPos = _networkProperties.Nodes[link.targetIdx].Position;
                    var linkObj = NodeLinkRenderUtils.MakeStraightLink(StraightLinkPrefab, NetworkTransform,
                        link, startPos, endPos, LinkWidth);
                    _linkGameObjs[link.linkIdx] = linkObj;
                }
            }
            // ...whereas this is concerned with the visible links between nodes in the graph

            foreach (var link in _networkData.Links)
            {
                Vector3 startPos = _networkProperties.Nodes[link.sourceIdx].Position,
                    endPos = _networkProperties.Nodes[link.targetIdx].Position;
                var linkObj = NodeLinkRenderUtils.MakeStraightLink(StraightLinkPrefab, NetworkTransform,
                    link, startPos, endPos, LinkWidth);
                _linkGameObjs[link.linkIdx] = linkObj;
            }
        }

        void UpdateNodes()
        {
            foreach (var node in _networkData.Nodes)
            {
                if (DrawVirtualNodes || !node.virtualNode)
                {
                    _nodeGameObjs[node.id].transform.localPosition = _networkProperties.Nodes[node.id].Position;
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
                    Vector3 startPos = _networkProperties.Nodes[link.sourceIdx].Position,
                        endPos = _networkProperties.Nodes[link.targetIdx].Position;
                    NodeLinkRenderUtils.UpdateStraightLink(_linkGameObjs[link.linkIdx],
                        link, startPos, endPos, LinkWidth);
                }
            }
            // ...whereas this is concerned with the visible links between nodes in the graph

            foreach (var link in _networkData.Links)
            {
                Vector3 startPos = _networkProperties.Nodes[link.sourceIdx].Position,
                    endPos = _networkProperties.Nodes[link.targetIdx].Position;
                NodeLinkRenderUtils.UpdateStraightLink(_linkGameObjs[link.linkIdx],
                    link, startPos, endPos, LinkWidth);
            }
        }
    }

}