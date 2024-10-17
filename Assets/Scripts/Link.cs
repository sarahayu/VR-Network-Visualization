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
        public bool localLayout = false;

        public Node sourceNode;
        public Node targetNode;
        // the shortest path between two nodes in the hierarchical tree
        public List<Node> pathInTree = new List<Node>();
        public LinkStateStack state = new LinkStateStack();

        public Link(LinkFileData linkData)
        {
            spline = linkData.spline;
            linkIdx = linkData.linkIdx;
            sourceIdx = linkData.sourceIdx;
            targetIdx = linkData.targetIdx;
        }

        public Link(Node source, Node target, int id)
        {
            sourceNode = source;
            targetNode = target;
            sourceIdx = source.idx;
            targetIdx = target.idx;
            linkIdx = id;
        }
    }

    public enum LinkState
    {
        HighLight,
        Context,
        Focus2Context,
        Focus,
        Normal,
        HighLightFocus,
    }

    public class LinkStateStack
    {
        private LinkState curState = LinkState.Normal;
        private LinkState lastState = LinkState.Normal;

        public LinkState CurState { get { return curState; } }
        public LinkState LastState { get { return lastState; } }

        public void SetLinkState(LinkState s)
        {
            lastState = curState;
            curState = s;
        }
    }
}