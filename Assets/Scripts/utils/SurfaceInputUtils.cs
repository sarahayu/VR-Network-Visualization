using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace VidiGraph
{
    public static class SurfaceInputUtils
    {
        public static void CalcPosAndRot(Transform origin, Vector3 offset, out Vector3 position, out Quaternion rotation)
        {
            position = origin.position + origin.rotation * offset;
            rotation = Quaternion.FromToRotation(Vector3.forward, new Vector3(origin.forward.x, 0, origin.forward.z)) * Quaternion.Euler(-90, 0, 0);
        }
    }
}