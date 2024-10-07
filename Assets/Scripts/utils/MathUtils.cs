using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public static class MathUtils
    {
        public static Vector3 ArrToVec(float[] arr)
        {
            return new Vector3(arr[0], arr[1], arr[2]);
        }
    }
}