using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VidiGraph
{
    public class BasicSubnetwork : NodeLinkNetwork
    {
        [SerializeField] bool _overrideContextSettings;
        [SerializeField] NodeLinkContext.Settings _settings;

        void Update()
        {
            Draw();
        }

        public void Initialize(IEnumerable<int> nodeIDs, NodeLinkContext sourceContext, bool useShell = true)
        {
            GetManager();

            InitContext(nodeIDs, sourceContext, useShell);
            InitTransformers();

            // apply initial transformations before first render so we don't get a weird jump

            if (_overrideContextSettings)
                TransformNetworkNoRender("encoding");
            SetLinksBundlingStrength(_context.Links.Keys, 0f, updateStorage: true, updateRenderElements: false);
            // TransformNetworkNoRender("edit");

            InitRenderer();
        }

        public void Destroy()
        {
            _renderer.Destroy();
        }

        public override void ReturnNodes(IEnumerable<int> nodeIDs, Action onFinished = null)
        {
            Debug.Log("not implemented yet");
        }

        /*=============== start private methods ===================*/

        void InitContext(IEnumerable<int> nodeIDs, NodeLinkContext sourceContext, bool useShell)
        {
            _context = new NodeLinkContext(subnetworkID: _id, useShell);
            _context.SetFromContext(_manager.NetworkGlobal, sourceContext, nodeIDs);

            if (_overrideContextSettings)
            {
                _context.ContextSettings = _settings;
                _context.SetDefaultEncodings(null, null);
            }
        }
    }
}