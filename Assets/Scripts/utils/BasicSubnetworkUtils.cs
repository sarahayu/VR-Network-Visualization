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

        public static void SetContextSettings(MultiLayoutContext.Settings settings, BasicSubnetwork.Settings baseSettings)
        {
            settings.NodeScale = baseSettings.NodeScale;
            settings.LinkWidth = baseSettings.LinkWidth;
            settings.EdgeBundlingStrength = baseSettings.EdgeBundlingStrength;
            settings.CommSelectColor = baseSettings.CommSelectColor;
            settings.NodeSelectColor = baseSettings.NodeSelectColor;
            settings.LinkSelectColor = baseSettings.LinkSelectColor;
            settings.CommHoverColor = baseSettings.CommHoverColor;
            settings.NodeHoverColor = baseSettings.NodeHoverColor;
            settings.LinkHoverColor = baseSettings.LinkHoverColor;
            settings.LinkMinimumAlpha = baseSettings.LinkMinimumAlpha;
            settings.LinkNormalAlphaFactor = baseSettings.LinkNormalAlphaFactor;
            settings.LinkContextAlphaFactor = baseSettings.LinkContextAlphaFactor;
            settings.LinkContext2FocusAlphaFactor = baseSettings.LinkContext2FocusAlphaFactor;
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
        }
    }
}