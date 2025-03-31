using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        MultiLayoutContext _networkContext = new MultiLayoutContext();
        public MultiLayoutContext Context { get { return _networkContext; } }

        NetworkManager _manager;

        NetworkInput _multiLayoutInput;
        NetworkRenderer _multiLayoutRenderer;

        Dictionary<string, NetworkContextTransformer> _transformers = new Dictionary<string, NetworkContextTransformer>();

        // keep a reference to sphericallayout specifically to focus on individual communities
        SphericalLayoutTransformer _sphericalLayoutTransformer;

        // keep a reference to spiderlayout specifically to focus on individual communities
        SpiderLayoutTransformer _spiderLayoutTransformer;

        // keep a reference to bringNodeLayout to focus on individual nodes
        BringNodeTransformer _bringNodeTransformer;

        // keep a reference to floorLayout specifically to focus on individual communities
        FloorLayoutTransformer _floorLayoutTransformer;

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
            _sphericalLayoutTransformer.UpdateCommOnNextApply(_networkContext.Communities.Keys.ToList());
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

            _sphericalLayoutTransformer = GetComponentInChildren<SphericalLayoutTransformer>();
            _transformers["spherical"] = _sphericalLayoutTransformer;
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

            _transformers["encoding"] = GetComponentInChildren<MLEncodingTransformer>();
            _transformers["encoding"].Initialize(_manager.NetworkGlobal, _networkContext);

            _transformers["highlight"] = GetComponentInChildren<HighlightTransformer>();
            _transformers["highlight"].Initialize(_manager.NetworkGlobal, _networkContext);
        }

        public void CycleCommunityFocus(int community, bool animated = true)
        {
            // only focus community if we're in spherical layout
            if (!_isSphericalLayout) return;

            var nextState = CycleCommunityState(community);

            switch (nextState)
            {
                case MultiLayoutContext.CommunityState.None:
                    TransformNetwork("spherical", animated);
                    break;
                case MultiLayoutContext.CommunityState.Spider:
                    TransformNetwork("spider", animated);
                    break;
                case MultiLayoutContext.CommunityState.Floor:
                    TransformNetwork("floor", animated);
                    break;
                default:
                    break;
            }
        }

        public void ToggleSphericalAndHairball(bool animated = true)
        {
            _isSphericalLayout = !_isSphericalLayout;

            string layout = _isSphericalLayout ? "spherical" : "hairball";

            if (layout == "spherical")
            {
                foreach (var commID in _networkContext.Communities.Keys)
                {
                    _sphericalLayoutTransformer.UpdateCommOnNextApply(commID);
                    ClearCommunityState(commID);
                }
            }

            TransformNetwork(layout, animated);
        }

        public void UpdateSelectedElements()
        {
            TransformNetwork("highlight", animated: false);
        }

        // layout = [spherical, spider, floor]
        public void SetLayout(List<int> commIDs, string layout)
        {
            foreach (var commID in commIDs)
            {
                bool isCommFocused = layout == "floor" || layout == "spider";
                _manager.NetworkGlobal.Communities[commID].Focus = isCommFocused;
                _networkContext.Communities[commID].State = MultiLayoutContext.StrToState(layout);

                if (layout == "spider")
                {
                    _spiderLayoutTransformer.UpdateOnNextApply(commID);
                }
                else if (layout == "floor")
                {
                    _floorLayoutTransformer.UpdateOnNextApply(commID);
                }
                else if (layout == "spherical")
                {
                    _sphericalLayoutTransformer.UpdateCommOnNextApply(commID);
                }
            }

            TransformNetwork(layout, animated: true);

        }

        public void SetNodesBrought(List<int> nodeIDs, bool brought)
        {
            foreach (var nodeID in nodeIDs)
            {
                _bringNodeTransformer.SetFocusNodeQueue(nodeID, brought);
            }

            TransformNetwork("bringNode", animated: true);
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

        MultiLayoutContext.CommunityState GetNextCommunityState(int community)
        {
            var curState = _networkContext.Communities[community].State;
            return (MultiLayoutContext.CommunityState)((uint)(curState + 1) % (uint)MultiLayoutContext.CommunityState.NumStates);
        }

        void ClearCommunityState(int community)
        {
            _manager.NetworkGlobal.Communities[community].Focus = false;
            _networkContext.Communities[community].State = MultiLayoutContext.CommunityState.None;
        }

        MultiLayoutContext.CommunityState CycleCommunityState(int community)
        {
            var nextState = GetNextCommunityState(community);

            if (nextState == MultiLayoutContext.CommunityState.Spider)
            {
                _spiderLayoutTransformer.UpdateOnNextApply(community);
            }
            else if (nextState == MultiLayoutContext.CommunityState.Floor)
            {
                _floorLayoutTransformer.UpdateOnNextApply(community);
            }
            else
            {
                _sphericalLayoutTransformer.UpdateCommOnNextApply(community);
            }

            bool isCommFocused = nextState == MultiLayoutContext.CommunityState.Floor || nextState == MultiLayoutContext.CommunityState.Spider;
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