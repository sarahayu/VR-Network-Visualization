using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{

    public static class ColorUtils
    {
        public struct HSV
        {
            public float H, S, V, A;
        }
        public static Color StringToColor(string colorStr)
        {
            Color outColor;

            if (!ColorUtility.TryParseHtmlString(colorStr, out outColor))
            {
                outColor = Color.white;
            }

            return outColor;
        }

        // https://en.wikipedia.org/wiki/HSL_and_HSV
        public static HSV RGBToHSV(Color color)
        {
            float X_max = Mathf.Max(Mathf.Max(color.r, color.g), color.b);
            float X_min = Mathf.Min(Mathf.Min(color.r, color.g), color.b);

            float V = X_max;

            float C = X_max - X_min;

            float L = (X_max + X_min) / 2;

            float H;

            // C == 0
            if (C < 0.001f)
            {
                H = 0f;
            }
            // V == R
            else if (Mathf.Abs(V - color.r) < 0.001f)
            {
                H = 60f * (((color.g - color.b) / C) % 6);
            }
            // V == G
            else if (Mathf.Abs(V - color.g) < 0.001f)
            {
                H = 60f * (((color.b - color.r) / C) + 2);
            }
            // V == B
            else if (Mathf.Abs(V - color.b) < 0.001f)
            {
                H = 60f * (((color.r - color.g) / C) + 4);
            }
            else
            {
                H = 0f;
            }

            float S;

            // V == 0
            if (V < 0.001f)
            {
                S = 0f;
            }
            else
            {
                S = C / V;
            }

            return new()
            {
                H = H,
                S = S,
                V = V,
                A = color.a,
            };
        }

        public static Color HSVToRGB(HSV hsv)
        {
            Color rgb = new();
            rgb.a = hsv.A;

            float C = hsv.V * hsv.S;

            float H_p = hsv.H / 60;

            float X = C * (1 - Mathf.Abs(H_p % 2 - 1));

            if (H_p < 1)
            {
                rgb.r = C;
                rgb.g = X;
                rgb.b = 0f;
            }
            else if (H_p < 2)
            {
                rgb.r = X;
                rgb.g = C;
                rgb.b = 0f;
            }
            else if (H_p < 3)
            {
                rgb.r = 0f;
                rgb.g = C;
                rgb.b = X;
            }
            else if (H_p < 4)
            {
                rgb.r = 0f;
                rgb.g = X;
                rgb.b = C;
            }
            else if (H_p < 5)
            {
                rgb.r = X;
                rgb.g = 0f;
                rgb.b = C;
            }
            else if (H_p < 6)
            {
                rgb.r = C;
                rgb.g = 0f;
                rgb.b = X;
            }

            return rgb;
        }

        public static Color Saturate(Color color, float percToFullySaturated)
        {
            HSV hsv = RGBToHSV(color);

            hsv.S = Mathf.Lerp(hsv.S, 1, percToFullySaturated);

            return HSVToRGB(hsv);
        }
    }

}