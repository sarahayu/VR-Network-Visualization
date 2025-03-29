using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{

    public static class CommunityRenderUtils
    {

        public static GameObject MakeCommunity(GameObject prefab, Transform transform, MultiLayoutContext.Community community)
        {
            GameObject commObj = UnityEngine.Object.Instantiate(prefab, transform);

            return UpdateCommunity(commObj, community);
        }

        public static GameObject UpdateCommunity(GameObject commObj, MultiLayoutContext.Community community, Renderer renderer = null)
        {
            commObj.transform.localPosition = community.MassCenter;
            commObj.transform.localScale = Vector3.one * (float)community.Size;

            if (!renderer)
                renderer = commObj.GetComponentInChildren<Renderer>();

            MaterialPropertyBlock props = new MaterialPropertyBlock();

            renderer.GetPropertyBlock(props);
            props.SetColor("_Color", new Color(0f, 0f, 0f, 0f));
            renderer.SetPropertyBlock(props);

            return commObj;
        }

        public static GameObject SetCommunityColor(GameObject commObj, Color color, Renderer renderer = null)
        {
            if (!renderer)
                renderer = commObj.GetComponentInChildren<Renderer>();

            MaterialPropertyBlock props = new MaterialPropertyBlock();

            renderer.GetPropertyBlock(props);
            props.SetColor("_Color", color);
            renderer.SetPropertyBlock(props);

            return commObj;
        }
    }

}