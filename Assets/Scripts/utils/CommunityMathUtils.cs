using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{

    public static class CommunityMathUtils
    {
        public static void ComputeMassProperties(List<Node> nodes, Dictionary<int, MultiLayoutContext.Node> nodeContexts,
            out double mass, out Vector3 massCenter)
        {
            mass = 0;
            massCenter = Vector3.zero;

            foreach (var node in nodes)
            {
                mass += node.Degree + 0.01;
                massCenter += nodeContexts[node.ID].Position;
            }

            massCenter /= nodes.Count;
        }

        public static float ComputeSize(List<Node> nodes, Dictionary<int, MultiLayoutContext.Node> nodeContexts, Vector3 massCenter)
        {
            float size = 0.1f;

            foreach (var node in nodes)
            {
                var dist = Vector3.Distance(massCenter, nodeContexts[node.ID].Position);
                size = Math.Max(dist, size);
            }

            return size;
        }

        // Key should have the community ID that is smaller first so we don't have doubles of links
        public static Tuple<int, int> IDsToLinkKey(int c1, int c2)
        {
            if (c1 > c2)
                return new Tuple<int, int>(c2, c1);
            else
                return new Tuple<int, int>(c1, c2);
        }
    }

}