using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace VidiGraph
{
    public class Frame : MonoBehaviour
    {
        Color OrigColor;

        public static Color HoverColor;
        public static Color SelectColor;

        void Start()
        {
            OrigColor = GameObjectUtils.GetColor(gameObject.GetNamedChild("Wood"));

            Debug.Log(OrigColor.ToString());
        }

        public void SetColor(Color color, float alpha = -1)
        {
            Color newCol = color;

            // if alpha is not close enough to default value of -1, assume custom alpha vlaue
            if (Mathf.Abs(alpha + 1) > 0.001f)
                newCol.a = alpha;

            GameObjectUtils.SetColor(gameObject.GetNamedChild("Wood"), newCol);
        }

        public void SetShellColor(Color color, float alpha = -1)
        {
            Color newCol = color;

            // if alpha is not close enough to default value of -1, assume custom alpha vlaue
            if (Mathf.Abs(alpha + 1) > 0.001f)
                newCol.a = alpha;

            GameObjectUtils.SetColor(gameObject.GetNamedChild("Shell"), newCol);
        }

        public void SetHover(bool hovered)
        {
            if (hovered) SetColor(HoverColor);
            else SetColor(OrigColor);
        }

        public void SetSelect(bool selected)
        {
            if (selected) SetShellColor(SelectColor);
            else SetShellColor(Color.black, 0f);
        }
    }
}