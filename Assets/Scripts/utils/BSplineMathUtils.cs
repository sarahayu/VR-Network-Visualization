using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VidiGraph
{
    public static class BSplineMathUtils
    {
        public static Vector3[] ControlPoints(Link link, NetworkGlobal networkData, NodeLinkContext networkProperties)
        {
            //TODO There has to some better way to do this, and also to provide a constant number of control points

            var sourceNode = link.SourceNode;
            var targetNode = link.TargetNode;
            var sourceNodeProps = networkProperties.Nodes[link.SourceNodeID];
            var targetNodeProps = networkProperties.Nodes[link.TargetNodeID];

            if (networkProperties.Links[link.ID].BundlingStrength < 0.001f)
            {
                return new[] {
                    sourceNodeProps.Position,
                    Vector3.Lerp(sourceNodeProps.Position, targetNodeProps.Position, 0.5f),
                    targetNodeProps.Position
                };
            }

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

            return link.PathInTree
                .Select(n => networkProperties.Nodes[n.ID].Position)
                .ToArray();
        }
    }
}