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
        public int RootIdx;
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
        public List<int> RealNodeIdx = new List<int>();
        [HideInInspector]
        public List<int> VirtualNodeIdx = new List<int>();

        public IDictionary<int, List<Link>> NodeLinkMatrix
             = new Dictionary<int, List<Link>>();

        public Dictionary<int, Community> Communities = new Dictionary<int, Community>();

        void Reset()
        {
            Nodes.Clear();
            Links.Clear();
            TreeLinks.Clear();
            RealNodeIdx.Clear();
            VirtualNodeIdx.Clear();
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
            RootIdx = fileLoader.GraphData.rootIdx;

            // 1. Calculate the number of real Node and virtual node
            // 2. Build Index
            for (var i = 0; i < fileNodes.Length; i++)
            {
                var node = PreprocFileUtils.NodeFromPreprocNode(fileNodes[i]);

                // Initialize the node-link matrix
                NodeLinkMatrix.Add(node.id, new List<Link>());

                // Group the nodes by virtual or not
                if (node.virtualNode) VirtualNodeIdx.Add(node.id);
                else RealNodeIdx.Add(node.id);

                Nodes.Add(node);
            }

            // Got the order list of all the ancestor of every node (sy: I guess to walk up the ancestor nodes?)
            // TODO turn into function
            foreach (var node in Nodes)
            {
                if (node.id == RootIdx)
                {
                    continue;
                }

                var ancNode = node.ancIdx;
                node.ancIdxOrderList.Add(ancNode);

                while (ancNode != RootIdx)
                {
                    ancNode = Nodes[ancNode].ancIdx;
                    node.ancIdxOrderList.Add(ancNode);
                }
            }

            // 1. Build Node-Link Matrix
            // 2. calculate the degree
            // 3. find spline control points
            for (var i = 0; i < fileLinks.Length; i++)
            {
                var link = PreprocFileUtils.LinkFromPreprocLink(fileLinks[i]);

                link.sourceNode = Nodes[link.sourceIdx];
                link.targetNode = Nodes[link.targetIdx];

                // Build Node-Link Matrix
                NodeLinkMatrix[link.sourceIdx].Add(link);
                NodeLinkMatrix[link.targetIdx].Add(link);
                link.linkIdx = i;

                // calculate the degree
                Nodes[link.sourceIdx].degree += 0.01;
                Nodes[link.targetIdx].degree += 0.01;

                Links.Add(link);

                InitPathInTree(link);
            }

            BuildTreeLinks(RootIdx);
            TagCommunities();
            CommunitiesInit();
        }

        void CommunitiesInit()
        {
            FindLinks4Communities();
            FindNodesInCommunities();

            // buildmatrix (sy: why was this a to do?)
        }
        void FindLinks4Communities()
        {
            foreach (var link in Links)
            {
                var sourceCommunity = Nodes[link.sourceIdx].communityIdx;
                var targetCommunity = Nodes[link.targetIdx].communityIdx;

                // for the link inside a community
                if (sourceCommunity == targetCommunity)
                {
                    Communities[sourceCommunity].innerLinks.Add(link);
                }
                else
                {
                    Communities[sourceCommunity].outerLinks.Add(link);
                    Communities[targetCommunity].outerLinks.Add(link);
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
                if (!node.virtualNode)
                {
                    Communities[node.communityIdx].communityNodes.Add(node);
                }
            }
        }

        /*
         * combine links with the same source community and the same target community
         */
        void CombineLinks(Community community)
        {
            foreach (var link in community.outerLinks)
            {
                var sourceCommunity = Nodes[link.sourceIdx].communityIdx;
                var targetCommunity = Nodes[link.targetIdx].communityIdx;

                var otherCommunity = community.communityIdx == sourceCommunity ? targetCommunity : sourceCommunity;

                if (!community.aggregateLinks.ContainsKey(otherCommunity))
                {
                    community.aggregateLinks.Add(otherCommunity, 0);
                }

                community.aggregateLinks[otherCommunity]++;
            }
        }

        // label communities
        void TagCommunities()
        {
            // TODO change from DFS to BFS for simplicity
            var nodeStack = new Stack<int>();
            nodeStack.Push(RootIdx);

            int clusterIdx = 0;
            while (nodeStack.Count != 0)
            {
                var curNode = Nodes[nodeStack.Pop()];

                if (HasLeaves(curNode))
                {
                    TagCommunity(curNode, clusterIdx++);
                }
                else
                {
                    foreach (var childIdx in curNode.childIdx)
                    {
                        nodeStack.Push(childIdx);
                    }
                }

            }

        }

        bool HasLeaves(Node node)
        {
            return node.childIdx.Count() != 0 && !Nodes[node.childIdx[0]].virtualNode;
        }

        void TagCommunity(Node node, int clusterIdx)
        {
            Debug.Assert(node.virtualNode);

            var community = new Community
            {
                root = node.id,
                communityIdx = clusterIdx
            };

            Communities.Add(clusterIdx, community);

            foreach (var childIdx in node.childIdx)
            {
                Nodes[childIdx].communityIdx = clusterIdx;
            }
        }

        // For every link, we find the shortest path in the hierarchy
        void InitPathInTree(Link link)
        {
            var sourceNode = Nodes[link.sourceIdx];
            var targetNode = Nodes[link.targetIdx];

            var sourceAncestors = sourceNode.ancIdxOrderList;
            var targetAncestors = targetNode.ancIdxOrderList;

            var commonAncestors = sourceAncestors.Intersect(targetAncestors);
            var commonAncIdx = commonAncestors.FirstOrDefault();

            var commonAncNode = Nodes[commonAncIdx];

            // add source node
            link.pathInTree.Add(sourceNode);

            // add source node ancestors up to (not including) common ancestor
            for (int i = 0; i < sourceAncestors.Count; i++)
            {
                if (sourceAncestors[i] == commonAncIdx) break;
                link.pathInTree.Add(Nodes[sourceAncestors[i]]);
            }

            // add common ancestor
            link.pathInTree.Add(commonAncNode);

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
                    link.pathInTree.Add(Nodes[targetAncestors[i]]);
                }
            }

            // add target node
            link.pathInTree.Add(targetNode);
        }

        private void BuildTreeLinks(int nodeIdx)
        {
            if (!Nodes[nodeIdx].virtualNode) return;

            foreach (var childIdx in Nodes[nodeIdx].childIdx)
            {
                // just make link idx the negative count to ensure it's unique from real links
                int treeLinkIdx = -TreeLinks.Count;

                TreeLinks.Add(new Link(Nodes[nodeIdx], Nodes[childIdx], treeLinkIdx));

                BuildTreeLinks(childIdx);
            }
        }
    }
}