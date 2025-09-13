using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VidiGraph
{
    public class FrameNetwork : NodeLinkNetwork
    {
        void Update()
        {
            Draw();
        }

        public void Initialize(MultiLayoutContext sourceContext, bool useShell = true)
        {
            GetManager();

            InitContext(sourceContext, useShell);
            InitTransformers();
            InitOtherTransformers();

            UpdateContext(sourceContext);
            SetLinksBundlingStrength(_context.Links.Keys, 0f, updateStorage: true, updateRenderElements: false);
            TransformNetworkNoRender("flattened");

            InitRenderer();
        }

        public void UpdateContext(MultiLayoutContext sourceContext)
        {
            _context.SetFromContext(_manager.NetworkGlobal, sourceContext, sourceContext.Nodes.Keys);
            _context.ContextSettings = BaseSettings;

            var relScale = GetComponentInChildren<WorldTransformTransformer>().transform.lossyScale.x;

            foreach (var node in _context.Nodes.Values)
            {
                node.Moveable = false;
                node.Size *= relScale;
            }

            // _context.ContextSettings.LinkWidth *= relScale;
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

        void InitContext(MultiLayoutContext sourceContext, bool useShell)
        {
            _context = new MultiLayoutContext(subnetworkID: _id, useShell);
        }

        void InitOtherTransformers()
        {
            _transformers["flattened"] = GetComponentInChildren<WorldTransformTransformer>();
            _transformers["flattened"].Initialize(_manager.NetworkGlobal, _context);
        }
    }
}