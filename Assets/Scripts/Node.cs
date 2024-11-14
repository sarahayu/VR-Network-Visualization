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
        public int CommunityID;
        public string Color;
        public Color ColorParsed;
        public double Degree = 0;
        public int Height;
        public int AncID;
        public int[] ChildIDs;

        public Vector3 PrecompPos3D;
        public IList<int> AncIDsOrderList = new List<int>();
    }
}