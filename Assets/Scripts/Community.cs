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

        public void ComputeGeometricProperty()
        {
            if (communityNodes.Count != 0)
            {
                CommunityUtils.ComputeMassProperties(communityNodes, out mass, out massCenter);

                size = CommunityUtils.ComputeSize(communityNodes, massCenter);
            }
        }
    }
}