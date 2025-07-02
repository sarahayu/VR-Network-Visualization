using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace VidiGraph
{
    public static class TimerUtils
    {
        const bool DEBUG = true;
        static Dictionary<string, float> _timeMap = new Dictionary<string, float>();

        public static void StartTime(string label)
        {
            if (DEBUG)
            {
                _timeMap[label] = Time.realtimeSinceStartup;
                Debug.Log($"<color=cyan>[{label}]</color> Starting...");
            }
        }

        public static void EndTime(string label)
        {
            if (DEBUG)
            {
                _timeMap.Remove(label, out var start);
                Debug.Log($"<color=cyan>[{label}]</color> Finished! ({Math.Round((Time.realtimeSinceStartup - start) * 1000, 2)}ms)");
            }
        }
    }
}