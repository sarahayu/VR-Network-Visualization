using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{

    public static class CommunityRenderUtils
    {

        public static GameObject MakeCommunity(GameObject prefab, Transform transform, Community community)
        {
            GameObject commObj = UnityEngine.Object.Instantiate(prefab, transform);

            return UpdateCommunity(commObj, community);
        }

        public static GameObject UpdateCommunity(GameObject commObj, Community community)
        {
            commObj.transform.localPosition = community.massCenter;
            commObj.transform.localScale = Vector3.one * (float)community.size;

            return commObj;
        }
    }

}