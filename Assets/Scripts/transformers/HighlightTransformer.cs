using System;
using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Assertions;

namespace VidiGraph
{
    public class HighlightTransformer : NetworkContextTransformer
    {
        NetworkGlobal _networkGlobal;
        MultiLayoutContext _networkContext;

        public override void Initialize(NetworkGlobal networkGlobal, NetworkContext networkContext)
        {
            _networkContext = (MultiLayoutContext)networkContext;

            var manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _networkGlobal = manager.NetworkGlobal;
        }

        public override void ApplyTransformation()
        {
            foreach (var node in _networkGlobal.Nodes)
            {
                if (node.Dirty)
                {
                    var selected = node.SelectedOnSubnetworks.Contains(_networkContext.SubnetworkID);
                    _networkContext.Nodes[node.ID].Dirty = true;

                    foreach (var link in _networkGlobal.NodeLinkMatrixUndir[node.ID])
                    {
                        _networkContext.Links[link.ID].Dirty = true;
                    }

                }
            }
        }

        public override TransformInterpolator GetInterpolator()
        {
            return new HighlightInterpolator();
        }
    }

    public class HighlightInterpolator : TransformInterpolator
    {
        public override void Interpolate(float t)
        {
            // leave empty, highlights shouldn't be interpolatable... for now
        }
    }

}