/*
* NetworkContext3D contains network information specific to MultiLayoutNetwork.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
            public float Size { get; set; } = 1f;
            public Vector3 Position { get; set; } = Vector3.zero;
            public Color Color { get; set; }

            // detect if node needs to be rerendered
            public bool Dirty { get; set; } = false;
        }

        public class Link
        {
            public float BundlingStrength { get; set; } = 0f;
            public float Width { get; set; } = 1f;
            public Color ColorStart { get; set; }
            public Color ColorEnd { get; set; }
            public float Alpha { get; set; } = 1f;

            public bool BundleStart { get; set; } = false;
            public bool BundleEnd { get; set; } = false;

            // detect if link needs to be rerendered
            public bool Dirty { get; set; } = false;
        }

        public class Community
        {

            public double Mass { get; set; }
            public Vector3 MassCenter { get; set; }
            public double Size { get; set; }

            public CommunityState State { get; set; } = CommunityState.None;

            // detect if link needs to be rerendered
            public bool Dirty { get; set; } = false;
        }

        public enum CommunityState
        {
            None,
            Cluster,
            Floor,
            Hairball,
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
        public Func<VidiGraph.Link, float> GetLinkBundlingStrength = null;
        public Func<VidiGraph.Link, Color> GetLinkColorStart = null;
        public Func<VidiGraph.Link, Color> GetLinkColorEnd = null;
        public Func<VidiGraph.Link, bool> GetLinkBundleStart = null;
        public Func<VidiGraph.Link, bool> GetLinkBundleEnd = null;
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

        public void SetFromGlobal(NetworkGlobal networkGlobal, NetworkFileData networkFile, IEnumerable<int> nodeIDs)
        {
            Nodes.Clear();
            Links.Clear();
            Communities.Clear();

            foreach (var nodeID in nodeIDs)
            {
                Nodes[nodeID] = new Node();
            }

            foreach (var link in networkGlobal.Links)
            {
                if (nodeIDs.Contains(link.SourceNodeID) || nodeIDs.Contains(link.TargetNodeID))
                {
                    Links[link.ID] = new Link();
                }
            }

            foreach (var community in networkGlobal.Communities.Values)
            {
                if (community.Nodes.Select(n => n.ID).ToHashSet().Intersect(nodeIDs).Count() != 0)
                {
                    Communities[community.ID] = new Community();
                }
            }

            SetDefaultEncodings(networkGlobal, networkFile);
        }

        public void RecomputeGeometricProps(NetworkGlobal networkGlobal)
        {
            foreach (var community in networkGlobal.Communities.Values)
            {
                var contextCommunity = Communities[community.ID];

                CommunityMathUtils.ComputeMassProperties(community.Nodes, Nodes,
                    out var mass, out var massCenter);

                contextCommunity.Mass = mass;
                contextCommunity.MassCenter = massCenter;

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

            // var linkColor = new Color(1f, 1f, 1f, 0.2f);

            // // Friend data
            // GetNodeSize = node => (float)Math.Log10(node.Degree * 100) * 2.5f + 0.5f;
            // GetNodeColor = node =>
            // {
            //     var fileNode = networkFile.nodes[node.IdxProcessed];

            //     var drinker = fileNode.props.drinker;
            //     var smoker = fileNode.props.smoker;

            //     if (drinker == true && smoker == true) return Color.magenta;
            //     if (drinker == true) return Color.red;
            //     if (smoker == true) return Color.blue;

            //     if (drinker == null || smoker == null) return Color.gray;

            //     return Color.white;
            // };

            // GetNodeColor = node =>
            // {
            //     var fileNode = networkFile.nodes[node.IdxProcessed];
            //     return fileNode.props.gpa == null ? Color.gray : Color.Lerp(Color.white, Color.green, Mathf.Pow((float)fileNode.props.gpa / 4f, 2));
            // };

            // GetLinkWidth = _ => ContextSettings.LinkWidth;
            // GetLinkColorStart = _ => linkColor;
            // GetLinkColorEnd = _ => linkColor;
            // GetLinkAlpha = _ => ContextSettings.LinkNormalAlphaFactor;


            GetNodeSize = _ => ContextSettings.NodeScale;
            GetNodeColor = _ => Color.gray;
            GetLinkWidth = _ => ContextSettings.LinkWidth;
            GetLinkBundlingStrength = _ => ContextSettings.EdgeBundlingStrength;
            GetLinkColorStart = _ => Color.white;
            GetLinkColorEnd = _ => Color.white;
            GetLinkBundleStart = _ => true;
            GetLinkBundleEnd = _ => true;
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
                case "hairball":
                    return CommunityState.Hairball;
                default:
                    return CommunityState.Other;
            }
        }
    }
}
