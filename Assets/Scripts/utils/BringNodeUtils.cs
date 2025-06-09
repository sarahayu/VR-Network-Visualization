using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public static class BringNodeUtils
    {
        public static Vector3 GetFocalPoint(Transform trans, float spread)
        {
            return trans.position - trans.forward * spread;
        }
        public static Vector3 GetFocalPoint(Vector3 from, Vector3 to, float spread)
        {
            return from - Vector3.Normalize(to - from) * spread;
        }

        public static Vector3 GetDestinationPoint(Vector3 focalPoint, Vector3 startPos, float spread, float offset)
        {
            var dist = Vector3.Distance(focalPoint, startPos);
            var frac = (spread + offset) / dist;
            var finalPos = Vector3.Lerp(focalPoint, startPos, frac);

            return finalPos;
        }
    }
}