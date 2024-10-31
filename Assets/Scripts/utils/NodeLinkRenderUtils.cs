using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace VidiGraph
{

    public static class NodeLinkRenderUtils
    {
        public static GameObject MakeNode(GameObject prefab, Transform transform, Node node)
        {
            return MakeNode(prefab, transform, node, node.colorParsed);
        }
        public static GameObject MakeNode(GameObject prefab, Transform transform, Node node, Color color)
        {
            GameObject nodeObj = UnityEngine.Object.Instantiate(prefab, transform);
            nodeObj.transform.localPosition = node.Position3D;

            MaterialPropertyBlock props = new MaterialPropertyBlock();
            props.SetColor("_Color", color);
            nodeObj.GetComponent<Renderer>().SetPropertyBlock(props);

            return nodeObj;
        }

        public static GameObject MakeStraightLink(GameObject prefab, Transform transform, Link link, float linkWidth)
        {
            GameObject linkObj = UnityEngine.Object.Instantiate(prefab, transform);

            return UpdateStraightLink(linkObj, link, linkWidth);
        }

        public static GameObject UpdateStraightLink(GameObject linkObj, Link link, float linkWidth)
        {
            var start = link.sourceNode.Position3D;
            var end = link.targetNode.Position3D;

            linkObj.transform.localPosition = (start + end) / 2.0f;
            linkObj.transform.localRotation = Quaternion.FromToRotation(Vector3.up, end - start);
            linkObj.transform.localScale = new Vector3(linkWidth, Vector3.Distance(start, end) * 0.5f, linkWidth);

            return linkObj;
        }

        public static GameObject MakeBSplineLink(GameObject prefab, Transform transform, Link link)
        {
            GameObject linkObj = UnityEngine.Object.Instantiate(prefab, transform);
            // currently do nothing
            return linkObj;
        }
    }

}