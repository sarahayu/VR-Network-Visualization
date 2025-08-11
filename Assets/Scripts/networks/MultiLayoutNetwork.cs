using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Neo4j.Driver;
using UnityEngine;

namespace VidiGraph
{
    public class MultiLayoutNetwork : Network
    {
        public MultiLayoutContext.Settings BaseSettings;

        // TODO restrict edit access
        public MultiLayoutContext Context { get { return _context; } }

        public HashSet<int> SelectedNodes { get { return Context.SelectedNodes; } }
        public HashSet<int> SelectedCommunities { get { return Context.SelectedCommunities; } }

        NetworkManager _manager;

        MultiLayoutNetworkInput _input;
        NetworkRenderer _renderer;

        MultiLayoutContext _context = new MultiLayoutContext(0);

        Dictionary<string, NetworkContextTransformer> _transformers = new Dictionary<string, NetworkContextTransformer>();

        // keep a reference to sphericallayout to focus on individual communities
        SphericalLayoutTransformer _sphericalLayoutTransformer;

        // keep a reference to hairballlayout to focus on individual communities
        HairballLayoutTransformer _hairballLayoutTransformer;

        // keep a reference to clusterlayout to focus on individual communities
        ClusterLayoutTransformer _clusterLayoutTransformer;

        // keep a reference to bringNodeLayout to focus on individual nodes
        BringNodeTransformer _bringNodeTransformer;

        // keep a reference to floorLayout to focus on individual communities
        FloorLayoutTransformer _floorLayoutTransformer;
        // keep a reference to encoding to change encoding
        MLEncodingTransformer _encodingTransformer;

        // keep a reference to edit to change network properties
        MLEditTransformer _editTransformer;

        bool _isSphericalLayout;

        Coroutine _curAnim = null;
        Coroutine _curMover = null;


        void Update()
        {
            Draw();
        }

