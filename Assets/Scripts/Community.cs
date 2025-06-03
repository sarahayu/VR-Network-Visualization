using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class Community
    {
        public int ID;
        public int RootNodeID;
        public List<Node> Nodes = new List<Node>();
        public List<Link> InnerLinks = new List<Link>();
        public List<Link> OuterLinks = new List<Link>();

        // Aggregate the outer links
        // target : link number
        public Dictionary<int, int> AggregateLinks = new Dictionary<int, int>();

        public Color Color;

        // detect if community needs to be rerendered
        public bool Dirty = false;
        public bool Selected = false;
    }
}