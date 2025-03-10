/*
*
* NetworkDataStructure is our central point for accessing any additional global properties computed that was not found in data files
* (i.e. in-game coordinates, current animated coordinates)
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
        bool _isCoded = false;
        int _rootNodeID;

        NodeCollection _nodes = new NodeCollection();
        List<Link> _links = new List<Link>();
        Dictionary<int, Community> _communities = new Dictionary<int, Community>();

        List<Link> _treeLinks = new List<Link>();
        List<int> _realNodeIDs = new List<int>();
        List<int> _virtualNodeIDs = new List<int>();
        IDictionary<int, List<Link>> _nodeLinkMatrix
             = new Dictionary<int, List<Link>>();


        public NodeCollection Nodes { get { return _nodes; } }
        public List<Link> Links { get { return _links; } }
        public Dictionary<int, Community> Communities { get { return _communities; } }
        public List<Link> TreeLinks { get { return _treeLinks; } }
        public Node HoveredNode = null;
        public Community HoveredCommunity = null;

        void Reset()
        {
            _nodes.Clear();
            _links.Clear();
            _treeLinks.Clear();
            _realNodeIDs.Clear();
            _virtualNodeIDs.Clear();
            _nodeLinkMatrix.Clear();
            _communities.Clear();
        }

        public void InitNetwork()
        {
            Reset();

            var fileLoader = GetComponent<NetworkFilesLoader>();
            var fileNodes = fileLoader.GraphData.nodes;
            var fileLinks = fileLoader.GraphData.links;

            _isCoded = fileLoader.GraphData.coded;
            _rootNodeID = fileLoader.GraphData.rootIdx;

            // 1. Calculate the number of real Node and virtual node
            // 2. Build Index
            for (var i = 0; i < fileNodes.Length; i++)
            {
                var node = PreprocFileUtils.NodeFromPreprocNode(fileNodes[i]);

                // Initialize the node-link matrix
                _nodeLinkMatrix.Add(node.ID, new List<Link>());

                // Group the nodes by virtual or not
                if (node.IsVirtualNode) _virtualNodeIDs.Add(node.ID);
                else _realNodeIDs.Add(node.ID);

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
                var link = PreprocFileUtils.LinkFromPreprocLink(fileLinks[i]);

                link.SourceNode = _nodes[link.SourceNodeID];
                link.TargetNode = _nodes[link.TargetNodeID];

                // Build Node-Link Matrix
                _nodeLinkMatrix[link.SourceNodeID].Add(link);
                _nodeLinkMatrix[link.TargetNodeID].Add(link);
                link.ID = i;

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

                if (HasLeaves(curNode))
                {
                    TagCommunity(curNode, communityID++);
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

        bool HasLeaves(Node node)
        {
            return node.ChildIDs.Count() != 0 && !_nodes[node.ChildIDs[0]].IsVirtualNode;
        }

        void TagCommunity(Node node, int clusterIdx)
        {
            Debug.Assert(node.IsVirtualNode);

            var community = new Community
            {
                RootNodeID = node.ID,
                ID = clusterIdx
            };

            _communities.Add(clusterIdx, community);

            foreach (var childIdx in node.ChildIDs)
            {
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

        private void BuildTreeLinks(int nodeID)
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
    }
}