using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public static class BSplineMathUtils
    {
        public static Vector3[] ControlPoints(Link link, NetworkGlobal networkData, MultiLayoutContext networkProperties)
        {
            //TODO There has to some better way to do this, and also to provide a constant number of control points

            var sourceNode = link.SourceNode;
            var targetNode = link.TargetNode;
            var sourceNodeProps = networkProperties.Nodes[link.SourceNodeID];
            var targetNodeProps = networkProperties.Nodes[link.TargetNodeID];

            var sCenter = networkProperties.Communities[sourceNode.CommunityID].MassCenter;
            var tCenter = networkProperties.Communities[targetNode.CommunityID].MassCenter;
            var sFocus = networkProperties.Links[link.ID].BundleStart;
            var tFocus = networkProperties.Links[link.ID].BundleEnd;

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

            Vector3[] result = new Vector3[link.PathInTree.Count];

            for (int i = 0; i < link.PathInTree.Count; i++)
            {
                result[i] = networkProperties.Nodes[link.PathInTree[i].ID].Position;
            }

            return result;
        }
    }
}