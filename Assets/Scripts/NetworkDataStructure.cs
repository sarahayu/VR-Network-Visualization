/*
*
* NetworkDataStructure is our central point for accessing any additional properties computed that was not found in data files
* (i.e. in-game coordinates, current animated coordinates)
*/

using System;
using System.Collections;
using System.Collections.Generic;
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
        public Node[] nodes;
        [HideInInspector]
        public Link[] links;
        [HideInInspector]
        public bool is2D;

        [HideInInspector]
        public List<Link> treeLinks = new List<Link>();
        [HideInInspector]
        public int numRealNode;
        [HideInInspector]
        public int numVirtualNode;
        [HideInInspector]
        public int[] realNodeIdx;
        [HideInInspector]
        public int[] virtualNodeIdx;
        [HideInInspector]
        public double maxDistance = 0.0;
        [HideInInspector]
        public double maxLimitation = 0.9;
        [HideInInspector]
        public int importanceNumberBound = 10;
        [HideInInspector]
        public double depthLevel = 2;

        public IDictionary<int, List<Link>> nodeLinkMatrix
             = new Dictionary<int, List<Link>>();

        // A inverted index allow reference the node by its own idx
        public IDictionary<int, int> nodeInvertedIndex = new Dictionary<int, int>();

        public Dictionary<int, Community> communities = new Dictionary<int, Community>();

        public Node this[int index]
        {
            get { return nodes[nodeInvertedIndex[index]]; }
            set { }
        }

        public void InitNetwork(bool in2D)
        {
            is2D = in2D;
            if (nodes != null)
            {
                // 1. Calculate the number of real Node and virtual node
                // 2. Build Index
                for (var i = 0; i < nodes.Length; i++)
                {
                    // Build inverted index
                    nodeInvertedIndex.Add(nodes[i].idx, i);
                    // Initialize the node-link matrix
                    nodeLinkMatrix.Add(nodes[i].idx, new List<Link>());

                    if (nodes[i].childIdx != null)
                    {
                        nodes[i].onRunChildIdx = new List<int>();
                        // change the childIdx Array to Running time Child Dynamic List
                        foreach (var idx in nodes[i].childIdx)
                        {
                            nodes[i].onRunChildIdx.Add(idx);
                        }
                    }

                    if (!nodes[i].virtualNode)
                    {
                        numRealNode++;
                    }
                }

                numVirtualNode = nodes.Length - numRealNode;
                realNodeIdx = new int[numRealNode];
                virtualNodeIdx = new int[numVirtualNode];

                // Got the order list of all the ancestor of every node
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
                        ancNode = this[ancNode].ancIdx;
                        node.ancIdxOrderList.Add(ancNode);
                    }
                }

                // 1. Build Node-Link Matrix
                // 2. calculate the degree
                // 3. find spline control points
                if (links != null)
                {
                    var idx = 0;
                    foreach (var link in links)
                    {
                        // Build Node-Link Matrix
                        nodeLinkMatrix[link.sourceIdx].Add(link);
                        nodeLinkMatrix[link.targetIdx].Add(link);
                        link.linkIdx = idx;
                        idx++;

                        // calculate the degree
                        this[link.sourceIdx].degree += 0.01;
                        this[link.targetIdx].degree += 0.01;

                        AddControlPoints4Links(link);
                    }
                }

                // Group the nodes by virtual or not
                var ri = 0;
                var vi = 0;
                for (var i = 0; i < nodes.Length; i++)
                {
                    if (nodes[i].virtualNode)
                    {
                        virtualNodeIdx[vi] = nodes[i].idx;
                        vi++;
                    }
                    else
                    {
                        realNodeIdx[ri] = nodes[i].idx;
                        ri++;
                    }
                }

                BuildTreeLinks(rootIdx);

                TagCommunities();
            }

        }

        public void CommunitiesInit()
        {
            FindLinks4Communities();
            FindNodesInCommunities();

            // TODO buildmatrix
        }
        public void FindLinks4Communities()
        {
            foreach (var link in links)
            {
                // for the link inside a community
                if (this[link.sourceIdx].communityIdx == this[link.targetIdx].communityIdx)
                {
                    communities[this[link.sourceIdx].communityIdx].innerLinks.Add(link);
                }
                else
                {
                    try
                    {
                        communities[this[link.sourceIdx].communityIdx].outerLinks.Add(link);
                        communities[this[link.targetIdx].communityIdx].outerLinks.Add(link);
                    }
                    catch (Exception)
                    {
                        Debug.Log(link.sourceIdx + "->" + this[link.sourceIdx].communityIdx);
                        Debug.Log(link.targetIdx + "->" + this[link.targetIdx].communityIdx);
                        throw;
                    }

                }
            }

            foreach (var entry in communities)
            {
                var community = entry.Value;
                community.CombineLinks(this);
            }
        }

        public void FindNodesInCommunities()
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
                entry.Value.ComputerGeometricProperty();
            }
        }

        // label communities
        public void TagCommunities()
        {
            var nodeStack = new Stack<int>();
            nodeStack.Push(rootIdx);
            int clusterIdx = 0;
            while (nodeStack.Count != 0)
            {
                var nodeIdx = nodeStack.Pop();
                var node = this[nodeIdx];

                // the node link to leaves
                if (node.onRunChildIdx.Count != 0 && !this[this[nodeIdx].onRunChildIdx[0]].virtualNode)
                {
                    TagCommunity(node, clusterIdx);
                    clusterIdx++;
                }

                foreach (var childIdx in node.onRunChildIdx)
                {
                    nodeStack.Push(childIdx);
                }
            }

        }

        /*
         * the node should be a virtual node
         */
        private void TagCommunity(Node node, int clusterIdx)
        {
            var community = new Community
            {
                root = node.idx,
                communityIdx = clusterIdx
            };

            communities.Add(clusterIdx, community);

            foreach (var childIdx in node.onRunChildIdx)
            {
                this[childIdx].communityIdx = clusterIdx;
            }
        }

        // For every link, we find the shortest path in the hierarchy
        public void AddControlPoints4Links(Link link)
        {
            link.pathInTree.Add(this[link.sourceIdx]);
            if (this[link.sourceIdx].ancIdx == this[link.targetIdx].ancIdx)
            {
                link.pathInTree.Add(this[this[link.sourceIdx].ancIdx]);
                goto afterAdd;
            }

            // Add control points for every link
            for (var i = 0; i < this[link.sourceIdx].ancIdxOrderList.Count; i++)
            {
                for (var j = 0; j < this[link.targetIdx].ancIdxOrderList.Count; j++)
                {
                    if (this[link.sourceIdx].ancIdxOrderList[i] == this[link.targetIdx].ancIdxOrderList[j])
                    {
                        for (var k = j; k >= 0; k--)
                        {
                            link.pathInTree.Add(this[this[link.targetIdx].ancIdxOrderList[k]]);
                        }
                        goto afterAdd;
                    }
                }
                link.pathInTree.Add(this[this[link.sourceIdx].ancIdxOrderList[i]]);
            }
        afterAdd: link.pathInTree.Add(this[link.targetIdx]);
        }

        private void BuildTreeLinks(int nodeIdx)
        {
            if (!this[nodeIdx].virtualNode)
            {
                return;
            }

            foreach (var childIdx in this[nodeIdx].childIdx)
            {
                var link = new Link
                {
                    sourceIdx = nodeIdx,
                    targetIdx = childIdx
                };
                treeLinks.Add(link);
                BuildTreeLinks(childIdx);
            }
        }
    }
}