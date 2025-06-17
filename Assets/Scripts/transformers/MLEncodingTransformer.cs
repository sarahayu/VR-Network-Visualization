using System;
using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Assertions;

namespace VidiGraph
{
    public class MLEncodingTransformer : NetworkContextTransformer
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
                var nodeContext = _networkContext.Nodes[node.ID];

                nodeContext.Size = _networkContext.GetNodeSize(node);
                nodeContext.Color = _networkContext.GetNodeColor(node);

                nodeContext.Dirty = true;
            }

            foreach (var link in _networkGlobal.Links)
            {
                var linkContext = _networkContext.Links[link.ID];

                linkContext.Width = _networkContext.GetLinkWidth(link);
                linkContext.ColorStart = _networkContext.GetLinkColorStart(link);
                linkContext.ColorEnd = _networkContext.GetLinkColorEnd(link);
                linkContext.Alpha = _networkContext.GetLinkAlpha(link);

                linkContext.Dirty = true;
            }
        }

        public override TransformInterpolator GetInterpolator()
        {
            return new MLEncodingInterpolator();
        }
    }

    public class MLEncodingInterpolator : TransformInterpolator
    {
        public override void Interpolate(float t)
        {
            // leave empty, encodings shouldn't be interpolatable... for now
        }
    }

}