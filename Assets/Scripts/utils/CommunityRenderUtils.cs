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

        public static GameObject UpdateCommunity(GameObject commObj, MultiLayoutContext.Community community)
        {
            commObj.transform.localPosition = community.MassCenter;
            commObj.transform.localScale = Vector3.one * (float)community.Size;
            return commObj;
        }
    }

}