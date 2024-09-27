using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{

    public static class NodeLinkRenderer
    {
        public static GameObject MakeNode(GameObject prefab, Transform transform, Node node, Color? color)
        {
            GameObject nodeObj = UnityEngine.Object.Instantiate(prefab, transform);
            nodeObj.transform.localPosition = MathUtils.ArrToVec(node.pos3D);

            Color colorActual;

            if (color != null)
            {
                colorActual = (Color)color;
            }
            else
            {
                colorActual = ColorUtils.StringToColor(node.color.ToUpper());
            }

            MaterialPropertyBlock props = new MaterialPropertyBlock();
            props.SetColor("_Color", colorActual);
            nodeObj.GetComponent<Renderer>().SetPropertyBlock(props);

            return nodeObj;
        }

        public static GameObject MakeStraightLink(GameObject prefab, Transform transform, Link link, float linkWidth)
        {
            GameObject linkObj = UnityEngine.Object.Instantiate(prefab, transform);
            var start = MathUtils.ArrToVec(link.sourceNode.pos3D);
            var end = MathUtils.ArrToVec(link.targetNode.pos3D);

            linkObj.transform.localPosition = (start + end) / 2.0f;
            linkObj.transform.localRotation = Quaternion.FromToRotation(Vector3.up, end - start);
            linkObj.transform.localScale = new Vector3(linkWidth, Vector3.Distance(start, end) * 0.5f, linkWidth);

            return linkObj;
        }

        public static GameObject MakeBSplineLink(GameObject linkObj, Link link)
        {
            // currently do nothing
            return linkObj;
        }
    }

}