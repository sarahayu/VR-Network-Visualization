/*
* NetworkGlobal contains all information about the network needed for the network representation(s) in the scene.
* To avoid bloating this class, anything specific to a network representation should go in a NetworkContext.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VidiGraph
{
    public class NetworkGlobal : MonoBehaviour
    {
        public NodeCollection Nodes { get { return _nodes; } }
        public List<Link> Links { get { return _links; } }
        public Dictionary<int, Community> Communities { get { return _communities; } }
        public List<Link> TreeLinks { get { return _treeLinks; } }
        public List<int> RealNodes { get { return _realNodeIDs; } }
        public List<int> VirtualNodes { get { return _virtualNodeIDs; } }
        public Dictionary<int, List<Link>> NodeLinkMatrix { get { return _nodeLinkMatrix; } }
        public Node HoveredNode = null;
        public Community HoveredCommunity = null;
        public HashSet<int> SelectedNodes { get { return _selectedNodes; } }
        public HashSet<int> SelectedCommunities { get { return _selectedComms; } }

        int _rootNodeID;
        // TODO change to maps... KISS
        NodeCollection _nodes = new NodeCollection();
        List<Link> _links = new List<Link>();
        Dictionary<int, Community> _communities = new Dictionary<int, Community>();

        List<Link> _treeLinks = new List<Link>();
        List<int> _realNodeIDs = new List<int>();
        List<int> _virtualNodeIDs = new List<int>();
        Dictionary<int, List<Link>> _nodeLinkMatrix
             = new Dictionary<int, List<Link>>();
        HashSet<int> _selectedNodes = new HashSet<int>();
        HashSet<int> _selectedComms = new HashSet<int>();

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

                node.IdxProcessed = i;

                // Initialize the node-link matrix
                _nodeLinkMatrix.Add(node.ID, new List<Link>());

                // Group the nodes by virtual or not
                if (node.IsVirtualNode) _virtualNodeIDs.Add(node.ID);
                else _realNodeIDs.Add(node.ID);

                node.Dirty = true;

                _nodes.Add(node);
            }

            // Got the order list of all the ancestor of every node (sy: I guess to walk up the ancestor nodes?)
            foreach (var node in _nodes) InitAncNodes(node);

            // 1. Build Node-Link Matrix
            // 2. calculate the degree
            // 3. find spline control points
            for (var i = 0; i < fileLinks.Length; i++)
            {
                var link = LoadFileUtils.LinkFromFileData(fileLinks[i]);

                if (_nodes[link.SourceNodeID].IsVirtualNode || _nodes[link.TargetNodeID].IsVirtualNode) continue;

                link.SourceNode = _nodes[link.SourceNodeID];
                link.TargetNode = _nodes[link.TargetNodeID];
                link.IdxProcessed = i;

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
            CreateCommunities();
            ComputeCommunityElems();
        }

        void InitAncNodes(Node node)
        {
            if (node.ID == _rootNodeID) return;

            var ancNode = node.AncID;
            node.AncIDsOrderList.Add(ancNode);

            while (ancNode != _rootNodeID)
            {
                ancNode = _nodes[ancNode].AncID;
                node.AncIDsOrderList.Add(ancNode);
            }
        }

        public void SetSelectedNodes(IEnumerable<int> nodeIDs, bool selected)
        {
            if (selected)
            {
                foreach (var nodeID in nodeIDs)
                {
                    if (Nodes[nodeID].IsVirtualNode) continue;
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
                    if (Nodes[nodeID].IsVirtualNode) continue;
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

        public void ToggleSelectedNodes(IEnumerable<int> nodeIDs)
        {
            foreach (var nodeID in nodeIDs)
            {
                if (Nodes[nodeID].IsVirtualNode) continue;

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

        public void SetSelectedCommunities(IEnumerable<int> commIDs, bool selected)
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

            UpdateSelectedCommunities();
        }

        public void ToggleSelectedCommunities(IEnumerable<int> commIDs)
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

            UpdateSelectedCommunities();
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

            UpdateSelectedCommunities();
        }

        /*=============== start private methods ===================*/

        void ComputeCommunityElems()
        {
            FindCommunityLinks();
            FindCommunityNodes();
        }

        void FindCommunityLinks()
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

        void FindCommunityNodes()
        {
            foreach (var node in _nodes)
            {
                if (!node.IsVirtualNode)
                {
                    if (node.CommunityID == -1) Debug.Log(node.ID);
                    _communities[node.CommunityID].Nodes.Add(node);
                }
            }
        }

        // combine links with the same source community and the same target community
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

        void CreateCommunities()
        {
            var nodeStack = new Stack<int>();
            nodeStack.Push(_rootNodeID);

            var nodesLeft = _nodes.NodeArray.Select(n => n.ID).ToHashSet();

            int communityID = 0;
            while (nodeStack.Count != 0)
            {
                var curNode = _nodes[nodeStack.Pop()];

                if (IsCommunity(curNode))
                {
                    CreateCommunity(curNode, communityID++);

                    Debug.Log(curNode.ID);

                    nodesLeft.ExceptWith(curNode.ChildIDs.Where(nid => !_nodes[nid].IsVirtualNode));

                    nodeStack.Concat(curNode.ChildIDs.Where(cid => _nodes[cid].IsVirtualNode));
                }
                else
                {
                    nodeStack.Concat(curNode.ChildIDs);
                }

            }

            foreach (var left in nodesLeft)
            {
                // Debug.Log(left);
            }

        }

        bool IsCommunity(Node node)
        {
            // node is root of a community if any of its children are non-virtual
            return node.ChildIDs.Count() != 0 && node.ChildIDs.Any(c => !_nodes[c].IsVirtualNode);
        }

        void CreateCommunity(Node node, int commID)
        {
            Debug.Assert(node.IsVirtualNode);

            var community = new Community
            {
                RootNodeID = node.ID,
                ID = commID,
                Color = HighContrastColors.GenerateRandomColor(),
                Dirty = true
            };

            _communities.Add(commID, community);

            foreach (var childIdx in node.ChildIDs)
            {
                if (!_nodes[childIdx].IsVirtualNode)
                    _nodes[childIdx].CommunityID = commID;
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

            _selectedComms = completeComms;
        }
    }
}