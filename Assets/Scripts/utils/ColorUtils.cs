using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{

    public static class ColorUtils
    {
        public static Color StringToColor(string colorStr)
        {
            Color outColor;

            if (!ColorUtility.TryParseHtmlString(colorStr, out outColor))
            {
                outColor = Color.white;
            }

            return outColor;
        }
    }

}