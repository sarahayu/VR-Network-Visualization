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
            rotation = Quaternion.FromToRotation(Vector3.up, -origin.forward);
        }
    }
}