
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public static class HighContrastColors
    {
        // generate color that is high contrast
        // https://www.researchgate.net/figure/Kellys-22-colours-of-maximum-contrast_fig2_237005166
        // once kelly colors are exhausted, generate random hsv
        static int index = 0;
        static string[] kellys = { "#fdfdfd", /* "#1d1d1d", */ "#ebce2b", "#702c8c", "#db6917", "#96cde6", "#ba1c30", "#c0bd7f", "#7f7e80", "#5fa641", "#d485b2", "#4277b6", "#df8461", "#463397", "#e1a11a", "#91218c", "#e8e948", "#7e1510", "#92ae31", "#6f340d", "#d32b1e", "#2b3514" };

        public static Color GenerateRandomColor()
        {
            if (index < kellys.Length)
            {
                ColorUtility.TryParseHtmlString(kellys[index++], out var color);
                return color;
            }
            return Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
        }

        public static void ResetRandomColor()
        {
            index = 0;
            Random.InitState(42);
        }
    }
}