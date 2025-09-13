using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VidiGraph
{
    public class BasicSubnetwork : NodeLinkNetwork
    {

        NodeLinkNetworkInteraction _input;

        static int _idCounter = 1;

        void Update()
        {
            Draw();
        }

        public void Initialize(IEnumerable<int> nodeIDs, MultiLayoutContext sourceContext, bool useShell = true)
        {
            _id = _idCounter++;
            GetManager();

            InitContext(nodeIDs, sourceContext, useShell);
            InitInput();
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
            _input.Destroy();
        }

        public override void ReturnNodes(IEnumerable<int> nodeIDs, Action onFinished = null)
        {
            Debug.Log("not implemented yet");
        }

        /*=============== start private methods ===================*/

        protected override void QueueLayoutChange(int commID, string layout)
        {
            switch (layout)
            {
                case "forcedDir": /*do nothing*/ break;
                default: break;
            }
        }

        void InitContext(IEnumerable<int> nodeIDs, MultiLayoutContext sourceContext, bool useShell)
        {
            _context = new MultiLayoutContext(subnetworkID: _id, useShell);
            _context.SetFromContext(_manager.NetworkGlobal, sourceContext, nodeIDs);
            _context.ContextSettings = BaseSettings;
        }

        void InitInput()
        {
            _input = GetComponent<NodeLinkNetworkInteraction>();
            _input.Initialize(ID);
        }
    }
}