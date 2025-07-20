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
        [SerializeField] float _surfaceAttractionDist = 0.1f;
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
            public Dictionary<int, Transform> Nodes = new Dictionary<int, Transform>();
        }

        Dictionary<int, Surface> _surfaces = new Dictionary<int, Surface>();
        Dictionary<int, Collider> _colliders = new Dictionary<int, Collider>();
        Dictionary<int, int> _nodeToSurf = new Dictionary<int, int>();

        int _curID = 0;
        int _curHoveredSurface = -1;

        NetworkManager _manager;
        NetworkRenderer _mlRenderer;

        MultiLayoutContext.Settings _mlSettings;


        Coroutine _surfaceHighlighter = null;
        Coroutine _surfaceMover = null;
        Coroutine _attachAnimation = null;

        Transform _userPos;

        // Start is called before the first frame update
        void Start()
        {
            _manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _mlRenderer = GameObject.Find("/MultiLayout Network").GetComponentInChildren<NetworkRenderer>();
            _mlSettings = GameObject.Find("/MultiLayout Network").GetComponent<MultiLayoutNetwork>().BaseSettings;

            _userPos = GameObject.FindWithTag("MainCamera").transform;

            _mlRenderer.OnNodeGrabEnter += OnNodeGrabEnter;
            _mlRenderer.OnNodeGrabExit += OnNodeGrabExit;
            _mlRenderer.OnCommunityGrabEnter += OnCommunityGrabEnter;
            _mlRenderer.OnCommunityGrabExit += OnCommunityGrabExit;
        }

        // return surface ID
        public int SpawnSurface(Vector3 position, Quaternion rotation)
        {
            var surfObject = UnityEngine.Object.Instantiate(_surfacePrefab, transform);

            int id = GetNextID();

            surfObject.transform.SetPositionAndRotation(position, rotation);
            TextMeshPro text = surfObject.GetComponentInChildren<TextMeshPro>();
            text.SetText(id.ToString());

            AddSurfaceInteraction(surfObject, id);

            var si = new Surface();
            var highlighter = surfObject.GetNamedChild("Highlight");

            si.ID = id;
            si.GameObject = surfObject;
            si.Renderer = highlighter.GetComponent<Renderer>();

            _surfaces[id] = si;
            _colliders[id] = highlighter.GetComponent<Collider>();

            return id;
        }

        // spawn in front of main camera
        public int SpawnSurface()
        {
            return SpawnSurface(_userPos.position + _userPos.rotation * Vector3.forward, Quaternion.FromToRotation(Vector3.up, -_userPos.forward));
        }

        public int SpawnSurfaceFromPointer()
        {
            SurfaceInputUtils.CalcPosAndRot(_spawnOrigin, _surfSpawnOffset, out var position, out var rotation);
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
        public int TryAttachNode(int nodeID)
        {
            // check distance
            // attach if distance less than threshold

            var closest = GetClosestSurface(_manager.GetMLNodeTransform(nodeID).position);

            if (closest != -1) AttachNodes(new List<int>() { nodeID }, closest);
            else DetachNodes(new List<int>() { nodeID });

            return closest;
        }

        // return ID of closest surface
        public int TryAttachNodes(IEnumerable<int> nodeIDs)
        {
            // check distance
            // attach if distance less than threshold

            // TODO use midpoint of points instead
            var closest = GetClosestSurface(_manager.GetMLCommTransform(_manager.NetworkGlobal.Nodes[nodeIDs.First()].CommunityID).position);

            if (closest != -1) AttachNodes(nodeIDs, closest);
            else DetachNodes(nodeIDs);

            return closest;
        }

        public void AttachNodes(IEnumerable<int> nodeIDs, int surfID)
        {
            if (!_surfaces.ContainsKey(surfID)) return;

            DetachNodes(nodeIDs);

            foreach (var nodeID in nodeIDs)
            {
                _surfaces[surfID].Nodes[nodeID] = _manager.GetMLNodeTransform(nodeID);
                _nodeToSurf[nodeID] = surfID;
            }

            _manager.PauseStorageUpdate();
            _manager.PauseRenderUpdate();

            GetInterAndOuterLinks(_surfaces[surfID].Nodes.Keys, out var interLinks, out var outerLinks, out var isStartOuterLinks);
            _manager.SetMLLinksBundlingStrength(interLinks, 0f);
            _manager.SetMLLinksBundleStart(outerLinks.Where((_, idx) => isStartOuterLinks[idx]), false);
            _manager.SetMLLinksBundleEnd(outerLinks.Where((_, idx) => !isStartOuterLinks[idx]), false);

            _manager.SetMLLinksAlpha(interLinks, _mlSettings.LinkNormalAlphaFactor);
            _manager.SetMLLinksAlpha(outerLinks, _mlSettings.LinkContext2FocusAlphaFactor);

            _manager.UnpauseRenderUpdate();
            // don't unpause storage update, it'll be updated at the end of animation

            StartAttachAnim(nodeIDs, surfID);
        }

        public void DetachNodes(IEnumerable<int> nodeIDs)
        {
            foreach (var nodeID in nodeIDs)
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

        void StartAttachAnim(IEnumerable<int> nodeIDs, int surfID)
        {
            CoroutineUtils.StopIfRunning(this, ref _attachAnimation);

            var startPositions = nodeIDs.Select(nid => _manager.GetMLNodeTransform(nid).position);
            var endPositions = SurfaceManagerUtils.CalcProjected(surfID, nodeIDs.Select(nid => _manager.NetworkGlobal.Nodes[nid]), this, _manager);

            _attachAnimation = StartCoroutine(CRAnimateNodesAttach(nodeIDs, startPositions, endPositions));
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
            });
        }

        void UnhighlightSurfaces()
        {
            foreach (var surf in _surfaces.Values)
            {
                SurfaceManagerUtils.UnhighlightSurface(surf.Renderer);
            }
        }

        void OnNodeGrabExit(Node node, SelectExitEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                CoroutineUtils.StopIfRunning(this, ref _surfaceHighlighter);
                TryAttachNode(node.ID);
                UnhighlightSurfaces();
            }
        }

        void OnNodeGrabEnter(Node node, SelectEnterEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                CoroutineUtils.StopIfRunning(this, ref _surfaceHighlighter);
                _surfaceHighlighter = StartCoroutine(CRHighlightClosestSurface(_manager.GetMLNodeTransform(node.ID)));
            }
        }

        void OnCommunityGrabExit(Community community, SelectExitEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                CoroutineUtils.StopIfRunning(this, ref _surfaceHighlighter);
                TryAttachNodes(community.Nodes.Select(n => n.ID));
                UnhighlightSurfaces();
            }
        }

        void OnCommunityGrabEnter(Community community, SelectEnterEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                CoroutineUtils.StopIfRunning(this, ref _surfaceHighlighter);
                _surfaceHighlighter = StartCoroutine(CRHighlightClosestSurface(_manager.GetMLCommTransform(community.ID)));
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

        IEnumerator CRAnimateNodesAttach(IEnumerable<int> nodeIDs, IEnumerable<Vector3> startPositions, IEnumerable<Vector3> endPositions)
        {
            float dur = 1.0f;

            _manager.PauseStorageUpdate();

            yield return AnimationUtils.Lerp(dur, t =>
            {
                var positions = startPositions.Zip(endPositions, Tuple.Create)
                    .Select((se, i) => Vector3.Lerp(se.Item1, se.Item2, Mathf.SmoothStep(0f, 1f, t)));

                _manager.SetMLNodesPosition(nodeIDs, positions);
            });

            _manager.SetMLNodesPosition(nodeIDs, endPositions);

            _manager.UnpauseStorageUpdate();

            _attachAnimation = null;
        }

        void GetInterAndOuterLinks(IEnumerable<int> nodeIDs, out List<int> interLinks, out List<int> outerLinks, out List<bool> isStartOuterLinks)
        {
            interLinks = new List<int>();
            outerLinks = new List<int>();
            isStartOuterLinks = new List<bool>();

            foreach (var nodeID in nodeIDs)
            {
                foreach (var link in _manager.NetworkGlobal.NodeLinkMatrixUndir[nodeID])
                {
                    // check other node of this link
                    if (nodeID == link.SourceNodeID)
                    {
                        // check targetnode of link
                        if (nodeIDs.Contains(link.TargetNodeID))
                        {
                            interLinks.Add(link.ID);
                        }
                        else
                        {
                            outerLinks.Add(link.ID);
                            isStartOuterLinks.Add(true);
                        }
                    }
                    else
                    {
                        // check sourcenode of link
                        if (nodeIDs.Contains(link.SourceNodeID))
                        {
                            interLinks.Add(link.ID);
                        }
                        else
                        {
                            outerLinks.Add(link.ID);
                            isStartOuterLinks.Add(false);
                        }
                    }
                }
            }
        }
    }

}