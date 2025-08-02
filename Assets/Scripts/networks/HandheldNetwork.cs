using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class HandheldNetwork : Network
    {
        [SerializeField] float _zoomSpeed = 1f;
        [SerializeField] float _scale = 40f;

        public MinimapContext Context { get { return _networkContext; } }

        MinimapContext _networkContext = new MinimapContext();
        NetworkManager _networkManager;
        InputManager _inputManager;
        NetworkRenderer _renderer;
        OverviewLayoutTransformer _transformer;

        MultiLayoutNetwork _mlNetwork;
        Dictionary<int, BasicSubnetwork> _subnetworks;

        public void Initialize(MultiLayoutNetwork mlNetwork, Dictionary<int, BasicSubnetwork> subnetworks)
        {
            _networkManager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _inputManager = GameObject.Find("/Input Manager").GetComponent<InputManager>();
            _mlNetwork = mlNetwork;
            _subnetworks = subnetworks;
            _networkContext.Scale = _scale;

            InitializeTransformers();

            _transformer.UpdateData(_mlNetwork, _subnetworks.Values);
            _transformer.ApplyTransformation();
            // _networkContext.RecomputeProps(_manager.NetworkGlobal);

            _renderer = GetComponentInChildren<NetworkRenderer>();
            _renderer.Initialize(_networkContext);
            _renderer.UpdateRenderElements();
        }

        public override void UpdateRenderElements()
        {
            _transformer.UpdateData(_mlNetwork, _subnetworks.Values);
            _transformer.ApplyTransformation();
            _renderer.UpdateRenderElements();
        }

        public override void DrawPreview()
        {
            Draw();
        }

        void Awake()
        {
        }

        void Start()
        {
        }

        void Update()
        {

            var thumbVal = _inputManager.LeftJoystick.ReadValue();

            if (thumbVal != Vector2.zero)
            {
                float zoom = -Vector2.Dot(thumbVal, Vector2.up) * Time.deltaTime * _zoomSpeed;
                _networkContext.Zoom = Mathf.Clamp(_networkContext.Zoom + zoom, 0.1f, 2);
            }

            Draw();
        }

        public void PushSelectionEvent(IEnumerable<int> nodes, int subnetworkID)
        {
            _transformer.PushSelectionEvent(nodes, subnetworkID);
        }


        /*=============== start private methods ===================*/

        void Draw()
        {
            _renderer.UpdateRenderElements();
            _renderer.Draw();
        }

        void InitializeTransformers()
        {
            _transformer = GetComponentInChildren<OverviewLayoutTransformer>();
            _transformer.Initialize(_networkManager.NetworkGlobal, _networkContext);
        }

        void OnZoomListener()
        {

        }

        /*=============== end private methods ===================*/
    }
}