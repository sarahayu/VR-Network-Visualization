using System;
using UnityEngine;
using TMPro;

namespace VidiGraph
{
    public static class ContextMenuUtils
    {
        public static GameObject MakeOption(GameObject prefab, Transform transform, string text, float angle)
        {
            GameObject optionObj = UnityEngine.Object.Instantiate(prefab, transform);

            optionObj.transform.Rotate(Vector3.up, angle);
            optionObj.transform.Rotate(Vector3.right, -45);
            optionObj.GetComponentInChildren<TextMeshProUGUI>().SetText(text);

            return optionObj;
        }

        public static int GetHoveredOpt(float angle, int optCount)
        {
            if (optCount == 0) return -1;

            float totPhi = 275f;

            float phi = totPhi / optCount;
            float curAngle = -totPhi / 2 + phi / 2;

            float minAnglDiff = 10000f;
            int optionMinDiff = -1;

            for (int i = 0; i < optCount; i++)
            {
                float curDiff = Math.Abs(angle - curAngle);
                if (curDiff < minAnglDiff)
                {
                    minAnglDiff = curDiff;
                    optionMinDiff = i;
                }

                curAngle += phi;
            }

            return optionMinDiff;
        }
    }
}