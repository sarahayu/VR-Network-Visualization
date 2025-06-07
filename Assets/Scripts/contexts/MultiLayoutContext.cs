/*
* NetworkContext3D contains network information specific to MultiLayoutNetwork.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace VidiGraph
{
    public class MultiLayoutContext : NetworkContext
    {
        public class Settings
        {
            public float NodeScale = 1f;
            public float LinkWidth = 0.0025f;
            public float EdgeBundlingStrength = 0.8f;

            public Color NodeSelectColor;
            public Color CommSelectColor;
            public Color LinkSelectColor;
            public Color NodeHoverColor;
            public Color CommHoverColor;
            public Color LinkHoverColor;

            public float LinkMinimumAlpha = 0.01f;
            public float LinkNormalAlphaFactor = 0.05f;
            public float LinkContextAlphaFactor = 0.5f;
            public float LinkContext2FocusAlphaFactor = 0.8f;
        }

        public class Node
        {
            public float Size = 1f;
            public Vector3 Position = Vector3.zero;
            public Color Color;

            // detect if node needs to be rerendered
            public bool Dirty = false;
        }

        public class Link
        {
            public float BundlingStrength = 0f;
            public float Width = 1f;
            public Color ColorStart;
            public Color ColorEnd;
            public float Alpha = 1f;

            public bool BundleStart = false;
            public bool BundleEnd = false;

            // detect if link needs to be rerendered
            public bool Dirty = false;
        }

        public class Community
        {

            public double Mass;
            public Vector3 MassCenter;
            public double Size;

            public CommunityState State = CommunityState.None;

            // detect if link needs to be rerendered
            public bool Dirty = false;
        }

        public enum CommunityState
        {
            None,
            Cluster,
            Floor,
            Other,
            NumStates,
        }

        public Settings ContextSettings = new Settings();

        public Dictionary<int, Node> Nodes = new Dictionary<int, Node>();
        public Dictionary<int, Link> Links = new Dictionary<int, Link>();
        public Dictionary<int, Community> Communities = new Dictionary<int, Community>();

        public Func<VidiGraph.Node, float> GetNodeSize = null;
        public Func<VidiGraph.Node, Color> GetNodeColor = null;
        public Func<VidiGraph.Link, float> GetLinkWidth = null;
        public Func<VidiGraph.Link, Color> GetLinkColorStart = null;
        public Func<VidiGraph.Link, Color> GetLinkColorEnd = null;
        public Func<VidiGraph.Link, float> GetLinkAlpha = null;

        public MultiLayoutContext()
        {
            // expose constructor
        }

        public void SetFromGlobal(NetworkGlobal networkGlobal, NetworkFileData networkFile)
        {
            Nodes.Clear();
            Links.Clear();
            Communities.Clear();

            foreach (var node in networkGlobal.Nodes)
            {
                Nodes[node.ID] = new Node();
            }

            foreach (var link in networkGlobal.Links)
            {
                Links[link.ID] = new Link();
            }

            foreach (var community in networkGlobal.Communities.Values)
            {
                Communities[community.ID] = new Community();
            }

            SetDefaultEncodings(networkGlobal, networkFile);
        }

        public void RecomputeGeometricProps(NetworkGlobal networkGlobal)
        {
            foreach (var community in networkGlobal.Communities.Values)
            {
                var contextCommunity = Communities[community.ID];

                CommunityMathUtils.ComputeMassProperties(community.Nodes, Nodes,
                    out contextCommunity.Mass, out contextCommunity.MassCenter);

                contextCommunity.Size = CommunityMathUtils.ComputeSize(community.Nodes, Nodes,
                    contextCommunity.MassCenter);

                contextCommunity.Dirty = true;
            }
        }

        void SetDefaultEncodings(NetworkGlobal networkGlobal, NetworkFileData networkFile)
        {
            // // Bully data
            // GetNodeSize = _ => 1f;
            // GetNodeColor = node => GetColor(networkGlobal.Nodes[node.ID].CommunityID, networkGlobal.Communities);
            // GetLinkWidth = _ => ContextSettings.LinkWidth;
            // GetLinkColorStart = link => GetColor(link.SourceNode.CommunityID, networkGlobal.Communities);
            // GetLinkColorEnd = link => GetColor(link.TargetNode.CommunityID, networkGlobal.Communities);
            // GetLinkAlpha = _ => ContextSettings.LinkNormalAlphaFactor;

            var linkColor = new Color(1f, 1f, 1f, 0.2f);

            // Friend data
            GetNodeSize = node => (float)Math.Log10(node.Degree * 100) * 3;
            GetNodeColor = node =>
            {
                var fileNode = networkFile.nodes[networkFile.idToIdx[node.ID]];

                var drinker = fileNode.props.drinker;
                var smoker = fileNode.props.smoker;

                if (drinker == true && smoker == true) return Color.magenta;
                if (drinker == true) return Color.red;
                if (smoker == true) return Color.blue;

                if (drinker == null || smoker == null) return Color.gray;

                return Color.white;
            };
            GetLinkWidth = _ => ContextSettings.LinkWidth;
            GetLinkColorStart = _ => linkColor;
            GetLinkColorEnd = _ => linkColor;
            GetLinkAlpha = _ => ContextSettings.LinkNormalAlphaFactor;
        }

        Color GetColor(int commID, Dictionary<int, VidiGraph.Community> comms)
        {
            return commID == -1 ? Color.black : comms[commID].Color;
        }

        static public CommunityState StrToState(string state)
        {
            switch (state)
            {
                case "spherical":
                    return CommunityState.None;
                case "cluster":
                    return CommunityState.Cluster;
                case "floor":
                    return CommunityState.Floor;
                default:
                    return CommunityState.Other;
            }
        }
    }
}
