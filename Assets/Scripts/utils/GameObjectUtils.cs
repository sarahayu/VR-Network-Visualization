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

        public static void SetColor(GameObject gameObject, Color color)
        {
            SetColor(gameObject.GetComponent<Renderer>(), color);
        }

        public static void SetColor(Renderer renderer, Color color)
        {
#if UNITY_EDITOR
            // the below doesn't work if we want to color multiple nodes different colors
            // renderer.sharedMaterial.color = color;

            MaterialPropertyBlock props = new MaterialPropertyBlock();

            renderer.GetPropertyBlock(props);
            props.SetColor("_Color", color);
            renderer.SetPropertyBlock(props);
#elif UNITY_STANDALONE
            renderer.material.color = color;
#endif
        }

        public static Color GetColor(GameObject gameObject)
        {
            return GetColor(gameObject.GetComponent<Renderer>());
        }

        public static Color GetColor(Renderer renderer)
        {
#if UNITY_EDITOR
            // I don't know if this will work for nodes of different colors...
            return renderer.sharedMaterial.color;
#elif UNITY_STANDALONE
            return renderer.material.color;
#endif
        }
    }
}