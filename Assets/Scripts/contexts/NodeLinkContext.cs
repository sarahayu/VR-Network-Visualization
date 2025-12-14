/*
* MultiLayoutContext contains network information specific to MultiLayoutNetwork and BasicSubnetwork.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VidiGraph
{
    public class NodeLinkContext : NetworkContext
    {
        [Serializable]
        public class Settings
        {
            public HighlightMode SelectionHighlightMode = HighlightMode.BlueTint;

            public float NodeScale = 1f;
            public float LinkWidth = 0.0025f;
            public float EdgeBundlingStrength = 0.8f;

            public Color NodeDefaultColor;
            public Color NodeSelectColor;
            public Color CommSelectColor;
            public Color LinkDefaultColor;
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
            public Node()
            {
                GUID = Guid.NewGuid().ToString();
            }

            public string GUID { get; set; }       // unique for all Context.Nodes, since duplicate nodes with same IDs can exist on multiple subnetworks
            public float Size { get; set; } = 1f;
            public Vector3 Position { get; set; } = Vector3.zero;
            public Color Color { get; set; }
            public int CommunityID { get; set; }
            public bool Moveable { get; set; } = true;
            // TODO restrict modification access
            public bool Selected { get; set; }          // DONT MODIFY DIRECTLY, use SetSelectedNodes/Communities

            // detect if node needs to be rerendered
            public bool Dirty { get; set; } = false;
        }

        public class Link
        {
            public Link()
            {
                GUID = Guid.NewGuid().ToString();
            }

            public string GUID { get; set; }       // unique for all Context.Links, since duplicate links with same IDs can exist on multiple subnetworks
            public float BundlingStrength { get; set; } = 0f;
            public float Width { get; set; } = 1f;
            public Color ColorStart { get; set; }
            public Color ColorEnd { get; set; }
            public float Alpha { get; set; } = 1f;

            public bool BundleStart { get; set; } = false;
            public bool BundleEnd { get; set; } = false;
            // TODO restrict modification access
            public bool Selected { get; set; }          // DONT MODIFY DIRECTLY, use SetSelectedNodes/Communities

            // detect if link needs to be rerendered
            public bool Dirty { get; set; } = false;
        }

        public class Community
        {
            public Community()
            {
                GUID = Guid.NewGuid().ToString();
            }

            public string GUID { get; set; }       // unique for all Context.Communities, since duplicate communities with same IDs can exist on multiple subnetworks
            // we want to store ID because communities in context may be different from global
            public int ID { get; set; }
            public double Mass { get; set; }
            public Vector3 MassCenter { get; set; }
            public double Size { get; set; }

            public CommunityState State { get; set; } = CommunityState.None;
            public Mesh Mesh { get; set; } = new();
            public IEnumerable<int> Nodes { get; set; }
            public bool Moveable { get; set; } = true;

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

        public enum HighlightMode
        {
            BlueTint,
            Saturate,
        }

        public Settings ContextSettings = new Settings();

        public Dictionary<int, Node> Nodes = new Dictionary<int, Node>();
        public Dictionary<int, Link> Links = new Dictionary<int, Link>();
        public Dictionary<int, Community> Communities = new Dictionary<int, Community>();

        public Dictionary<string, int> NodeGUIDToID = new();
        public Dictionary<string, int> LinkGUIDToID = new();
        public Dictionary<string, int> CommunityGUIDToID = new();

        public Dictionary<int, List<int>> NodeLinkMatrixDir = new();
        public Dictionary<int, List<int>> NodeLinkMatrixUndir = new();

        public Func<VidiGraph.Node, float> GetNodeSize = null;
        public Func<VidiGraph.Node, Color> GetNodeColor = null;
        public Func<VidiGraph.Link, float> GetLinkWidth = null;
        public Func<VidiGraph.Link, float> GetLinkBundlingStrength = null;
        public Func<VidiGraph.Link, Color> GetLinkColorStart = null;
        public Func<VidiGraph.Link, Color> GetLinkColorEnd = null;
        public Func<VidiGraph.Link, bool> GetLinkBundleStart = null;
        public Func<VidiGraph.Link, bool> GetLinkBundleEnd = null;
        public Func<VidiGraph.Link, float> GetLinkAlpha = null;

        public int SubnetworkID { get { return _subnetworkID; } }

        public HashSet<int> SelectedNodes { get { return _selectedNodes; } }
        public HashSet<int> SelectedLinks { get { return _selectedLinks; } }
        public HashSet<int> SelectedCommunities { get { return _selectedComms; } }

        int _subnetworkID;      // 0 means main multilayoutnetwork, 1 and up means subnetwork

        HashSet<int> _selectedNodes = new HashSet<int>();
        HashSet<int> _selectedLinks = new HashSet<int>();
        HashSet<int> _selectedComms = new HashSet<int>();
        public Vector3 MassCenter { get; set; }
        public Mesh Mesh { get; set; } = null;
        public bool UseShell { get; private set; } = true;
        // TODO restrict modification access
        public bool Selected { get; set; }          // DONT MODIFY DIRECTLY, use SetSelectedNodes/Communities

        public NodeLinkContext(int subnetworkID, bool useShell = true)
        {
            // expose constructor

            _subnetworkID = subnetworkID;
            UseShell = useShell;
        }

        public void SetFromGlobal(NetworkGlobal networkGlobal, NetworkFileData networkFile)
        {
            Nodes.Clear();
            Links.Clear();
            Communities.Clear();

            foreach (var node in networkGlobal.Nodes)
            {
                Nodes[node.ID] = new Node
                {
                    CommunityID = node.CommunityID,
                    Moveable = false
                };

                NodeGUIDToID[Nodes[node.ID].GUID] = node.ID;
            }

            foreach (var link in networkGlobal.Links.Values)
            {
                Links[link.ID] = new Link();

                LinkGUIDToID[Links[link.ID].GUID] = link.ID;
            }

            foreach (var community in networkGlobal.Communities.Values)
            {
                Communities[community.ID] = new Community()
                {
                    ID = community.ID,
                    Nodes = community.Nodes.Select(n => n.ID),
                    Mesh = IcoSphere.Create(0.1f),
                    Moveable = false
                };

                CommunityGUIDToID[Communities[community.ID].GUID] = community.ID;
            }

            var dir = networkGlobal.NodeLinkMatrixDir;
            var undir = networkGlobal.NodeLinkMatrixUndir;

            NodeLinkMatrixDir = dir.Keys
                .ToDictionary(nid => nid, nid => dir[nid].Select(l => l.ID).ToList());
            NodeLinkMatrixUndir = undir.Keys
                .ToDictionary(nid => nid, nid => undir[nid].Select(l => l.ID).ToList());

            _subnetworkID = 0;

            if (UseShell)
                Mesh = IcoSphere.Create(1f);

            SetDefaultEncodings(networkGlobal, networkFile);
        }

        public void SetFromContext(NetworkGlobal networkGlobal, NodeLinkContext otherContext, IEnumerable<int> nodeIDs)
        {
            Nodes.Clear();
            Links.Clear();
            Communities.Clear();

            foreach (var nodeID in nodeIDs)
            {
                Nodes[nodeID] = new Node()
                {
                    CommunityID = otherContext.Nodes[nodeID].CommunityID,
                    Size = otherContext.Nodes[nodeID].Size,
                    Color = otherContext.Nodes[nodeID].Color,
                    Position = otherContext.Nodes[nodeID].Position,
                    Dirty = true,
                    Moveable = true,
                };

                NodeGUIDToID[Nodes[nodeID].GUID] = nodeID;
            }

            foreach (var link in otherContext.Links.Keys.Select(lid => networkGlobal.Links[lid]))
            {
                if (!nodeIDs.Contains(link.SourceNodeID) || !nodeIDs.Contains(link.TargetNodeID)) continue;

                Links[link.ID] = new Link()
                {
                    Width = otherContext.Links[link.ID].Width,
                    BundlingStrength = otherContext.Links[link.ID].BundlingStrength,
                    ColorStart = otherContext.Links[link.ID].ColorStart,
                    ColorEnd = otherContext.Links[link.ID].ColorEnd,
                    BundleStart = otherContext.Links[link.ID].BundleStart,
                    BundleEnd = otherContext.Links[link.ID].BundleEnd,
                    Alpha = otherContext.Links[link.ID].Alpha,
                    Dirty = true,
                };

                LinkGUIDToID[Links[link.ID].GUID] = link.ID;
            }

            foreach (var community in otherContext.Communities.Values)
            {
                var intersectedNodes = community.Nodes.ToHashSet().Intersect(nodeIDs);
                if (intersectedNodes.Count() == 0) continue;

                Communities[community.ID] = new Community()
                {
                    ID = community.ID,
                    Nodes = intersectedNodes,
                    Dirty = true,
                    Mesh = IcoSphere.Create(0.1f),
                    Moveable = true,
                };

                CommunityGUIDToID[Communities[community.ID].GUID] = community.ID;
            }

            var dir = networkGlobal.NodeLinkMatrixDir;
            var undir = networkGlobal.NodeLinkMatrixUndir;

            NodeLinkMatrixDir = dir.Keys.Intersect(nodeIDs)
                .ToDictionary(nid => nid, nid => dir[nid]
                    .Select(l => l.ID)
                    .Intersect(Links.Keys)
                    .ToList());
            NodeLinkMatrixUndir = undir.Keys.Intersect(nodeIDs)
                .ToDictionary(nid => nid, nid => undir[nid]
                    .Select(l => l.ID)
                    .Intersect(Links.Keys)
                    .ToList());

            GetNodeSize = otherContext.GetNodeSize;
            GetNodeColor = otherContext.GetNodeColor;
            GetLinkWidth = otherContext.GetLinkWidth;
            GetLinkBundlingStrength = otherContext.GetLinkBundlingStrength;
            GetLinkColorStart = otherContext.GetLinkColorStart;
            GetLinkColorEnd = otherContext.GetLinkColorEnd;
            GetLinkBundleStart = otherContext.GetLinkBundleStart;
            GetLinkBundleEnd = otherContext.GetLinkBundleEnd;
            GetLinkAlpha = otherContext.GetLinkAlpha;

            ContextSettings = otherContext.ContextSettings;
        }

        // public void SetFromGlobal(NetworkGlobal networkGlobal, NetworkFileData networkFile, IEnumerable<int> nodeIDs, int subnetworkID = 0)
        // {
        //     Nodes.Clear();
        //     Links.Clear();
        //     Communities.Clear();

        //     foreach (var nodeID in nodeIDs)
        //     {
        //         Nodes[nodeID] = new Node();
        //         Nodes[nodeID].CommunityID = networkGlobal.Nodes[nodeID].CommunityID;
        //     }

        //     foreach (var link in networkGlobal.Links.Values)
        //     {
        //         if (nodeIDs.Contains(link.SourceNodeID) && nodeIDs.Contains(link.TargetNodeID))
        //         {
        //             Links[link.ID] = new Link();
        //         }
        //     }

        //     foreach (var community in networkGlobal.Communities.Values)
        //     {
        //         if (community.Nodes.Select(n => n.ID).ToHashSet().Intersect(nodeIDs).Count() != 0)
        //         {
        //             Communities[community.ID] = new Community()
        //             {
        //                 ID = community.ID
        //             };
        //         }
        //     }

        //     _subnetworkID = subnetworkID;

        //     SetDefaultEncodings(networkGlobal, networkFile);
        // }

        public void RecomputeCommProps(NetworkGlobal networkGlobal)
        {
            foreach (var (communityID, community) in Communities)
            {
                if (!community.Dirty) continue;
                var contextCommunity = Communities[communityID];
                var nodes = contextCommunity.Nodes.Select(nid => networkGlobal.Nodes[nid]);

                MultiLayoutContextUtils.ComputeProperties(nodes, Nodes,
                    out var cmass, out var cmassCenter, out var csize);

                contextCommunity.Mass = cmass;
                contextCommunity.MassCenter = cmassCenter;
                contextCommunity.Size = csize;
                contextCommunity.Mesh = MultiLayoutContextUtils.GenerateConvexHull(contextCommunity, nodes.Select(n => Nodes[n.ID]), ContextSettings.NodeScale);
            }

            if (UseShell)
            {
                MultiLayoutContextUtils.ComputeProperties(Nodes.Keys.Select(nid => networkGlobal.Nodes[nid]), Nodes,
                    out var mass, out var massCenter, out var size);
                MassCenter = massCenter;
                Mesh = MultiLayoutContextUtils.GenerateConvexHull(this, ContextSettings.NodeScale);
            }
        }

        // also selects connected links and fully selected communities
        public void SetSelectedNodes(IEnumerable<int> nodeIDs, bool isSelected)
        {
            foreach (var nodeID in nodeIDs)
            {
                if (Nodes[nodeID].Selected != isSelected)
                {
                    Nodes[nodeID].Selected = isSelected;
                    Nodes[nodeID].Dirty = true;
                }

                foreach (var linkID in NodeLinkMatrixUndir[nodeID])
                {
                    if (Links[linkID].Selected != isSelected)
                    {
                        Links[linkID].Selected = isSelected;
                        Links[linkID].Dirty = true;
                    }
                }
            }

            RecomputeSelecteds();
        }

        public void SetSelectedLinks(IEnumerable<int> linkIDs, bool isSelected)
        {
            foreach (var linkID in linkIDs)
            {
                if (Links[linkID].Selected != isSelected)
                {
                    Links[linkID].Selected = isSelected;
                    Links[linkID].Dirty = true;
                }
            }

            RecomputeSelecteds();
        }

        // also selects connected links (internal and external) and nodes inside communities
        public void SetSelectedComms(IEnumerable<int> commIDs, bool isSelected)
        {
            foreach (var commID in commIDs)
            {
                Communities[commID].Dirty = true;
            }

            SetSelectedNodes(GetNodesFromCommunities(commIDs), isSelected);
        }

        // also selects connected links and fully selected communities
        public void SetSelectedNetwork(bool isSelected)
        {
            SetSelectedComms(Communities.Keys, isSelected);

        }

        // returns nodeIDs that are now selected
        public IEnumerable<int> ToggleSelectedNodes(IEnumerable<int> nodeIDs)
        {
            var selNodes = SelectedNodes;
            var newSelNodes = nodeIDs.Except(selNodes);
            var newUnselNodes = nodeIDs.Intersect(selNodes);

            SetSelectedNodes(newSelNodes, true);
            SetSelectedNodes(newUnselNodes, false);

            return newSelNodes;
        }

        // returns linkIDs that are now selected
        public IEnumerable<int> ToggleSelectedLinks(IEnumerable<int> linkIDs)
        {
            var selLinks = SelectedLinks;
            var newSelLinks = linkIDs.Except(selLinks);
            var newUnselLinks = linkIDs.Intersect(selLinks);

            SetSelectedLinks(newSelLinks, true);
            SetSelectedLinks(newUnselLinks, false);

            return newSelLinks;
        }

        // returns commIDs that are now selected
        public IEnumerable<int> ToggleSelectedComms(IEnumerable<int> commIDs)
        {
            var selComms = SelectedCommunities;
            var newSelComms = commIDs.Except(selComms);
            var newUnselComms = commIDs.Intersect(selComms);

            SetSelectedComms(newSelComms, true);
            SetSelectedComms(newUnselComms, false);

            return newSelComms;
        }

        public bool ToggleSelectedNetwork()
        {
            var newSelected = !Selected;
            SetSelectedNetwork(newSelected);
            return newSelected;
        }

        public void ClearSelection()
        {
            // SetSelectedNodes(SelectedNodes, false);
            // SetSelectedLinks(SelectedLinks, false);
            // SetSelectedComms(SelectedCommunities, false);
            SetSelectedNetwork(false);
        }

        public void SetDefaultEncodings(NetworkGlobal networkGlobal, NetworkFileData networkFile)
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
            GetNodeColor = _ => ContextSettings.NodeDefaultColor;
            GetLinkWidth = _ => ContextSettings.LinkWidth;
            GetLinkBundlingStrength = _ => ContextSettings.EdgeBundlingStrength;
            GetLinkColorStart = _ => ContextSettings.LinkDefaultColor;
            GetLinkColorEnd = _ => ContextSettings.LinkDefaultColor;
            GetLinkBundleStart = _ => true;
            GetLinkBundleEnd = _ => true;
            GetLinkAlpha = _ => ContextSettings.LinkNormalAlphaFactor;
        }

        /*=============== start private methods ===================*/


        Color GetColor(int commID, Dictionary<int, VidiGraph.Community> comms)
        {
            return commID == -1 ? Color.black : comms[commID].Color;
        }

        void RecomputeSelecteds()
        {
            _selectedNodes = Nodes.Keys.Where(nid => Nodes[nid].Selected).ToHashSet();
            _selectedLinks = Links.Keys.Where(nid => Links[nid].Selected).ToHashSet();
            _selectedComms = Communities.Keys
                .Where(cid => Communities[cid].Nodes
                    .All(nid => Nodes[nid].Selected))
                .ToHashSet();

            Selected = Communities.Keys.Except(_selectedComms).Count() == 0;
        }

        HashSet<int> GetNodesFromCommunities(IEnumerable<int> commIDs)
        {
            var nodes = new HashSet<int>();

            foreach (var commID in commIDs) nodes.UnionWith(Communities[commID].Nodes);

            return nodes;
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
