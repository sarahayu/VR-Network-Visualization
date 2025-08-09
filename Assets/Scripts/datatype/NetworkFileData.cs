/*
*
* NetworkFileData stores ONLY our JSON file data (no in-game variables like in game position, animation time, etc).
*
*/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    // uncomment to parse custom propdata inside file

    // using Props = BullyProps;
    using Props = SchoolProps;

    // using Props = DefaultProps;

    [Serializable]
    public class NetworkFileData
    {
        public bool coded = false;
        public int rootIdx;
        public NodeFileData[] nodes;
        public LinkFileData[] links;
    }

    [Serializable]
    public class NodeFileData
    {
        public int communityIdx = Int32.MaxValue;
        public string label;
        public int idx;
        public string color = null;
        public bool virtualNode;
        public int height;
        public int ancIdx = -1;     // -1 to signify no ancestor, possible with root node
        public int[] childIdx;
        public Vector3 _position3D;
        public float[] pos2D;
        public float[] pos3D;

        public Props.Node props = new Props.Node();
    }

    [Serializable]
    public class LinkFileData
    {
        public bool spline = true;
        public int linkIdx;
        public int sourceIdx;
        public int targetIdx;

        public Props.Link props = new Props.Link();
    }
}