using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
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
    Color _highlightColor = Color.blue;

    Dictionary<int, GameObject> _surfaces = new Dictionary<int, GameObject>();
    Dictionary<int, Renderer> _surfRenderers = new Dictionary<int, Renderer>();

    public Dictionary<int, GameObject> Surfaces { get { return _surfaces; } }
    Dictionary<int, List<Transform>> _surfaceChildren = new Dictionary<int, List<Transform>>();
    Dictionary<int, List<int>> _surfaceChildrenNodes = new Dictionary<int, List<int>>();

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
    }

    // Update is called once per frame
    void Update()
    {

    }

    Color? defaultColor = null;

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
        _surfRenderers[id] = surfObject.GetComponentInChildren<Renderer>();
        _surfaceChildren[id] = new List<Transform>();
        _surfaceChildrenNodes[id] = new List<int>();

        if (defaultColor == null)
        {
            // TODO figure out why basecolor is black
            MaterialPropertyBlock props = new MaterialPropertyBlock();
            _surfRenderers[id].GetPropertyBlock(props);
            defaultColor = props.GetColor("_BaseColor");
            print(defaultColor.ToString());
        }

        return id;
    }

    public void DeleteSurface(int surfID)
    {
        if (_surfaces.ContainsKey(surfID))
        {
            Object.Destroy(_surfaces[surfID]);

            _surfaces.Remove(surfID);
        }
    }

    public void TryAttach(int nodeID)
    {
        // check distance
        // attach if distance less

        var closest = GetClosestSurface(_manager.GetMLNodeTransform(nodeID).position);

        if (closest != -1)
        {
            _surfaceChildrenNodes[closest].Add(nodeID);
            _surfaceChildren[closest].Add(_manager.GetMLNodeTransform(nodeID));
        }
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
        });
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
                //  TODO fix why this doesn't change color
                var renderer = _surfRenderers[surfID];
                MaterialPropertyBlock props = new MaterialPropertyBlock();

                renderer.GetPropertyBlock(props);
                props.SetColor("_BaseColor", (Color)(surfID == closest ? _highlightColor : defaultColor));
                renderer.SetPropertyBlock(props);

            }
            yield return null;
        }
    }
}
