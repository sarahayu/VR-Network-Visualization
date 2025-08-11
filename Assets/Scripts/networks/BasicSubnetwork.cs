using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VidiGraph
{
    public class BasicSubnetwork : NodeLinkNetwork
    {

        BasicSubnetworkInput _input;

        static int _idCounter = 1;

        void Update()
        {
            Draw();
        }

        public void Initialize(IEnumerable<int> nodeIDs, MultiLayoutContext sourceContext)
        {
            _id = _idCounter++;
            GetManager();

            InitContext(nodeIDs, sourceContext);
            InitInput();
            InitTransformers();

            // apply initial transformations before first render so we don't get a weird jump
            TransformNetworkNoRender("encoding");
            SetLinksBundlingStrength(_context.Links.Keys, 0f, updateStorage: true, updateRenderElements: false);
            TransformNetworkNoRender("edit");

            InitRenderer();
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
                case "hairball": _hairballLayoutTransformer.UpdateOnNextApply(commID); break;
                default: break;
            }
        }

        void InitContext(IEnumerable<int> nodeIDs, MultiLayoutContext sourceContext)
        {
            _context = new MultiLayoutContext(_id);
            _context.SetFromContext(_manager.NetworkGlobal, sourceContext, nodeIDs);
            _context.ContextSettings = BaseSettings;
        }

        void InitInput()
        {
            _input = GetComponent<BasicSubnetworkInput>();
            _input.Initialize(ID);
        }
    }
}