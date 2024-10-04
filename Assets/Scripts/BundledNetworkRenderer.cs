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
        public
         float linkMinimumAlpha = 0.01f;
        [Range(0.0f, 1.0f)]
        public
         float linkNormalAlphaFactor = 1f;
        [Range(0.0f, 1.0f)]
        public
         float linkContextAlphaFactor = 0.5f;
        [Range(0.0f, 1.0f)]
        public float linkContext2FocusAlphaFactor = 0.8f;

        public bool drawVirtualNodes = true;
        public bool drawTreeStructure = false;
        public Transform networkTransform;
        public ComputeShader computeShader;

        List<GameObject> gameObjects = new List<GameObject>();
        Material batchSplineMaterial;

        BSplineShaderWrapper shaderWrapper = new BSplineShaderWrapper();

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

            var networkData = GetComponentInParent<NetworkDataStructure>();

            CreateNodes(networkData);
            CreateLinks(networkData);

            CreateGPULinks(networkData);
        }

        public override void DrawNetwork()
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

        void CreateNodes(NetworkDataStructure networkData)
        {
            foreach (var node in networkData.nodes)
            {
                if (drawVirtualNodes || !node.virtualNode)
                {
                    Color? col = node.virtualNode ? (Color?)Color.black : null;
                    var nodeObj = NodeLinkRenderer.MakeNode(nodePrefab, networkTransform, node, col);

                    gameObjects.Add(nodeObj);
                }
            }
        }

        void CreateLinks(NetworkDataStructure networkData)
        {
            if (drawTreeStructure)
            {
                foreach (var link in networkData.treeLinks)
                {
                    var linkObj = NodeLinkRenderer.MakeStraightLink(straightLinkPrefab, networkTransform, link, linkWidth);
                    gameObjects.Add(linkObj);
                }
            }
        }

        void CreateGPULinks(NetworkDataStructure networkData)
        {
            ComputeControlPoints(networkData);
            shaderWrapper.PrepareBuffers(networkData);
            // shaderWrapper.UpdateBuffers(networkData);
        }

        void ComputeControlPoints(NetworkDataStructure networkData)
        {
            foreach (var link in networkData.links)
            {
                float beta = edgeBundlingStrength;

                Vector3[] controlPoints = BSplineMathUtils.ControlPoints(link, networkData);
                int length = controlPoints.Length;

                Vector3 source = controlPoints[0];
                Vector3 target = controlPoints[length - 1];
                Vector3 dVector3 = target - source;

                Vector3[] straightenPoints = new Vector3[length + 2];

                straightenPoints[0] = source;

                for (int i = 0; i < length; i++)
                {
                    Vector3 point = controlPoints[i];

                    straightenPoints[i + 1].x = beta * point.x + (1 - beta) * (source.x + (i + 1) * dVector3.x / length);
                    straightenPoints[i + 1].y = beta * point.y + (1 - beta) * (source.y + (i + 1) * dVector3.y / length);
                    straightenPoints[i + 1].z = beta * point.z + (1 - beta) * (source.z + (i + 1) * dVector3.z / length);
                }
                straightenPoints[length + 1] = target;

                link.straightenPoints = new List<Vector3>(straightenPoints);
            }
        }

    }
}