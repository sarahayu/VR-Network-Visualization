using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class BundledNetworkRenderer : NetworkRenderer
    {
        public GameObject nodePrefab;
        public GameObject straightLinkPrefab;

        [Range(0.0f, 0.1f)]
        public float linkWidth = 0.005f;
        [Range(0.0f, 1.0f)]
        public float edgeBundlingStrength = 0.8f;

        public Color nodeHighlightColor;
        public Color linkHighlightColor;
        public Color linkFocusColor;
        [Range(0.0f, 0.1f)]
        public float linkMinimumAlpha = 0.01f;
        [Range(0.0f, 1.0f)]
        public float linkNormalAlphaFactor = 1f;
        [Range(0.0f, 1.0f)]
        public float linkContextAlphaFactor = 0.5f;
        [Range(0.0f, 1.0f)]
        public float linkContext2FocusAlphaFactor = 0.8f;

        public bool drawVirtualNodes = true;
        public bool drawTreeStructure = false;
        public Transform networkTransform;
        public ComputeShader computeShader;

        List<GameObject> gameObjects = new List<GameObject>();
        Dictionary<int, List<Vector3>> ControlPointsMap = new Dictionary<int, List<Vector3>>();
        Material batchSplineMaterial;

        BSplineShaderWrapper shaderWrapper = new BSplineShaderWrapper();
        NetworkDataStructure NetworkData;

        void Reset()
        {
            if (Application.isEditor)
            {
                GameObjectUtils.ChildrenDestroyImmediate(networkTransform);
            }
            else
            {
                GameObjectUtils.ChildrenDestroy(networkTransform);
            }

            gameObjects.Clear();
        }

        public override void Initialize()
        {
            Reset();

            InitializeShaders();

            NetworkData = GetComponentInParent<NetworkDataStructure>();

            CreateNodes();
            CreateLinks();

            CreateGPULinks();
        }

        public override void Update()
        {
            // TODO create proper updater
            Reset();

            CreateNodes();
            CreateLinks();

            ComputeControlPoints();
            shaderWrapper.UpdateBuffers(NetworkData, ControlPointsMap);
        }

        public override void Draw()
        {
            shaderWrapper.Draw();
        }

        void InitializeShaders()
        {
            batchSplineMaterial = new Material(Shader.Find("Custom/Batch BSpline Unlit"));
            batchSplineMaterial.SetFloat("_LineWidth", linkWidth);

            // Configure the spline compute shader
            computeShader.SetVector("COLOR_HIGHLIGHT", linkHighlightColor);
            computeShader.SetVector("COLOR_FOCUS", linkFocusColor);
            computeShader.SetFloat("COLOR_MINIMUM_ALPHA", linkMinimumAlpha);
            computeShader.SetFloat("COLOR_NORMAL_ALPHA_FACTOR", linkNormalAlphaFactor);
            computeShader.SetFloat("COLOR_CONTEXT_ALPHA_FACTOR", linkContextAlphaFactor);
            computeShader.SetFloat("COLOR_FOCUS2CONTEXT_ALPHA_FACTOR", linkContext2FocusAlphaFactor);

            shaderWrapper.Initialize(computeShader, batchSplineMaterial);
        }

        void CreateNodes()
        {
            foreach (var node in NetworkData.nodes)
            {
                if (drawVirtualNodes || !node.virtualNode)
                {
                    var nodeObj = node.virtualNode
                        ? NodeLinkRenderer.MakeNode(nodePrefab, networkTransform, node, Color.black)
                        : NodeLinkRenderer.MakeNode(nodePrefab, networkTransform, node);

                    gameObjects.Add(nodeObj);
                }
            }
        }

        void CreateLinks()
        {
            if (drawTreeStructure)
            {
                foreach (var link in NetworkData.treeLinks)
                {
                    var linkObj = NodeLinkRenderer.MakeStraightLink(straightLinkPrefab, networkTransform, link, linkWidth);
                    gameObjects.Add(linkObj);
                }
            }
        }

        void CreateGPULinks()
        {
            ComputeControlPoints();
            shaderWrapper.PrepareBuffers(NetworkData, ControlPointsMap);
        }

        void ComputeControlPoints()
        {
            foreach (var link in NetworkData.links)
            {
                float beta = edgeBundlingStrength;

                Vector3[] cp = BSplineMathUtils.ControlPoints(link, NetworkData);
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

                ControlPointsMap[link.linkIdx] = new List<Vector3>(cpDistributed);
            }
        }

    }
}