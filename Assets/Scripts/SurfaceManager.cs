using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VidiGraph;

public class SurfaceManager : MonoBehaviour
{
    [SerializeField]
    GameObject _surfacePrefab;
    [SerializeField]
    float _surfaceAttractionDist = 0.1f;
    [SerializeField]
    Color _highlightCol = Color.green;

    Dictionary<int, GameObject> _surfaces = new Dictionary<int, GameObject>();
    Dictionary<int, Renderer> _surfRenderers = new Dictionary<int, Renderer>();

    public Dictionary<int, GameObject> Surfaces { get { return _surfaces; } }
    Dictionary<int, List<Transform>> _surfaceChildren = new Dictionary<int, List<Transform>>();
    Dictionary<int, List<int>> _surfaceChildrenNodes = new Dictionary<int, List<int>>();
    Dictionary<int, int> _nodeToSurf = new Dictionary<int, int>();

    public delegate void SurfaceHoverEnterEvent(int surfaceID, HoverEnterEventArgs evt);
    public event SurfaceHoverEnterEvent OnSurfaceHoverEnter;
    public delegate void SurfaceHoverExitEvent(int surfaceID, HoverExitEventArgs evt);
    public event SurfaceHoverExitEvent OnSurfaceHoverExit;

    int _curID = 0;

    NetworkManager _manager;
    NetworkRenderer _mlRenderer;

    Vector3 lastSurfPosition = Vector3.positiveInfinity;
    Quaternion lastSurfRotation = Quaternion.identity;

    Coroutine _surfaceHighlighter = null;
    Coroutine _surfaceMover = null;

    // Start is called before the first frame update
    void Start()
    {
        _manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
        _mlRenderer = GameObject.Find("/MultiLayout Network").GetComponentInChildren<NetworkRenderer>();


        _mlRenderer.OnNodeGrabEnter += OnNodeGrabEnter;
        _mlRenderer.OnNodeGrabExit += OnNodeGrabExit;
        _mlRenderer.OnCommunityGrabEnter += OnCommunityGrabEnter;
        _mlRenderer.OnCommunityGrabExit += OnCommunityGrabExit;
    }

    // return surface ID
    public int SpawnSurface(Vector3 position, Quaternion rotation)
    {
        var surfObject = Object.Instantiate(_surfacePrefab, transform);

        int id = GetNextID();

        surfObject.transform.SetPositionAndRotation(position, rotation);
        TextMeshPro text = surfObject.GetComponentInChildren<TextMeshPro>();
        text.SetText(id.ToString());

        AddSurfaceInteraction(surfObject, id);

        _surfaces[id] = surfObject;
        _surfRenderers[id] = surfObject.GetNamedChild("Highlight").GetComponent<Renderer>();
        _surfaceChildren[id] = new List<Transform>();
        _surfaceChildrenNodes[id] = new List<int>();

        return id;
    }

    public void DeleteSurface(int surfID)
    {
        if (_surfaces.ContainsKey(surfID))
        {
            Object.Destroy(_surfaces[surfID]);

            _surfaces.Remove(surfID);
            _surfRenderers.Remove(surfID);

            foreach (var nodeID in _surfaceChildrenNodes[surfID])
            {
                _nodeToSurf.Remove(nodeID);
            }

            _surfaceChildren.Remove(surfID);
            _surfaceChildrenNodes.Remove(surfID);
        }
    }

    // return ID of closest surface
    public int TryAttach(int nodeID)
    {
        // check distance
        // attach if distance less

        var closest = GetClosestSurface(_manager.GetMLNodeTransform(nodeID).position);

        if (closest != -1)
        {
            if (_nodeToSurf.ContainsKey(nodeID))
            {
                var parent = _nodeToSurf[nodeID];
                var indInList = _surfaceChildrenNodes[parent].IndexOf(nodeID);

                _surfaceChildren[parent].RemoveAt(indInList);
                _surfaceChildrenNodes[parent].RemoveAt(indInList);
            }

            _surfaceChildrenNodes[closest].Add(nodeID);
            _surfaceChildren[closest].Add(_manager.GetMLNodeTransform(nodeID));
            _nodeToSurf[nodeID] = closest;
        }

        return closest;
    }
    // return ID of closest surface
    public int TryAttach(List<int> nodeIDs)
    {
        // check distance
        // attach if distance less

        var closest = GetClosestSurface(_manager.GetMLNodeTransform(nodeIDs[0]).position);

        if (closest != -1)
        {
            foreach (var nodeID in nodeIDs)
            {
                if (_nodeToSurf.ContainsKey(nodeID))
                {
                    var parent = _nodeToSurf[nodeID];
                    var indInList = _surfaceChildrenNodes[parent].IndexOf(nodeID);

                    _surfaceChildren[parent].RemoveAt(indInList);
                    _surfaceChildrenNodes[parent].RemoveAt(indInList);
                }

                _surfaceChildrenNodes[closest].Add(nodeID);
                _surfaceChildren[closest].Add(_manager.GetMLNodeTransform(nodeID));
                _nodeToSurf[nodeID] = closest;
            }
        }

        return closest;
    }

