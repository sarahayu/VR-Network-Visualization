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

        public float depth = 1;
        public bool focus = false;
    }
}