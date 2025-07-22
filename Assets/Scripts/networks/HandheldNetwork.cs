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
        OverviewLayoutTransformer _transformer;

        MultiLayoutNetwork _mlNetwork;
        Dictionary<int, BasicSubnetwork> _subnetworks;

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

        public void Initialize(MultiLayoutNetwork mlNetwork, Dictionary<int, BasicSubnetwork> subnetworks)
        {
            _manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _mlNetwork = mlNetwork;
            _subnetworks = subnetworks;

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
            _renderer.UpdateRenderElements();
        }

        public override void DrawPreview()
        {
            Draw();
        }

        void Draw()
        {
            _renderer.UpdateRenderElements();
            _renderer.Draw();
        }

        void InitializeTransformers()
        {
            _transformer = GetComponentInChildren<OverviewLayoutTransformer>();
            _transformer.Initialize(_manager.NetworkGlobal, _networkContext);
        }
    }
}