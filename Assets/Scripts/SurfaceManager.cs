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
        [SerializeField]
        GameObject _surfacePrefab;
        [SerializeField]
        float _surfaceAttractionDist = 0.1f;
        [SerializeField]
        Color _highlightCol = Color.green;

        public Dictionary<int, GameObject> Surfaces { get { return _surfaces.Values.ToDictionary(si => si.ID, si => si.GameObject); } }

        public delegate void SurfaceHoverEnterEvent(int surfaceID, HoverEnterEventArgs evt);
        public event SurfaceHoverEnterEvent OnSurfaceHoverEnter;
        public delegate void SurfaceHoverExitEvent(int surfaceID, HoverExitEventArgs evt);
        public event SurfaceHoverExitEvent OnSurfaceHoverExit;

        class Surface
        {
            public int ID;
            public GameObject GameObject;
            public Renderer Renderer;
            public Dictionary<int, Transform> Nodes = new Dictionary<int, Transform>();
        }

        Dictionary<int, Surface> _surfaces = new Dictionary<int, Surface>();
        Dictionary<int, int> _nodeToSurf = new Dictionary<int, int>();

        int _curID = 0;

        NetworkManager _manager;
        NetworkRenderer _mlRenderer;


        Coroutine _surfaceHighlighter = null;
        Coroutine _surfaceMover = null;
        Coroutine _attachAnimation = null;

        Transform _userPos;

        // Start is called before the first frame update
        void Start()
        {
            _manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _mlRenderer = GameObject.Find("/MultiLayout Network").GetComponentInChildren<NetworkRenderer>();

            _userPos = GameObject.FindWithTag("MainCamera").transform;

            // _mlRenderer.OnNodeGrabEnter += OnNodeGrabEnter;
            // _mlRenderer.OnNodeGrabExit += OnNodeGrabExit;
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

            si.ID = id;
            si.GameObject = surfObject;
            si.Renderer = surfObject.GetNamedChild("Highlight").GetComponent<Renderer>();

            _surfaces[id] = si;

            return id;
        }

        // spawn in front of main camera
        public int SpawnSurface()
        {
            return SpawnSurface(_userPos.position + _userPos.rotation * Vector3.forward, Quaternion.FromToRotation(Vector3.up, -_userPos.forward));
        }


        public void DeleteSurface(int surfID)
        {
            if (_surfaces.ContainsKey(surfID))
            {
                UnityEngine.Object.Destroy(_surfaces[surfID].GameObject);

                foreach (var nodeID in _surfaces[surfID].Nodes.Keys) _nodeToSurf.Remove(nodeID);
                _surfaces.Remove(surfID);
            }
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
        public int TryAttachNodes(List<int> nodeIDs)
        {
            // check distance
            // attach if distance less than threshold

            var closest = GetClosestSurface(_manager.GetMLNodeTransform(nodeIDs[0]).position);

            if (closest != -1) AttachNodes(nodeIDs, closest);
            else DetachNodes(nodeIDs);

            return closest;
        }

        public void AttachNodes(List<int> nodeIDs, int surfID)
        {
            if (!_surfaces.ContainsKey(surfID)) return;

            DetachNodes(nodeIDs);

            foreach (var nodeID in nodeIDs)
            {
                _surfaces[surfID].Nodes[nodeID] = _manager.GetMLNodeTransform(nodeID);
                _nodeToSurf[nodeID] = surfID;
            }

            StartAttachAnim(nodeIDs, surfID);
        }

        public void DetachNodes(List<int> nodeIDs)
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

        void StartAttachAnim(List<int> nodeIDs, int surfID)
        {
            CoroutineUtils.StopIfRunning(this, _attachAnimation);

            var startPositions = nodeIDs.Select(nid => _manager.GetMLNodeTransform(nid).position).ToList();
            var endPositions = SurfaceManagerUtils.CalcProjected(surfID, nodeIDs, this, _manager);

            _attachAnimation = StartCoroutine(CRAnimateNodesAttach(nodeIDs, startPositions, endPositions));
        }

        void AddSurfaceInteraction(GameObject gameObject, int id)
        {
            XRGrabInteractable xrInteractable = gameObject.GetComponent<XRGrabInteractable>();

            xrInteractable.hoverEntered.AddListener(evt =>
            {
                OnSurfaceHoverEnter(id, evt);
            });

            xrInteractable.hoverExited.AddListener(evt =>
            {
                OnSurfaceHoverExit(id, evt);
            });

            xrInteractable.selectEntered.AddListener(evt =>
            {
                // start coroutine to change transforms
                // start node moves
                _surfaceMover = StartCoroutine(CRMoveSurfaceAndChildren(id));
                _manager.StartMLNodesMove(_surfaces[id].Nodes.Keys.ToList());
            });

            xrInteractable.selectExited.AddListener(evt =>
            {
                // end coroutine to change transforms
                // end node moves
                StopCoroutine(_surfaceMover);
                _manager.EndMLNodesMove(_surfaces[id].Nodes.Keys.ToList());
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
                CoroutineUtils.StopIfRunning(this, _surfaceHighlighter);
                TryAttachNode(node.ID);
                UnhighlightSurfaces();
            }
        }

        void OnNodeGrabEnter(Node node, SelectEnterEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                CoroutineUtils.StopIfRunning(this, _surfaceHighlighter);
                _surfaceHighlighter = StartCoroutine(CRHighlightClosestSurface(_manager.GetMLNodeTransform(node.ID)));
            }
        }

        void OnCommunityGrabExit(Community community, SelectExitEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                CoroutineUtils.StopIfRunning(this, _surfaceHighlighter);
                TryAttachNodes(community.Nodes.Select(n => n.ID).ToList());
                UnhighlightSurfaces();
            }
        }

        void OnCommunityGrabEnter(Community community, SelectEnterEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                CoroutineUtils.StopIfRunning(this, _surfaceHighlighter);
                _surfaceHighlighter = StartCoroutine(CRHighlightClosestSurface(_manager.GetMLCommTransform(community.ID)));
            }
        }

        int GetClosestSurface(Vector3 position)
        {
            int closest = -1;
            float closestDist = float.PositiveInfinity;

            foreach (var surf in _surfaces.Values)
            {
                float dist = Vector3.Distance(surf.GameObject.transform.position, position);

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
            var childrenTransform = _surfaces[surfID].Nodes.Keys.Select(nid => _manager.GetMLNodeTransform(nid)).ToList();

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

        IEnumerator CRAnimateNodesAttach(List<int> nodeIDs, List<Vector3> startPositions, List<Vector3> endPositions)
        {
            float dur = 1.0f;

            yield return AnimationUtils.Lerp(dur, t =>
            {
                var positions = startPositions.Select((sp, i) => Vector3.Lerp(startPositions[i], endPositions[i], Mathf.SmoothStep(0f, 1f, t))).ToList();

                _manager.SetNodesPosition(nodeIDs, positions, updateStorage: false);
            });

            _manager.SetNodesPosition(nodeIDs, endPositions, updateStorage: true);

            _attachAnimation = null;
        }
    }

}