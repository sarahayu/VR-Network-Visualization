using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public static class BSplineMathUtils
    {
        public static Vector3[] ControlPoints(Link link, NetworkDataStructure networkData, NetworkContext3D networkProperties)
        {
            //TODO There has to some better way to do this, and also to provide a constant number of control points

            var sourceNode = link.sourceNode;
            var targetNode = link.targetNode;
            var sourceNodeProps = networkProperties.Nodes[link.sourceIdx];
            var targetNodeProps = networkProperties.Nodes[link.targetIdx];

            var sCenter = networkProperties.Communities[sourceNode.communityIdx].MassCenter;
            var tCenter = networkProperties.Communities[targetNode.communityIdx].MassCenter;
            var sFocus = networkData.Communities[sourceNode.communityIdx].focus;
            var tFocus = networkData.Communities[targetNode.communityIdx].focus;

            if (networkData.Is2D)
            {
                sCenter.z = 0.25f;
                tCenter.z = 0.25f;
            }
            // Between focus and context
            if (sFocus && !tFocus)
            {
                return new[] {
                    sourceNodeProps.Position,
                    (sCenter + tCenter) / 2,
                    tCenter,
                    targetNodeProps.Position
                };
            }
            if (tFocus && !sFocus)
            {
                return new[] {
                    sourceNodeProps.Position,
                    sCenter,
                    (sCenter + tCenter) / 2,
                    targetNodeProps.Position
                };
            }

            Vector3[] result = new Vector3[link.pathInTree.Count];

            for (int i = 0; i < link.pathInTree.Count; i++)
            {
                result[i] = networkProperties.Nodes[link.pathInTree[i].id].Position;
            }

            return result;
        }
    }
}