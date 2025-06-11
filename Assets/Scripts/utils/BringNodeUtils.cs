using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public static class BringNodeUtils
    {
        public static Vector3 GetDestinationPoint(Vector3 nodePos, Transform viewer, float spread, float offset)
        {
            var focalPoint = viewer.position + viewer.forward * offset;
            var finalPos = Vector3.Lerp(focalPoint, nodePos, spread / Vector3.Distance(nodePos, focalPoint));

            return finalPos;
        }
    }
}