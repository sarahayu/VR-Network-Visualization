using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace VidiGraph
{

    public static class NodeLinkRenderUtils
    {
        public static GameObject MakeNode(GameObject prefab, Transform parent, Node node, NetworkContext3D.Node nodeProps)
        {
            return MakeNode(prefab, parent, node, nodeProps, node.ColorParsed);
        }
        public static GameObject MakeNode(GameObject prefab, Transform parent, Node node, NetworkContext3D.Node nodeProps, Color color)
        {
            GameObject nodeObj = UnityEngine.Object.Instantiate(prefab, parent);
            nodeObj.transform.localPosition = nodeProps.Position;

            var renderer = nodeObj.GetComponentInChildren<Renderer>();
            MaterialPropertyBlock props = new MaterialPropertyBlock();

            renderer.GetPropertyBlock(props);
            props.SetColor("_Color", color);

            renderer.SetPropertyBlock(props);

            return nodeObj;
        }

        public static GameObject MakeStraightLink(GameObject prefab, Transform parent,
            Link link, Vector3 startPos, Vector3 endPos, float linkWidth)
        {
            GameObject linkObj = UnityEngine.Object.Instantiate(prefab, parent);

            return UpdateStraightLink(linkObj, link, startPos, endPos, linkWidth);
        }

        public static GameObject UpdateStraightLink(GameObject linkObj, Link link,
            Vector3 startPos, Vector3 endPos, float linkWidth)
        {
            linkObj.transform.localPosition = (startPos + endPos) / 2.0f;
            linkObj.transform.localRotation = Quaternion.FromToRotation(Vector3.up, endPos - startPos);
            linkObj.transform.localScale = new Vector3(linkWidth, Vector3.Distance(startPos, endPos) * 0.5f, linkWidth);

            return linkObj;
        }

        public static GameObject MakeBSplineLink(GameObject prefab, Transform parent, Link link)
        {
            GameObject linkObj = UnityEngine.Object.Instantiate(prefab, parent);
            // currently do nothing
            return linkObj;
        }
    }

}