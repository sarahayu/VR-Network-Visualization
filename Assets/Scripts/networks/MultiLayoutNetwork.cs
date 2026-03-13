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
        public MultiLayoutContext.Settings BaseSettings;
        // for now, only one multilayoutnetwork shall exist and it is the main network in the scene
        public const int InstanceID = 0;

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
            _id = InstanceID;
            GetManager();

            InitContext();
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
                    PreprocLayoutChange(commID, "spherical");
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

            if (animated) TransformNetworkFast(layout);
            else TransformNetwork(layout, animated);

            _isSphericalLayout = newIsSphericalLayout;
        }

        public override void ReturnNodes(IEnumerable<int> nodeIDs, Action onFinished = null)
        {
            _sphericalLayoutTransformer.UpdateNodesOnNextApply(nodeIDs);

            TransformNetwork("spherical", animated: true, onFinished: onFinished);
        }

        /*=============== start private methods ===================*/

        void TransformNetworkFast(string transformer, Action onFinished = null,
            bool updateCommunityProps = true, bool updateStorage = true, bool updateRenderElements = true)
        {
            if (CoroutineUtils.StopIfRunning(this, ref _curAnim))
            {
                // update network since we cancelled coroutine prematurely

                UpdateNetwork(
                    updateCommunityProps: true,
                    updateStorage: false,
                    updateRenderElements: true
                );
            }

            _curAnim = StartCoroutine(CRAnimateTransformationFast(
                transformer: transformer,
                onFinished: onFinished,
                updateCommunityProps: updateCommunityProps,
                updateStorage: updateStorage,
                updateRenderElements: updateRenderElements
            ));

        }

        void InitContext()
        {
            _context = new MultiLayoutContext(subnetworkID: 0, useShell: false);
            _context.SetFromGlobal(_manager.NetworkGlobal, _manager.FileLoader.SphericalLayout);
            _context.ContextSettings = BaseSettings;
        }

        protected override void PreprocLayoutChange(int commID, string layout)
        {
            switch (layout)
            {
                case "cluster": _clusterLayoutTransformer.UpdateOnNextApply(commID); break;
                case "floor": _floorLayoutTransformer.UpdateOnNextApply(commID); break;
                case "spherical": _sphericalLayoutTransformer.UpdateCommOnNextApply(commID); break;
                default: break;
            }
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

        protected IEnumerator CRAnimateTransformationFast(string transformer, Action onFinished = null,
            bool updateCommunityProps = true, bool updateStorage = false, bool updateRenderElements = true)
        {
            float dur = 1.0f;
            var interpolator = _transformers[transformer]?.GetInterpolator();

            if (interpolator == null) yield return null;

            yield return AnimationUtils.Lerp(dur, t =>
            {
                interpolator.Interpolate(t);
                // update network without updating the storage for performance reasons
                // only update storage at the end

                UpdateNetwork(
                    updateCommunityProps: false,
                    updateStorage: false,
                    updateRenderElements: true
                );
            });

            interpolator.Interpolate(1f);

            UpdateNetwork(
                updateCommunityProps: updateCommunityProps,
                updateStorage: updateStorage,
                updateRenderElements: updateRenderElements
            );

            _curAnim = null;

            onFinished?.Invoke();
        }
    }
}