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
            userPos.y = 0;
            for (int ii = 0; ii < nodes.Count; ++ii)
            {
                _nodes[ii].position = Quaternion.AngleAxis(-_userTransform.rotation.eulerAngles.y, Vector3.up) * ((nodes[ii].Position - userPos) / 40);
                _nodes[ii].startColor = nodes[ii].Color;
                _nodes[ii].startSize = 0.01f;
                _nodes[ii].remainingLifetime = 1000000f;
            }
            for (int ii = nodes.Count; ii < MAX_NODES; ++ii)
            {
                _nodes[ii].remainingLifetime = -1f;
            }
        }
    }
}