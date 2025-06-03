using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace VidiGraph
{

    public static class NodeLinkRenderUtils
    {
        public static GameObject MakeNode(GameObject prefab, Transform parent,
            Node node, MultiLayoutContext.Node nodeProps, float nodeScale)
        {
            GameObject nodeObj = UnityEngine.Object.Instantiate(prefab, parent);

            // node won't be selected anyways so just set select color to transparent
            return UpdateNode(nodeObj, node, nodeProps, nodeScale);
        }
        public static GameObject UpdateNode(GameObject nodeObj,
            Node node, MultiLayoutContext.Node nodeProps, float nodeScale, Renderer renderer = null)
        {
            nodeObj.transform.position = nodeProps.Position;
            nodeObj.transform.localScale = Vector3.one * nodeProps.Size * nodeScale;

            if (!renderer)
                renderer = nodeObj.GetComponentInChildren<Renderer>();

            MaterialPropertyBlock props = new MaterialPropertyBlock();

            renderer.GetPropertyBlock(props);
            props.SetColor("_Color", nodeProps.Color);
            renderer.SetPropertyBlock(props);

            return nodeObj;
        }
        public static GameObject SetNodeColor(GameObject nodeObj, Color color, Renderer renderer = null)
        {
            if (!renderer)
                renderer = nodeObj.GetComponentInChildren<Renderer>();

            MaterialPropertyBlock props = new MaterialPropertyBlock();

            renderer.GetPropertyBlock(props);
            props.SetColor("_Color", color);
            renderer.SetPropertyBlock(props);

            return nodeObj;
        }
        public static GameObject MakeCommunityNode(GameObject prefab, Transform parent,
            MinimapContext.Node nodeProps, float nodeScale)
        {
            GameObject nodeObj = UnityEngine.Object.Instantiate(prefab, parent);

            return UpdateCommunityNode(nodeObj, nodeProps, nodeScale);
        }
        public static GameObject UpdateCommunityNode(GameObject nodeObj,
            MinimapContext.Node nodeProps, float nodeScale, Renderer renderer = null)
        {
            nodeObj.transform.position = nodeProps.Position;
            nodeObj.transform.localScale = Vector3.one * nodeProps.Size * nodeScale;

            if (!renderer)
                renderer = nodeObj.GetComponentInChildren<Renderer>();

            MaterialPropertyBlock props = new MaterialPropertyBlock();

            renderer.GetPropertyBlock(props);
            props.SetColor("_Color", nodeProps.Color);
            renderer.SetPropertyBlock(props);

            return nodeObj;
        }


        public static GameObject MakeStraightLink(GameObject prefab, Transform parent,
            Vector3 startPos, Vector3 endPos, float linkWidth)
        {
            GameObject linkObj = UnityEngine.Object.Instantiate(prefab, parent);

            return UpdateStraightLink(linkObj, startPos, endPos, linkWidth);
        }

        public static GameObject UpdateStraightLink(GameObject linkObj,
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