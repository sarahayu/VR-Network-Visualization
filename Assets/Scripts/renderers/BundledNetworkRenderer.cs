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

        BSplineShaderWrapper _shaderWrapper = new BSplineShaderWrapper();
        NetworkDataStructure _networkData;
        NetworkContext3D _networkContext;

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

            _networkData = GameObject.Find("/Network Manager").GetComponent<NetworkDataStructure>();
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
            foreach (var node in _networkData.Nodes)
            {
                if (DrawVirtualNodes || !node.IsVirtualNode)
                {
                    var nodeProps = _networkContext.Nodes[node.ID];
                    var nodeObj = node.IsVirtualNode
                        ? NodeLinkRenderUtils.MakeNode(NodePrefab, transform, node, nodeProps, Color.black)
                        : NodeLinkRenderUtils.MakeNode(NodePrefab, transform, node, nodeProps);

                    _nodeGameObjs[node.ID] = nodeObj;

                    AddNodeInteraction(nodeObj, node);
                }
            }
        }

        void CreateCommunities()
        {
            foreach (var (communityID, community) in _networkData.Communities)
            {
                var communityProps = _networkContext.Communities[communityID];
                var commObj = CommunityRenderUtils.MakeCommunity(CommunityPrefab, transform,
                    communityProps);

                _communityGameObjs[communityID] = commObj;

                AddCommunityInteraction(commObj, community);
            }
        }

        void CreateLinks()
        {
            if (DrawTreeStructure)
            {
                foreach (var link in _networkData.TreeLinks)
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
            _shaderWrapper.PrepareBuffers(_networkData, _networkContext, _controlPointsMap);
        }

        void ComputeControlPoints()
        {
            foreach (var link in _networkData.Links)
            {
                float beta = Settings.EdgeBundlingStrength;

                Vector3[] cp = BSplineMathUtils.ControlPoints(link, _networkData, _networkContext);
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
            foreach (var node in _networkData.Nodes)
            {
                if (DrawVirtualNodes || !node.IsVirtualNode)
                {
                    _nodeGameObjs[node.ID].transform.localPosition = _networkContext.Nodes[node.ID].Position;

                }
            }
        }

        void UpdateCommunities()
        {
            foreach (var communityID in _networkData.Communities.Keys)
            {
                var communityProps = _networkContext.Communities[communityID];
                CommunityRenderUtils.UpdateCommunity(_communityGameObjs[communityID], communityProps);
            }
        }

        void UpdateLinks()
        {
            if (DrawTreeStructure)
            {
                foreach (var link in _networkData.TreeLinks)
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
            _shaderWrapper.UpdateBuffers(_networkData, _networkContext, _controlPointsMap);
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