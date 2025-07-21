using UnityEngine;

namespace VidiGraph
{
    public class OverviewRenderer : NetworkRenderer
    {
        const float HEADSET_FOV = 100f;     // Meta Quest FOV

        [SerializeField] Transform _networkTransform;

        [SerializeField] GameObject _floorPrefab;
        [SerializeField] GameObject _surfacePrefab;

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

            var gobj = UnityEngine.Object.Instantiate(_floorPrefab, _networkTransform);

            MaterialPropertyBlock props = new MaterialPropertyBlock();
            var renderer = gobj.GetComponent<Renderer>();

            renderer.GetPropertyBlock(props);
            props.SetFloat("_FOV", HEADSET_FOV);
            renderer.SetPropertyBlock(props);
        }

        public override void Draw()
        {
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
        }
    }
}