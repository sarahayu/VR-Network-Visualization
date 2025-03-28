using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class Node
    {
        public int ID;
        public string Label;
        public bool IsVirtualNode;
        // TODO change to Community object
        public int CommunityID = -1;
        public string Color;
        public Color ColorParsed;
        public double Degree = 0;
        public int Height;
        public int[] ChildIDs;

        // TODO change to Nodes instead of int?
        public int AncID;
        public IList<int> AncIDsOrderList = new List<int>();

        // detect if node needs to be rerendered
        public bool Dirty = false;
        public bool Selected = false;
    }
}