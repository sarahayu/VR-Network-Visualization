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

        string _curLayout;
        Coroutine _curAnim = null;

        public string CurLayout { get { return _curLayout; } }

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
            _networkContext.Update(_manager.Data);

            _bigNetworkInput = GetComponent<NetworkInput>();
            _bigNetworkRenderer = GetComponentInChildren<NetworkRenderer>();

            _bigNetworkInput.Initialize();
            _bigNetworkRenderer.Initialize(_networkContext);

            InitializeLayouts();
            ChangeToLayout("spherical", false);
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

            _layouts["spherical"] = GetComponentInChildren<SphericalLayout>();
            _layouts["spherical"].Initialize(_networkContext);

            _spiderLayout = GetComponentInChildren<SpiderLayout>();
            _layouts["spider"] = _spiderLayout;
            _layouts["spider"].Initialize(_networkContext);
        }

        public void ToggleCommunityFocus(int community, bool animated = true)
        {
            bool isFocused = _manager.Data.Communities[community].focus;

            _spiderLayout.SetFocusCommunity(community, !isFocused);

            _curLayout = "spider";

            if (animated)
            {
                ToggleCommunityFocusAnimated();
            }
            else
            {
                ToggleCommunityFocusUnanimated();
            }
        }

        public void ChangeToLayout(string layout, bool animated = true)
        {
            _curLayout = layout;

            if (animated)
            {
                ChangeToLayoutAnimated(layout);
            }
            else
            {
                ChangeToLayoutUnanimated(layout);
            }
        }

        void ChangeToLayoutAnimated(string layout)
        {
            if (_curAnim != null)
            {
                StopCoroutine(_curAnim);
            }

            _curAnim = StartCoroutine(CRAnimateLayout(layout));
        }

        void ChangeToLayoutUnanimated(string layout)
        {
            _layouts[layout].ApplyLayout();
            _networkContext.RecomputeGeometricProps(_manager.Data);
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
            _networkContext.RecomputeGeometricProps(_manager.Data);
            _bigNetworkRenderer.UpdateRenderElements();

            _curAnim = null;
        }
    }
}