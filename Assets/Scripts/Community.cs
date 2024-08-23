using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class Community
    {
        public int communityIdx;
        public int root;
        public List<Node> communityNodes = new List<Node>();
        public List<Link> innerLinks = new List<Link>();
        public List<Link> outerLinks = new List<Link>();

        // Aggregate the outer links
        // target : link number
        public Dictionary<int, int> aggregateLinks = new Dictionary<int, int>();

        public double mass = 0;
        public double size = 0;
        public Vector3 massCenter = Vector3.zero;

        public float depth = 1;
        public bool focus = false;
        public bool onMove = false;

        public void ComputerGeometricProperty()
        {
            if (communityNodes.Count != 0)
            {
                massCenter = Vector3.zero;
                foreach (var node in communityNodes)
                {
                    mass += node.degree + 0.01;
                    massCenter += node.Position3D;
                }

                massCenter /= communityNodes.Count;

                ComputeSize();
            }
        }

        private void ComputeSize()
        {
            foreach (var node in communityNodes)
            {
                var dist = Vector3.Distance(massCenter, node.Position3D);
                size = System.Math.Max(dist, size);
            }
        }

        /*
         * combine links with the same source community and the same target community
         */
        // TODO move this to NetworkDataStrcture
        public void CombineLinks(NetworkDataStructure network)
        {
            foreach (var link in outerLinks)
            {
                if (communityIdx == network[link.sourceIdx].communityIdx)
                {
                    var targetCommunity = network[link.targetIdx].communityIdx;
                    if (!aggregateLinks.ContainsKey(targetCommunity))
                    {
                        aggregateLinks.Add(targetCommunity, 1);
                        continue;
                    }

                    aggregateLinks[targetCommunity]++;
                }
                else
                {
                    var targetCommunity = network[link.sourceIdx].communityIdx;
                    if (!aggregateLinks.ContainsKey(targetCommunity))
                    {
                        aggregateLinks.Add(targetCommunity, 1);
                        continue;
                    }

                    aggregateLinks[targetCommunity]++;
                }
            }
        }
    }
}