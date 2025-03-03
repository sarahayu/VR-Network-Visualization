using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class BigNetwork : Network
    {
        NetworkManager _manager;

        NetworkInput _bigNetworkInput;
        NetworkRenderer _bigNetworkRenderer;
        NetworkContext3D _networkContext = new NetworkContext3D();

        Dictionary<string, NetworkLayout> _layouts = new Dictionary<string, NetworkLayout>();
        // keep a reference to spiderlayout specifically to focus on individual communities
        SpiderLayout _spiderLayout;
        // keep a reference to sphericallayout to focus on individual nodes
        SphericalLayout _sphericalLayout;
        bool _isSphericalLayout;
        Coroutine _curAnim = null;

        public bool IsSphericalLayout { get { return _isSphericalLayout; } }

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
            _networkContext.Update(_manager.NetworkData);

            _bigNetworkInput = GetComponent<NetworkInput>();
            _bigNetworkRenderer = GetComponentInChildren<NetworkRenderer>();

            _bigNetworkInput.Initialize();
            _bigNetworkRenderer.Initialize(_networkContext);

            InitializeLayouts();

            _isSphericalLayout = true;
            UpdateWithLayoutUnanimated("spherical");
        }

        public override void UpdateLayouts()
        {
            // TODO implement
        }

        public override void UpdateRenderElements()
        {
            // TODO implement
        }

        public override void DrawPreview()
        {
            Draw();
        }

        void Draw()
        {
            _bigNetworkRenderer.Draw();
        }

        void InitializeLayouts()
        {
            _layouts["hairball"] = GetComponentInChildren<HairballLayout>();
            _layouts["hairball"].Initialize(_networkContext);

            _sphericalLayout = GetComponentInChildren<SphericalLayout>();
            _layouts["spherical"] = _sphericalLayout;
            _layouts["spherical"].Initialize(_networkContext);

            _spiderLayout = GetComponentInChildren<SpiderLayout>();
            _layouts["spider"] = _spiderLayout;
            _layouts["spider"].Initialize(_networkContext);
        }

        public void ToggleCommunityFocus(int community, bool animated = true)
        {
            bool isFocused = _manager.NetworkData.Communities[community].Focus;

            _spiderLayout.SetFocusCommunity(community, !isFocused);
            _manager.NetworkData.Communities[community].Focus = !isFocused;

            if (animated)
            {
                ToggleCommunityFocusAnimated();
            }
            else
            {
                ToggleCommunityFocusUnanimated();
            }
        }

        public void ToggleSphericalAndHairball(bool animated = true)
        {
            if (_isSphericalLayout)
            {
                foreach (var communityIdx in _manager.NetworkData.Communities.Keys)
                {
                    _spiderLayout.SetFocusCommunity(communityIdx, false);
                    _manager.NetworkData.Communities[communityIdx].Focus = false;
                }

                UpdateWithLayoutUnanimated("spider");
                _bigNetworkRenderer.UpdateRenderElements();
            }

            _isSphericalLayout = !_isSphericalLayout;

            string layout = _isSphericalLayout ? "spherical" : "hairball";

            if (animated)
            {
                UpdateWithLayoutAnimated(layout);
            }
            else
            {
                UpdateWithLayoutUnanimated(layout);
            }
        }

        public void HoverNode(int nodeID)
        {
            _sphericalLayout.SetHoverNode(nodeID);
            _bigNetworkRenderer.UpdateRenderElements();
        }

        public void UnhoverNode(int nodeID)
        {
            _sphericalLayout.ClearHoverNode();
            _bigNetworkRenderer.UpdateRenderElements();
        }

        void UpdateWithLayoutAnimated(string layout)
        {
            if (_curAnim != null)
            {
                StopCoroutine(_curAnim);
            }

            _curAnim = StartCoroutine(CRAnimateLayout(layout));
        }

        void UpdateWithLayoutUnanimated(string layout)
        {
            _layouts[layout].ApplyLayout();
            _networkContext.RecomputeGeometricProps(_manager.NetworkData);
            _bigNetworkRenderer.UpdateRenderElements();
        }

        void ToggleCommunityFocusAnimated()
        {
            if (_curAnim != null)
            {
                StopCoroutine(_curAnim);
            }

            _curAnim = StartCoroutine(CRAnimateLayout("spider"));
        }

        void ToggleCommunityFocusUnanimated()
        {
            _layouts["spider"].ApplyLayout();
            _bigNetworkRenderer.UpdateRenderElements();
        }

        IEnumerator CRAnimateLayout(string layout)
        {
            float dur = 1.0f;
            var interpolator = _layouts[layout].GetInterpolator();

            yield return AnimationUtils.Lerp(dur, t =>
            {
                interpolator.Interpolate(t);
                _bigNetworkRenderer.UpdateRenderElements();
            });

            // update render elements one more time to update input elements after recomputing geometric info
            _networkContext.RecomputeGeometricProps(_manager.NetworkData);
            _bigNetworkRenderer.UpdateRenderElements();

            _curAnim = null;
        }
    }
}