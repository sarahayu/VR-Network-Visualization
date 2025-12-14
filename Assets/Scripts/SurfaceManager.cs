/*
*
* SurfaceManager is where all surface operations are done from.
*
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VidiGraph
{
    public class SurfaceManager : MonoBehaviour
    {
        [SerializeField] GameObject _surfacePrefab;
        [SerializeField] float _surfaceAttractionDist = 0.025f;
        [SerializeField] Color _highlightCol = Color.green;
        [SerializeField] Vector3 _surfSpawnOffset = Vector3.zero;
        [SerializeField] Transform _spawnOrigin;

        public Dictionary<int, GameObject> Surfaces { get { return _surfaces.Values.ToDictionary(si => si.ID, si => si.GameObject); } }

        public bool IsMovingSurface { get { return _surfaceMover != null; } }
        public int CurHoveredSurface { get { return _curHoveredSurface; } }

        class Surface
        {
            public int ID;
            public GameObject GameObject;
            public Renderer Renderer;
            public Dictionary<string, Transform> Nodes = new();
        }

        Dictionary<int, Surface> _surfaces = new Dictionary<int, Surface>();
        Dictionary<int, Collider> _colliders = new Dictionary<int, Collider>();
        Dictionary<string, int> _nodeToSurf = new();

        int _curID = 0;
        int _curHoveredSurface = -1;

        NetworkManager _manager;
        NetworkRenderer _mlRenderer;

        NodeLinkContext.Settings _mlSettings;


        Coroutine _surfaceHighlighter = null;
        Coroutine _surfaceMover = null;
        Coroutine _attachAnimation = null;

        Transform _userPos;

        // Start is called before the first frame update
        void Start()
        {
            _manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();

            var mlNetwork = GameObject.Find("/MultiLayout Network");
            _mlRenderer = mlNetwork.GetComponentInChildren<NetworkRenderer>();
            _mlSettings = mlNetwork.GetComponent<MultiLayoutNetwork>().BaseSettings;

            _userPos = GameObject.FindWithTag("MainCamera").transform;

            AttachListeners(mlNetwork.GetComponent<NodeLinkNetwork>());

            _manager.OnSubnetworkCreate += subn => AttachListeners(subn);
            _manager.OnSubnetworkDestroy += subn => DetachListeners(subn);
        }

        class Listeners
        {
            public NetworkRenderer.NodeSelectEnterEvent NodeGrabEnter;
            public NetworkRenderer.NodeSelectExitEvent NodeGrabExit;
            public NetworkRenderer.CommunitySelectEnterEvent CommGrabEnter;
            public NetworkRenderer.CommunitySelectExitEvent CommGrabExit;
            public NetworkRenderer.NetworkSelectEnterEvent NetworkGrabEnter;
            public NetworkRenderer.NetworkSelectExitEvent NetworkGrabExit;
        }

        Dictionary<int, Listeners> _subnListeners = new();

        public void AttachListeners(NodeLinkNetwork network)
        {
            var renderer = network.GetComponentInChildren<NetworkRenderer>();
            var listeners = CreateListeners(network);

            renderer.OnNodeGrabEnter += listeners.NodeGrabEnter;
            renderer.OnNodeGrabExit += listeners.NodeGrabExit;
            renderer.OnCommunityGrabEnter += listeners.CommGrabEnter;
            renderer.OnCommunityGrabExit += listeners.CommGrabExit;
            renderer.OnNetworkGrabEnter += listeners.NetworkGrabEnter;
            renderer.OnNetworkGrabExit += listeners.NetworkGrabExit;

            _subnListeners[network.ID] = listeners;
        }

        public void DetachListeners(NodeLinkNetwork network)
        {
            var renderer = network.GetComponentInChildren<NetworkRenderer>();
            var listeners = _subnListeners[network.ID];

            renderer.OnNodeGrabEnter -= listeners.NodeGrabEnter;
            renderer.OnNodeGrabExit -= listeners.NodeGrabExit;
            renderer.OnCommunityGrabEnter -= listeners.CommGrabEnter;
            renderer.OnCommunityGrabExit -= listeners.CommGrabExit;
            renderer.OnNetworkGrabEnter -= listeners.NetworkGrabEnter;
            renderer.OnNetworkGrabExit -= listeners.NetworkGrabExit;

            _subnListeners.Remove(network.ID);
        }

        Listeners CreateListeners(NodeLinkNetwork network)
        {
            Listeners listeners = new();

            listeners.NodeGrabEnter = (node, args) => OnNodeGrabEnter(node, network.ID, args);
            listeners.NodeGrabExit = (node, args) => OnNodeGrabExit(node, network.ID, args);
            listeners.CommGrabEnter = (comm, args) => OnCommunityGrabEnter(comm, network.ID, args);
            listeners.CommGrabExit = (comm, args) => OnCommunityGrabExit(comm, network.ID, args);
            listeners.NetworkGrabEnter = (network, args) => OnNetworkGrabEnter(network, ((NodeLinkContext)network).SubnetworkID, args);
            listeners.NetworkGrabExit = (network, args) => OnNetworkGrabExit(network, ((NodeLinkContext)network).SubnetworkID, args);

            return listeners;
        }

        // return surface ID
        public int SpawnSurface(Vector3 position, Quaternion rotation)
        {
            var surfObject = UnityEngine.Object.Instantiate(_surfacePrefab, transform);

            int id = GetNextID();

            surfObject.transform.SetPositionAndRotation(position, rotation);
            TextMeshPro text = surfObject.GetComponentInChildren<TextMeshPro>();
            text.SetText($"Surface {id}");

            AddSurfaceInteraction(surfObject, id);

            var si = new Surface();
            var highlighter = surfObject.GetNamedChild("Highlight");

            si.ID = id;
            si.GameObject = surfObject;
            si.Renderer = highlighter.GetComponent<Renderer>();

            _surfaces[id] = si;
            _colliders[id] = highlighter.GetComponent<Collider>();

            _manager.TriggerRenderUpdate();

            return id;
        }

        // spawn in front of main camera
        public int SpawnSurface()
        {
            _manager.TriggerRenderUpdate();
            return SpawnSurface(_userPos.position + _userPos.rotation * Vector3.forward, Quaternion.FromToRotation(Vector3.up, -_userPos.forward));
        }

        public int SpawnSurfaceFromPointer()
        {
            SurfaceInputUtils.CalcPosAndRot(_spawnOrigin, _surfSpawnOffset, out var position, out var rotation);
            _manager.TriggerRenderUpdate();
            return SpawnSurface(position, rotation);
        }


        public void DeleteSurface(int surfID)
        {
            if (_surfaces.ContainsKey(surfID))
            {
                UnityEngine.Object.Destroy(_surfaces[surfID].GameObject);

                foreach (var nodeID in _surfaces[surfID].Nodes.Keys) _nodeToSurf.Remove(nodeID);
                _surfaces.Remove(surfID);
                _colliders.Remove(surfID);
            }
            _manager.TriggerRenderUpdate();
        }

        public void HoverSurface(int surfID)
        {
            _curHoveredSurface = surfID;
        }

        public void UnhoverSurface()
        {
            _curHoveredSurface = -1;
        }

        // return ID of closest surface
        public int TryAttachNode(string nodeGUID)
        {
            // check distance
            // attach if distance less than threshold

            var closest = GetClosestSurface(_manager.GetMLNodeTransform(nodeGUID).position);

            if (closest != -1) AttachNodes(new List<string>() { nodeGUID }, closest);
            else DetachNodes(new List<string>() { nodeGUID });

            return closest;
        }

        // return ID of closest surface
        public int TryAttachNodes(IEnumerable<string> nodeGUIDs)
        {
            // check distance
            // attach if distance less than threshold

            var nGUIDs = nodeGUIDs.ToList();

            var closest = GetClosestSurface(GetMidpoint(nGUIDs));

            if (closest != -1) AttachNodes(nGUIDs, closest);
            else DetachNodes(nGUIDs);

            return closest;
        }

        Vector3 GetMidpoint(ICollection<string> nodeGUIDs)
        {
            Vector3 pos = Vector3.zero;
            int count = 0;

            foreach (var nGUID in nodeGUIDs)
            {
                pos += _manager.GetMLNodeTransform(nGUID).position;
                count += 1;
            }

            return count != 0 ? pos / count : pos;
        }

        public void AttachNodes(IEnumerable<string> nodeGUIDs, int surfID)
        {
            if (!_surfaces.ContainsKey(surfID)) return;

            var nodeGUIDsList = nodeGUIDs.ToList();

            DetachNodes(nodeGUIDsList);

            foreach (var nodeGUID in nodeGUIDsList)
            {
                _surfaces[surfID].Nodes[nodeGUID] = _manager.GetMLNodeTransform(nodeGUID);
                _nodeToSurf[nodeGUID] = surfID;
            }

            _manager.PauseStorageUpdate();
            _manager.PauseRenderUpdate();

            _manager.GetInnerAndOuterLinks(_surfaces[surfID].Nodes.Keys, out var innerLinks, out var outerLinks, out var isStartOuterLinks);
            _manager.SetMLLinksBundlingStrength(innerLinks, 0f);
            _manager.SetMLLinksBundleStart(outerLinks.Where((_, idx) => isStartOuterLinks[idx]), false);
            _manager.SetMLLinksBundleEnd(outerLinks.Where((_, idx) => !isStartOuterLinks[idx]), false);

            _manager.SetMLLinksAlpha(innerLinks, _mlSettings.LinkContextAlphaFactor);
            _manager.SetMLLinksAlpha(outerLinks, _mlSettings.LinkContext2FocusAlphaFactor);

            _manager.UnpauseRenderUpdate();
            // don't unpause storage update, it'll be updated at the end of animation

            StartAttachAnim(nodeGUIDsList, surfID);
        }

        public void DetachNodes(IEnumerable<string> nodeGUIDs)
        {
            foreach (var nodeID in nodeGUIDs)
            {
                if (_nodeToSurf.ContainsKey(nodeID))
                {
                    var parent = _nodeToSurf[nodeID];

                    _surfaces[parent].Nodes.Remove(nodeID);
                    _nodeToSurf.Remove(nodeID);
                }
            }
        }

        /*=============== start private methods ===================*/

        void StartAttachAnim(IEnumerable<string> nodeGUIDs, int surfID)
        {
            CoroutineUtils.StopIfRunning(this, ref _attachAnimation);

            var startPositions = nodeGUIDs.Select(nid => _manager.GetMLNodeTransform(nid).position);
            var endPositions = SurfaceManagerUtils.CalcProjected(surfID, nodeGUIDs, this, _manager);

            _attachAnimation = StartCoroutine(CRAnimateNodesAttach(nodeGUIDs, startPositions, endPositions));
        }

        void AddSurfaceInteraction(GameObject gameObject, int id)
        {
            XRGrabInteractable xrInteractable = gameObject.GetComponent<XRGrabInteractable>();

            xrInteractable.hoverEntered.AddListener(evt =>
            {
                HoverSurface(id);
            });

            xrInteractable.hoverExited.AddListener(evt =>
            {
                UnhoverSurface();
            });

            xrInteractable.selectEntered.AddListener(evt =>
            {
                // start coroutine to change transforms
                // start node moves
                _surfaceMover = StartCoroutine(CRMoveSurfaceAndChildren(id));
                _manager.StartMLNodesMove(_surfaces[id].Nodes.Keys);
            });

            xrInteractable.selectExited.AddListener(evt =>
            {
                // end coroutine to change transforms
                // end node moves
                StopCoroutine(_surfaceMover);
                _surfaceMover = null;
                _manager.EndMLNodesMove();
                _manager.TriggerRenderUpdate();
            });
        }

        void UnhighlightSurfaces()
        {
            foreach (var surf in _surfaces.Values)
            {
                SurfaceManagerUtils.UnhighlightSurface(surf.Renderer);
            }
        }

        void OnNodeGrabExit(Node node, int subnetworkID, SelectExitEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                CoroutineUtils.StopIfRunning(this, ref _surfaceHighlighter);

                var nodeGUID = _manager.NodeIDsToNodeGUIDs(new int[] { node.ID }, subnetworkID).First();

                if (_manager.SelectedNodeGUIDs.Contains(nodeGUID))
                {
                    TryAttachNodes(_manager.SelectedNodeGUIDs);
                }
                else
                {
                    TryAttachNode(nodeGUID);
                }

                UnhighlightSurfaces();
            }
        }

        void OnNodeGrabEnter(Node node, int subnetworkID, SelectEnterEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                CoroutineUtils.StopIfRunning(this, ref _surfaceHighlighter);
                _surfaceHighlighter = StartCoroutine(CRHighlightClosestSurface(_manager.GetMLNodeTransform(node.ID, subnetworkID)));
            }
        }

        void OnCommunityGrabExit(Community community, int subnetworkID, SelectExitEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                CoroutineUtils.StopIfRunning(this, ref _surfaceHighlighter);

                var communityGUID = _manager.CommIDsToCommGUIDs(new int[] { community.ID }, subnetworkID).First();

                if (_manager.SelectedCommunityGUIDs.Contains(communityGUID))
                {
                    TryAttachNodes(_manager.SelectedNodeGUIDs);
                }
                else
                {
                    TryAttachNodes(_manager.NodeIDsToNodeGUIDs(community.Nodes.Select(n => n.ID), subnetworkID));
                }

                UnhighlightSurfaces();
            }
        }

        void OnCommunityGrabEnter(Community community, int subnetworkID, SelectEnterEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                CoroutineUtils.StopIfRunning(this, ref _surfaceHighlighter);
                _surfaceHighlighter = StartCoroutine(CRHighlightClosestSurface(_manager.GetMLCommTransform(community.ID, subnetworkID)));
            }
        }

        void OnNetworkGrabExit(NetworkContext network, int subnetworkID, SelectExitEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                CoroutineUtils.StopIfRunning(this, ref _surfaceHighlighter);

                if (_manager.SelectedNetworks.Contains(subnetworkID))
                {
                    TryAttachNodes(_manager.SelectedNodeGUIDs);

                }
                else
                {
                    var mlc = (NodeLinkContext)network;
                    TryAttachNodes(_manager.NodeIDsToNodeGUIDs(mlc.Nodes.Keys, subnetworkID));
                }

                UnhighlightSurfaces();
            }
        }

        void OnNetworkGrabEnter(NetworkContext network, int subnetworkID, SelectEnterEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                CoroutineUtils.StopIfRunning(this, ref _surfaceHighlighter);
                _surfaceHighlighter = StartCoroutine(CRHighlightClosestSurface(_manager.GetMLNetworkTransform(subnetworkID)));
            }
        }

        int GetClosestSurface(Vector3 position)
        {
            int closest = -1;
            float closestDist = float.PositiveInfinity;

            foreach (var surf in _surfaces.Values)
            {
                var posOnCollider = _colliders[surf.ID].ClosestPointOnBounds(position);
                float dist = Vector3.Distance(posOnCollider, position);

                if (dist < closestDist && dist < _surfaceAttractionDist)
                {
                    closest = surf.ID;
                    closestDist = dist;
                }
            }

            return closest;
        }

        int GetNextID()
        {
            return _curID++;
        }

        IEnumerator CRMoveSurfaceAndChildren(int surfID)
        {
            Vector3 lastSurfPosition = Vector3.positiveInfinity;
            Quaternion lastSurfRotation = Quaternion.identity;

            var surfTransform = _surfaces[surfID].GameObject.transform;
            var childrenTransform = _surfaces[surfID].Nodes.Keys.Select(nid => _manager.GetMLNodeTransform(nid));

            while (true)
            {
                var curPosition = surfTransform.position;
                var curRotation = surfTransform.rotation;

                if (float.IsFinite(lastSurfPosition.x))
                {
                    var diff = curPosition - lastSurfPosition;
                    var diffRot = curRotation * Quaternion.Inverse(lastSurfRotation);
                    diffRot.ToAngleAxis(out var angle, out var axis);

                    foreach (var child in childrenTransform)
                    {
                        child.RotateAround(lastSurfPosition, axis, angle);

                        child.position += diff;
                    }
                }

                lastSurfPosition = curPosition;
                lastSurfRotation = curRotation;
                yield return null;
            }
        }

        IEnumerator CRHighlightClosestSurface(Transform nodeTransform)
        {
            while (true)
            {
                int closest = GetClosestSurface(nodeTransform.position);

                foreach (var surf in _surfaces.Values)
                {
                    var renderer = surf.Renderer;
                    if (surf.ID == closest) SurfaceManagerUtils.HighlightSurface(renderer, _highlightCol);
                    else SurfaceManagerUtils.UnhighlightSurface(renderer);

                }
                yield return null;
            }
        }

        IEnumerator CRAnimateNodesAttach(IEnumerable<string> nodeGUIDs, IEnumerable<Vector3> startPositions, IEnumerable<Vector3> endPositions)
        {
            float dur = 1.0f;

            _manager.PauseStorageUpdate();

            yield return AnimationUtils.Lerp(dur, t =>
            {
                var positions = startPositions.Zip(endPositions, Tuple.Create)
                    .Select((se, i) => Vector3.Lerp(se.Item1, se.Item2, Mathf.SmoothStep(0f, 1f, t)));

                _manager.SetMLNodesPosition(nodeGUIDs, positions);
            });

            _manager.SetMLNodesPosition(nodeGUIDs, endPositions);

            _manager.TriggerRenderUpdate();
            _manager.UnpauseStorageUpdate();

            _attachAnimation = null;
        }
    }

}