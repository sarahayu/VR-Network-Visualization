using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class HandheldNetwork : Network
    {
        MinimapContext _networkContext = new MinimapContext();
        public MinimapContext Context { get { return _networkContext; } }
        NetworkManager _manager;
        NetworkRenderer _renderer;
        NetworkContextTransformer _transformer;

        void Awake()
        {
        }

        void Start()
        {
        }

        void Update()
        {
            Draw();
        }

        public void Initialize()
        {
            _manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _networkContext.SetFromGlobal(_manager.NetworkGlobal);

            InitializeTransformers();

            _transformer.ApplyTransformation();
            _networkContext.RecomputeProps(_manager.NetworkGlobal);

            _renderer = GetComponentInChildren<NetworkRenderer>();
            _renderer.Initialize(_networkContext);
            _renderer.UpdateRenderElements();
        }

        public override void UpdateRenderElements()
        {
            _renderer.UpdateRenderElements();
        }

        public override void DrawPreview()
        {
            Draw();
        }

        void Draw()
        {
            _renderer.Draw();
        }

        void InitializeTransformers()
        {
            _transformer = GetComponentInChildren<NetworkContextTransformer>();
            _transformer.Initialize(_manager.NetworkGlobal, _networkContext);
        }
    }
}