    int GetNextID()
    {
        return _curID++;
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
            _surfaceMover = StartCoroutine(CRMoveSurfaceAndChildren(_surfaces[id].transform, _surfaceChildren[id]));
            _manager.StartMLNodesMove(_surfaceChildrenNodes[id]);
        });

        xrInteractable.selectExited.AddListener(evt =>
        {
            // end coroutine to change transforms
            // end node moves
            StopCoroutine(_surfaceMover);
            _manager.EndMLNodesMove(_surfaceChildrenNodes[id]);

            lastSurfPosition = Vector3.positiveInfinity;
        });
    }

    void UnhighlightSurfaces()
    {
        foreach (var (surfID, surf) in _surfaces)
        {
            var renderer = _surfRenderers[surfID];
            MaterialPropertyBlock props = new MaterialPropertyBlock();

            renderer.GetPropertyBlock(props);
            props.SetColor("_Color", Color.clear);
            renderer.SetPropertyBlock(props);

        }
    }

    IEnumerator CRMoveSurfaceAndChildren(Transform surf, List<Transform> toMove)
    {
        for (; ; )
        {
            var curPosition = surf.transform.position;
            var curRotation = surf.transform.rotation;

            if (float.IsFinite(lastSurfPosition.x))
            {
                var diff = curPosition - lastSurfPosition;
                var diffRot = curRotation * Quaternion.Inverse(lastSurfRotation);
                diffRot.ToAngleAxis(out var angle, out var axis);

                foreach (var tform in toMove)
                {

                    tform.RotateAround(lastSurfPosition, axis, angle);

                    tform.position += diff;
                }
            }

            lastSurfPosition = curPosition;
            lastSurfRotation = curRotation;
            yield return null;
        }
    }

    void OnNodeGrabExit(Node node, SelectExitEventArgs evt)
    {
        if (evt.interactorObject.handedness == InteractorHandedness.Right)
        {
            StopCoroutine(_surfaceHighlighter);
            TryAttach(node.ID);
            UnhighlightSurfaces();
        }
    }

    void OnNodeGrabEnter(Node node, SelectEnterEventArgs evt)
    {
        if (evt.interactorObject.handedness == InteractorHandedness.Right)
        {
            if (_surfaceHighlighter != null)
            {
                StopCoroutine(_surfaceHighlighter);
            }

            _surfaceHighlighter = StartCoroutine(CRHighlightClosestSurface(_manager.GetMLNodeTransform(node.ID)));
        }
    }

    void OnCommunityGrabExit(Community community, SelectExitEventArgs evt)
    {
        if (evt.interactorObject.handedness == InteractorHandedness.Right)
        {
            StopCoroutine(_surfaceHighlighter);
            TryAttach(community.Nodes.Select(n => n.ID).ToList());
            UnhighlightSurfaces();
        }
    }

    void OnCommunityGrabEnter(Community community, SelectEnterEventArgs evt)
    {
        if (evt.interactorObject.handedness == InteractorHandedness.Right)
        {
            if (_surfaceHighlighter != null)
            {
                StopCoroutine(_surfaceHighlighter);
            }

            _surfaceHighlighter = StartCoroutine(CRHighlightClosestSurface(_manager.GetMLCommTransform(community.ID)));
        }
    }

    int GetClosestSurface(Vector3 position)
    {
        int closest = -1;
        float closestDist = float.PositiveInfinity;
        foreach (var (surfID, surf) in _surfaces)
        {
            float dist = Vector3.Distance(surf.transform.position, position);

            if (dist < closestDist && dist < _surfaceAttractionDist)
            {
                closest = surfID;
                closestDist = dist;
            }
        }

        return closest;
    }

    IEnumerator CRHighlightClosestSurface(Transform nodeTransform)
    {
        for (; ; )
        {
            int closest = GetClosestSurface(nodeTransform.position);

            foreach (var (surfID, surf) in _surfaces)
            {
                var renderer = _surfRenderers[surfID];
                MaterialPropertyBlock props = new MaterialPropertyBlock();

                renderer.GetPropertyBlock(props);
                props.SetColor("_Color", surfID == closest ? _highlightCol : Color.clear);
                renderer.SetPropertyBlock(props);

            }
            yield return null;
        }
    }
}
