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
    using NodePropType = BullyProps.Node;
    using LinkPropType = BullyProps.Link;

    // using NodePropType = EmptyProp;
    // using LinkPropType = EmptyProp;

    [Serializable]
    public class NetworkFileData
    {
        public bool coded = false;
        public int rootIdx;
        public NodeFileData[] nodes;
        public LinkFileData[] links;

        // use for reverse search into nodes array, this data is not found in the file but will be initialized later
        public Dictionary<int, int> idToIdx;
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

        public NodePropType props = new NodePropType();
    }

    [Serializable]
    public class LinkFileData
    {
        public bool spline = true;
        public int linkIdx;
        public int sourceIdx;
        public int targetIdx;

        public LinkPropType props = new LinkPropType();
    }

    [Serializable]
    public class EmptyProp
    {

    }
}