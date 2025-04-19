using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SurfaceManager : MonoBehaviour
{
    [SerializeField]
    GameObject _surfacePrefab;

    Dictionary<int, GameObject> _surfaces = new Dictionary<int, GameObject>();

    public Dictionary<int, GameObject> Surfaces { get { return _surfaces; } }

    int _curID = 0;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    // return surface ID
    public int SpawnSurface(Vector3 position, Quaternion rotation)
    {
        var surfObject = Object.Instantiate(_surfacePrefab, transform);

        surfObject.transform.SetPositionAndRotation(position, rotation);

        int id = GetNextID();

        _surfaces[id] = surfObject;

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

    int GetNextID()
    {
        return _curID++;
    }
}
