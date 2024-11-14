using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class SmallNetwork : Network
    {
        NetworkManager _manager;
        NetworkRenderer _renderer;
        NetworkContext3D _networkContext = new NetworkContext3D();
        NetworkLayout _layout;

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

        public override void Initialize()
        {
            _manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _networkContext.Update(_manager.Data);

            _renderer = GetComponentInChildren<NetworkRenderer>();
            _renderer.Initialize(_networkContext);

            InitializeLayouts();

            _layout.ApplyLayout();
            _networkContext.RecomputeGeometricProps(_manager.Data);
            _renderer.UpdateRenderElements();
        }
        public override void UpdateLayouts()
        {
            // TODO implement
        }

        public override void UpdateRenderElements()
        {
            // TODO implement
        }

        public override void DrawPreview()
        {
            Draw();
        }

        void Draw()
        {
            _renderer.Draw();
        }

        void InitializeLayouts()
        {
            _layout = GetComponentInChildren<HairballLayout>();
            _layout.Initialize(_networkContext);
        }
    }
}