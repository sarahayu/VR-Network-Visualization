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
        [SerializeField] GameObject _nodeCloudOutlinePrefab;

        [SerializeField] float _headsetFOV = 100f;     // Meta Quest FOV
        [SerializeField] float _nodeSize = 0.01f;
        [SerializeField] float _appearThreshold = 0.7f;

        const int MAX_NODES = 3000;
        ParticleSystem.Particle[] _nodes;
        ParticleSystem.Particle[] _nodeOutlines;
        ParticleSystem _nodeParticles;
        ParticleSystem _nodeParticleOutlines;
        GameObject _floor;
        GameObject _person;
        MinimapContext _networkContext;
        NetworkGlobal _networkGlobal;

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
            _networkGlobal = GameObject.Find("/Network Manager").GetComponent<NetworkManager>().NetworkGlobal;

            _gyroscope = new GameObject("gyroscope");
            _gyroscope.transform.parent = _networkTransform;
            _gyroscope.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            _gyroscope.transform.localScale = Vector3.one;

            var floorObj = Instantiate(_floorPrefab, _gyroscope.transform);
            var nodesObj = Instantiate(_nodeCloudPrefab, _gyroscope.transform);
            var nodeOutlinesObj = Instantiate(_nodeCloudOutlinePrefab, _gyroscope.transform);

            _floor = floorObj.GetNamedChild("Floor");
            _person = floorObj.GetNamedChild("Person");

            var renderer = _floor.GetNamedChild("Floor Visual").GetComponent<Renderer>();
            MaterialPropertyBlock props = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(props);
            props.SetFloat("_FOV", _headsetFOV);
            renderer.SetPropertyBlock(props);

            _nodeParticles = nodesObj.GetComponent<ParticleSystem>();
            _nodeParticleOutlines = nodeOutlinesObj.GetComponent<ParticleSystem>();

            _nodes = new ParticleSystem.Particle[MAX_NODES];
            _nodeOutlines = new ParticleSystem.Particle[MAX_NODES];
        }

        public override void Draw()
        {
            _nodeParticles.SetParticles(_nodes, _nodes.Length);
            _nodeParticleOutlines.SetParticles(_nodeOutlines, _nodeOutlines.Length);
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
            userPos.y -= 1.36144f / 2;
            var totScale = _networkContext.Scale * _networkContext.Zoom;

            for (int i = 0; i < nodes.Count; i++)
            {
                _nodes[i].position = (nodes[i].Position - userPos) / totScale;
                _nodes[i].startColor = nodes[i].Color;
                _nodes[i].startSize = _nodeSize * nodes[i].Size;
                _nodes[i].remainingLifetime = Mathf.Infinity;
            }

            for (int i = nodes.Count; i < MAX_NODES; i++)
            {
                _nodes[i].remainingLifetime = -1f;
            }

            List<int> hoveredNodes = new();

            if (_networkGlobal.HoveredNode != null)
            {
                hoveredNodes.Add(_networkGlobal.HoveredNode.ID);
            }
            else if (_networkGlobal.HoveredCommunity != null)
            {
                hoveredNodes = hoveredNodes.Union(_networkGlobal.HoveredCommunity.Nodes.Select(n => n.ID)).ToList();
            }

            for (int i = 0; i < hoveredNodes.Count; i++)
            {
                var nid = hoveredNodes[i];
                _nodeOutlines[i].position = (_networkContext.Nodes[nid].Position - userPos) / totScale;
                _nodeOutlines[i].startColor = Color.green;
                _nodeOutlines[i].startSize = _nodeSize * _networkContext.Nodes[nid].Size * 1.5f;
                _nodeOutlines[i].remainingLifetime = Mathf.Infinity;
            }

            for (int i = hoveredNodes.Count; i < MAX_NODES; i++)
            {
                _nodeOutlines[i].startSize = 0;
                _nodeOutlines[i].remainingLifetime = -1f;
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
                surf.transform.localScale = Vector3.one / _networkContext.Zoom;
                surf.transform.SetLocalPositionAndRotation(pos, _networkContext.Surfaces[surfID].Rotation);
            }

            _person.transform.localScale = Vector3.one / _networkContext.Zoom;
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