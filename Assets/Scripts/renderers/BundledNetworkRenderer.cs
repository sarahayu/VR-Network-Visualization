using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class BundledNetworkRenderer : NetworkRenderer
    {
        public GameObject NodePrefab;
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

        List<GameObject> _gameObjects = new List<GameObject>();
        Dictionary<int, List<Vector3>> _controlPointsMap = new Dictionary<int, List<Vector3>>();
        Material _batchSplineMaterial;

        BSplineShaderWrapper _shaderWrapper = new BSplineShaderWrapper();
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

            InitializeShaders();

            _networkData = GetComponentInParent<NetworkDataStructure>();

            CreateNodes();
            CreateLinks();

            CreateGPULinks();
        }

        public override void UpdateRenderElements()
        {
            // TODO create proper updater
            Reset();

            CreateNodes();
            CreateLinks();

            ComputeControlPoints();
            _shaderWrapper.UpdateBuffers(_networkData, _controlPointsMap);
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
                    var nodeObj = node.virtualNode
                        ? NodeLinkRenderer.MakeNode(NodePrefab, NetworkTransform, node, Color.black)
                        : NodeLinkRenderer.MakeNode(NodePrefab, NetworkTransform, node);

                    _gameObjects.Add(nodeObj);
                }
            }
        }

        void CreateLinks()
        {
            if (DrawTreeStructure)
            {
                foreach (var link in _networkData.TreeLinks)
                {
                    var linkObj = NodeLinkRenderer.MakeStraightLink(StraightLinkPrefab, NetworkTransform, link, LinkWidth);
                    _gameObjects.Add(linkObj);
                }
            }
        }

        void CreateGPULinks()
        {
            ComputeControlPoints();
            _shaderWrapper.PrepareBuffers(_networkData, _controlPointsMap);
        }

        void ComputeControlPoints()
        {
            foreach (var link in _networkData.Links)
            {
                float beta = EdgeBundlingStrength;

                Vector3[] cp = BSplineMathUtils.ControlPoints(link, _networkData);
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

                _controlPointsMap[link.linkIdx] = new List<Vector3>(cpDistributed);
            }
        }

    }
}