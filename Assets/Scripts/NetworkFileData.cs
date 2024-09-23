/*
*
* NetworkFileData stores ONLY our JSON file data (no in game variables like in game position, animation time, etc)
*
*/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{

    [Serializable]
    public class NetworkFileData
    {
        public bool coded = false;
        public int rootIdx;
        public NodeFileData[] nodes;
        public LinkFileData[] links;
        public bool is2D;

    }

    [Serializable]
    public class NodeFileData
    {
        public int communityIdx = Int32.MaxValue;
        public string label;
        public int idx;
        public string color = null;
        public bool virtualNode;
        public double degree = 0;
        public int height;
        public int ancIdx;
        public int[] childIdx;
        public float[] pos2D;
        public float[] pos3D;
    }

    [Serializable]
    public class LinkFileData
    {
        public bool spline = true;
        public int linkIdx;
        public int sourceIdx;
        public int targetIdx;
    }
}