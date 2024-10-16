using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public static class BSplineMathUtils
    {
        public static Vector3[] ControlPoints(Link link, NetworkDataStructure networkData)
        {
            //TODO There has to some better way to do this, and also to provide a constant number of control points

            var sourceNode = link.sourceNode;
            var targetNode = link.targetNode;

            var clusterS = networkData.Communities[sourceNode.communityIdx];
            var clusterT = networkData.Communities[targetNode.communityIdx];
            var sCenter = clusterS.massCenter;
            var tCenter = clusterT.massCenter;

            if (networkData.Is2D)
            {
                sCenter.z = 0.25f;
                tCenter.z = 0.25f;
            }
            // Between focus and context
            if (clusterS.focus && !clusterT.focus)
            {
                return new[] {
                    sourceNode.Position3D,
                    (sCenter + tCenter) / 2,
                    tCenter,
                    targetNode.Position3D
                };
            }
            if (clusterT.focus && !clusterS.focus)
            {
                return new[] {
                    sourceNode.Position3D,
                    sCenter,
                    (sCenter + tCenter) / 2,
                    targetNode.Position3D
                };
            }

            Vector3[] result = new Vector3[link.pathInTree.Count];

            for (int i = 0; i < link.pathInTree.Count; i++)
            {
                result[i] = link.pathInTree[i].Position3D;
            }

            return result;
        }
    }
}