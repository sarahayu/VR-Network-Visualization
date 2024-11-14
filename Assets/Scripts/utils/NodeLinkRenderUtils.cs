using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace VidiGraph
{

    public static class NodeLinkRenderUtils
    {
        public static GameObject MakeNode(GameObject prefab, Transform transform, Node node, NetworkContext3D.Node nodeProps)
        {
            return MakeNode(prefab, transform, node, nodeProps, node.colorParsed);
        }
        public static GameObject MakeNode(GameObject prefab, Transform transform, Node node, NetworkContext3D.Node nodeProps, Color color)
        {
            GameObject nodeObj = UnityEngine.Object.Instantiate(prefab, transform);
            nodeObj.transform.localPosition = nodeProps.Position;

            MaterialPropertyBlock props = new MaterialPropertyBlock();
            props.SetColor("_Color", color);
            nodeObj.GetComponent<Renderer>().SetPropertyBlock(props);

            return nodeObj;
        }

        public static GameObject MakeStraightLink(GameObject prefab, Transform transform,
            Link link, Vector3 startPos, Vector3 endPos, float linkWidth)
        {
            GameObject linkObj = UnityEngine.Object.Instantiate(prefab, transform);

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

        public static GameObject MakeBSplineLink(GameObject prefab, Transform transform, Link link)
        {
            GameObject linkObj = UnityEngine.Object.Instantiate(prefab, transform);
            // currently do nothing
            return linkObj;
        }
    }

}