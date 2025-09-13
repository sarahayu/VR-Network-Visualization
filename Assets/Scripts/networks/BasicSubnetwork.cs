using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VidiGraph
{
    public class BasicSubnetwork : NodeLinkNetwork
    {
        void Update()
        {
            Draw();
        }

        public void Initialize(IEnumerable<int> nodeIDs, MultiLayoutContext sourceContext, bool useShell = true)
        {
            GetManager();

            InitContext(nodeIDs, sourceContext, useShell);
            InitTransformers();

            // apply initial transformations before first render so we don't get a weird jump
            // TransformNetworkNoRender("encoding");
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

        void InitContext(IEnumerable<int> nodeIDs, MultiLayoutContext sourceContext, bool useShell)
        {
            _context = new MultiLayoutContext(subnetworkID: _id, useShell);
            _context.SetFromContext(_manager.NetworkGlobal, sourceContext, nodeIDs);
            _context.ContextSettings = BaseSettings;
        }
    }
}