using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VidiGraph
{
    public class OverviewRenderer : NetworkRenderer
    {
        const float HEADSET_FOV = 100f;     // Meta Quest FOV

        [SerializeField] Transform _networkTransform;
        [SerializeField] Transform _userTransform;

        [SerializeField] GameObject _floorPrefab;
        [SerializeField] GameObject _surfacePrefab;
        [SerializeField] GameObject _nodeCloudPrefab;

        const int MAX_NODES = 3000;
        ParticleSystem.Particle[] _nodes;
        ParticleSystem _nodeParticles;
        MinimapContext _networkContext;

        Dictionary<int, GameObject> _surfaces = new();

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

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

            var floorObj = UnityEngine.Object.Instantiate(_floorPrefab, _networkTransform);

            MaterialPropertyBlock props = new MaterialPropertyBlock();
            var renderer = floorObj.GetComponent<Renderer>();

            renderer.GetPropertyBlock(props);
            props.SetFloat("_FOV", HEADSET_FOV);
            renderer.SetPropertyBlock(props);

            var nodesObj = Instantiate(_nodeCloudPrefab, _networkTransform);
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

            var userPos = _userTransform.position;
            var userForward = _userTransform.forward;
            userPos.y = 0;
            var rot = Quaternion.AngleAxis(Vector2.SignedAngle(Vector2.up, new Vector2(userForward.x, userForward.z)), Vector3.up);
            for (int ii = 0; ii < nodes.Count; ++ii)
            {
                _nodes[ii].position = rot * ((nodes[ii].Position - userPos) / (_networkContext.Scale * _networkContext.Zoom));
                _nodes[ii].startColor = nodes[ii].Color;
                _nodes[ii].startSize = 0.01f;
                _nodes[ii].remainingLifetime = 1000000f;
            }
            for (int ii = nodes.Count; ii < MAX_NODES; ++ii)
            {
                _nodes[ii].remainingLifetime = -1f;
            }

            foreach (var surfID in _networkContext.Surfaces.Keys.Except(_surfaces.Keys))
            {
                _surfaces[surfID] = Instantiate(_surfacePrefab, _networkTransform);
            }

            foreach (var surfID in _surfaces.Keys.Except(_networkContext.Surfaces.Keys))
            {
                Destroy(_surfaces[surfID]);

                _surfaces.Remove(surfID);
            }

            foreach (var (surfID, surf) in _surfaces)
            {
                var pos = _networkContext.Surfaces[surfID].Position;
                pos.y = 0;

                pos = rot * ((pos - userPos) / (_networkContext.Scale * _networkContext.Zoom));
                surf.transform.SetLocalPositionAndRotation(pos, _networkContext.Surfaces[surfID].Rotation);
            }
        }
    }
}