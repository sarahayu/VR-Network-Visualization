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

            node.communityIdx = fileNode.communityIdx;
            node.label = fileNode.label;

            // idx gets remapped to node id, because we do not assume it's the same as the order stated in datafile. why? idk the original code didn't either.
            node.id = fileNode.idx;
            node.color = fileNode.color;

            if (node.color != null)
                node.colorParsed = ColorUtils.StringToColor(node.color.ToUpper());

            node.virtualNode = fileNode.virtualNode;
            node.degree = fileNode.degree;
            node.height = fileNode.height;
            node.ancIdx = fileNode.ancIdx;
            node.childIdx = fileNode.childIdx;
            node.precompPos3D = fileNode._position3D;

            return node;
        }

        public static Link LinkFromPreprocLink(LinkFileData fileLink)
        {
            Link link = new Link();

            link.spline = fileLink.spline;
            link.linkIdx = fileLink.linkIdx;
            link.sourceIdx = fileLink.sourceIdx;
            link.targetIdx = fileLink.targetIdx;

            return link;
        }
    }
}