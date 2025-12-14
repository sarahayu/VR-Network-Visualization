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
        NetworkManager _manager;
        NetworkGlobal _networkGlobal;
        NodeLinkContext _networkContext;

        public override void Initialize(NetworkGlobal networkGlobal, NetworkContext networkContext)
        {
            _networkGlobal = networkGlobal;
            _networkContext = (NodeLinkContext)networkContext;

            _manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
        }

        public override void ApplyTransformation()
        {
            foreach (var (nodeID, node) in _networkContext.Nodes)
            {
                if (node.Dirty)
                {
                    var selected = _manager.IsNodeSelected(nodeID, _networkContext.SubnetworkID);

                    foreach (var link in _networkGlobal.NodeLinkMatrixUndir[nodeID])
                    {
                        if (_networkContext.Links.ContainsKey(link.ID))
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