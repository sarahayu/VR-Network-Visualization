using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class TerrainNetworkRenderer : NetworkRenderer
    {
        const int GRAPH_AREA_LEN = 81;
        const int TEX_RES_NORMAL = 480;
        const int TEX_RES_ALBEDO = 1280;
        public Transform NetworkTransform;


        [SerializeField]
        float _meshSize = 1f;
        [SerializeField]
        float _meshHeight = 1f;

        [SerializeField]
        float _falloff = 1f;

        [SerializeField]
        float _lineColorIntensity = 0.2f;

        [SerializeField]
        AnimationCurve _falloffShapeFunc = AnimationCurve.Linear(0, 0, 1, 1);

        [SerializeField]
        AnimationCurve _peakHeightFunc = AnimationCurve.Linear(0, 0, 1, 1);

        [SerializeField]
        AnimationCurve _slackFunc = AnimationCurve.Linear(0, 0.5f, 1, 1);

        [SerializeField]
        float _curvatureRadius = 100f;

        Dictionary<int, GameObject> _nodeGameObjs = new Dictionary<int, GameObject>();
        Dictionary<Tuple<int, int>, GameObject> _linkGameObjs = new Dictionary<Tuple<int, int>, GameObject>();
        NetworkGlobal _networkGlobal;
        MinimapContext _networkContext;
        HeightMap _heightMap;

        [SerializeField]
        GameObject _meshPrefab;

        MeshFilter _meshFilter;
        MeshRenderer _meshRenderer;
        MeshCollider _meshCollider;
        Texture2D _lineTex = null;
        Texture2D _heightTex = null;
        Texture2D _normalTex = null;
        Texture2D _nodeColTex = null;

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
            _lineTex = null;
            _heightTex = null;
            _normalTex = null;
            _nodeColTex = null;
        }

        public override void Initialize(NetworkContext networkContext)
        {
            Reset();

            _networkGlobal = GameObject.Find("/Network Manager").GetComponent<NetworkGlobal>();
            _networkContext = (MinimapContext)networkContext;

            GameObject linkObj = Instantiate(_meshPrefab, NetworkTransform);

            _meshFilter = linkObj.GetComponent<MeshFilter>();
            _meshRenderer = linkObj.GetComponent<MeshRenderer>();
            _meshCollider = linkObj.GetComponent<MeshCollider>();

            var mMaterial = GetMeshMaterial();

            mMaterial.SetFloat("_MaxHeight", _meshHeight);
            mMaterial.SetFloat("_CurvatureRadius", _curvatureRadius);

            var flatMesh = new FlatMesh(
                networkGlobal: _networkGlobal,
                networkContext: _networkContext,
                subdivideSunflower: 2000,
                subdivideRidges: 50
            );

            flatMesh.CalcMeshPoints();

            _heightMap = new HeightMap(
                falloffDistance: _falloff * GRAPH_AREA_LEN,
                falloffShapeFunc: _falloffShapeFunc,
                peakHeightFunc: _peakHeightFunc,
                slackFunc: _slackFunc
            );

            _heightMap.CalcMaxes(_networkContext);

            GenerateTerrainLowQuality(flatMesh);

            // GenerateTerrainTextureLines();
            mMaterial.SetTexture("_LineTex", _lineTex);

            GenerateTerrainTextureHeightMap(flatMesh);
            mMaterial.SetTexture("_HeightMap", _heightTex);
            mMaterial.SetInt("_UseHeightMap", 0);

            GenerateTerrainSmoothNormals();
            mMaterial.SetTexture("_BumpMap", _normalTex);

            GenerateNodeColors();
            mMaterial.SetTexture("_NodeColTex", _nodeColTex);

            // mMaterial.SetTexture("_SelectionTex", _selectionTex);

        }

        public override void UpdateRenderElements()
        {
            // do nothing
        }

        public override void Draw()
        {
            // nothing to call
        }

        public override Transform GetNodeTransform(int nodeID)
        {
            // nothing to implement
            return transform;
        }

        void GenerateTerrainLowQuality(FlatMesh meshCalculator)
        {
            // make mesh from heightmap
            var terrainMesh = new TerrainMesh(
                meshSize: _meshSize,
                heightScale: _meshHeight,
                curvatureRadius: _curvatureRadius,
                useNormalMap: false
            );

            terrainMesh.Generate(_networkContext, _heightMap, meshCalculator);

            _meshFilter.sharedMesh = terrainMesh.Mesh;

            var colFlat = new Color[TEX_RES_NORMAL * TEX_RES_NORMAL];
            for (int y = 0; y < TEX_RES_NORMAL; y++)
                for (int x = 0; x < TEX_RES_NORMAL; x++)
                    colFlat[y * TEX_RES_NORMAL + x] = Color.black;

            var texFlat = new Texture2D(TEX_RES_NORMAL, TEX_RES_NORMAL);
            texFlat.SetPixels(colFlat);
            texFlat.Apply();

            var mMaterial = GetMeshMaterial();

            mMaterial.SetTexture("_LineTex", texFlat);
            mMaterial.SetFloat("_MaxHeight", _meshHeight);
            mMaterial.SetFloat("_CurvatureRadius", _curvatureRadius);
        }

        public void GenerateTerrainTextureLines()
        {
            _lineTex = _heightMap.GenerateTextureLines(_networkContext, TEX_RES_ALBEDO, TEX_RES_ALBEDO, _lineColorIntensity);
        }

        Material GetMeshMaterial()
        {
            return Application.isPlaying ? _meshRenderer.material : _meshRenderer.sharedMaterial;
        }

        void GenerateTerrainTextureHeightMap(FlatMesh meshCalculator)
        {
            _heightTex = _heightMap.GenerateTextureHeight(_networkContext, meshCalculator, TEX_RES_NORMAL, TEX_RES_NORMAL);
        }

        void GenerateTerrainSmoothNormals()
        {
            _normalTex = TerrainTextureUtils.GenerateNormalFromHeight(_heightTex, _meshHeight, GRAPH_AREA_LEN);

        }

        void GenerateNodeColors()
        {
            _nodeColTex = TerrainTextureUtils.GenerateNodeColsFromGraph(_networkGlobal, _networkContext, _heightMap, TEX_RES_ALBEDO, TEX_RES_ALBEDO);
        }
    }

}