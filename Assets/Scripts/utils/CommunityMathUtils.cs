using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{

    public static class CommunityMathUtils
    {
        public static void ComputeMassProperties(List<Node> nodes, out double mass, out Vector3 massCenter)
        {
            mass = 0;
            massCenter = Vector3.zero;

            foreach (var node in nodes)
            {
                mass += node.degree + 0.01;
                massCenter += node.Position3D;
            }

            massCenter /= nodes.Count;
        }

        public static float ComputeSize(List<Node> nodes, Vector3 massCenter)
        {
            float size = 0.1f;

            foreach (var node in nodes)
            {
                var dist = Vector3.Distance(massCenter, node.Position3D);
                size = Math.Max(dist, size);
            }

            return size;
        }
    }

}