using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VidiGraph
{
    public static class BasicSubnetworkUtils
    {
        public static BasicSubnetwork CreateBasicSubnetwork(GameObject prefab, Transform transform,
            IEnumerable<int> nodeIDs, MultiLayoutContext sourceContext, out GameObject gameObj)
        {
            gameObj = UnityEngine.Object.Instantiate(prefab, transform);

            BasicSubnetwork retVal = gameObj.GetComponent<BasicSubnetwork>();

            retVal.Initialize(nodeIDs, sourceContext);

            return retVal;
        }
    }
}