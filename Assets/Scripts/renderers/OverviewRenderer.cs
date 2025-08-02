using System.Collections.Generic;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace VidiGraph
{
    public class OverviewRenderer : NetworkRenderer
    {

        [SerializeField] Transform _networkTransform;
        [SerializeField] Transform _headTransform;
        [SerializeField] Transform _wristTransform;

        [SerializeField] GameObject _floorPrefab;
        [SerializeField] GameObject _surfacePrefab;
        [SerializeField] GameObject _nodeCloudPrefab;

        [SerializeField] float _headsetFOV = 100f;     // Meta Quest FOV
        [SerializeField] float _nodeSize = 0.01f;
        [SerializeField] float _appearThreshold = 0.7f;

        const int MAX_NODES = 3000;
        ParticleSystem.Particle[] _nodes;
        ParticleSystem _nodeParticles;
        GameObject _floor;
        MinimapContext _networkContext;

        Dictionary<int, GameObject> _surfaces = new();
        GameObject _gyroscope;

        // Start is called before the first frame update
        void Start()
        {
            _gyroscope.SetActive(false);
        }

        // Update is called once per frame
        void Update()
        {
            if (Vector3.Dot(-_wristTransform.right, Vector3.up) > _appearThreshold)
            {
                _gyroscope.transform.rotation = Quaternion.identity;
                _floor.transform.rotation = GetTransformRotationXY(_headTransform);
                _gyroscope.SetActive(true);
            }
            else
            {
                _gyroscope.SetActive(false);

            }
        }

        void Reset()
        {
            if (Application.isEditor)
            {
                GameObjectUtils.ChildrenDestroyImmediate(_networkTransform);
            }
            else
            {
                GameObjectUtils.ChildrenDestroy(_networkTransform);
            }
        }

        public override void Initialize(NetworkContext networkContext)
        {
            Reset();

            _networkContext = (MinimapContext)networkContext;

            _gyroscope = new GameObject("gyroscope");
            _gyroscope.transform.parent = _networkTransform;
            _gyroscope.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            _gyroscope.transform.localScale = Vector3.one;

            var floorObj = Instantiate(_floorPrefab, _gyroscope.transform);
            var nodesObj = Instantiate(_nodeCloudPrefab, _gyroscope.transform);

            _floor = floorObj.GetNamedChild("Floor");

            var renderer = _floor.GetNamedChild("Floor Visual").GetComponent<Renderer>();
            MaterialPropertyBlock props = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(props);
            props.SetFloat("_FOV", _headsetFOV);
            renderer.SetPropertyBlock(props);

            _nodeParticles = nodesObj.GetComponent<ParticleSystem>();

            _nodes = new ParticleSystem.Particle[MAX_NODES];
        }

        public override void Draw()
        {
            _nodeParticles.SetParticles(_nodes, _nodes.Length);
        }

        public override Transform GetCommTransform(int commID)
        {
            return transform;
        }

        public override Transform GetNodeTransform(int nodeID)
        {
            return transform;
        }

        public override void UpdateRenderElements()
        {
            var nodes = _networkContext.Nodes.Values.ToList();

            var userPos = _wristTransform.position;
            var totScale = _networkContext.Scale * _networkContext.Zoom;

            for (int i = 0; i < nodes.Count; i++)
            {
                _nodes[i].position = (nodes[i].Position - userPos) / totScale;
                _nodes[i].startColor = nodes[i].Color;
                _nodes[i].startSize = _nodeSize;
                _nodes[i].remainingLifetime = Mathf.Infinity;
            }

            for (int i = nodes.Count; i < MAX_NODES; i++)
            {
                _nodes[i].remainingLifetime = -1f;
            }

            foreach (var surfID in _networkContext.Surfaces.Keys.Except(_surfaces.Keys))
            {
                _surfaces[surfID] = Instantiate(_surfacePrefab, _gyroscope.transform);
            }

            foreach (var surfID in _surfaces.Keys.Except(_networkContext.Surfaces.Keys))
            {
                Destroy(_surfaces[surfID]);

                _surfaces.Remove(surfID);
            }

            foreach (var (surfID, surf) in _surfaces)
            {
                var pos = (_networkContext.Surfaces[surfID].Position - userPos) / totScale;
                surf.transform.SetLocalPositionAndRotation(pos, _networkContext.Surfaces[surfID].Rotation);
            }
        }

        static Quaternion GetTransformRotationXY(Transform transform, bool reverse = false)
        {
            // TODO better way to do this??
            return Quaternion.AngleAxis(
                    (reverse ? 1 : -1) * Vector2.SignedAngle(
                        Vector2.up,
                        new Vector2(transform.forward.x, transform.forward.z)),
                    Vector3.up);
        }
    }
}