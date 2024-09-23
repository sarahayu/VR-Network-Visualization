/*
*
* NetworkDataStructure is our central point for accessing any additional properties computed that was not found in data files
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
        [HideInInspector]
        public bool coded = false;
        [HideInInspector]
        public int rootIdx;
        [HideInInspector]
        public NodeCollection nodes = new NodeCollection();
        [HideInInspector]
        public List<Link> links = new List<Link>();
        [HideInInspector]
        public bool is2D;

        [HideInInspector]
        public List<Link> treeLinks = new List<Link>();

        [HideInInspector]
        public int numVirtualNode;
        [HideInInspector]
        public List<int> realNodeIdx = new List<int>();
        [HideInInspector]
        public List<int> virtualNodeIdx = new List<int>();

        public IDictionary<int, List<Link>> nodeLinkMatrix
             = new Dictionary<int, List<Link>>();

        public Dictionary<int, Community> communities = new Dictionary<int, Community>();

        void Reset()
        {
            nodes.Clear();
            links.Clear();
            treeLinks.Clear();
            realNodeIdx.Clear();
            virtualNodeIdx.Clear();
            nodeLinkMatrix.Clear();
            communities.Clear();
        }

        public void InitNetwork(bool in2D)
        {
            Reset();

            var fileLoader = GetComponent<NetworkFilesLoader>();
            var fileNodes = fileLoader.GraphData.nodes;
            var fileLinks = fileLoader.GraphData.links;

            is2D = in2D;

            coded = fileLoader.GraphData.coded;
            rootIdx = fileLoader.GraphData.rootIdx;

            // 1. Calculate the number of real Node and virtual node
            // 2. Build Index
            for (var i = 0; i < fileNodes.Length; i++)
            {
                var node = new Node(fileNodes[i]);

                // Initialize the node-link matrix
                nodeLinkMatrix.Add(node.idx, new List<Link>());

                // Group the nodes by virtual or not
                if (node.virtualNode) virtualNodeIdx.Add(node.idx);
                else realNodeIdx.Add(node.idx);

                nodes.Add(node);
            }

            // Got the order list of all the ancestor of every node (sy: I guess to walk up the ancestor nodes?)
            // TODO turn into function
            foreach (var node in nodes)
            {
                if (node.idx == rootIdx)
                {
                    continue;
                }

                var ancNode = node.ancIdx;
                node.ancIdxOrderList.Add(ancNode);

                while (ancNode != rootIdx)
                {
                    ancNode = nodes[ancNode].ancIdx;
                    node.ancIdxOrderList.Add(ancNode);
                }
            }

            // 1. Build Node-Link Matrix
            // 2. calculate the degree
            // 3. find spline control points
            for (var i = 0; i < fileLinks.Length; i++)
            {
                var link = new Link(fileLinks[i]);

                // Build Node-Link Matrix
                nodeLinkMatrix[link.sourceIdx].Add(link);
                nodeLinkMatrix[link.targetIdx].Add(link);
                link.linkIdx = i;

                // calculate the degree
                nodes[link.sourceIdx].degree += 0.01;
                nodes[link.targetIdx].degree += 0.01;

                links.Add(link);

                InitPathInTree(link);
            }

            BuildTreeLinks(rootIdx);
            TagCommunities();
        }

        public void CommunitiesInit()
        {
            FindLinks4Communities();
            FindNodesInCommunities();

            // buildmatrix (sy: why was this a to do?)
        }
        public void FindLinks4Communities()
        {
            foreach (var link in links)
            {
                var sourceCommunity = nodes[link.sourceIdx].communityIdx;
                var targetCommunity = nodes[link.targetIdx].communityIdx;

                // for the link inside a community
                if (sourceCommunity == targetCommunity)
                {
                    communities[sourceCommunity].innerLinks.Add(link);
                }
                else
                {
                    communities[sourceCommunity].outerLinks.Add(link);
                    communities[targetCommunity].outerLinks.Add(link);
                }
            }

            foreach (var entry in communities)
            {
                var community = entry.Value;
                CombineLinks(community);
            }
        }

        void FindNodesInCommunities()
        {
            foreach (var node in nodes)
            {
                if (!node.virtualNode)
                {
                    communities[node.communityIdx].communityNodes.Add(node);
                }
            }

            foreach (var entry in communities)
            {
                entry.Value.ComputeGeometricProperty();
            }
        }

        /*
         * combine links with the same source community and the same target community
         */
        void CombineLinks(Community community)
        {
            foreach (var link in community.outerLinks)
            {
                var sourceCommunity = nodes[link.sourceIdx].communityIdx;
                var targetCommunity = nodes[link.targetIdx].communityIdx;

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
            nodeStack.Push(rootIdx);

            int clusterIdx = 0;
            while (nodeStack.Count != 0)
            {
                var curNode = nodes[nodeStack.Pop()];

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
            return node.childIdx.Count() != 0 && !nodes[node.childIdx[0]].virtualNode;
        }

        void TagCommunity(Node node, int clusterIdx)
        {
            Debug.Assert(node.virtualNode);

            var community = new Community
            {
                root = node.idx,
                communityIdx = clusterIdx
            };

            communities.Add(clusterIdx, community);

            foreach (var childIdx in node.childIdx)
            {
                nodes[childIdx].communityIdx = clusterIdx;
            }
        }

        // For every link, we find the shortest path in the hierarchy
        void InitPathInTree(Link link)
        {
            var sourceNode = nodes[link.sourceIdx];
            var targetNode = nodes[link.targetIdx];

            var sourceAncestors = sourceNode.ancIdxOrderList;
            var targetAncestors = targetNode.ancIdxOrderList;

            var commonAncestors = sourceAncestors.Intersect(targetAncestors);

            var ancNode = nodes[commonAncestors.FirstOrDefault()];

            link.pathInTree.Add(sourceNode);
            link.pathInTree.Add(ancNode);
            link.pathInTree.Add(targetNode);
        }

        private void BuildTreeLinks(int nodeIdx)
        {
            if (!nodes[nodeIdx].virtualNode) return;

            foreach (var childIdx in nodes[nodeIdx].childIdx)
            {
                treeLinks.Add(new Link(nodeIdx, childIdx));

                BuildTreeLinks(childIdx);
            }
        }
    }
}