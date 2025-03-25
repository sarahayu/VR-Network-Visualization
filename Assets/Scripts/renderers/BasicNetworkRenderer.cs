using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class BasicNetworkRenderer : NetworkRenderer
    {
        public GameObject NodePrefab;
        public GameObject StraightLinkPrefab;

        [Range(0.0f, 100f)]
        public float NodeScale = 1f;
        [Range(0.0f, 0.1f)]
        public float LinkWidth = 0.005f;
        public bool DrawVirtualNodes = true;
        public bool DrawTreeStructure = false;
        public Transform NetworkTransform;

        Dictionary<int, GameObject> _nodeGameObjs = new Dictionary<int, GameObject>();
        Dictionary<int, GameObject> _linkGameObjs = new Dictionary<int, GameObject>();
        NetworkGlobal _networkData;
        MultiLayoutContext _networkProperties;

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

            _networkData = GameObject.Find("/Network Manager").GetComponent<NetworkGlobal>();
            _networkProperties = (MultiLayoutContext)networkContext;

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
                if (DrawVirtualNodes || !node.IsVirtualNode)
                {
                    var nodeProps = _networkProperties.Nodes[node.ID];
                    var nodeObj = NodeLinkRenderUtils.MakeNode(NodePrefab, NetworkTransform, node, nodeProps, NodeScale);

                    _nodeGameObjs[node.ID] = nodeObj;
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
                    Vector3 startPos = _networkProperties.Nodes[link.SourceNodeID].Position,
                        endPos = _networkProperties.Nodes[link.TargetNodeID].Position;
                    var linkObj = NodeLinkRenderUtils.MakeStraightLink(StraightLinkPrefab, NetworkTransform,
                        startPos, endPos, LinkWidth);
                    _linkGameObjs[link.ID] = linkObj;
                }
            }
            // ...whereas this is concerned with the visible links between nodes in the graph

            foreach (var link in _networkData.Links)
            {
                Vector3 startPos = _networkProperties.Nodes[link.SourceNodeID].Position,
                    endPos = _networkProperties.Nodes[link.TargetNodeID].Position;
                var linkObj = NodeLinkRenderUtils.MakeStraightLink(StraightLinkPrefab, NetworkTransform,
                    startPos, endPos, LinkWidth);
                _linkGameObjs[link.ID] = linkObj;
            }
        }

        void UpdateNodes()
        {
            foreach (var node in _networkData.Nodes)
            {
                if (DrawVirtualNodes || !node.IsVirtualNode)
                {
                    var nodeProps = _networkProperties.Nodes[node.ID];
                    NodeLinkRenderUtils.UpdateNode(NodePrefab, node, nodeProps, NodeScale);
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
                    Vector3 startPos = _networkProperties.Nodes[link.SourceNodeID].Position,
                        endPos = _networkProperties.Nodes[link.TargetNodeID].Position;
                    NodeLinkRenderUtils.UpdateStraightLink(_linkGameObjs[link.ID],
                        startPos, endPos, LinkWidth);
                }
            }
            // ...whereas this is concerned with the visible links between nodes in the graph

            foreach (var link in _networkData.Links)
            {
                Vector3 startPos = _networkProperties.Nodes[link.SourceNodeID].Position,
                    endPos = _networkProperties.Nodes[link.TargetNodeID].Position;
                NodeLinkRenderUtils.UpdateStraightLink(_linkGameObjs[link.ID],
                    startPos, endPos, LinkWidth);
            }
        }
    }

}