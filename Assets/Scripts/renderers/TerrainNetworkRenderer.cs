/*
*
* TerrainNetworkRenderer is a terrain representation renderer for the minimap network.
*
*/

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


        [SerializeField] float _meshSize = 1f;
        [SerializeField] float _meshHeight = 1f;

        [SerializeField] float _falloff = 1f;

        [SerializeField] float _lineColorIntensity = 0.2f;

        [SerializeField] AnimationCurve _falloffShapeFunc = AnimationCurve.Linear(0, 0, 1, 1);

        [SerializeField] AnimationCurve _peakHeightFunc = AnimationCurve.Linear(0, 0, 1, 1);

        [SerializeField] AnimationCurve _slackFunc = AnimationCurve.Linear(0, 0.5f, 1, 1);

        [SerializeField] float _curvatureRadius = 100f;
        [SerializeField] GameObject _communityPointPrefab;
        [SerializeField] float _communityPointSize = 0.1f;

        Dictionary<int, GameObject> _commSpheres = new Dictionary<int, GameObject>();
        Dictionary<int, Renderer> _commSphereRends = new Dictionary<int, Renderer>();
        NetworkManager _networkManager;
        MinimapContext _networkContext;
        HeightMap _heightMap;

        [SerializeField] GameObject _meshPrefab;

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

            _commSpheres.Clear();
            _commSphereRends.Clear();
            _lineTex = null;
            _heightTex = null;
            _normalTex = null;
            _nodeColTex = null;
        }

        public override void Initialize(NetworkContext networkContext)
        {
            Reset();

            _networkManager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _networkContext = (MinimapContext)networkContext;

            GameObject linkObj = Instantiate(_meshPrefab, NetworkTransform);

            _meshFilter = linkObj.GetComponent<MeshFilter>();
            _meshRenderer = linkObj.GetComponent<MeshRenderer>();
            _meshCollider = linkObj.GetComponent<MeshCollider>();

            var mMaterial = GetMeshMaterial();

            mMaterial.SetFloat("_MaxHeight", _meshHeight);
            mMaterial.SetFloat("_CurvatureRadius", _curvatureRadius);

            var flatMesh = new FlatMesh(
                networkGlobal: _networkManager.NetworkGlobal,
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


            foreach (var (communityID, community) in _networkContext.CommunityNodes)
            {

                GameObject nodeObj = UnityEngine.Object.Instantiate(_communityPointPrefab, NetworkTransform);
                nodeObj.transform.localPosition = new Vector3(
                    community.Position.x,
                    _peakHeightFunc.Evaluate((float)community.Size / _heightMap.MaxNodeSize)
                        * _meshHeight * _meshSize,
                    community.Position.y);
                nodeObj.transform.localScale = Vector3.one * _communityPointSize;
                nodeObj.SetActive(false);

                _commSpheres[communityID] = nodeObj;
            }

        }

        public override void UpdateRenderElements()
        {
            foreach (var (communityID, community) in _networkManager.NetworkGlobal.Communities)
            {
                if (_networkManager.SubnSelectedCommunities(0).Contains(communityID))
                    _commSpheres[communityID].SetActive(true);
                else _commSpheres[communityID].SetActive(false);
            }
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

        public override Transform GetCommTransform(int commID)
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
            _nodeColTex = TerrainTextureUtils.GenerateNodeColsFromGraph(_networkManager.NetworkGlobal, _networkContext, _heightMap, TEX_RES_ALBEDO, TEX_RES_ALBEDO);
        }
    }

}