using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VidiGraph;

public class SurfaceManager : MonoBehaviour
{
    [SerializeField]
    GameObject _surfacePrefab;

    Dictionary<int, GameObject> _surfaces = new Dictionary<int, GameObject>();

    public Dictionary<int, GameObject> Surfaces { get { return _surfaces; } }
    Dictionary<int, List<Transform>> _surfaceChildren = new Dictionary<int, List<Transform>>();
    Dictionary<int, List<int>> _surfaceChildrenNodes = new Dictionary<int, List<int>>();

    public delegate void SurfaceHoverEnterEvent(int surfaceID, HoverEnterEventArgs evt);
    public event SurfaceHoverEnterEvent OnSurfaceHoverEnter;
    public delegate void SurfaceHoverExitEvent(int surfaceID, HoverExitEventArgs evt);
    public event SurfaceHoverExitEvent OnSurfaceHoverExit;

    int _curID = 0;

    NetworkManager _manager;

    // Start is called before the first frame update
    void Start()
    {
        _manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
    }

    // Update is called once per frame
    void Update()
    {

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
        }
    }

    public void TryAttach(int nodeID)
    {
        // check distance
        // attach if distance less

        int surfID = _surfaces.Keys.ToList()[0];

        _surfaceChildrenNodes[surfID].Add(nodeID);
        _surfaceChildren[surfID].Add(_manager.GetMLNodeTransform(nodeID));
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
            StartCoroutine(CRMoveSurfaceAndChildren(_surfaces[id].transform, _surfaceChildren[id]));
            _manager.StartMLNodesMove(_surfaceChildrenNodes[id]);
        });

        xrInteractable.selectExited.AddListener(evt =>
        {
            // end coroutine to change transforms
            // end node moves
        });
    }

    Vector3 lastSurfPosition = Vector3.positiveInfinity;

    IEnumerator CRMoveSurfaceAndChildren(Transform surf, List<Transform> toMove)
    {
        for (; ; )
        {
            var curPosition = surf.transform.position;

            if (float.IsFinite(lastSurfPosition.x))
            {
                var diff = curPosition - lastSurfPosition;

                foreach (var tform in toMove)
                {
                    tform.position += diff;
                }
            }

            lastSurfPosition = curPosition;
            yield return null;
        }
    }
}
