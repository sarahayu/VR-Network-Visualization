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

        // allow us to access what color nodes/links are supposed to be
        MLEncodingTransformer _encoder;

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
            _encoder = transform.parent.parent.GetComponentInChildren<MLEncodingTransformer>();

            InitializeShaders();

            UpdateNetworkTransform();

            CreateNodes();
            CreateCommunities();
            CreateLinks();

            CreateGPULinks();

            UpdateRenderElements();
        }

        public override void UpdateRenderElements()
        {
            UpdateNetworkTransform();

            UpdateNodes();
            UpdateCommunities();
            UpdateLinks();

            UpdateGPULinks();
        }

        public override void Draw()
        {
            _shaderWrapper.Draw();
        }

        void InitializeShaders()
        {
            _batchSplineMaterial = new Material(Shader.Find("Custom/Batch BSpline Unlit"));
            _batchSplineMaterial.SetFloat("_LineWidth", _networkContext.ContextSettings.LinkWidth);

            _shaderWrapper.Initialize(SplineComputeShader, _batchSplineMaterial, _networkContext.ContextSettings);
        }

        void UpdateNetworkTransform()
        {
            _networkContext.CurrentTransform.AssignToTransform(transform);
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
            foreach (var (communityID, community) in _networkGlobal.Communities)
            {
                var communityProps = _networkContext.Communities[communityID];
                var commObj = CommunityRenderUtils.MakeCommunity(CommunityPrefab, transform,
                    communityProps);

                _communityGameObjs[communityID] = commObj;
                _commRenderers[communityID] = commObj.GetComponentInChildren<Renderer>();

                AddCommunityInteraction(commObj, community);
            }
        }

        void CreateLinks()
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
            _shaderWrapper.PrepareBuffers(_networkGlobal, _networkContext, _controlPointsMap);
        }

        void ComputeControlPoints()
        {
            foreach (var link in _networkGlobal.Links)
            {
                float beta;

                // TODO always get beta from context
                if (_networkContext.Links[link.ID].OverrideBundlingStrength != -1f)
                    beta = _networkContext.Links[link.ID].OverrideBundlingStrength;
                else
                    beta = _networkContext.ContextSettings.EdgeBundlingStrength;

                Vector3[] cp = BSplineMathUtils.ControlPoints(link, _networkGlobal, _networkContext);
                int length = cp.Length;

                Vector3 source = cp[0];
                Vector3 target = cp[length - 1];
                Vector3 dVector3 = target - source;

                Vector3[] cpDistributed = new Vector3[length + 2];

                cpDistributed[0] = _networkContext.CurrentTransform.TransformPoint(source);

                for (int i = 0; i < length; i++)
                {
                    Vector3 point = cp[i];

                    cpDistributed[i + 1].x = beta * point.x + (1 - beta) * (source.x + (i + 1) * dVector3.x / length);
                    cpDistributed[i + 1].y = beta * point.y + (1 - beta) * (source.y + (i + 1) * dVector3.y / length);
                    cpDistributed[i + 1].z = beta * point.z + (1 - beta) * (source.z + (i + 1) * dVector3.z / length);

                    cpDistributed[i + 1] = _networkContext.CurrentTransform.TransformPoint(cpDistributed[i + 1]);
                }
                cpDistributed[length + 1] = _networkContext.CurrentTransform.TransformPoint(target);

                _controlPointsMap[link.ID] = new List<Vector3>(cpDistributed);
            }
        }

        void UpdateNodes()
        {
            foreach (var nodeID in _networkContext.Nodes.Keys)
            {
                Node globalNode = _networkGlobal.Nodes[nodeID];
                MultiLayoutContext.Node contextNode = _networkContext.Nodes[nodeID];

                // update if global or context node is dirty, or it's been unhovered
                bool needsUpdate = globalNode.Dirty || contextNode.Dirty || _lastHoveredNode == nodeID || _lastHoveredComm == globalNode.CommunityID;

                if ((DrawVirtualNodes || !globalNode.IsVirtualNode) && needsUpdate)
                {
                    NodeLinkRenderUtils.UpdateNode(_nodeGameObjs[nodeID], globalNode, contextNode,
                        _networkContext.ContextSettings.NodeScale, _nodeRenderers[nodeID]);
                    globalNode.Dirty = contextNode.Dirty = false;
                }

                if (nodeID == _networkGlobal.HoveredNode?.ID || globalNode.CommunityID == _networkGlobal.HoveredCommunity?.ID)
                {
                    var highlightCol = _networkContext.ContextSettings.NodeHighlightColor;
                    NodeLinkRenderUtils.SetNodeColor(_nodeGameObjs[nodeID], highlightCol, _nodeRenderers[nodeID]);
                }
            }

            BookkeepHoverNode();
        }

        void UpdateCommunities()
        {
            foreach (var communityID in _networkGlobal.Communities.Keys)
            {
                var globalComm = _networkGlobal.Communities[communityID];
                var contextComm = _networkContext.Communities[communityID];

                // update if global or context community is dirty, or it's been unhovered
                bool needsUpdate = globalComm.Dirty || contextComm.Dirty || _lastHoveredComm == communityID;

                if (needsUpdate)
                {
                    Color color = globalComm.Selected ? _networkContext.ContextSettings.CommHighlightColor : new Color(0f, 0f, 0f, 0f);
                    CommunityRenderUtils.UpdateCommunity(_communityGameObjs[communityID], contextComm, _commRenderers[communityID]);
                    CommunityRenderUtils.SetCommunityColor(_communityGameObjs[communityID], color, _commRenderers[communityID]);
                    globalComm.Dirty = contextComm.Dirty = false;
                }

                if (communityID == _networkGlobal.HoveredCommunity?.ID)
                {
                    var highlightCol = _networkContext.ContextSettings.CommHighlightColor;
                    CommunityRenderUtils.SetCommunityColor(_communityGameObjs[communityID], highlightCol, _commRenderers[communityID]);
                }


            }

            BookkeepHoverCommunity();
        }

        void UpdateLinks()
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
            XRSimpleInteractable xrInteractable = gameObject.GetComponent<XRSimpleInteractable>();

            xrInteractable.hoverEntered.AddListener(evt =>
            {
                CallCommunityHoverEnter(community, evt);
            });

            xrInteractable.hoverExited.AddListener(evt =>
            {
                CallCommunityHoverExit(community, evt);
            });
        }

        void AddNodeInteraction(GameObject gameObject, Node node)
        {
            XRSimpleInteractable xrInteractable = gameObject.GetComponentInChildren<XRSimpleInteractable>();

            xrInteractable.hoverEntered.AddListener(evt =>
            {
                CallNodeHoverEnter(node, evt);
            });

            xrInteractable.hoverExited.AddListener(evt =>
            {
                CallNodeHoverExit(node, evt);
            });
        }

    }
}