        public void Initialize()
        {
            GetManager();

            InitContext();
            InitInput();
            InitTransformers();

            // apply initial transformations before first render so we don't get a weird jump
            TransformNetworkNoRender("encoding");
            _isSphericalLayout = true;
            _sphericalLayoutTransformer.UpdateCommsOnNextApply(_context.Communities.Keys);
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
                    _context.Communities[commID].State = MultiLayoutContext.CommunityState.None;
                }
            }
            else
            {

                foreach (var commID in _context.Communities.Keys)
                {
                    _context.Communities[commID].State = MultiLayoutContext.CommunityState.Hairball;
                    _hairballLayoutTransformer.UpdateOnNextApply(_manager.NetworkGlobal.Communities[commID].Nodes.Select(n => n.ID));
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
        public void SetLayout(IEnumerable<int> commIDs, string layout, Action onFinished = null)
        {
            foreach (var commID in commIDs)
            {
                _context.Communities[commID].State = MultiLayoutContext.StrToState(layout);

                QueueLayoutChange(commID, layout);
            }

            TransformNetwork(layout, animated: true, onFinished: onFinished);
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

        public void SetSelectedNodes(IEnumerable<int> nodeIDs, bool isSelected)
        {
            var validNodes = Context.Nodes.Keys.Intersect(nodeIDs);
            var updateNodes = nodeIDs.Except(SelectedNodes);        // only update necessary nodes

            if (validNodes.Count() != nodeIDs.Count())
            {
                Debug.LogWarning($"Nodes {string.Join(", ", nodeIDs.Except(validNodes))} not found in subnetwork {0}");
            }

            Context.SetSelectedNodes(updateNodes, isSelected);
        }

        public void SetSelectedComms(IEnumerable<int> commIDs, bool isSelected)
        {
            var validComms = Context.Communities.Keys.Intersect(commIDs);

            if (validComms.Count() != commIDs.Count())
            {
                Debug.LogWarning($"Communities {string.Join(", ", commIDs.Except(validComms))} not found in subnetwork {0}");
            }

            Context.SetSelectedComms(validComms, isSelected);
        }

        public IEnumerable<int> ToggleSelectedNodes(IEnumerable<int> nodeIDs)
        {
            var validNodes = Context.Nodes.Keys.Intersect(nodeIDs);

            if (validNodes.Count() != nodeIDs.Count())
            {
                Debug.LogWarning($"Nodes {string.Join(", ", nodeIDs.Except(validNodes))} not found in subnetwork {0}");
            }

            return Context.ToggleSelectedNodes(validNodes);
        }

        public IEnumerable<int> ToggleSelectedComms(IEnumerable<int> commIDs)
        {
            var validComms = Context.Communities.Keys.Intersect(commIDs);

            if (validComms.Count() != commIDs.Count())
            {
                Debug.LogWarning($"Communities {string.Join(", ", commIDs.Except(validComms))} not found in subnetwork {0}");
            }

            return Context.ToggleSelectedComms(validComms);
        }

        public void ClearSelection()
        {
            Context.ClearSelection();
        }

        public void BringNodes(IEnumerable<int> nodeIDs, Action onFinished = null)
        {
            _bringNodeTransformer.UpdateOnNextApply(nodeIDs);

            TransformNetwork("bringNode", animated: true, onFinished: onFinished);
        }

        public void ReturnNodes(IEnumerable<int> nodeIDs, Action onFinished = null)
        {
            _sphericalLayoutTransformer.UpdateNodesOnNextApply(nodeIDs);

            TransformNetwork("spherical", animated: true, onFinished: onFinished);
        }

        public void StartNodeMove(int nodeID)
        {
            CoroutineUtils.StopIfRunning(this, ref _curMover);
            _curMover = StartCoroutine(CRMoveNode(nodeID));
        }

        public void StartNodesMove(IEnumerable<int> nodeIDs)
        {
            CoroutineUtils.StopIfRunning(this, ref _curMover);
            _curMover = StartCoroutine(CRMoveNodes(nodeIDs));
        }

        public void EndNodesMove()
        {
            CoroutineUtils.StopIfRunning(this, ref _curMover);
            UpdateNetwork(
                updateCommunityProps: true,
                updateStorage: true,
                updateRenderElements: true
            );
        }

        public void StartCommMove(int commID)
        {
            CoroutineUtils.StopIfRunning(this, ref _curMover);
            _curMover = StartCoroutine(CRMoveComm(commID));
        }

        public void StartCommsMove(IEnumerable<int> commIDs)
        {
            CoroutineUtils.StopIfRunning(this, ref _curMover);
            _curMover = StartCoroutine(CRMoveComms(commIDs));
        }

        public void EndCommsMove()
        {
            CoroutineUtils.StopIfRunning(this, ref _curMover);
            UpdateNetwork(
                updateCommunityProps: true,
                updateStorage: true,
                updateRenderElements: true
            );
        }

        public Transform GetNodeTransform(int nodeID)
        {
            return _renderer.GetNodeTransform(nodeID);
        }

        public Transform GetCommTransform(int commID)
        {
            return _renderer.GetCommTransform(commID);
        }

        public void SetNodesSize(IEnumerable<int> nodeIDs, float size, bool updateStorage, bool updateRenderElements)
        {
            _editTransformer.SetNodesSize(nodeIDs, size);
            TransformNetwork("edit", animated: false,
                updateStorage: updateStorage, updateRenderElements: updateRenderElements);
        }

        public void SetNodesColor(IEnumerable<int> nodeIDs, string color, bool updateStorage, bool updateRenderElements)
        {
            _editTransformer.SetNodesColor(nodeIDs, color);
            TransformNetwork("edit", animated: false,
                updateStorage: updateStorage, updateRenderElements: updateRenderElements);
        }

        public void SetLinksWidth(IEnumerable<int> linkIDs, float width, bool updateStorage, bool updateRenderElements)
        {
            _editTransformer.SetLinksWidth(linkIDs, width);
            TransformNetwork("edit", animated: false,
                updateStorage: updateStorage, updateRenderElements: updateRenderElements);
        }

        public void SetLinksColorStart(IEnumerable<int> linkIDs, string color, bool updateStorage, bool updateRenderElements)
        {
            _editTransformer.SetLinksColorStart(linkIDs, color);
            TransformNetwork("edit", animated: false,
                updateStorage: updateStorage, updateRenderElements: updateRenderElements);
        }

        public void SetLinksColorEnd(IEnumerable<int> linkIDs, string color, bool updateStorage, bool updateRenderElements)
        {
            _editTransformer.SetLinksColorEnd(linkIDs, color);
            TransformNetwork("edit", animated: false,
                updateStorage: updateStorage, updateRenderElements: updateRenderElements);
        }

        public void SetLinksAlpha(IEnumerable<int> linkIDs, float alpha, bool updateStorage, bool updateRenderElements)
        {
            _editTransformer.SetLinksAlpha(linkIDs, alpha);
            TransformNetwork("edit", animated: false,
                updateStorage: updateStorage, updateRenderElements: updateRenderElements);
        }

        public void SetLinksBundlingStrength(IEnumerable<int> linkIDs, float bundlingStrength, bool updateStorage, bool updateRenderElements)
        {
            _editTransformer.SetLinksBundlingStrength(linkIDs, bundlingStrength);
            TransformNetwork("edit", animated: false,
                updateStorage: updateStorage, updateRenderElements: updateRenderElements);
        }

        public void SetLinksBundleStart(IEnumerable<int> linkIDs, bool bundleStart, bool updateStorage, bool updateRenderElements)
        {
            _editTransformer.SetLinksBundleStart(linkIDs, bundleStart);
            TransformNetwork("edit", animated: false,
                updateStorage: updateStorage, updateRenderElements: updateRenderElements);
        }

        public void SetLinksBundleEnd(IEnumerable<int> linkIDs, bool bundleEnd, bool updateStorage, bool updateRenderElements)
        {
            _editTransformer.SetLinksBundleEnd(linkIDs, bundleEnd);
            TransformNetwork("edit", animated: false,
                updateStorage: updateStorage, updateRenderElements: updateRenderElements);
        }


        public void SetNodesPosition(IEnumerable<int> nodeIDs, Vector3 position, bool updateStorage, bool updateRenderElements)
        {
            _editTransformer.SetNodesPosition(nodeIDs, position);
            TransformNetwork("edit", animated: false,
                updateStorage: updateStorage, updateRenderElements: updateRenderElements);
        }

        public void SetNodesPosition(IEnumerable<int> nodeIDs, IEnumerable<Vector3> positions, bool updateStorage, bool updateRenderElements)
        {
            _editTransformer.SetNodesPosition(nodeIDs, positions);
            TransformNetwork("edit", animated: false,
                updateStorage: updateStorage, updateRenderElements: updateRenderElements);
        }

        public bool SetNodeColorEncoding(string prop, float min, float max, string color,
            bool updateStorage, bool updateRenderElements)
        {
            var success = _encodingTransformer.SetNodeColorEncoding(prop, min, max, color);

            if (success)
                TransformNetwork("encoding", animated: false,
                    updateStorage: updateStorage, updateRenderElements: updateRenderElements);

            return success;
        }

        public bool SetNodeColorEncoding(string prop, Dictionary<string, string> valueToColor,
            bool updateStorage, bool updateRenderElements)
        {
            var success = _encodingTransformer.SetNodeColorEncoding(prop, valueToColor);

            if (success)
                TransformNetwork("encoding", animated: false,
                    updateStorage: updateStorage, updateRenderElements: updateRenderElements);

            return success;
        }

        public bool SetNodeColorEncoding(string prop, Dictionary<bool?, string> valueToColor,
            bool updateStorage, bool updateRenderElements)
        {
            var success = _encodingTransformer.SetNodeColorEncoding(prop, valueToColor);

            if (success)
                TransformNetwork("encoding", animated: false,
                    updateStorage: updateStorage, updateRenderElements: updateRenderElements);

            return success;
        }

        public bool SetNodeSizeEncoding(string prop, float min, float max,
            bool updateStorage, bool updateRenderElements)
        {
            var success = _encodingTransformer.SetNodeSizeEncoding(prop, min, max);

            if (success)
                TransformNetwork("encoding", animated: false,
                    updateStorage: updateStorage, updateRenderElements: updateRenderElements);

            return success;
        }

        public bool SetNodeSizeEncoding(string prop, Dictionary<string, float> valueToSize,
            bool updateStorage, bool updateRenderElements)
        {
            var success = _encodingTransformer.SetNodeSizeEncoding(prop, valueToSize);

            if (success)
                TransformNetwork("encoding", animated: false,
                    updateStorage: updateStorage, updateRenderElements: updateRenderElements);

            return success;
        }

        public bool SetNodeSizeEncoding(string prop, Dictionary<bool?, float> valueToSize,
            bool updateStorage, bool updateRenderElements)
        {
            var success = _encodingTransformer.SetNodeSizeEncoding(prop, valueToSize);

            if (success)
                TransformNetwork("encoding", animated: false,
                    updateStorage: updateStorage, updateRenderElements: updateRenderElements);

            return success;
        }

        public bool SetLinkWidthEncoding(string prop, float min, float max,
            bool updateStorage, bool updateRenderElements)
        {
            var success = _encodingTransformer.SetLinkWidthEncoding(prop, min, max);

            if (success)
                TransformNetwork("encoding", animated: false,
                    updateStorage: updateStorage, updateRenderElements: updateRenderElements);

            return success;
        }

        public bool SetLinkWidthEncoding(string prop, Dictionary<string, float> valueToWidth,
            bool updateStorage, bool updateRenderElements)
        {
            var success = _encodingTransformer.SetLinkWidthEncoding(prop, valueToWidth);

            if (success)
                TransformNetwork("encoding", animated: false,
                    updateStorage: updateStorage, updateRenderElements: updateRenderElements);

            return success;
        }

        public bool SetLinkWidthEncoding(string prop, Dictionary<bool?, float> valueToWidth,
            bool updateStorage, bool updateRenderElements)
        {
            var success = _encodingTransformer.SetLinkWidthEncoding(prop, valueToWidth);

            if (success)
                TransformNetwork("encoding", animated: false,
                    updateStorage: updateStorage, updateRenderElements: updateRenderElements);

            return success;
        }

        public bool SetLinkBundlingStrengthEncoding(string prop, float min, float max,
            bool updateStorage, bool updateRenderElements)
        {
            var success = _encodingTransformer.SetLinkBundlingStrengthEncoding(prop, min, max);

            if (success)
                TransformNetwork("encoding", animated: false,
                    updateStorage: updateStorage, updateRenderElements: updateRenderElements);

            return success;
        }

        public bool SetLinkBundlingStrengthEncoding(string prop, Dictionary<string, float> valueToBundlingStrength,
            bool updateStorage, bool updateRenderElements)
        {
            var success = _encodingTransformer.SetLinkBundlingStrengthEncoding(prop, valueToBundlingStrength);

            if (success)
                TransformNetwork("encoding", animated: false,
                    updateStorage: updateStorage, updateRenderElements: updateRenderElements);

            return success;
        }

        public bool SetLinkBundlingStrengthEncoding(string prop, Dictionary<bool?, float> valueToBundlingStrength,
            bool updateStorage, bool updateRenderElements)
        {
            var success = _encodingTransformer.SetLinkBundlingStrengthEncoding(prop, valueToBundlingStrength);

            if (success)
                TransformNetwork("encoding", animated: false,
                    updateStorage: updateStorage, updateRenderElements: updateRenderElements);

            return success;
        }

        public bool SetLinkColorStartEncoding(string prop, float min, float max, string colorStart,
            bool updateStorage, bool updateRenderElements)
        {
            var success = _encodingTransformer.SetLinkColorStartEncoding(prop, min, max, colorStart);

            if (success)
                TransformNetwork("encoding", animated: false,
                    updateStorage: updateStorage, updateRenderElements: updateRenderElements);

            return success;
        }

        public bool SetLinkColorStartEncoding(string prop, Dictionary<string, string> valueToColorStart,
            bool updateStorage, bool updateRenderElements)
        {
            var success = _encodingTransformer.SetLinkColorStartEncoding(prop, valueToColorStart);

            if (success)
                TransformNetwork("encoding", animated: false,
                    updateStorage: updateStorage, updateRenderElements: updateRenderElements);

            return success;
        }

        public bool SetLinkColorStartEncoding(string prop, Dictionary<bool?, string> valueToColorStart,
            bool updateStorage, bool updateRenderElements)
        {
            var success = _encodingTransformer.SetLinkColorStartEncoding(prop, valueToColorStart);

            if (success)
                TransformNetwork("encoding", animated: false,
                    updateStorage: updateStorage, updateRenderElements: updateRenderElements);

            return success;
        }

        public bool SetLinkColorEndEncoding(string prop, float min, float max, string colorEnd,
            bool updateStorage, bool updateRenderElements)
        {
            var success = _encodingTransformer.SetLinkColorEndEncoding(prop, min, max, colorEnd);

            if (success)
                TransformNetwork("encoding", animated: false,
                    updateStorage: updateStorage, updateRenderElements: updateRenderElements);

            return success;
        }

        public bool SetLinkColorEndEncoding(string prop, Dictionary<string, string> valueToColorEnd,
            bool updateStorage, bool updateRenderElements)
        {
            var success = _encodingTransformer.SetLinkColorEndEncoding(prop, valueToColorEnd);

            if (success)
                TransformNetwork("encoding", animated: false,
                    updateStorage: updateStorage, updateRenderElements: updateRenderElements);

            return success;
        }

        public bool SetLinkColorEndEncoding(string prop, Dictionary<bool?, string> valueToColorEnd,
            bool updateStorage, bool updateRenderElements)
        {
            var success = _encodingTransformer.SetLinkColorEndEncoding(prop, valueToColorEnd);

            if (success)
                TransformNetwork("encoding", animated: false,
                    updateStorage: updateStorage, updateRenderElements: updateRenderElements);

            return success;
        }

        public bool SetLinkBundleStartEncoding(string prop, Dictionary<string, bool> valueToDoBundle,
            bool updateStorage, bool updateRenderElements)
        {
            var success = _encodingTransformer.SetLinkBundleStartEncoding(prop, valueToDoBundle);

            if (success)
                TransformNetwork("encoding", animated: false,
                    updateStorage: updateStorage, updateRenderElements: updateRenderElements);

            return success;
        }

        public bool SetLinkBundleStartEncoding(string prop, Dictionary<bool?, bool> valueToDoBundle,
            bool updateStorage, bool updateRenderElements)
        {
            var success = _encodingTransformer.SetLinkBundleStartEncoding(prop, valueToDoBundle);

            if (success)
                TransformNetwork("encoding", animated: false,
                    updateStorage: updateStorage, updateRenderElements: updateRenderElements);

            return success;
        }

        public bool SetLinkBundleEndEncoding(string prop, Dictionary<string, bool> valueToDoBundle,
            bool updateStorage, bool updateRenderElements)
        {
            var success = _encodingTransformer.SetLinkBundleEndEncoding(prop, valueToDoBundle);

            if (success)
                TransformNetwork("encoding", animated: false,
                    updateStorage: updateStorage, updateRenderElements: updateRenderElements);

            return success;
        }

        public bool SetLinkBundleEndEncoding(string prop, Dictionary<bool?, bool> valueToDoBundle,
            bool updateStorage, bool updateRenderElements)
        {
            var success = _encodingTransformer.SetLinkBundleEndEncoding(prop, valueToDoBundle);

            if (success)
                TransformNetwork("encoding", animated: false,
                    updateStorage: updateStorage, updateRenderElements: updateRenderElements);

            return success;
        }

        public bool SetLinkAlphaEncoding(string prop, float min, float max,
            bool updateStorage, bool updateRenderElements)
        {
            var success = _encodingTransformer.SetLinkAlphaEncoding(prop, min, max);

            if (success)
                TransformNetwork("encoding", animated: false,
                    updateStorage: updateStorage, updateRenderElements: updateRenderElements);

            return success;
        }

        public bool SetLinkAlphaEncoding(string prop, Dictionary<string, float> valueToAlpha,
            bool updateStorage, bool updateRenderElements)
        {
            var success = _encodingTransformer.SetLinkAlphaEncoding(prop, valueToAlpha);

            if (success)
                TransformNetwork("encoding", animated: false,
                    updateStorage: updateStorage, updateRenderElements: updateRenderElements);

            return success;
        }

        public bool SetLinkAlphaEncoding(string prop, Dictionary<bool?, float> valueToAlpha,
            bool updateStorage, bool updateRenderElements)
        {
            var success = _encodingTransformer.SetLinkAlphaEncoding(prop, valueToAlpha);

            if (success)
                TransformNetwork("encoding", animated: false,
                    updateStorage: updateStorage, updateRenderElements: updateRenderElements);

            return success;
        }

        /*=============== start private methods ===================*/

        void GetManager()
        {
            _manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
        }

        void InitContext()
        {
            _context.SetFromGlobal(_manager.NetworkGlobal, _manager.FileLoader.SphericalLayout);
            SetContextSettings();
        }

        void InitInput()
        {
            _input = GetComponent<MultiLayoutNetworkInput>();
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
            _context.ContextSettings = BaseSettings;
        }

        void InitTransformers()
        {
            _hairballLayoutTransformer = GetComponentInChildren<HairballLayoutTransformer>();
            _transformers["hairball"] = _hairballLayoutTransformer;
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

            _editTransformer = GetComponentInChildren<MLEditTransformer>();
            _transformers["edit"] = _editTransformer;
            _transformers["edit"].Initialize(_manager.NetworkGlobal, _context);

            _transformers["highlight"] = GetComponentInChildren<HighlightTransformer>();
            _transformers["highlight"].Initialize(_manager.NetworkGlobal, _context);
        }

        void TransformNetwork(string transformer, bool animated = true, Action onFinished = null,
            bool updateCommunityProps = true, bool updateStorage = true, bool updateRenderElements = true)
        {
            if (animated)
            {
                if (CoroutineUtils.StopIfRunning(this, ref _curAnim))
                {
                    // update network since we cancelled coroutine prematurely

                    UpdateNetwork(
                        updateCommunityProps: true,
                        updateStorage: false,
                        updateRenderElements: true
                    );
                }

                _curAnim = StartCoroutine(CRAnimateTransformation(
                    transformer: transformer,
                    onFinished: onFinished,
                    updateCommunityProps: updateCommunityProps,
                    updateStorage: updateStorage,
                    updateRenderElements: updateRenderElements
                ));
            }
            else
            {
                _transformers[transformer]?.ApplyTransformation();

                UpdateNetwork(
                    updateCommunityProps: updateCommunityProps,
                    updateStorage: updateStorage,
                    updateRenderElements: updateRenderElements
                );

                onFinished?.Invoke();
            }
        }

        void TransformNetworkNoRender(string layout, Action cb = null)
        {
            _transformers[layout].ApplyTransformation();

            UpdateNetwork(
                updateCommunityProps: true,
                updateStorage: true,
                updateRenderElements: false
            );

            cb?.Invoke();
        }

        IEnumerator CRAnimateTransformation(string transformer, Action onFinished = null,
            bool updateCommunityProps = true, bool updateStorage = false, bool updateRenderElements = true)
        {
            float dur = 1.0f;
            var interpolator = _transformers[transformer]?.GetInterpolator();

            if (interpolator == null) yield return null;

            yield return AnimationUtils.Lerp(dur, t =>
            {
                interpolator.Interpolate(t);
                // update network without updating the storage for performance reasons
                // only update storage at the end

                UpdateNetwork(
                    updateCommunityProps: updateCommunityProps,
                    updateStorage: false,
                    updateRenderElements: updateRenderElements
                );
            });

            interpolator.Interpolate(1f);

            UpdateNetwork(
                updateCommunityProps: updateCommunityProps,
                updateStorage: updateStorage,
                updateRenderElements: updateRenderElements
            );

            _curAnim = null;

            onFinished?.Invoke();
        }

        IEnumerator CRMoveNode(int nodeID)
        {
            var nodeTransform = GetNodeTransform(nodeID);

            while (true)
            {
                _context.Nodes[nodeID].Position = nodeTransform.position;
                _context.Nodes[nodeID].Dirty = true;
                _context.Communities[_context.Nodes[nodeID].CommunityID].Dirty = true;

                UpdateNetwork(
                    updateCommunityProps: true,
                    updateStorage: false,
                    updateRenderElements: true
                );

                yield return null;
            }

        }

        IEnumerator CRMoveNodes(IEnumerable<int> nodeIDs)
        {
            var nodeTransforms = nodeIDs.Select(nid => GetNodeTransform(nid));

            while (true)
            {
                foreach (var (nodeID, nodeTransform) in nodeIDs.Zip(nodeTransforms, Tuple.Create))
                {
                    _context.Nodes[nodeID].Position = nodeTransform.position;
                    _context.Nodes[nodeID].Dirty = true;
                    _context.Communities[_context.Nodes[nodeID].CommunityID].Dirty = true;
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

        IEnumerator CRMoveComm(int commID)
        {
            Vector3 lastCommPosition = Vector3.positiveInfinity;
            Quaternion lastCommRotation = Quaternion.identity;

            var comm = GetCommTransform(commID);

            var nodeIDs = _context.Communities[commID].Nodes;
            var nodeTransforms = nodeIDs.Select(nid => GetNodeTransform(nid));

            while (true)
            {
                var curPosition = comm.transform.position;
                var curRotation = comm.transform.rotation;

                if (float.IsFinite(lastCommPosition.x))
                {
                    var diff = curPosition - lastCommPosition;
                    var diffRot = curRotation * Quaternion.Inverse(lastCommRotation);
                    diffRot.ToAngleAxis(out var angle, out var axis);

                    foreach (var (nodeID, nodeTransform) in nodeIDs.Zip(nodeTransforms, Tuple.Create))
                    {
                        nodeTransform.RotateAround(lastCommPosition, axis, angle);

                        nodeTransform.position += diff;
                        _context.Nodes[nodeID].Position = nodeTransform.position;
                        _context.Nodes[nodeID].Dirty = true;
                    }
                }

                _context.Communities[commID].Dirty = true;

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

        IEnumerator CRMoveComms(IEnumerable<int> commIDs)
        {
            Vector3 lastCommPosition = Vector3.positiveInfinity;
            Quaternion lastCommRotation = Quaternion.identity;

            var commTransform = GetCommTransform(commIDs.First());

            Dictionary<int, IEnumerable<int>> nodeIDs = new();
            Dictionary<int, IEnumerable<Transform>> nodeTransforms = new();

            foreach (var commID in commIDs)
            {
                nodeIDs[commID] = _context.Communities[commID].Nodes;
                nodeTransforms[commID] = nodeIDs[commID].Select(nid => GetNodeTransform(nid));
            }

            while (true)
            {
                var curPosition = commTransform.position;
                var curRotation = commTransform.rotation;

                if (float.IsFinite(lastCommPosition.x))
                {
                    var diff = curPosition - lastCommPosition;
                    var diffRot = curRotation * Quaternion.Inverse(lastCommRotation);
                    diffRot.ToAngleAxis(out var angle, out var axis);

                    foreach (var commID in commIDs)
                    {
                        foreach (var (nodeID, nodeTransform) in nodeIDs[commID].Zip(nodeTransforms[commID], Tuple.Create))
                        {
                            nodeTransform.RotateAround(lastCommPosition, axis, angle);

                            nodeTransform.position += diff;
                            _context.Nodes[nodeID].Position = nodeTransform.position;
                            _context.Nodes[nodeID].Dirty = true;
                        }

                        _context.Communities[commID].Dirty = true;
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
            _context.RecomputeCommProps(_manager.NetworkGlobal);
        }
    }
}