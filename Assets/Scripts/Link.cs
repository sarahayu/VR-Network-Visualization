using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class Link
    {
        public bool spline = true;
        public int linkIdx;
        public int sourceIdx;
        public int targetIdx;

        public Node sourceNode;
        public Node targetNode;
        // the shortest path between two nodes in the hierarchical tree
        public List<Node> pathInTree = new List<Node>();

        public Link()
        {
            // leave empty
        }

        public Link(Node source, Node target, int id)
        {
            sourceNode = source;
            targetNode = target;
            sourceIdx = source.id;
            targetIdx = target.id;
            linkIdx = id;
        }
    }
}