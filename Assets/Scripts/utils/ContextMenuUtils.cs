using System;
using System.Collections;
using System.Collections.Generic;
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
    }

}