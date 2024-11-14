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

        public bool DrawVirtualNodes = true;
        public bool DrawTreeStructure = false;
        public Transform NetworkTransform;
        public ComputeShader SplineComputeShader;

        Dictionary<int, GameObject> _nodeGameObjs = new Dictionary<int, GameObject>();
        Dictionary<int, GameObject> _linkGameObjs = new Dictionary<int, GameObject>();
        Dictionary<int, GameObject> _communityGameObjs = new Dictionary<int, GameObject>();
        Dictionary<int, List<Vector3>> _controlPointsMap = new Dictionary<int, List<Vector3>>();
        Material _batchSplineMaterial;

        BSplineShaderWrapper _shaderWrapper = new BSplineShaderWrapper();
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
            _communityGameObjs.Clear();
        }

        public override void Initialize(NetworkContext networkContext)
        {
            Reset();

            InitializeShaders();

            _networkData = GameObject.Find("/Network Manager").GetComponent<NetworkDataStructure>();
            _networkProperties = (NetworkContext3D)networkContext;

            CreateNodes();
            CreateCommunities();
            CreateLinks();

            CreateGPULinks();
        }

        public override void UpdateRenderElements()
        {
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
            _batchSplineMaterial.SetFloat("_LineWidth", LinkWidth);

            // Configure the spline compute shader
            SplineComputeShader.SetVector("COLOR_HIGHLIGHT", LinkHighlightColor);
            SplineComputeShader.SetVector("COLOR_FOCUS", LinkFocusColor);
            SplineComputeShader.SetFloat("COLOR_MINIMUM_ALPHA", LinkMinimumAlpha);
            SplineComputeShader.SetFloat("COLOR_NORMAL_ALPHA_FACTOR", LinkNormalAlphaFactor);
            SplineComputeShader.SetFloat("COLOR_CONTEXT_ALPHA_FACTOR", LinkContextAlphaFactor);
            SplineComputeShader.SetFloat("COLOR_FOCUS2CONTEXT_ALPHA_FACTOR", LinkContext2FocusAlphaFactor);

            _shaderWrapper.Initialize(SplineComputeShader, _batchSplineMaterial);
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

        void CreateCommunities()
        {
            foreach (var (communityID, community) in _networkData.Communities)
            {
                var communityProps = _networkProperties.Communities[communityID];
                var commObj = CommunityRenderUtils.MakeCommunity(CommunityPrefab, NetworkTransform,
                    communityProps);

                _communityGameObjs[communityID] = commObj;

                AddInteractionListeners(commObj, community);
            }
        }

        void CreateLinks()
        {
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
        }

        void CreateGPULinks()
        {
            ComputeControlPoints();
            _shaderWrapper.PrepareBuffers(_networkData, _networkProperties, _controlPointsMap);
        }

        void ComputeControlPoints()
        {
            foreach (var link in _networkData.Links)
            {
                float beta = EdgeBundlingStrength;

                Vector3[] cp = BSplineMathUtils.ControlPoints(link, _networkData, _networkProperties);
                int length = cp.Length;

                Vector3 source = cp[0];
                Vector3 target = cp[length - 1];
                Vector3 dVector3 = target - source;

                Vector3[] cpDistributed = new Vector3[length + 2];

                cpDistributed[0] = NetworkTransform.TransformPoint(source);

                for (int i = 0; i < length; i++)
                {
                    Vector3 point = cp[i];

                    cpDistributed[i + 1].x = beta * point.x + (1 - beta) * (source.x + (i + 1) * dVector3.x / length);
                    cpDistributed[i + 1].y = beta * point.y + (1 - beta) * (source.y + (i + 1) * dVector3.y / length);
                    cpDistributed[i + 1].z = beta * point.z + (1 - beta) * (source.z + (i + 1) * dVector3.z / length);

                    cpDistributed[i + 1] = NetworkTransform.TransformPoint(cpDistributed[i + 1]);
                }
                cpDistributed[length + 1] = NetworkTransform.TransformPoint(target);

                _controlPointsMap[link.linkIdx] = new List<Vector3>(cpDistributed);
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

        void UpdateCommunities()
        {
            foreach (var communityID in _networkData.Communities.Keys)
            {
                var communityProps = _networkProperties.Communities[communityID];
                CommunityRenderUtils.UpdateCommunity(_communityGameObjs[communityID], communityProps);
            }
        }

        void UpdateLinks()
        {
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
        }

        void UpdateGPULinks()
        {
            ComputeControlPoints();
            _shaderWrapper.UpdateBuffers(_networkData, _networkProperties, _controlPointsMap);
        }

        void AddInteractionListeners(GameObject gameObject, Community community)
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

    }
}