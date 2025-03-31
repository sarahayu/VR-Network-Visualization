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
        public Transform MlEncodingPosition;

        NetworkGlobal _networkGlobal;
        MultiLayoutContext _networkContext;

        TransformInfo _mlEncodingTransform;

        public override void Initialize(NetworkGlobal networkGlobal, NetworkContext networkContext)
        {
            _networkContext = (MultiLayoutContext)networkContext;

            var manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _networkGlobal = manager.NetworkGlobal;
            _mlEncodingTransform = new TransformInfo(MlEncodingPosition);
        }

        public override void ApplyTransformation()
        {
            foreach (var nodeID in _networkContext.Nodes.Keys)
            {
                Node globalNode = _networkGlobal.Nodes[nodeID];
                MultiLayoutContext.Node contextNode = _networkContext.Nodes[nodeID];

                contextNode.Size = _networkContext.GetNodeSize(globalNode);
                contextNode.Color = _networkContext.GetNodeColor(globalNode);
            }

            foreach (var linkID in _networkContext.Links.Keys)
            {
                Link globalLink = _networkGlobal.Links[linkID];
                MultiLayoutContext.Link contextLink = _networkContext.Links[linkID];

                contextLink.Width = _networkContext.GetLinkWidth(globalLink);
                contextLink.ColorStart = _networkContext.GetLinkColorStart(globalLink);
                contextLink.ColorEnd = _networkContext.GetLinkColorEnd(globalLink);
                contextLink.Alpha = _networkContext.GetLinkAlpha(globalLink);
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