using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public static class GameObjectUtils
    {
        public static void ChildrenDestroyImmediate(Transform transform)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(transform.GetChild(i).gameObject);
            }
        }

        public static void ChildrenDestroy(Transform transform)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(transform.GetChild(i).gameObject);
            }
        }

        public static void LerpTransform(Transform toLerp, Transform start, Transform end, float t)
        {
            toLerp.position = Vector3.Lerp(start.position, end.position, t);
            toLerp.localScale = Vector3.Lerp(start.localScale, end.localScale, t);
        }

        public static void LerpTransform(TransformInfo toLerp, TransformInfo start, TransformInfo end, float t)
        {
            toLerp.position = Vector3.Lerp(start.position, end.position, t);
            toLerp.scale = Vector3.Lerp(start.scale, end.scale, t);
        }
    }
}