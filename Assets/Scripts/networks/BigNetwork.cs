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

        // keep a reference to floorLayout specifically to focus on individual communities
        FloorLayout _floorLayout;

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
            _networkContext.Update(_manager.NetworkGlobal);

            _bigNetworkInput = GetComponent<NetworkInput>();
            _bigNetworkRenderer = GetComponentInChildren<NetworkRenderer>();

            _bigNetworkInput.Initialize();
            _bigNetworkRenderer.Initialize(_networkContext);

            InitializeLayouts();

            _isSphericalLayout = true;
            UpdateWithLayoutUnanimated("spherical");
        }

        public override void UpdateRenderElements()
        {
            _bigNetworkRenderer.UpdateRenderElements();
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

            _floorLayout = GetComponentInChildren<FloorLayout>();
            _layouts["floor"] = _floorLayout;
            _layouts["floor"].Initialize(_networkContext);
        }

        public void CycleCommunityFocus(int community, bool animated = true)
        {
            var nextState = CycleCommunityState(community);

            if (animated)
            {
                switch (nextState)
                {
                    case NetworkContext3D.Community.CommunityState.None:
                        UpdateWithLayoutAnimated("floor");
                        break;
                    case NetworkContext3D.Community.CommunityState.Spider:
                        UpdateWithLayoutAnimated("spider");
                        break;
                    case NetworkContext3D.Community.CommunityState.Floor:
                        UpdateWithLayoutAnimated("floor");
                        break;
                    default:
                        break;
                }
            }
            else
            {
                switch (nextState)
                {
                    case NetworkContext3D.Community.CommunityState.None:
                        UpdateWithLayoutUnanimated("floor");
                        break;
                    case NetworkContext3D.Community.CommunityState.Spider:
                        UpdateWithLayoutUnanimated("spider");
                        break;
                    case NetworkContext3D.Community.CommunityState.Floor:
                        UpdateWithLayoutUnanimated("floor");
                        break;
                    default:
                        break;
                }
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
            _networkContext.RecomputeGeometricProps(_manager.NetworkGlobal);
            _bigNetworkRenderer.UpdateRenderElements();
        }

        NetworkContext3D.Community.CommunityState GetNextCommunityState(int community)
        {
            var curState = _networkContext.Communities[community].State;
            return (NetworkContext3D.Community.CommunityState)((uint)(curState + 1) % (uint)NetworkContext3D.Community.CommunityState.NumStates);
        }

        void ClearCommunityState(int community)
        {
            _spiderLayout.SetFocusCommunityNoRelayout(community, false);
            _floorLayout.SetFocusCommunityNoRelayout(community, false);
            _manager.NetworkGlobal.Communities[community].Focus = false;
            _networkContext.Communities[community].State = NetworkContext3D.Community.CommunityState.None;
        }

        NetworkContext3D.Community.CommunityState CycleCommunityState(int community)
        {
            var nextState = GetNextCommunityState(community);

            if (nextState == NetworkContext3D.Community.CommunityState.Spider)
            {
                _spiderLayout.SetFocusCommunity(community, true);
            }
            else if (nextState == NetworkContext3D.Community.CommunityState.Floor)
            {
                _spiderLayout.SetFocusCommunityNoRelayout(community, false);
                _floorLayout.SetFocusCommunity(community, true);
            }
            else
            {
                _floorLayout.SetFocusCommunity(community, false);
            }

            _manager.NetworkGlobal.Communities[community].Focus = nextState == NetworkContext3D.Community.CommunityState.Floor || nextState == NetworkContext3D.Community.CommunityState.Spider;
            _networkContext.Communities[community].State = nextState;

            return nextState;
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
            _networkContext.RecomputeGeometricProps(_manager.NetworkGlobal);
            _bigNetworkRenderer.UpdateRenderElements();

            _curAnim = null;
        }
    }
}