using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace VidiGraph
{
    public static class CoroutineUtils
    {
        public static bool StopIfRunning(MonoBehaviour mb, Coroutine cr)
        {
            if (cr != null)
            {
                mb.StopCoroutine(cr);
                return true;
            }

            return false;
        }
    }
}