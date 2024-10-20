/*
*
* SpiderFileDecoder decodes spider .dat files 
*
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace VidiGraph
{
    [Serializable]
    public class RNetwork
    {
        public RNode[] nodes;
        public RLink[] links;

        // use for reverse search into nodes array, this data is not found in the file but will be initialized later
        public Dictionary<int, int> idToIdx;
    }

    [Serializable]
    public class RNode
    {
        public int idx;
        public int communityIdx;
        public RPoint spherePos;
        public RPoint spiderPos;
    }

    [Serializable]
    public class RLink
    {
        public int linkIdx;
        public int sourceNode;
        public int targetNode;
        public RPoint[] sphereStraighten;
        public RPoint[] spiderStraighten;
    }

    [Serializable]
    public class RPoint
    {
        public float x;
        public float y;
        public float z;
    }

    public static class SpiderFileDecoder
    {
        public static RNetwork Decode(string filename)
        {
            string filepath = $"{Application.streamingAssetsPath}/{filename}";

            using FileStream file = File.OpenRead(filepath);

            BinaryFormatter bf = new BinaryFormatter();
            RNetwork rNetwork = (RNetwork)bf.Deserialize(file);

            return rNetwork;
        }
    }
}