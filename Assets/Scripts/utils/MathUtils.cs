using System;
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
        public static float TWO_SQRT_SIX = 2 * Mathf.Sqrt(6);
        public static LineDistItem LineDistSq(float x, float y, float x1, float y1, float x2, float y2)
        {
            //https://stackoverflow.com/a/6853926

            var A = x - x1;
            var B = y - y1;
            var C = x2 - x1;
            var D = y2 - y1;

            var dot = A * C + B * D;
            var len_sq = C * C + D * D;
            var param = -1f;
            if (len_sq != 0) //in case of 0 length line
                param = dot / len_sq;

            float xx, yy;

            if (param < 0)
            {
                xx = x1;
                yy = y1;
            }
            else if (param > 1)
            {
                xx = x2;
                yy = y2;
            }
            else
            {
                xx = x1 + param * C;
                yy = y1 + param * D;
            }

            var dx = x - xx;
            var dy = y - yy;

            return new LineDistItem { param = param, dist_sq = dx * dx + dy * dy };
        }

        public static float clerp(float first, float second, float sag, float w)
        {
            if (sag < 0.0001) return Mathf.Lerp(first, second, w);
            var s = 1 + sag;
            var v = second - first;
            var a = 1 / (TWO_SQRT_SIX * Math.Sqrt(Math.Sqrt(s * s + v * v) - 1));
            var j = -a * Math.Log((first - second - Math.Sqrt(Math.Pow(second - first, 2) + a * a * Math.Pow(1 - Math.Exp(1 / a), 2) / Math.Exp(1 / a))) / (a * (1 - Math.Exp(1 / a))));
            var k = first - a * Math.Cosh(-j / a);
            var final = a * Math.Cosh((w - j) / a) + k;
            // print(j, k);
            return (float)final;
        }

        public static float rlerp(float c, float a, float b)
        {
            return (c - a) / (b - a);
        }

        // consider v=(x,y), returns how much along <v2-v1> the projection of v on it is
        public static float proj_factor(float x, float y, float x1, float y1, float x2, float y2)
        {
            float ux = x - x1, uy = y - y1, vx = x2 - x1, vy = y2 - y1;
            return (ux * vx + uy * vy) / (vx * vx + vy * vy);
        }

        public static float distSq(float x1, float y1, float x2, float y2)
        {
            return Mathf.Pow(x1 - x2, 2) + Mathf.Pow(y1 - y2, 2);
        }

        // http://blog.ivank.net/fastest-gaussian-blur.html
        // Photopea creator is the MVP
        public static void gaussBlur_4(float[] scl, float[] tcl, int w, int h, int r)
        {
            var bxs = boxesForGauss(r, 3);
            boxBlur_4(scl, tcl, w, h, (bxs[0] - 1) / 2);
            boxBlur_4(tcl, scl, w, h, (bxs[1] - 1) / 2);
            boxBlur_4(scl, tcl, w, h, (bxs[2] - 1) / 2);
        }

        static int[] boxesForGauss(float sigma, int n)  // standard deviation, number of boxes
        {
            var wIdeal = Mathf.Sqrt((12 * sigma * sigma / n) + 1);  // Ideal averaging filter width 
            var wl = Mathf.Floor(wIdeal); if (wl % 2 == 0) wl--;
            var wu = wl + 2;

            var mIdeal = (12 * sigma * sigma - n * wl * wl - 4 * n * wl - 3 * n) / (-4 * wl - 4);
            var m = Mathf.Round(mIdeal);
            // var sigmaActual = Math.sqrt( (m*wl*wl + (n-m)*wu*wu - n)/12 );

            var sizes = new int[n]; for (var i = 0; i < n; i++) sizes[i] = (int)(i < m ? wl : wu);
            return sizes;
        }
        static void boxBlur_4(float[] scl, float[] tcl, int w, int h, int r)
        {
            for (var i = 0; i < scl.Length; i++) tcl[i] = scl[i];
            boxBlurH_4(tcl, scl, w, h, r);
            boxBlurT_4(scl, tcl, w, h, r);
        }
        static void boxBlurH_4(float[] scl, float[] tcl, int w, int h, int r)
        {
            var iarr = 1f / (r + r + 1);
            for (var i = 0; i < h; i++)
            {
                int ti = i * w, li = ti, ri = ti + r;
                float fv = scl[ti], lv = scl[ti + w - 1], val = (float)(r + 1) * fv;
                for (var j = 0; j < r; j++) val += scl[ti + j];
                for (var j = 0; j <= r; j++) { val += scl[ri++] - fv; tcl[ti++] = val * iarr; }
                for (var j = r + 1; j < w - r; j++) { val += scl[ri++] - scl[li++]; tcl[ti++] = val * iarr; }
                for (var j = w - r; j < w; j++) { val += lv - scl[li++]; tcl[ti++] = val * iarr; }
            }
        }
        static void boxBlurT_4(float[] scl, float[] tcl, int w, int h, int r)
        {
            var iarr = 1f / (r + r + 1);
            for (var i = 0; i < w; i++)
            {
                int ti = i, li = ti, ri = ti + r * w;
                float fv = scl[ti], lv = scl[ti + w * (h - 1)], val = (float)(r + 1) * fv;
                for (var j = 0; j < r; j++) val += scl[ti + j * w];
                for (var j = 0; j <= r; j++) { val += scl[ri] - fv; tcl[ti] = val * iarr; ri += w; ti += w; }
                for (var j = r + 1; j < h - r; j++) { val += scl[ri] - scl[li]; tcl[ti] = val * iarr; li += w; ri += w; ti += w; }
                for (var j = h - r; j < h; j++) { val += lv - scl[li]; tcl[ti] = val * iarr; li += w; ti += w; }
            }
        }

        static int getX(int i, int w)
        {
            return i % w;
        }

        static int getY(int i, int w)
        {
            return i / w;
        }

        // https://stackoverflow.com/a/28572551
        public static void PopulateSunflowerPoints(List<Vector3> points, float radius, int n, int alpha)
        {
            int b = (int)Math.Round(alpha * Mathf.Sqrt(n));
            float phi = (Mathf.Sqrt(5) + 1) / 2;

            for (int k = 1; k <= n; k++)
            {
                float r = _GetSunflowerRad(k, n, b) * radius;
                float theta = 2 * Mathf.PI * k / Mathf.Pow(phi, 2);
                points.Add(new Vector3(r * Mathf.Cos(theta), 0, r * Mathf.Sin(theta)));
            }
        }

        static float _GetSunflowerRad(int k, int n, int b)
        {
            if (k > n - b)
                return 1f;

            return Mathf.Sqrt((k - 0.5f) / (n - (b + 1f) / 2f));
        }

        // KISS, generalize later...
        public static void Normalize2DPointsAsCircle<T>(Dictionary<T, float[]> pointMap, float padding = 0.05f)
        {
            float minX = Mathf.Infinity, maxX = Mathf.NegativeInfinity,
                minY = Mathf.Infinity, maxY = Mathf.NegativeInfinity;
            foreach (var points in pointMap.Values)
            {
                minX = Mathf.Min(minX, points[0]);
                maxX = Mathf.Max(maxX, points[0]);
                minY = Mathf.Min(minY, points[1]);
                maxY = Mathf.Max(maxY, points[1]);
            }

            float firstBound = (2 - Mathf.Sqrt(2)) / 4 + padding,
                secondBound = 1 - firstBound;

            foreach (var points in pointMap.Values)
            {
                points[0] = Mathf.Lerp(firstBound, secondBound,
                    Mathf.InverseLerp(minX, maxX, points[0]));
                points[1] = Mathf.Lerp(firstBound, secondBound,
                    Mathf.InverseLerp(minY, maxY, points[1]));
            }
        }

        // returns link weight as a percentage of the maximum possible number of links between two groups (which is the product of the two group sizes)
        public static float RelWeight(float linkWeight, float groupSize1, float groupSize2)
        {
            return linkWeight / (groupSize1 * groupSize2);
        }

        public static bool VecEqual(Vector3 a, Vector3 b, float delta = 0.01f)
        {
            return Vector3.Distance(a, b) < delta;
        }
    }

    public class LineDistItem
    {
        public float param;
        public float dist_sq;
    }
}