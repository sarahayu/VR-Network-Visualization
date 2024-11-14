using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class Link
    {
        public bool IsSpline = true;
        public int ID;
        public int SourceNodeID;
        public int TargetNodeID;

        public Node SourceNode;
        public Node TargetNode;
        // the shortest path between two nodes in the hierarchical tree
        public List<Node> PathInTree = new List<Node>();

        public Link()
        {
            // leave empty
        }

        public Link(Node source, Node target, int id)
        {
            SourceNode = source;
            TargetNode = target;
            SourceNodeID = source.ID;
            TargetNodeID = target.ID;
            this.ID = id;
        }
    }
}