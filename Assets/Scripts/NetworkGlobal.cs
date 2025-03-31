/*
* NetworkGlobal contains all information about the network needed for the network representation(s) in the scene.
* To avoid bloating this class, anything specific to a network representation should go in a NetworkContext.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VidiGraph
{
    public class NetworkGlobal : MonoBehaviour
    {
        int _rootNodeID;

        // TODO change to maps... KISS
        NodeCollection _nodes = new NodeCollection();
        public NodeCollection Nodes { get { return _nodes; } }
        List<Link> _links = new List<Link>();
        public List<Link> Links { get { return _links; } }
        Dictionary<int, Community> _communities = new Dictionary<int, Community>();
        public Dictionary<int, Community> Communities { get { return _communities; } }

        List<Link> _treeLinks = new List<Link>();
        public List<Link> TreeLinks { get { return _treeLinks; } }
        List<int> _realNodeIDs = new List<int>();
        List<int> _virtualNodeIDs = new List<int>();
        Dictionary<int, List<Link>> _nodeLinkMatrix
             = new Dictionary<int, List<Link>>();

        public Dictionary<int, List<Link>> NodeLinkMatrix { get { return _nodeLinkMatrix; } }

        public Node HoveredNode = null;
        public Community HoveredCommunity = null;
        HashSet<int> _selectedNodes = new HashSet<int>();
        public HashSet<int> SelectedNodes { get { return _selectedNodes; } }

        public HashSet<int> SelectedCommunities { get { return GetCompleteCommunities(SelectedNodes); } }

        void Reset()
        {
            _nodes.Clear();
            _links.Clear();
            _treeLinks.Clear();
            _realNodeIDs.Clear();
            _virtualNodeIDs.Clear();
            _nodeLinkMatrix.Clear();
            _communities.Clear();
            HighContrastColors.ResetRandomColor();
        }

        public void InitNetwork()
        {
            Reset();

            var fileLoader = GetComponent<NetworkFilesLoader>();
            var fileNodes = fileLoader.SphericalLayout.nodes;
            var fileLinks = fileLoader.SphericalLayout.links;

            _rootNodeID = fileLoader.SphericalLayout.rootIdx;

            // 1. Calculate the number of real Node and virtual node
            // 2. Build Index
            for (var i = 0; i < fileNodes.Length; i++)
            {
                var node = LoadFileUtils.NodeFromFileData(fileNodes[i]);

                // Initialize the node-link matrix
                _nodeLinkMatrix.Add(node.ID, new List<Link>());

                // Group the nodes by virtual or not
                if (node.IsVirtualNode) _virtualNodeIDs.Add(node.ID);
                else _realNodeIDs.Add(node.ID);

                node.Dirty = true;

                _nodes.Add(node);
            }

            // Got the order list of all the ancestor of every node (sy: I guess to walk up the ancestor nodes?)
            // TODO turn into function
            foreach (var node in _nodes)
            {
                if (node.ID == _rootNodeID)
                {
                    continue;
                }

                var ancNode = node.AncID;
                node.AncIDsOrderList.Add(ancNode);

                while (ancNode != _rootNodeID)
                {
                    ancNode = _nodes[ancNode].AncID;
                    node.AncIDsOrderList.Add(ancNode);
                }
            }

            // 1. Build Node-Link Matrix
            // 2. calculate the degree
            // 3. find spline control points
            for (var i = 0; i < fileLinks.Length; i++)
            {
                var link = LoadFileUtils.LinkFromFileData(fileLinks[i]);

                link.SourceNode = _nodes[link.SourceNodeID];
                link.TargetNode = _nodes[link.TargetNodeID];

                // Build Node-Link Matrix
                _nodeLinkMatrix[link.SourceNodeID].Add(link);
                _nodeLinkMatrix[link.TargetNodeID].Add(link);

                link.ID = i;
                link.Dirty = true;

                // calculate the degree
                _nodes[link.SourceNodeID].Degree += 0.01;
                _nodes[link.TargetNodeID].Degree += 0.01;

                _links.Add(link);

                InitPathInTree(link);
            }

            BuildTreeLinks(_rootNodeID);
            TagCommunities();
            CommunitiesInit();
        }

        public List<Community> HighlightedCommunities()
        {
            return (from comms in _communities.Values where comms.Focus == true select comms).ToList();
        }

        public void SetSelectedNodes(List<int> nodeIDs, bool selected)
        {
            if (selected)
            {
                foreach (var nodeID in nodeIDs)
                {
                    if (!_selectedNodes.Contains(nodeID))
                    {
                        _selectedNodes.Add(nodeID);

                        Nodes[nodeID].Selected = true;
                        Nodes[nodeID].Dirty = true;
                    }
                }
            }
            else
            {
                foreach (var nodeID in nodeIDs)
                {
                    if (_selectedNodes.Contains(nodeID))
                    {
                        _selectedNodes.Remove(nodeID);

                        Nodes[nodeID].Selected = false;
                        Nodes[nodeID].Dirty = true;
                    }
                }
            }

            UpdateSelectedCommunities();
        }

        public void ToggleSelectedNodes(List<int> nodeIDs)
        {
            foreach (var nodeID in nodeIDs)
            {
                if (_selectedNodes.Contains(nodeID))
                {
                    _selectedNodes.Remove(nodeID);

                    Nodes[nodeID].Selected = false;
                    Nodes[nodeID].Dirty = true;
                }
                else
                {
                    _selectedNodes.Add(nodeID);

                    Nodes[nodeID].Selected = true;
                    Nodes[nodeID].Dirty = true;
                }
            }

            UpdateSelectedCommunities();
        }

        public void SetSelectedCommunities(List<int> commIDs, bool selected)
        {
            if (selected)
            {
                foreach (var commID in commIDs)
                {
                    var globalComm = Communities[commID];
                    if (!globalComm.Selected)
                    {
                        globalComm.Selected = true;
                        _selectedNodes.UnionWith(globalComm.Nodes.Select(n => n.ID));
                    }
                    globalComm.Dirty = true;
                }
            }
            else
            {
                foreach (var commID in commIDs)
                {
                    var globalComm = Communities[commID];
                    if (globalComm.Selected)
                    {
                        globalComm.Selected = false;
                        _selectedNodes.ExceptWith(globalComm.Nodes.Select(n => n.ID));
                    }
                    globalComm.Dirty = true;
                }
            }
        }

        public void ToggleSelectedCommunities(List<int> commIDs)
        {
            var oldSelectedNodes = new HashSet<int>(_selectedNodes);

            foreach (var commID in commIDs)
            {
                var globalComm = Communities[commID];
                if (globalComm.Selected)
                {
                    globalComm.Selected = false;
                    _selectedNodes.ExceptWith(globalComm.Nodes.Select(n => n.ID));
                }
                else
                {
                    globalComm.Selected = true;
                    _selectedNodes.UnionWith(globalComm.Nodes.Select(n => n.ID));
                }
                globalComm.Dirty = true;
            }

            var newSelected = _selectedNodes.Except(oldSelectedNodes);
            var removed = oldSelectedNodes.Except(_selectedNodes);

            foreach (var newNodeID in newSelected)
            {
                Nodes[newNodeID].Selected = true;
                Nodes[newNodeID].Dirty = true;
            }

            foreach (var oldNodeID in removed)
            {
                Nodes[oldNodeID].Selected = false;
                Nodes[oldNodeID].Dirty = true;
            }
        }

        // clears both nodes and communities
        public void ClearSelectedItems()
        {
            foreach (var nodeID in _selectedNodes)
            {
                Nodes[nodeID].Selected = false;
                Nodes[nodeID].Dirty = true;
            }

            _selectedNodes.Clear();

            foreach (var (_, comm) in Communities)
            {
                comm.Selected = false;
                // just mark all of them dirty, there usually isn't many communities
                comm.Dirty = true;
            }
        }

        void CommunitiesInit()
        {
            FindLinks4Communities();
            FindNodesInCommunities();
        }
        void FindLinks4Communities()
        {
            foreach (var link in _links)
            {
                var sourceCommunity = _nodes[link.SourceNodeID].CommunityID;
                var targetCommunity = _nodes[link.TargetNodeID].CommunityID;

                if (sourceCommunity == -1 || targetCommunity == -1) continue;

                // for the link inside a community
                if (sourceCommunity == targetCommunity)
                {
                    _communities[sourceCommunity].InnerLinks.Add(link);
                }
                else
                {
                    _communities[sourceCommunity].OuterLinks.Add(link);
                    _communities[targetCommunity].OuterLinks.Add(link);
                }
            }

            foreach (var entry in _communities)
            {
                var community = entry.Value;
                CombineLinks(community);
            }
        }

        void FindNodesInCommunities()
        {
            foreach (var node in _nodes)
            {
                if (!node.IsVirtualNode)
                {
                    _communities[node.CommunityID].Nodes.Add(node);
                }
            }
        }

        /*
         * combine links with the same source community and the same target community
         */
        void CombineLinks(Community community)
        {
            foreach (var link in community.OuterLinks)
            {
                var sourceCommunity = _nodes[link.SourceNodeID].CommunityID;
                var targetCommunity = _nodes[link.TargetNodeID].CommunityID;

                var otherCommunity = community.ID == sourceCommunity ? targetCommunity : sourceCommunity;

                if (!community.AggregateLinks.ContainsKey(otherCommunity))
                {
                    community.AggregateLinks.Add(otherCommunity, 0);
                }

                community.AggregateLinks[otherCommunity]++;
            }
        }

        // label communities
        void TagCommunities()
        {
            // TODO change from DFS to BFS for simplicity
            var nodeStack = new Stack<int>();
            nodeStack.Push(_rootNodeID);

            int communityID = 0;
            while (nodeStack.Count != 0)
            {
                var curNode = _nodes[nodeStack.Pop()];

                if (IsCommunity(curNode))
                {
                    TagCommunity(curNode, communityID++);

                    foreach (var childID in curNode.ChildIDs)
                    {
                        if (_nodes[childID].IsVirtualNode)
                            nodeStack.Push(childID);
                    }
                }
                else
                {
                    foreach (var childID in curNode.ChildIDs)
                    {
                        nodeStack.Push(childID);
                    }
                }

            }

        }

        bool IsCommunity(Node node)
        {
            // node is root of a community if any of its children are non-virtual
            return node.ChildIDs.Count() != 0 && node.ChildIDs.Any(c => !_nodes[c].IsVirtualNode);
        }

        void TagCommunity(Node node, int clusterIdx)
        {
            Debug.Assert(node.IsVirtualNode);

            var community = new Community
            {
                RootNodeID = node.ID,
                ID = clusterIdx,
                Color = HighContrastColors.GenerateRandomColor(),
                Dirty = true
            };

            _communities.Add(clusterIdx, community);

            foreach (var childIdx in node.ChildIDs)
            {
                if (!_nodes[childIdx].IsVirtualNode)
                    _nodes[childIdx].CommunityID = clusterIdx;
            }
        }

        // For every link, we find the shortest path in the hierarchy
        void InitPathInTree(Link link)
        {
            var sourceNode = link.SourceNode;
            var targetNode = link.TargetNode;

            var sourceAncestors = sourceNode.AncIDsOrderList;
            var targetAncestors = targetNode.AncIDsOrderList;

            var commonAncestors = sourceAncestors.Intersect(targetAncestors);
            var commonAncIdx = commonAncestors.FirstOrDefault();

            var commonAncNode = _nodes[commonAncIdx];

            // add source node
            link.PathInTree.Add(sourceNode);

            // add source node ancestors up to (not including) common ancestor
            for (int i = 0; i < sourceAncestors.Count; i++)
            {
                if (sourceAncestors[i] == commonAncIdx) break;
                link.PathInTree.Add(_nodes[sourceAncestors[i]]);
            }

            // add common ancestor
            link.PathInTree.Add(commonAncNode);

            // add target node ancestors from (not including) common ancestor
            for (int i = targetAncestors.Count - 1, found = 0; i >= 0; i--)
            {
                if (targetAncestors[i] == commonAncIdx)
                {
                    found = 1;
                    continue;
                }
                if (found == 1)
                {
                    link.PathInTree.Add(_nodes[targetAncestors[i]]);
                }
            }

            // add target node
            link.PathInTree.Add(targetNode);
        }

        void BuildTreeLinks(int nodeID)
        {
            if (!_nodes[nodeID].IsVirtualNode) return;

            foreach (var childID in _nodes[nodeID].ChildIDs)
            {
                // just make link idx the negative count to ensure it's unique from real links
                int treeLinkID = -_treeLinks.Count;

                _treeLinks.Add(new Link(_nodes[nodeID], _nodes[childID], treeLinkID));

                BuildTreeLinks(childID);
            }
        }

        // return a list of communityIDs of communities where all of its nodes are selected.
        // this helps us select a community by also selecting all of its nodes.
        // hashsets ensure no duplicates.
        HashSet<int> GetCompleteCommunities(HashSet<int> nodeIDs)
        {
            Dictionary<int, int> curCommSize = new Dictionary<int, int>();

            foreach (var nodeID in nodeIDs)
            {
                int commID = Nodes[nodeID].CommunityID;

                if (!curCommSize.ContainsKey(commID))
                    curCommSize[commID] = 0;

                curCommSize[commID] += 1;
            }

            HashSet<int> completeComm = new HashSet<int>();

            foreach (var (commID, size) in curCommSize)
            {
                if (size == Communities[commID].Nodes.Count)
                    completeComm.Add(commID);
            }

            return completeComm;
        }

        void UpdateSelectedCommunities()
        {
            // we'll just calculate complete communities every time since there usually isn't a lot of communities.
            var completeComms = GetCompleteCommunities(_selectedNodes);
            var incompleteComms = Communities.Keys.ToHashSet().Except(completeComms);

            foreach (var comm in completeComms)
            {
                Communities[comm].Selected = true;
                Communities[comm].Dirty = true;
            }

            foreach (var comm in incompleteComms)
            {
                Communities[comm].Selected = false;
                Communities[comm].Dirty = true;
            }
        }
    }
}