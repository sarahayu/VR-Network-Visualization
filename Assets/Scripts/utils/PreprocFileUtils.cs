using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public static class PreprocFileUtils
    {
        public static Node NodeFromPreprocNode(NodeFileData fileNode)
        {
            Node node = new Node();

            node.CommunityID = fileNode.communityIdx;
            node.Label = fileNode.label;

            // idx gets remapped to node id, because we do not assume it's the same as the order stated in datafile. why? idk the original code didn't either.
            node.ID = fileNode.idx;
            node.Color = fileNode.color;

            if (node.Color != null)
                node.ColorParsed = ColorUtils.StringToColor(node.Color.ToUpper());

            node.IsVirtualNode = fileNode.virtualNode;
            node.Degree = fileNode.degree;
            node.Height = fileNode.height;
            node.AncID = fileNode.ancIdx;
            node.ChildIDs = fileNode.childIdx;
            node.PrecompPos3D = fileNode._position3D;

            return node;
        }

        public static Link LinkFromPreprocLink(LinkFileData fileLink)
        {
            Link link = new Link();

            link.IsSpline = fileLink.spline;
            link.ID = fileLink.linkIdx;
            link.SourceNodeID = fileLink.sourceIdx;
            link.TargetNodeID = fileLink.targetIdx;

            return link;
        }
    }
}