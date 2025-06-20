using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class Link
    {
        public bool IsSpline { get; set; } = true;
        public int ID { get; set; }
        // TODO remove
        public int SourceNodeID { get; set; }
        public int TargetNodeID { get; set; }
        public int IdxProcessed { get; set; }

        public Node SourceNode;
        public Node TargetNode;
        // the shortest path between two nodes in the hierarchical tree
        public List<Node> PathInTree = new List<Node>();

        // detect if link needs to be rerendered
        public bool Dirty = false;
        public bool Selected = false;

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
            ID = id;
        }
    }
}