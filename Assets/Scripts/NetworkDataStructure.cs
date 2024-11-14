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
    public class NetworkDataStructure : MonoBehaviour
    {
        // TODO change these to getters perhaps
        [HideInInspector]
        public bool IsCoded = false;
        [HideInInspector]
        public int RootNodeID;
        [HideInInspector]
        public NodeCollection Nodes = new NodeCollection();
        [HideInInspector]
        public List<Link> Links = new List<Link>();
        [HideInInspector]
        public bool Is2D;

        [HideInInspector]
        public List<Link> TreeLinks = new List<Link>();

        [HideInInspector]
        public int NumVirtualNode;
        [HideInInspector]
        public List<int> RealNodeIDs = new List<int>();
        [HideInInspector]
        public List<int> VirtualNodeIDs = new List<int>();

        public IDictionary<int, List<Link>> NodeLinkMatrix
             = new Dictionary<int, List<Link>>();

        public Dictionary<int, Community> Communities = new Dictionary<int, Community>();

        void Reset()
        {
            Nodes.Clear();
            Links.Clear();
            TreeLinks.Clear();
            RealNodeIDs.Clear();
            VirtualNodeIDs.Clear();
            NodeLinkMatrix.Clear();
            Communities.Clear();
        }

        public void InitNetwork()
        {
            Reset();

            var fileLoader = GetComponent<NetworkFilesLoader>();
            var fileNodes = fileLoader.GraphData.nodes;
            var fileLinks = fileLoader.GraphData.links;

            Is2D = fileLoader.Is2D;

            IsCoded = fileLoader.GraphData.coded;
            RootNodeID = fileLoader.GraphData.rootIdx;

            // 1. Calculate the number of real Node and virtual node
            // 2. Build Index
            for (var i = 0; i < fileNodes.Length; i++)
            {
                var node = PreprocFileUtils.NodeFromPreprocNode(fileNodes[i]);

                // Initialize the node-link matrix
                NodeLinkMatrix.Add(node.ID, new List<Link>());

                // Group the nodes by virtual or not
                if (node.IsVirtualNode) VirtualNodeIDs.Add(node.ID);
                else RealNodeIDs.Add(node.ID);

                Nodes.Add(node);
            }

            // Got the order list of all the ancestor of every node (sy: I guess to walk up the ancestor nodes?)
            // TODO turn into function
            foreach (var node in Nodes)
            {
                if (node.ID == RootNodeID)
                {
                    continue;
                }

                var ancNode = node.AncID;
                node.AncIDsOrderList.Add(ancNode);

                while (ancNode != RootNodeID)
                {
                    ancNode = Nodes[ancNode].AncID;
                    node.AncIDsOrderList.Add(ancNode);
                }
            }

            // 1. Build Node-Link Matrix
            // 2. calculate the degree
            // 3. find spline control points
            for (var i = 0; i < fileLinks.Length; i++)
            {
                var link = PreprocFileUtils.LinkFromPreprocLink(fileLinks[i]);

                link.SourceNode = Nodes[link.SourceNodeID];
                link.TargetNode = Nodes[link.TargetNodeID];

                // Build Node-Link Matrix
                NodeLinkMatrix[link.SourceNodeID].Add(link);
                NodeLinkMatrix[link.TargetNodeID].Add(link);
                link.ID = i;

                // calculate the degree
                Nodes[link.SourceNodeID].Degree += 0.01;
                Nodes[link.TargetNodeID].Degree += 0.01;

                Links.Add(link);

                InitPathInTree(link);
            }

            BuildTreeLinks(RootNodeID);
            TagCommunities();
            CommunitiesInit();
        }

        void CommunitiesInit()
        {
            FindLinks4Communities();
            FindNodesInCommunities();
        }
        void FindLinks4Communities()
        {
            foreach (var link in Links)
            {
                var sourceCommunity = Nodes[link.SourceNodeID].CommunityID;
                var targetCommunity = Nodes[link.TargetNodeID].CommunityID;

                // for the link inside a community
                if (sourceCommunity == targetCommunity)
                {
                    Communities[sourceCommunity].InnerLinks.Add(link);
                }
                else
                {
                    Communities[sourceCommunity].OuterLinks.Add(link);
                    Communities[targetCommunity].OuterLinks.Add(link);
                }
            }

            foreach (var entry in Communities)
            {
                var community = entry.Value;
                CombineLinks(community);
            }
        }

        void FindNodesInCommunities()
        {
            foreach (var node in Nodes)
            {
                if (!node.IsVirtualNode)
                {
                    Communities[node.CommunityID].Nodes.Add(node);
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
                var sourceCommunity = Nodes[link.SourceNodeID].CommunityID;
                var targetCommunity = Nodes[link.TargetNodeID].CommunityID;

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
            nodeStack.Push(RootNodeID);

            int communityID = 0;
            while (nodeStack.Count != 0)
            {
                var curNode = Nodes[nodeStack.Pop()];

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
            return node.ChildIDs.Count() != 0 && !Nodes[node.ChildIDs[0]].IsVirtualNode;
        }

        void TagCommunity(Node node, int clusterIdx)
        {
            Debug.Assert(node.IsVirtualNode);

            var community = new Community
            {
                RootNodeID = node.ID,
                ID = clusterIdx
            };

            Communities.Add(clusterIdx, community);

            foreach (var childIdx in node.ChildIDs)
            {
                Nodes[childIdx].CommunityID = clusterIdx;
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

            var commonAncNode = Nodes[commonAncIdx];

            // add source node
            link.PathInTree.Add(sourceNode);

            // add source node ancestors up to (not including) common ancestor
            for (int i = 0; i < sourceAncestors.Count; i++)
            {
                if (sourceAncestors[i] == commonAncIdx) break;
                link.PathInTree.Add(Nodes[sourceAncestors[i]]);
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
                    link.PathInTree.Add(Nodes[targetAncestors[i]]);
                }
            }

            // add target node
            link.PathInTree.Add(targetNode);
        }

        private void BuildTreeLinks(int nodeID)
        {
            if (!Nodes[nodeID].IsVirtualNode) return;

            foreach (var childID in Nodes[nodeID].ChildIDs)
            {
                // just make link idx the negative count to ensure it's unique from real links
                int treeLinkID = -TreeLinks.Count;

                TreeLinks.Add(new Link(Nodes[nodeID], Nodes[childID], treeLinkID));

                BuildTreeLinks(childID);
            }
        }
    }
}