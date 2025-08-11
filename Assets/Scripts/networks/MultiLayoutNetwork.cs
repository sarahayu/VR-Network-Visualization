using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Neo4j.Driver;
using UnityEngine;

namespace VidiGraph
{
    public class MultiLayoutNetwork : NodeLinkNetwork
    {
        MultiLayoutNetworkInput _input;

        // keep a reference to sphericallayout to focus on individual communities
        SphericalLayoutTransformer _sphericalLayoutTransformer;


        // keep a reference to clusterlayout to focus on individual communities
        ClusterLayoutTransformer _clusterLayoutTransformer;

        // keep a reference to floorLayout to focus on individual communities
        FloorLayoutTransformer _floorLayoutTransformer;

        bool _isSphericalLayout;

        void Update()
        {
            Draw();
        }

        public void Initialize()
        {
            _id = 0;
            GetManager();

            InitContext();
            InitInput();
            InitTransformers();

            // apply initial transformations before first render so we don't get a weird jump
            TransformNetworkNoRender("encoding");
            _isSphericalLayout = true;
            _sphericalLayoutTransformer.UpdateCommsOnNextApply(_context.Communities.Keys);
            TransformNetworkNoRender("spherical");

            InitRenderer();
        }

        public void ToggleSphericalAndHairball(bool animated = true)
        {
            bool newIsSphericalLayout = !_isSphericalLayout;

            string layout = newIsSphericalLayout ? "spherical" : "hairball";

            if (layout == "spherical")
            {
                foreach (var commID in _context.Communities.Keys)
                {
                    QueueLayoutChange(commID, "spherical");
                    _context.Communities[commID].State = MultiLayoutContext.CommunityState.None;
                }
            }
            else
            {

                foreach (var commID in _context.Communities.Keys)
                {
                    _context.Communities[commID].State = MultiLayoutContext.CommunityState.Hairball;
                    _hairballLayoutTransformer.UpdateOnNextApply(_manager.NetworkGlobal.Communities[commID].Nodes.Select(n => n.ID));
                }
            }

            TransformNetwork(layout, animated);

            _isSphericalLayout = newIsSphericalLayout;
        }

        public override void ReturnNodes(IEnumerable<int> nodeIDs, Action onFinished = null)
        {
            _sphericalLayoutTransformer.UpdateNodesOnNextApply(nodeIDs);

            TransformNetwork("spherical", animated: true, onFinished: onFinished);
        }

        /*=============== start private methods ===================*/

        protected override void QueueLayoutChange(int commID, string layout)
        {
            switch (layout)
            {
                case "cluster": _clusterLayoutTransformer.UpdateOnNextApply(commID); break;
                case "floor": _floorLayoutTransformer.UpdateOnNextApply(commID); break;
                case "spherical": _sphericalLayoutTransformer.UpdateCommOnNextApply(commID); break;
                default: break;
            }
        }

        void InitInput()
        {
            _input = GetComponent<MultiLayoutNetworkInput>();
            _input.Initialize();
        }

        new void InitTransformers()
        {
            base.InitTransformers();

            _sphericalLayoutTransformer = GetComponentInChildren<SphericalLayoutTransformer>();
            _transformers["spherical"] = _sphericalLayoutTransformer;
            _transformers["spherical"].Initialize(_manager.NetworkGlobal, _context);

            _clusterLayoutTransformer = GetComponentInChildren<ClusterLayoutTransformer>();
            _transformers["cluster"] = _clusterLayoutTransformer;
            _transformers["cluster"].Initialize(_manager.NetworkGlobal, _context);

            _floorLayoutTransformer = GetComponentInChildren<FloorLayoutTransformer>();
            _transformers["floor"] = _floorLayoutTransformer;
            _transformers["floor"].Initialize(_manager.NetworkGlobal, _context);
        }
    }
}