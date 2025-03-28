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

        public Func<Node, float> GetNodeSize = null;
        public Func<Node, Color> GetNodeColor = null;
        public Func<Link, float> GetLinkWidth = null;
        public Func<Link, Color> GetLinkColorStart = null;
        public Func<Link, Color> GetLinkColorEnd = null;
        public Func<Link, float> GetLinkAlpha = null;

        public override void Initialize(NetworkGlobal networkGlobal, NetworkContext networkContext)
        {
            _networkContext = (MultiLayoutContext)networkContext;

            var manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _networkGlobal = manager.NetworkGlobal;
            _mlEncodingTransform = new TransformInfo(MlEncodingPosition);

            SetDefaultEncodings();
        }

        public override void ApplyTransformation()
        {
            foreach (var nodeID in _networkContext.Nodes.Keys)
            {
                Node globalNode = _networkGlobal.Nodes[nodeID];
                MultiLayoutContext.Node contextNode = _networkContext.Nodes[nodeID];

                contextNode.Size = GetNodeSize(globalNode);
                contextNode.Color = GetNodeColor(globalNode);
            }

            foreach (var linkID in _networkContext.Links.Keys)
            {
                Link globalLink = _networkGlobal.Links[linkID];
                MultiLayoutContext.Link contextLink = _networkContext.Links[linkID];

                contextLink.Width = GetLinkWidth(globalLink) * _networkContext.ContextSettings.LinkWidth;
                contextLink.ColorStart = GetLinkColorStart(globalLink);
                contextLink.ColorEnd = GetLinkColorEnd(globalLink);
                contextLink.Alpha = GetLinkAlpha(globalLink) * _networkContext.ContextSettings.LinkNormalAlphaFactor;
            }
        }

        public override TransformInterpolator GetInterpolator()
        {
            return new MLEncodingInterpolator();
        }

        void SetDefaultEncodings()
        {
            GetNodeSize = _ => 1f;
            GetNodeColor = node => GetColor(_networkGlobal.Nodes[node.ID].CommunityID);
            GetLinkWidth = _ => 1f;
            GetLinkColorStart = link => GetColor(link.SourceNode.CommunityID);
            GetLinkColorEnd = link => GetColor(link.TargetNode.CommunityID);
            GetLinkAlpha = _ => 1f;
        }

        Color GetColor(int commID)
        {
            return commID == -1 ? Color.black : _networkGlobal.Communities[commID].Color;
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