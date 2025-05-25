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
        public Transform HighlightPosition;
        public MLEncodingTransformer MLEncoder;

        NetworkGlobal _networkGlobal;
        MultiLayoutContext _networkContext;

        TransformInfo _highlightTransform;

        public override void Initialize(NetworkGlobal networkGlobal, NetworkContext networkContext)
        {
            _networkContext = (MultiLayoutContext)networkContext;

            var manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _networkGlobal = manager.NetworkGlobal;
            _highlightTransform = new TransformInfo(HighlightPosition);
        }

        public override void ApplyTransformation()
        {
            foreach (var nodeID in _networkContext.Nodes.Keys)
            {
                var node = _networkGlobal.Nodes[nodeID];

                if (node.Dirty)
                {
                    var selected = node.Selected;

                    // _networkContext.Nodes[nodeID].Color = selected
                    //     ? _networkContext.ContextSettings.NodeHighlightColor
                    //     : _networkContext.GetNodeColor(node);

                    _networkContext.Nodes[nodeID].Dirty = true;

                    foreach (var link in _networkGlobal.NodeLinkMatrix[nodeID])
                    {
                        // _networkContext.Links[link.ID].ColorStart = selected
                        //     ? _networkContext.ContextSettings.LinkHighlightColor
                        //     : _networkContext.GetLinkColorStart(link);
                        // _networkContext.Links[link.ID].ColorEnd = selected
                        //     ? _networkContext.ContextSettings.LinkHighlightColor
                        //     : _networkContext.GetLinkColorEnd(link);
                        // _networkContext.Links[link.ID].Alpha = selected
                        //     ? _networkContext.ContextSettings.LinkHighlightColor.a
                        //     : _networkContext.GetLinkAlpha(link);
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