using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class MultiLayoutNetwork : Network
    {
        NetworkManager _manager;

        NetworkInput _bigNetworkInput;
        NetworkRenderer _bigNetworkRenderer;
        MultiLayoutContext _networkContext = new MultiLayoutContext();

        Dictionary<string, NetworkContextTransformer> _transformers = new Dictionary<string, NetworkContextTransformer>();

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

            _bigNetworkInput = GetComponent<NetworkInput>();
            _bigNetworkInput.Initialize();

            InitializeTransformers();

            _isSphericalLayout = true;
            TransformNetworkNoRender("spherical");

            _bigNetworkRenderer = GetComponentInChildren<NetworkRenderer>();
            _bigNetworkRenderer.Initialize(_networkContext);
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
                _bigNetworkRenderer.UpdateRenderElements();
            }

            _isSphericalLayout = !_isSphericalLayout;

            string layout = _isSphericalLayout ? "spherical" : "hairball";

            TransformNetwork(layout, animated);
        }

        public void ToggleFocusNodes(int[] nodeIDs, bool animated = true)
        {
            foreach (var nodeID in nodeIDs)
            {
                bool setFocus = _networkContext.Nodes[nodeID].State == MultiLayoutContext.Node.NodeState.None;

                _bringNodeTransformer.SetFocusNodeQueue(nodeID, setFocus);

                _networkContext.Nodes[nodeID].State = setFocus ? MultiLayoutContext.Node.NodeState.Bring : MultiLayoutContext.Node.NodeState.None;
            }

            TransformNetwork("bringNode", animated);
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
                _bigNetworkRenderer.UpdateRenderElements();
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
                _bigNetworkRenderer.UpdateRenderElements();
            });

            // update render elements one more time to update input elements after recomputing geometric info
            _networkContext.RecomputeGeometricProps(_manager.NetworkGlobal);
            _bigNetworkRenderer.UpdateRenderElements();

            _curAnim = null;
        }
    }
}