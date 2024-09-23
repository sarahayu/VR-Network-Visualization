using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{

    public static class NodeRenderer
    {
        public static GameObject MakeNode(GameObject nodeObj, Node node, Color? color)
        {
            nodeObj.transform.position = new Vector3(node.pos3D[0], node.pos3D[2], node.pos3D[1]);

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
    }

}