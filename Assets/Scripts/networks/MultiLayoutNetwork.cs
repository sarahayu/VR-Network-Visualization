using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class MultiLayoutNetwork : Network
    {
        [Serializable]
        public class Settings
        {
            [Range(0.0f, 100f)]
            public float NodeScale = 1f;
            [Range(0.0f, 0.1f)]
            public float LinkWidth = 0.0025f;
            [Range(0.0f, 1.0f)]
            public float EdgeBundlingStrength = 0.8f;

            public Color CommHighlightColor;
            public Color NodeHighlightColor;
            public Color LinkHighlightColor;
            public Color LinkFocusColor;

            [Range(0.0f, 0.1f)]
            public float LinkMinimumAlpha = 0.01f;
            [Range(0.0f, 1.0f)]
            public float LinkNormalAlphaFactor = 0.05f;
            [Range(0.0f, 1.0f)]
            public float LinkContextAlphaFactor = 0.5f;
            [Range(0.0f, 1.0f)]
            public float LinkContext2FocusAlphaFactor = 0.8f;
        }

        public Settings BaseSettings;

        NetworkManager _manager;

        NetworkInput _multiLayoutInput;
        NetworkRenderer _multiLayoutRenderer;
        MultiLayoutContext _networkContext = new MultiLayoutContext();

        Dictionary<string, NetworkContextTransformer> _transformers = new Dictionary<string, NetworkContextTransformer>();

        // keep a reference to spiderlayout specifically to focus on individual communities
        SpiderLayoutTransformer _spiderLayoutTransformer;

        // keep a reference to bringNodeLayout to focus on individual nodes
        BringNodeTransformer _bringNodeTransformer;

        // keep a reference to floorLayout specifically to focus on individual communities
        FloorLayoutTransformer _floorLayoutTransformer;

        // keep a reference to mlEncoding specifically to be able to change encodings
        MLEncodingTransformer _mlEncodingTransformer;

        bool _isSphericalLayout;
        Coroutine _curAnim = null;

        void Awake()
        {
        }

        void Start()
        {
        }

        void Update()
        {
            Draw();
        }

        public override void Initialize()
        {
            _manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _networkContext.SetFromGlobal(_manager.NetworkGlobal);
            SetContextSettings();

            _multiLayoutInput = GetComponent<NetworkInput>();
            _multiLayoutInput.Initialize();

            InitializeTransformers();

            TransformNetworkNoRender("encoding");
            _isSphericalLayout = true;
            TransformNetworkNoRender("spherical");

            _multiLayoutRenderer = GetComponentInChildren<NetworkRenderer>();
            _multiLayoutRenderer.Initialize(_networkContext);
        }

        public override void UpdateRenderElements()
        {
            _multiLayoutRenderer.UpdateRenderElements();
        }

        public override void DrawPreview()
        {
            Draw();
        }

        void Draw()
        {
            _multiLayoutRenderer.Draw();
        }

        void SetContextSettings()
        {
            _networkContext.ContextSettings.NodeScale = BaseSettings.NodeScale;
            _networkContext.ContextSettings.LinkWidth = BaseSettings.LinkWidth;
            _networkContext.ContextSettings.EdgeBundlingStrength = BaseSettings.EdgeBundlingStrength;
            _networkContext.ContextSettings.CommHighlightColor = BaseSettings.CommHighlightColor;
            _networkContext.ContextSettings.NodeHighlightColor = BaseSettings.NodeHighlightColor;
            _networkContext.ContextSettings.LinkHighlightColor = BaseSettings.LinkHighlightColor;
            _networkContext.ContextSettings.LinkFocusColor = BaseSettings.LinkFocusColor;
            _networkContext.ContextSettings.LinkMinimumAlpha = BaseSettings.LinkMinimumAlpha;
            _networkContext.ContextSettings.LinkNormalAlphaFactor = BaseSettings.LinkNormalAlphaFactor;
            _networkContext.ContextSettings.LinkContextAlphaFactor = BaseSettings.LinkContextAlphaFactor;
            _networkContext.ContextSettings.LinkContext2FocusAlphaFactor = BaseSettings.LinkContext2FocusAlphaFactor;
        }

        void InitializeTransformers()
        {
            _transformers["hairball"] = GetComponentInChildren<HairballLayoutTransformer>();
            _transformers["hairball"].Initialize(_manager.NetworkGlobal, _networkContext);

            _transformers["spherical"] = GetComponentInChildren<SphericalLayoutTransformer>();
            _transformers["spherical"].Initialize(_manager.NetworkGlobal, _networkContext);

            _bringNodeTransformer = GetComponentInChildren<BringNodeTransformer>();
            _transformers["bringNode"] = _bringNodeTransformer;
            _transformers["bringNode"].Initialize(_manager.NetworkGlobal, _networkContext);

            _spiderLayoutTransformer = GetComponentInChildren<SpiderLayoutTransformer>();
            _transformers["spider"] = _spiderLayoutTransformer;
            _transformers["spider"].Initialize(_manager.NetworkGlobal, _networkContext);

            _floorLayoutTransformer = GetComponentInChildren<FloorLayoutTransformer>();
            _transformers["floor"] = _floorLayoutTransformer;
            _transformers["floor"].Initialize(_manager.NetworkGlobal, _networkContext);

            _mlEncodingTransformer = GetComponentInChildren<MLEncodingTransformer>();
            _transformers["encoding"] = _mlEncodingTransformer;
            _transformers["encoding"].Initialize(_manager.NetworkGlobal, _networkContext);
        }

        public void CycleCommunityFocus(int community, bool animated = true)
        {
            // only focus community if we're in spherical layout
            if (!_isSphericalLayout) return;

            var nextState = CycleCommunityState(community);

            switch (nextState)
            {
                case MultiLayoutContext.Community.CommunityState.None:
                    TransformNetwork("floor", animated);
                    break;
                case MultiLayoutContext.Community.CommunityState.Spider:
                    TransformNetwork("spider", animated);
                    break;
                case MultiLayoutContext.Community.CommunityState.Floor:
                    TransformNetwork("floor", animated);
                    break;
                default:
                    break;
            }
        }

        public void ToggleSphericalAndHairball(bool animated = true)
        {
            if (_isSphericalLayout)
            {
                foreach (var communityIdx in _manager.NetworkGlobal.Communities.Keys)
                {
                    ClearCommunityState(communityIdx);
                }

                TransformNetwork("spider", animated: false);
                _multiLayoutRenderer.UpdateRenderElements();
            }

            _isSphericalLayout = !_isSphericalLayout;

            string layout = _isSphericalLayout ? "spherical" : "hairball";

            TransformNetwork(layout, animated);
        }

        public void SetNodeSelected(int nodeID, bool selected)
        {
            _networkContext.Nodes[nodeID].Color = selected
                ? BaseSettings.NodeHighlightColor
                : _mlEncodingTransformer.GetNodeColor(_manager.NetworkGlobal.Nodes[nodeID]);

            _networkContext.Nodes[nodeID].Dirty = true;

            foreach (var link in _manager.NetworkGlobal.NodeLinkMatrix[nodeID])
            {
                _networkContext.Links[link.ID].ColorStart = selected
                    ? BaseSettings.LinkHighlightColor
                    : _mlEncodingTransformer.GetLinkColorStart(link);
                _networkContext.Links[link.ID].ColorEnd = selected
                    ? BaseSettings.LinkHighlightColor
                    : _mlEncodingTransformer.GetLinkColorEnd(link);
                _networkContext.Links[link.ID].Alpha = selected
                    ? BaseSettings.LinkHighlightColor.a
                    : _mlEncodingTransformer.GetLinkAlpha(link);
                _networkContext.Links[link.ID].Dirty = true;
            }

        }

        public void SetNodesSelected(List<int> nodeIDs, bool selected)
        {
            foreach (var nodeID in nodeIDs)
            {
                SetNodeSelected(nodeID, selected);
            }
        }

        public void SetCommunitySelected(int communityID, bool selected)
        {
            // TODO change comm color?
            _networkContext.Communities[communityID].Dirty = true;
        }

        public void SetCommunitiesSelected(List<int> communityIDs, bool selected)
        {
            // TODO change comm color?
            foreach (var commID in communityIDs)
            {
                SetCommunitySelected(commID, selected);
            }
        }

        public void ClearSelection()
        {
            foreach (var nodeID in _networkContext.Nodes.Keys)
            {
                SetNodeSelected(nodeID, false);
            }
            _multiLayoutRenderer.UpdateRenderElements();
        }

        void TransformNetwork(string layout, bool animated)
        {
            if (animated)
            {
                if (_curAnim != null)
                {
                    StopCoroutine(_curAnim);
                }

                _curAnim = StartCoroutine(CRAnimateLayout(layout));
            }
            else
            {
                _transformers[layout].ApplyTransformation();
                _networkContext.RecomputeGeometricProps(_manager.NetworkGlobal);
                _multiLayoutRenderer.UpdateRenderElements();
            }
        }

        void TransformNetworkNoRender(string layout)
        {
            _transformers[layout].ApplyTransformation();
            _networkContext.RecomputeGeometricProps(_manager.NetworkGlobal);
        }

        MultiLayoutContext.Community.CommunityState GetNextCommunityState(int community)
        {
            var curState = _networkContext.Communities[community].State;
            return (MultiLayoutContext.Community.CommunityState)((uint)(curState + 1) % (uint)MultiLayoutContext.Community.CommunityState.NumStates);
        }

        void ClearCommunityState(int community)
        {
            _spiderLayoutTransformer.SetFocusCommunityImm(community, false);
            _floorLayoutTransformer.SetFocusCommunityImm(community, false);
            _manager.NetworkGlobal.Communities[community].Focus = false;
            _networkContext.Communities[community].State = MultiLayoutContext.Community.CommunityState.None;
        }

        MultiLayoutContext.Community.CommunityState CycleCommunityState(int community)
        {
            var nextState = GetNextCommunityState(community);

            if (nextState == MultiLayoutContext.Community.CommunityState.Spider)
            {
                _spiderLayoutTransformer.SetFocusCommunityQueue(community, true);
            }
            else if (nextState == MultiLayoutContext.Community.CommunityState.Floor)
            {
                _spiderLayoutTransformer.SetFocusCommunityImm(community, false);
                _floorLayoutTransformer.SetFocusCommunityQueue(community, true);
            }
            else
            {
                _floorLayoutTransformer.SetFocusCommunityQueue(community, false);
            }

            bool isCommFocused = nextState == MultiLayoutContext.Community.CommunityState.Floor || nextState == MultiLayoutContext.Community.CommunityState.Spider;
            _manager.NetworkGlobal.Communities[community].Focus = isCommFocused;
            _networkContext.Communities[community].State = nextState;

            return nextState;
        }

        IEnumerator CRAnimateLayout(string layout)
        {
            float dur = 1.0f;
            var interpolator = _transformers[layout].GetInterpolator();

            yield return AnimationUtils.Lerp(dur, t =>
            {
                interpolator.Interpolate(t);
                _networkContext.RecomputeGeometricProps(_manager.NetworkGlobal);
                _multiLayoutRenderer.UpdateRenderElements();
            });

            // // update render elements one more time to update input elements after recomputing geometric info
            // _networkContext.RecomputeGeometricProps(_manager.NetworkGlobal);
            // _multiLayoutRenderer.UpdateRenderElements();

            _curAnim = null;
        }
    }
}