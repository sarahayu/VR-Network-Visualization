using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VidiGraph
{
    public class BundledNetworkRenderer : NetworkRenderer
    {
        public GameObject NodePrefab;
        public GameObject CommunityPrefab;
        public GameObject StraightLinkPrefab;

        public bool DrawVirtualNodes = true;
        public bool DrawTreeStructure = false;
        public ComputeShader SplineComputeShader;

        Dictionary<int, GameObject> _nodeGameObjs = new Dictionary<int, GameObject>();
        Dictionary<int, GameObject> _linkGameObjs = new Dictionary<int, GameObject>();
        Dictionary<int, GameObject> _communityGameObjs = new Dictionary<int, GameObject>();
        Dictionary<int, List<Vector3>> _controlPointsMap = new Dictionary<int, List<Vector3>>();
        Material _batchSplineMaterial;


        Dictionary<int, Renderer> _nodeRenderers = new Dictionary<int, Renderer>();
        Dictionary<int, Renderer> _commRenderers = new Dictionary<int, Renderer>();

        BSplineShaderWrapper _shaderWrapper = new BSplineShaderWrapper();
        NetworkGlobal _networkGlobal;
        MultiLayoutContext _networkContext;
        int _lastHoveredNode = -1;
        int _lastHoveredComm = -1;

        void Reset()
        {
            if (Application.isEditor)
            {
                GameObjectUtils.ChildrenDestroyImmediate(transform);
            }
            else
            {
                GameObjectUtils.ChildrenDestroy(transform);
            }

            _nodeGameObjs.Clear();
            _linkGameObjs.Clear();
            _communityGameObjs.Clear();
        }

        public override void Initialize(NetworkContext networkContext)
        {
            Reset();

            _networkGlobal = GameObject.Find("/Network Manager").GetComponent<NetworkGlobal>();
            _networkContext = (MultiLayoutContext)networkContext;

            InitializeShaders();

            CreateNodes();
            CreateCommunities();
            CreateMeshLinks();
            CreateGPULinks();

            UpdateRenderElements();
        }

        public override void UpdateRenderElements()
        {
            UpdateNodes();
            UpdateCommunities();
            UpdateMeshLinks();
            UpdateGPULinks();
        }

        public override void Draw()
        {
            _shaderWrapper.Draw();
        }

        public override Transform GetNodeTransform(int nodeID)
        {
            return _nodeGameObjs[nodeID].transform;
        }

        public override Transform GetCommTransform(int commID)
        {
            return _communityGameObjs[commID].transform;
        }

        void InitializeShaders()
        {
            _batchSplineMaterial = new Material(Shader.Find("Custom/Batch BSpline Unlit"));
            _batchSplineMaterial.SetFloat("_LineWidth", _networkContext.ContextSettings.LinkWidth);

            _shaderWrapper.Initialize(SplineComputeShader, _batchSplineMaterial, _networkContext.ContextSettings);
        }

        void CreateNodes()
        {
            foreach (var node in _networkGlobal.Nodes)
            {
                if (DrawVirtualNodes || !node.IsVirtualNode)
                {
                    var nodeProps = _networkContext.Nodes[node.ID];
                    var nodeObj = NodeLinkRenderUtils.MakeNode(NodePrefab, transform, node, nodeProps,
                        _networkContext.ContextSettings.NodeScale);

                    _nodeGameObjs[node.ID] = nodeObj;
                    _nodeRenderers[node.ID] = nodeObj.GetComponentInChildren<Renderer>();

                    AddNodeInteraction(nodeObj, node);
                }
            }
        }

        void CreateCommunities()
        {
            foreach (var (commID, community) in _networkGlobal.Communities)
            {
                var communityProps = _networkContext.Communities[commID];
                var commObj = CommunityRenderUtils.MakeCommunity(CommunityPrefab, transform,
                    community, communityProps);

                _communityGameObjs[commID] = commObj;
                _commRenderers[commID] = commObj.GetComponentInChildren<Renderer>();

                AddCommunityInteraction(commObj, community);
            }
        }

        void CreateMeshLinks()
        {
            if (DrawTreeStructure)
            {
                foreach (var link in _networkGlobal.TreeLinks)
                {
                    Vector3 startPos = _networkContext.Nodes[link.SourceNodeID].Position,
                        endPos = _networkContext.Nodes[link.TargetNodeID].Position;
                    var linkObj = NodeLinkRenderUtils.MakeStraightLink(StraightLinkPrefab, transform,
                        startPos, endPos, _networkContext.ContextSettings.LinkWidth);
                    _linkGameObjs[link.ID] = linkObj;
                }
            }
        }

        void CreateGPULinks()
        {
            ComputeControlPoints();
            PrepareBuffers();
        }

        void ComputeControlPoints()
        {
            foreach (var link in _networkGlobal.Links)
            {
                float beta = _networkContext.Links[link.ID].BundlingStrength;

                Vector3[] cp = BSplineMathUtils.ControlPoints(link, _networkGlobal, _networkContext);
                int length = cp.Length;

                Vector3 source = cp[0];
                Vector3 target = cp[length - 1];
                Vector3 dVector3 = target - source;

                Vector3[] cpDistributed = new Vector3[length + 2];

                cpDistributed[0] = source;

                for (int i = 0; i < length; i++)
                {
                    Vector3 point = cp[i];

                    cpDistributed[i + 1].x = beta * point.x + (1 - beta) * (source.x + (i + 1) * dVector3.x / length);
                    cpDistributed[i + 1].y = beta * point.y + (1 - beta) * (source.y + (i + 1) * dVector3.y / length);
                    cpDistributed[i + 1].z = beta * point.z + (1 - beta) * (source.z + (i + 1) * dVector3.z / length);
                }
                cpDistributed[length + 1] = target;

                _controlPointsMap[link.ID] = new List<Vector3>(cpDistributed);
            }
        }

        void PrepareBuffers()
        {
            _shaderWrapper.PrepareBuffers(_networkGlobal, _networkContext, _controlPointsMap);
        }

        void UpdateNodes()
        {
            foreach (var nodeID in _networkContext.Nodes.Keys)
            {
                Node globalNode = _networkGlobal.Nodes[nodeID];
                MultiLayoutContext.Node contextNode = _networkContext.Nodes[nodeID];

                if (DrawVirtualNodes || !globalNode.IsVirtualNode)
                {
                    if ((nodeID == _networkGlobal.HoveredNode?.ID || globalNode.CommunityID == _networkGlobal.HoveredCommunity?.ID) && !globalNode.Selected)
                    {
                        var hoverCol = _networkContext.ContextSettings.NodeHoverColor;
                        NodeLinkRenderUtils.SetNodeColor(_nodeGameObjs[nodeID], hoverCol, _nodeRenderers[nodeID]);
                    }

                    if (NodeNeedsRenderUpdate(nodeID))
                    {
                        NodeLinkRenderUtils.UpdateNode(_nodeGameObjs[nodeID], globalNode, contextNode,
                            _networkContext.ContextSettings.NodeScale, _nodeRenderers[nodeID]);

                        if (nodeID == _networkGlobal.HoveredNode?.ID || globalNode.CommunityID == _networkGlobal.HoveredCommunity?.ID)
                        {
                            var hoverCol = _networkContext.ContextSettings.NodeHoverColor;
                            NodeLinkRenderUtils.SetNodeColor(_nodeGameObjs[nodeID], hoverCol, _nodeRenderers[nodeID]);
                        }
                        if (globalNode.Selected)
                            NodeLinkRenderUtils.SetNodeColor(_nodeGameObjs[nodeID], _networkContext.ContextSettings.NodeSelectColor, _nodeRenderers[nodeID]);
                        globalNode.Dirty = contextNode.Dirty = false;
                    }
                }
            }

            BookkeepHoverNode();
        }

        void UpdateCommunities()
        {
            foreach (var commID in _networkGlobal.Communities.Keys)
            {

                Community globalComm = _networkGlobal.Communities[commID];
                if (commID == _networkGlobal.HoveredCommunity?.ID && !globalComm.Selected)
                {
                    var hoverCol = _networkContext.ContextSettings.CommHoverColor;
                    CommunityRenderUtils.SetCommunityColor(_communityGameObjs[commID], hoverCol, _commRenderers[commID]);
                }

                if (CommNeedsRenderUpdate(commID))
                {
                    MultiLayoutContext.Community contextComm = _networkContext.Communities[commID];

                    CommunityRenderUtils.UpdateCommunity(_communityGameObjs[commID], globalComm, contextComm,
                        _networkContext.ContextSettings.CommSelectColor, _commRenderers[commID]);

                    if (commID == _networkGlobal.HoveredCommunity?.ID)
                    {
                        var hoverCol = _networkContext.ContextSettings.CommHoverColor;
                        CommunityRenderUtils.SetCommunityColor(_communityGameObjs[commID], hoverCol, _commRenderers[commID]);
                    }
                    if (globalComm.Selected)
                        CommunityRenderUtils.SetCommunityColor(_communityGameObjs[commID], _networkContext.ContextSettings.CommSelectColor, _commRenderers[commID]);
                    globalComm.Dirty = contextComm.Dirty = false;
                }


            }

            BookkeepHoverCommunity();
        }

        void UpdateMeshLinks()
        {
            if (DrawTreeStructure)
            {
                foreach (var link in _networkGlobal.TreeLinks)
                {
                    Vector3 startPos = _networkContext.Nodes[link.SourceNodeID].Position,
                        endPos = _networkContext.Nodes[link.TargetNodeID].Position;
                    NodeLinkRenderUtils.UpdateStraightLink(_linkGameObjs[link.ID],
                        startPos, endPos, _networkContext.ContextSettings.LinkWidth);
                }
            }
        }

        void UpdateGPULinks()
        {
            ComputeControlPoints();
            _shaderWrapper.UpdateBuffers(_networkGlobal, _networkContext, _controlPointsMap);
        }

        bool NodeNeedsRenderUpdate(int nodeID)
        {
            var globalNode = _networkGlobal.Nodes[nodeID];
            var contextNode = _networkContext.Nodes[nodeID];

            return globalNode.Dirty
                || contextNode.Dirty
                || _lastHoveredNode == nodeID
                || _lastHoveredComm == globalNode.CommunityID;
        }

        bool CommNeedsRenderUpdate(int commID)
        {
            return _networkGlobal.Communities[commID].Dirty
                || _networkContext.Communities[commID].Dirty
                || _lastHoveredComm == commID;
        }

        void BookkeepHoverNode()
        {
            if (_networkGlobal.HoveredNode != null)
            {
                _lastHoveredNode = _networkGlobal.HoveredNode.ID;
            }
            else if (_lastHoveredNode != -1)
            {
                _lastHoveredNode = -1;
            }
        }

        void BookkeepHoverCommunity()
        {
            if (_networkGlobal.HoveredCommunity != null)
            {
                _lastHoveredComm = _networkGlobal.HoveredCommunity.ID;
            }
            else if (_lastHoveredComm != -1)
            {
                _lastHoveredComm = -1;
            }
        }

        void AddCommunityInteraction(GameObject gameObject, Community community)
        {
            XRGrabInteractable xrInteractable = gameObject.GetComponent<XRGrabInteractable>();

            xrInteractable.hoverEntered.AddListener(evt =>
            {
                CallCommunityHoverEnter(community, evt);
            });

            xrInteractable.hoverExited.AddListener(evt =>
            {
                CallCommunityHoverExit(community, evt);
            });

            xrInteractable.selectEntered.AddListener(evt =>
            {
                CallCommunitySelectEnter(community, evt);
            });

            xrInteractable.selectExited.AddListener(evt =>
            {
                CallCommunitySelectExit(community, evt);

            });
        }

        void AddNodeInteraction(GameObject gameObject, Node node)
        {
            XRGrabInteractable xrInteractable = gameObject.GetComponentInChildren<XRGrabInteractable>();

            xrInteractable.hoverEntered.AddListener(evt =>
            {
                CallNodeHoverEnter(node, evt);
            });

            xrInteractable.hoverExited.AddListener(evt =>
            {
                CallNodeHoverExit(node, evt);
            });

            xrInteractable.selectEntered.AddListener(evt =>
            {
                CallNodeSelectEnter(node, evt);
            });

            xrInteractable.selectExited.AddListener(evt =>
            {
                CallNodeSelectExit(node, evt);
            });
        }

    }
}