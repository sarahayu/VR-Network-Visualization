using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class Node
    {
        public int id;
        public string label;
        public bool virtualNode;
        public int communityIdx;
        public string color;
        public Color colorParsed;
        public double degree = 0;
        public int height;
        public int ancIdx;
        public int[] childIdx;

        public Vector3 precompPos3D;
        public IList<int> ancIdxOrderList = new List<int>();
        public bool isSpider = false;
    }
}