using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace VidiGraph
{
    public static class CoroutineUtils
    {
        public static bool StopIfRunning(MonoBehaviour mb, ref Coroutine cr)
        {
            if (cr != null)
            {
                mb.StopCoroutine(cr);
                cr = null;
                return true;
            }

            return false;
        }
    }
}