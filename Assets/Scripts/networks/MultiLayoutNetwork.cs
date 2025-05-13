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

        // keep a reference to clusterlayout specifically to focus on individual communities
        ClusterLayoutTransformer _clusterLayoutTransformer;

        // keep a reference to bringNodeLayout to focus on individual nodes
        BringNodeTransformer _bringNodeTransformer;

        // keep a reference to floorLayout specifically to focus on individual communities
        FloorLayoutTransformer _floorLayoutTransformer;
        // keep a reference to encoding specifically to change encoding
        MLEncodingTransformer _encodingTransformer;

        bool _isSphericalLayout;
        Coroutine _curAnim = null;
        Coroutine _curNodeMover = null;

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

            _clusterLayoutTransformer = GetComponentInChildren<ClusterLayoutTransformer>();
            _transformers["cluster"] = _clusterLayoutTransformer;
            _transformers["cluster"].Initialize(_manager.NetworkGlobal, _networkContext);

            _floorLayoutTransformer = GetComponentInChildren<FloorLayoutTransformer>();
            _transformers["floor"] = _floorLayoutTransformer;
            _transformers["floor"].Initialize(_manager.NetworkGlobal, _networkContext);

            _encodingTransformer = GetComponentInChildren<MLEncodingTransformer>();
            _transformers["encoding"] = _encodingTransformer;
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
                case MultiLayoutContext.CommunityState.Cluster:
                    TransformNetwork("cluster", animated);
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

        // layout = [spherical, cluster, floor]
        public void SetLayout(List<int> commIDs, string layout)
        {
            foreach (var commID in commIDs)
            {
                bool isCommFocused = layout == "floor" || layout == "cluster";
                _manager.NetworkGlobal.Communities[commID].Focus = isCommFocused;
                _networkContext.Communities[commID].State = MultiLayoutContext.StrToState(layout);

                if (layout == "cluster")
                {
                    _clusterLayoutTransformer.UpdateOnNextApply(commID);
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

        public void StartNodeMove(int nodeID, Transform toTrack)
        {
            if (_curNodeMover != null)
            {
                StopCoroutine(_curNodeMover);
            }

            _curNodeMover = StartCoroutine(CRMoveNode(nodeID, toTrack));
        }

        public void StartNodesMove(List<int> nodeIDs, List<Transform> toTracks)
        {
            if (_curNodeMover != null)
            {
                StopCoroutine(_curNodeMover);
            }

            _curNodeMover = StartCoroutine(CRMoveNodes(nodeIDs, toTracks));
        }

        public void EndNodeMove(int nodeID)
        {
            if (_curNodeMover != null)
            {
                StopCoroutine(_curNodeMover);
            }
        }

        public void EndNodesMove(List<int> nodeIDs)
        {
            if (_curNodeMover != null)
            {
                StopCoroutine(_curNodeMover);
            }
        }

        public void SetNodeSizeEncoding(Func<VidiGraph.Node, float> func)
        {
            _networkContext.GetNodeSize = func;
            TransformNetwork("encoding", animated: false);

        }

        public Transform GetNodeTransform(int nodeID)
        {
            return _multiLayoutRenderer.GetNodeTransform(nodeID);
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

            if (nextState == MultiLayoutContext.CommunityState.Cluster)
            {
                _clusterLayoutTransformer.UpdateOnNextApply(community);
            }
            else if (nextState == MultiLayoutContext.CommunityState.Floor)
            {
                _floorLayoutTransformer.UpdateOnNextApply(community);
            }
            else
            {
                _sphericalLayoutTransformer.UpdateCommOnNextApply(community);
            }

            bool isCommFocused = nextState == MultiLayoutContext.CommunityState.Floor || nextState == MultiLayoutContext.CommunityState.Cluster;
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

            interpolator.Interpolate(1f);
            _networkContext.RecomputeGeometricProps(_manager.NetworkGlobal);
            _multiLayoutRenderer.UpdateRenderElements();

            _curAnim = null;
        }

        IEnumerator CRMoveNode(int nodeID, Transform toTrack)
        {
            for (; ; )
            {
                _networkContext.Nodes[nodeID].Position = toTrack.position;
                _networkContext.Nodes[nodeID].Dirty = true;
                _networkContext.RecomputeGeometricProps(_manager.NetworkGlobal);
                _multiLayoutRenderer.UpdateRenderElements();
                yield return null;
            }
        }

        IEnumerator CRMoveNodes(List<int> nodeIDs, List<Transform> toTracks)
        {
            for (; ; )
            {
                for (int i = 0; i < nodeIDs.Count; i++)
                {
                    _networkContext.Nodes[nodeIDs[i]].Position = toTracks[i].position;
                    _networkContext.Nodes[nodeIDs[i]].Dirty = true;
                }
                _networkContext.RecomputeGeometricProps(_manager.NetworkGlobal);
                _multiLayoutRenderer.UpdateRenderElements();
                yield return null;
            }
        }
    }
}