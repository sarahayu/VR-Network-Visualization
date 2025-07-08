using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{

    public static class CommunityRenderUtils
    {

        public static GameObject MakeCommunity(GameObject prefab, Transform transform, Community commGlobal,
            MultiLayoutContext.Community commProps, int subnetworkID)
        {
            GameObject commObj = UnityEngine.Object.Instantiate(prefab, transform);

            // community won't be selected anyways so just set select color to transparent
            return UpdateCommunity(commObj, commGlobal, commProps, Color.clear, subnetworkID);
        }

        public static GameObject UpdateCommunity(GameObject commObj, Community commGlobal,
            MultiLayoutContext.Community commProps, Color selectColor, int subnetworkID, Renderer renderer = null)
        {
            commObj.transform.localPosition = commProps.MassCenter;
            commObj.transform.localScale = Vector3.one * (float)commProps.Size * 2;

            if (!renderer)
                renderer = commObj.GetComponentInChildren<Renderer>();

            MaterialPropertyBlock props = new MaterialPropertyBlock();

            renderer.GetPropertyBlock(props);
            props.SetColor("_Color", commGlobal.SelectedOnSubnetworks.Contains(subnetworkID) ? selectColor : new Color(0f, 0f, 0f, 0f));
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