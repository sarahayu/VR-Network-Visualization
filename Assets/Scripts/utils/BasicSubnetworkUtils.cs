using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public static class BasicSubnetworkUtils
    {
        public static BasicSubnetwork CreateBasicSubnetwork(GameObject prefab, Transform transform,
            IEnumerable<int> nodeIDs, MultiLayoutContext sourceContext)
        {
            GameObject subnetworkGO = UnityEngine.Object.Instantiate(prefab, transform);

            BasicSubnetwork retVal = subnetworkGO.GetComponent<BasicSubnetwork>();

            retVal.Initialize(nodeIDs, sourceContext);

            return retVal;
        }

        public static void InitFromContext(MultiLayoutContext targetContext, MultiLayoutContext sourceContext)
        {
            foreach (var (nodeID, tNode) in targetContext.Nodes)
            {
                var sNode = sourceContext.Nodes[nodeID];

                tNode.Size = sNode.Size;
                tNode.Color = sNode.Color;
                tNode.Position = sNode.Position;

                tNode.Dirty = true;
            }

            foreach (var (linkID, tLink) in targetContext.Links)
            {
                var sLink = sourceContext.Links[linkID];

                tLink.Width = sLink.Width;
                tLink.BundlingStrength = sLink.BundlingStrength;
                tLink.ColorStart = sLink.ColorStart;
                tLink.ColorEnd = sLink.ColorEnd;
                tLink.BundleStart = sLink.BundleStart;
                tLink.BundleEnd = sLink.BundleEnd;
                tLink.Alpha = sLink.Alpha;

                tLink.Dirty = true;
            }

            targetContext.GetNodeSize = sourceContext.GetNodeSize;
            targetContext.GetNodeColor = sourceContext.GetNodeColor;
            targetContext.GetLinkWidth = sourceContext.GetLinkWidth;
            targetContext.GetLinkBundlingStrength = sourceContext.GetLinkBundlingStrength;
            targetContext.GetLinkColorStart = sourceContext.GetLinkColorStart;
            targetContext.GetLinkColorEnd = sourceContext.GetLinkColorEnd;
            targetContext.GetLinkBundleStart = sourceContext.GetLinkBundleStart;
            targetContext.GetLinkBundleEnd = sourceContext.GetLinkBundleEnd;
            targetContext.GetLinkAlpha = sourceContext.GetLinkAlpha;
        }
    }
}