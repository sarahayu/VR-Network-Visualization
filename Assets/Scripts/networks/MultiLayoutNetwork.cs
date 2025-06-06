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

            public Color CommSelectColor;
            public Color NodeSelectColor;
            public Color LinkSelectColor;
            public Color CommHoverColor;
            public Color NodeHoverColor;
            public Color LinkHoverColor;

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
        public MultiLayoutContext Context { get { return _context; } }

        NetworkManager _manager;

        NetworkInput _input;
        NetworkRenderer _renderer;

        MultiLayoutContext _context = new MultiLayoutContext();

        Dictionary<string, NetworkContextTransformer> _transformers = new Dictionary<string, NetworkContextTransformer>();

        // keep a reference to sphericallayout to focus on individual communities
        SphericalLayoutTransformer _sphericalLayoutTransformer;

        // keep a reference to clusterlayout to focus on individual communities
        ClusterLayoutTransformer _clusterLayoutTransformer;

        // keep a reference to bringNodeLayout to focus on individual nodes
        BringNodeTransformer _bringNodeTransformer;

        // keep a reference to floorLayout to focus on individual communities
        FloorLayoutTransformer _floorLayoutTransformer;
        // keep a reference to encoding to change encoding
        MLEncodingTransformer _encodingTransformer;

        bool _isSphericalLayout;

        Coroutine _curAnim = null;
        Coroutine _curNodeMover = null;
        Coroutine _curCommMover = null;

        void Update()
        {
            Draw();
        }

        public override void Initialize()
        {
            GetManager();

            InitContext();
            InitInput();
            InitTransformers();

            // apply initial transformations before first render so we don't get a weird jump
            TransformNetworkNoRender("encoding");
            _isSphericalLayout = true;
            _sphericalLayoutTransformer.UpdateCommsOnNextApply(_context.Communities.Keys.ToList());
            TransformNetworkNoRender("spherical");

            InitRenderer();
        }

        public override void UpdateRenderElements()
        {
            _renderer.UpdateRenderElements();
        }

        public override void DrawPreview()
        {
            Draw();
        }

        public void ToggleSphericalAndHairball(bool animated = true)
        {
            bool newIsSphericalLayout = !_isSphericalLayout;

            string layout = newIsSphericalLayout ? "spherical" : "hairball";

            if (layout == "spherical")
            {
                foreach (var commID in _context.Communities.Keys)
                {
                    QueueLayoutChange(commID, "spherical");
                    ClearCommunityState(commID);
                }
            }

            TransformNetwork(layout, animated);

            _isSphericalLayout = newIsSphericalLayout;
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
                _context.Communities[commID].State = MultiLayoutContext.StrToState(layout);

                QueueLayoutChange(commID, layout);
            }

            TransformNetwork(layout, animated: true);

        }

        void QueueLayoutChange(int commID, string layout)
        {
            switch (layout)
            {
                case "cluster": _clusterLayoutTransformer.UpdateOnNextApply(commID); break;
                case "floor": _floorLayoutTransformer.UpdateOnNextApply(commID); break;
                case "spherical": _sphericalLayoutTransformer.UpdateCommOnNextApply(commID); break;
                default: break;
            }
        }

        public void BringNodes(List<int> nodeIDs)
        {
            _bringNodeTransformer.UpdateOnNextApply(nodeIDs);

            TransformNetwork("bringNode", animated: true);
        }

        public void ReturnNodes(List<int> nodeIDs)
        {
            _sphericalLayoutTransformer.UpdateNodesOnNextApply(nodeIDs);

            TransformNetwork("spherical", animated: true);
        }

        public void StartNodeMove(int nodeID, Transform toTrack)
        {
            CoroutineUtils.StopIfRunning(this, _curNodeMover);
            _curNodeMover = StartCoroutine(CRMoveNode(nodeID, toTrack));
        }

        public void StartNodesMove(List<int> nodeIDs, List<Transform> toTracks)
        {
            CoroutineUtils.StopIfRunning(this, _curNodeMover);
            _curNodeMover = StartCoroutine(CRMoveNodes(nodeIDs, toTracks));
        }

        public void EndNodeMove(int nodeID)
        {
            CoroutineUtils.StopIfRunning(this, _curNodeMover);
            UpdateNetwork(
                updateCommunityProps: true,
                updateStorage: true,
                updateRenderElements: true
            );
        }

        public void EndNodesMove()
        {
            CoroutineUtils.StopIfRunning(this, _curNodeMover);
            UpdateNetwork(
                updateCommunityProps: true,
                updateStorage: true,
                updateRenderElements: true
            );
        }

        public void StartCommMove(int commID, Transform toTrack)
        {
            CoroutineUtils.StopIfRunning(this, _curCommMover);

            var nodeIDs = _manager.NetworkGlobal.Communities[commID].Nodes.Select(n => n.ID).ToList();
            var nodeTransforms = nodeIDs.Select(nid => GetNodeTransform(nid)).ToList();

            _curCommMover = StartCoroutine(CRMoveComm(toTrack, nodeIDs, nodeTransforms));
        }

        public void EndCommMove()
        {
            CoroutineUtils.StopIfRunning(this, _curCommMover);
            UpdateNetwork(
                updateCommunityProps: true,
                updateStorage: true,
                updateRenderElements: true
            );
        }

        public void SetNodeSizeEncoding(Func<VidiGraph.Node, float> func)
        {
            _context.GetNodeSize = func;
            TransformNetwork("encoding", animated: false);
        }

        public Transform GetNodeTransform(int nodeID)
        {
            return _renderer.GetNodeTransform(nodeID);
        }

        public Transform GetCommTransform(int commID)
        {
            return _renderer.GetCommTransform(commID);
        }

        public void SetNodesSize(List<int> nodeIDs, float size, bool updateStorage, bool updateRenderElements)
        {
            foreach (var node in nodeIDs.Select(nid => _context.Nodes[nid]))
            {
                node.Size = size;
                node.Dirty = true;
            }

            UpdateNetwork(
                updateCommunityProps: true,
                updateStorage: updateStorage,
                updateRenderElements: updateRenderElements
            );
        }

        public void SetNodesColor(List<int> nodeIDs, Color color, bool updateStorage, bool updateRenderElements)
        {
            foreach (var node in nodeIDs.Select(nid => _context.Nodes[nid]))
            {
                node.Color = color;
                node.Dirty = true;
            }

            UpdateNetwork(
                updateCommunityProps: true,
                updateStorage: updateStorage,
                updateRenderElements: updateRenderElements
            );
        }

        public void SetLinksWidth(List<int> linkIDs, float width, bool updateStorage, bool updateRenderElements)
        {
            foreach (var link in linkIDs.Select(lid => _context.Links[lid]))
            {
                link.Width = width;
                link.Dirty = true;
            }

            UpdateNetwork(
                updateCommunityProps: true,
                updateStorage: updateStorage,
                updateRenderElements: updateRenderElements
            );
        }

        public void SetLinksColorStart(List<int> linkIDs, Color color, bool updateStorage, bool updateRenderElements)
        {
            foreach (var link in linkIDs.Select(lid => _context.Links[lid]))
            {
                link.ColorStart = color;
                link.Dirty = true;
            }

            UpdateNetwork(
                updateCommunityProps: true,
                updateStorage: updateStorage,
                updateRenderElements: updateRenderElements
            );
        }

        public void SetLinksColorEnd(List<int> linkIDs, Color color, bool updateStorage, bool updateRenderElements)
        {
            foreach (var link in linkIDs.Select(lid => _context.Links[lid]))
            {
                link.ColorEnd = color;
                link.Dirty = true;
            }

            UpdateNetwork(
                updateCommunityProps: true,
                updateStorage: updateStorage,
                updateRenderElements: updateRenderElements
            );
        }

        public void SetLinksAlpha(List<int> linkIDs, float alpha, bool updateStorage, bool updateRenderElements)
        {
            foreach (var link in linkIDs.Select(lid => _context.Links[lid]))
            {
                link.Alpha = alpha;
                link.Dirty = true;
            }

            UpdateNetwork(
                updateCommunityProps: true,
                updateStorage: updateStorage,
                updateRenderElements: updateRenderElements
            );
        }

        public void SetLinksBundlingStrength(List<int> linkIDs, float bundlingStrength, bool updateStorage, bool updateRenderElements)
        {
            foreach (var link in linkIDs.Select(lid => _context.Links[lid]))
            {
                link.BundlingStrength = bundlingStrength;
                link.Dirty = true;
            }

            UpdateNetwork(
                updateCommunityProps: true,
                updateStorage: updateStorage,
                updateRenderElements: updateRenderElements
            );
        }

        public void SetLinksBundleStart(List<int> linkIDs, bool bundleStart, bool updateStorage, bool updateRenderElements)
        {
            foreach (var link in linkIDs.Select(lid => _context.Links[lid]))
            {
                link.BundleStart = bundleStart;
                link.Dirty = true;
            }

            UpdateNetwork(
                updateCommunityProps: true,
                updateStorage: updateStorage,
                updateRenderElements: updateRenderElements
            );
        }

        public void SetLinksBundleEnd(List<int> linkIDs, bool bundleEnd, bool updateStorage, bool updateRenderElements)
        {
            foreach (var link in linkIDs.Select(lid => _context.Links[lid]))
            {
                link.BundleEnd = bundleEnd;
                link.Dirty = true;
            }

            UpdateNetwork(
                updateCommunityProps: true,
                updateStorage: updateStorage,
                updateRenderElements: updateRenderElements
            );
        }


        public void SetNodesPosition(List<int> nodeIDs, Vector3 position, bool updateStorage, bool updateRenderElements)
        {
            foreach (var node in nodeIDs.Select(nid => _context.Nodes[nid]))
            {
                node.Position = position;
                node.Dirty = true;
            }

            UpdateNetwork(
                updateCommunityProps: true,
                updateStorage: updateStorage,
                updateRenderElements: updateRenderElements
            );
        }

        public void SetNodesPosition(List<int> nodeIDs, List<Vector3> positions, bool updateStorage, bool updateRenderElements)
        {
            var nodes = nodeIDs.Select(nid => _context.Nodes[nid]).ToList();

            for (int i = 0; i < nodeIDs.Count; i++)
            {
                var node = nodes[i];

                node.Position = positions[i];
                node.Dirty = true;
            }

            UpdateNetwork(
                updateCommunityProps: true,
                updateStorage: updateStorage,
                updateRenderElements: updateRenderElements
            );
        }

        /*=============== start private methods ===================*/

        void GetManager()
        {
            _manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
        }

        void InitContext()
        {
            _context.SetFromGlobal(_manager.NetworkGlobal);
            SetContextSettings();
        }

        void InitInput()
        {
            _input = GetComponent<NetworkInput>();
            _input.Initialize();
        }

        void InitRenderer()
        {
            _renderer = GetComponentInChildren<NetworkRenderer>();
            _renderer.Initialize(_context);
        }

        void Draw()
        {
            _renderer.Draw();
        }

        void SetContextSettings()
        {
            _context.ContextSettings.NodeScale = BaseSettings.NodeScale;
            _context.ContextSettings.LinkWidth = BaseSettings.LinkWidth;
            _context.ContextSettings.EdgeBundlingStrength = BaseSettings.EdgeBundlingStrength;
            _context.ContextSettings.CommSelectColor = BaseSettings.CommSelectColor;
            _context.ContextSettings.NodeSelectColor = BaseSettings.NodeSelectColor;
            _context.ContextSettings.LinkSelectColor = BaseSettings.LinkSelectColor;
            _context.ContextSettings.CommHoverColor = BaseSettings.CommHoverColor;
            _context.ContextSettings.NodeHoverColor = BaseSettings.NodeHoverColor;
            _context.ContextSettings.LinkHoverColor = BaseSettings.LinkHoverColor;
            _context.ContextSettings.LinkMinimumAlpha = BaseSettings.LinkMinimumAlpha;
            _context.ContextSettings.LinkNormalAlphaFactor = BaseSettings.LinkNormalAlphaFactor;
            _context.ContextSettings.LinkContextAlphaFactor = BaseSettings.LinkContextAlphaFactor;
            _context.ContextSettings.LinkContext2FocusAlphaFactor = BaseSettings.LinkContext2FocusAlphaFactor;
        }

        void InitTransformers()
        {
            _transformers["hairball"] = GetComponentInChildren<HairballLayoutTransformer>();
            _transformers["hairball"].Initialize(_manager.NetworkGlobal, _context);

            _sphericalLayoutTransformer = GetComponentInChildren<SphericalLayoutTransformer>();
            _transformers["spherical"] = _sphericalLayoutTransformer;
            _transformers["spherical"].Initialize(_manager.NetworkGlobal, _context);

            _bringNodeTransformer = GetComponentInChildren<BringNodeTransformer>();
            _transformers["bringNode"] = _bringNodeTransformer;
            _transformers["bringNode"].Initialize(_manager.NetworkGlobal, _context);

            _clusterLayoutTransformer = GetComponentInChildren<ClusterLayoutTransformer>();
            _transformers["cluster"] = _clusterLayoutTransformer;
            _transformers["cluster"].Initialize(_manager.NetworkGlobal, _context);

            _floorLayoutTransformer = GetComponentInChildren<FloorLayoutTransformer>();
            _transformers["floor"] = _floorLayoutTransformer;
            _transformers["floor"].Initialize(_manager.NetworkGlobal, _context);

            _encodingTransformer = GetComponentInChildren<MLEncodingTransformer>();
            _transformers["encoding"] = _encodingTransformer;
            _transformers["encoding"].Initialize(_manager.NetworkGlobal, _context);

            _transformers["highlight"] = GetComponentInChildren<HighlightTransformer>();
            _transformers["highlight"].Initialize(_manager.NetworkGlobal, _context);
        }


        void TransformNetwork(string layout, bool animated = true)
        {
            if (animated)
            {
                if (CoroutineUtils.StopIfRunning(this, _curAnim))
                {
                    // update network since we cancelled coroutine prematurely

                    UpdateNetwork(
                        updateCommunityProps: true,
                        updateStorage: false,
                        updateRenderElements: true
                    );
                }

                _curAnim = StartCoroutine(CRAnimateLayout(layout));
            }
            else
            {
                _transformers[layout]?.ApplyTransformation();

                UpdateNetwork(
                    updateCommunityProps: true,
                    updateStorage: true,
                    updateRenderElements: true
                );
            }
        }

        void TransformNetworkNoRender(string layout, bool updateStorage = true)
        {
            _transformers[layout].ApplyTransformation();
            UpdateCommunityProps();
            if (updateStorage) UpdateStorage();
        }

        void ClearCommunityState(int community)
        {
            _context.Communities[community].State = MultiLayoutContext.CommunityState.None;
        }

        IEnumerator CRAnimateLayout(string layout)
        {
            float dur = 1.0f;
            var interpolator = _transformers[layout]?.GetInterpolator();

            if (interpolator == null) yield return null;

            yield return AnimationUtils.Lerp(dur, t =>
            {
                interpolator.Interpolate(t);
                // update network without updating the storage for performance reasons
                // only update storage at the end

                UpdateNetwork(
                    updateCommunityProps: true,
                    updateStorage: false,
                    updateRenderElements: true
                );
            });

            interpolator.Interpolate(1f);

            UpdateNetwork(
                updateCommunityProps: true,
                updateStorage: true,
                updateRenderElements: true
            );

            _curAnim = null;
        }

        IEnumerator CRMoveNode(int nodeID, Transform toTrack)
        {
            while (true)
            {
                _context.Nodes[nodeID].Position = toTrack.position;
                _context.Nodes[nodeID].Dirty = true;

                UpdateNetwork(
                    updateCommunityProps: true,
                    updateStorage: false,
                    updateRenderElements: true
                );

                yield return null;
            }

        }

        IEnumerator CRMoveNodes(List<int> nodeIDs, List<Transform> toTracks)
        {
            while (true)
            {
                for (int i = 0; i < nodeIDs.Count; i++)
                {
                    _context.Nodes[nodeIDs[i]].Position = toTracks[i].position;
                    _context.Nodes[nodeIDs[i]].Dirty = true;
                }

                // update network without updating the storage for performance reasons

                UpdateNetwork(
                    updateCommunityProps: true,
                    updateStorage: false,
                    updateRenderElements: true
                );

                yield return null;
            }
        }

        IEnumerator CRMoveComm(Transform comm, List<int> nodeIDs, List<Transform> toMove)
        {
            Vector3 lastCommPosition = Vector3.positiveInfinity;
            Quaternion lastCommRotation = Quaternion.identity;

            while (true)
            {
                var curPosition = comm.transform.position;
                var curRotation = comm.transform.rotation;

                if (float.IsFinite(lastCommPosition.x))
                {
                    var diff = curPosition - lastCommPosition;
                    var diffRot = curRotation * Quaternion.Inverse(lastCommRotation);
                    diffRot.ToAngleAxis(out var angle, out var axis);

                    for (int i = 0; i < nodeIDs.Count; i++)
                    {
                        toMove[i].RotateAround(lastCommPosition, axis, angle);

                        toMove[i].position += diff;
                        _context.Nodes[nodeIDs[i]].Position = toMove[i].position;
                        _context.Nodes[nodeIDs[i]].Dirty = true;
                    }
                }

                lastCommPosition = curPosition;
                lastCommRotation = curRotation;

                // update network without updating the storage for performance reasons
                UpdateNetwork(
                    updateCommunityProps: true,
                    updateStorage: false,
                    updateRenderElements: true
                );

                yield return null;
            }
        }

        void UpdateNetwork(bool updateCommunityProps, bool updateStorage, bool updateRenderElements)
        {
            if (updateCommunityProps) UpdateCommunityProps();
            if (updateStorage) UpdateStorage();
            if (updateRenderElements) UpdateRenderElements();
        }

        void UpdateCommunityProps()
        {
            _context.RecomputeGeometricProps(_manager.NetworkGlobal);
        }
    }
}