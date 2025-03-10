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
        [Serializable]
        public class BundledNetworkSettings
        {
            [Range(0.0f, 0.1f)]
            public float LinkWidth = 0.005f;
            [Range(0.0f, 1.0f)]
            public float EdgeBundlingStrength = 0.8f;

            public Color NodeHighlightColor;
            public Color LinkHighlightColor;
            public Color LinkFocusColor;

            [Range(0.0f, 0.1f)]
            public float LinkMinimumAlpha = 0.01f;
            [Range(0.0f, 1.0f)]
            public float LinkNormalAlphaFactor = 1f;
            [Range(0.0f, 1.0f)]
            public float LinkContextAlphaFactor = 0.5f;
            [Range(0.0f, 1.0f)]
            public float LinkContext2FocusAlphaFactor = 0.8f;

        }
        public GameObject NodePrefab;
        public GameObject CommunityPrefab;
        public GameObject StraightLinkPrefab;

        public BundledNetworkSettings Settings;

        public bool DrawVirtualNodes = true;
        public bool DrawTreeStructure = false;
        public ComputeShader SplineComputeShader;

        Dictionary<int, GameObject> _nodeGameObjs = new Dictionary<int, GameObject>();
        Dictionary<int, GameObject> _linkGameObjs = new Dictionary<int, GameObject>();
        Dictionary<int, GameObject> _communityGameObjs = new Dictionary<int, GameObject>();
        Dictionary<int, List<Vector3>> _controlPointsMap = new Dictionary<int, List<Vector3>>();
        Material _batchSplineMaterial;


        Dictionary<int, Renderer> _nodeColors = new Dictionary<int, Renderer>();
        Dictionary<int, Renderer> _commColors = new Dictionary<int, Renderer>();

        BSplineShaderWrapper _shaderWrapper = new BSplineShaderWrapper();
        NetworkGlobal _networkGlobal;
        NetworkContext3D _networkContext;
        int _lastHoveredNode = -1;

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

            InitializeShaders();

            _networkGlobal = GameObject.Find("/Network Manager").GetComponent<NetworkGlobal>();
            _networkContext = (NetworkContext3D)networkContext;

            UpdateNetworkTransform();

            CreateNodes();
            CreateCommunities();
            CreateLinks();

            CreateGPULinks();
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
            _batchSplineMaterial.SetFloat("_LineWidth", Settings.LinkWidth);

            _shaderWrapper.Initialize(SplineComputeShader, _batchSplineMaterial, Settings);
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
                    var nodeObj = node.IsVirtualNode
                        ? NodeLinkRenderUtils.MakeNode(NodePrefab, transform, node, nodeProps, Color.black)
                        : NodeLinkRenderUtils.MakeNode(NodePrefab, transform, node, nodeProps);

                    _nodeGameObjs[node.ID] = nodeObj;
                    _nodeColors[node.ID] = nodeObj.GetComponentInChildren<Renderer>();

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
                _commColors[communityID] = commObj.GetComponentInChildren<Renderer>();

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
                        link, startPos, endPos, Settings.LinkWidth);
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
                float beta = Settings.EdgeBundlingStrength;

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
            foreach (var node in _networkGlobal.Nodes)
            {
                if (DrawVirtualNodes || !node.IsVirtualNode)
                {
                    _nodeGameObjs[node.ID].transform.localPosition = _networkContext.Nodes[node.ID].Position;

                    if (node.ID == _lastHoveredNode)
                    {
                        MaterialPropertyBlock props = new MaterialPropertyBlock();
                        var renderer = _nodeColors[node.ID];

                        renderer.GetPropertyBlock(props);
                        props.SetColor("_Color", node.IsVirtualNode ? Color.black : node.ColorParsed);
                        renderer.SetPropertyBlock(props);
                    }

                    if (_networkGlobal.HoveredNode != null && node.ID == _networkGlobal.HoveredNode.ID)
                    {
                        MaterialPropertyBlock props = new MaterialPropertyBlock();
                        var renderer = _nodeColors[node.ID];

                        renderer.GetPropertyBlock(props);
                        props.SetColor("_Color", Settings.NodeHighlightColor);
                        renderer.SetPropertyBlock(props);
                    }
                }
            }

            if (_networkGlobal.HoveredNode != null)
                _lastHoveredNode = _networkGlobal.HoveredNode.ID;
            else
                _lastHoveredNode = -1;
        }

        void UpdateCommunities()
        {
            foreach (var communityID in _networkGlobal.Communities.Keys)
            {
                var communityProps = _networkContext.Communities[communityID];
                CommunityRenderUtils.UpdateCommunity(_communityGameObjs[communityID], communityProps);

                if (_networkGlobal.HoveredCommunity != null && _networkGlobal.HoveredCommunity.ID == communityID)
                {
                    MaterialPropertyBlock props = new MaterialPropertyBlock();
                    var renderer = _commColors[communityID];

                    renderer.GetPropertyBlock(props);
                    props.SetColor("_Color", new Color(1f, 1f, 1f, 0.1f));
                    renderer.SetPropertyBlock(props);
                }
                else
                {
                    MaterialPropertyBlock props = new MaterialPropertyBlock();
                    var renderer = _commColors[communityID];

                    renderer.GetPropertyBlock(props);
                    props.SetColor("_Color", new Color(0f, 0f, 0f, 0f));
                    renderer.SetPropertyBlock(props);
                }
            }
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
                        link, startPos, endPos, Settings.LinkWidth);
                }
            }
        }

        void UpdateGPULinks()
        {
            ComputeControlPoints();
            _shaderWrapper.UpdateBuffers(_networkGlobal, _networkContext, _controlPointsMap);
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