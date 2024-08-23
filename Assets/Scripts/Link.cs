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

        // the shortest path between two nodes in the hierarchical tree
        public List<Node> pathInTree = new List<Node>();

        public IList<Vector3> straightenPoints = new List<Vector3>();

        // Control Points for Edge Bundling
        public Vector3[] ControlPoints(NetworkDataStructure network)
        {
            //TODO There has to some better way to do this, and also to provide a constant number of control points

            var clusterS = network.communities[network[sourceIdx].communityIdx];
            var clusterT = network.communities[network[targetIdx].communityIdx];
            var sCenter = clusterS.massCenter;
            var tCenter = clusterT.massCenter;
            if (network.is2D)
            {
                sCenter.z = 0.25f;
                tCenter.z = 0.25f;
            }
            // Between focus and context
            if (clusterS.focus && !clusterT.focus)
            {
                return new[] {
                    network[sourceIdx].Position3D,
                    (sCenter + tCenter) / 2,
                    tCenter,
                    network[targetIdx].Position3D
                };
            }
            if (clusterT.focus && !clusterS.focus)
            {
                return new[] {
                    network[sourceIdx].Position3D,
                    sCenter,
                    (sCenter + tCenter) / 2,
                    network[targetIdx].Position3D
                };
            }

            Vector3[] result = new Vector3[pathInTree.Count];
            for (int i = 0; i < pathInTree.Count; i++)
            {
                result[i] = pathInTree[i].Position3D;
            }

            // TODO Optimization
            //pathInTree.ForEach((n) =>
            //{
            //    // normalization
            //    retval.Add((n.Position3D));
            //});

            //return retval;

            return result;
        }
    }
}