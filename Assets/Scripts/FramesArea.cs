using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;


namespace VidiGraph
{
    public class FramesArea : MonoBehaviour
    {
        public partial class Frame { }

        public Vector3 Spacing;

        public Dictionary<int, Frame> Frames = new();

        [SerializeField] GameObject _framesPrefab;
        [SerializeField] Color _hoverColor;
        [SerializeField] Color _selectColor;

        Vector3 _curPosOffset = Vector3.zero;

        void OnEnable()
        {
            Frame.HoverColor = _hoverColor;
            Frame.SelectColor = _selectColor;
        }

        public void AddFrame(int ID,
            string displayName = null,
            Action<Frame> onClick = null)
        {
            if (Frames.ContainsKey(ID)) throw new System.Exception($"Frame exists with key {ID}");

            var frame = new Frame(Instantiate(_framesPrefab, transform));

            frame.FrameObject.transform.position += _curPosOffset;
            frame.FrameObject.GetComponentInChildren<TextMeshPro>().SetText($"{displayName ?? ID.ToString()}");
            frame.FrameObject.GetComponent<XRGrabInteractable>().selectExited.AddListener(_ => onClick?.Invoke(frame));
            frame.FrameObject.GetComponent<XRGrabInteractable>().hoverEntered.AddListener(_ => frame.SetHover(true));
            frame.FrameObject.GetComponent<XRGrabInteractable>().hoverExited.AddListener(_ => frame.SetHover(false));

            Frames[ID] = frame;

            _curPosOffset += Spacing;
        }

        public void RemoveFrame(int ID)
        {
            Destroy(Frames[ID].FrameObject);

            Frames.Remove(ID);
        }

        /*=============== start Frame class ===================*/

        public partial class Frame
        {
            public GameObject FrameObject;
            public Color OrigColor;

            public static Color HoverColor;
            public static Color SelectColor;

            public Frame(GameObject frameObject)
            {
                FrameObject = frameObject;
                OrigColor = GameObjectUtils.GetColor(FrameObject.GetNamedChild("Wood"));

                Debug.Log(OrigColor.ToString());
            }

            public void SetColor(Color color, float alpha = -1)
            {
                Color newCol = color;

                // if alpha is not close enough to default value of -1, assume custom alpha vlaue
                if (Mathf.Abs(alpha + 1) > 0.001f)
                    newCol.a = alpha;

                GameObjectUtils.SetColor(FrameObject.GetNamedChild("Wood"), newCol);
            }

            public void SetShellColor(Color color, float alpha = -1)
            {
                Color newCol = color;

                // if alpha is not close enough to default value of -1, assume custom alpha vlaue
                if (Mathf.Abs(alpha + 1) > 0.001f)
                    newCol.a = alpha;

                GameObjectUtils.SetColor(FrameObject.GetNamedChild("Shell"), newCol);
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

        /*=============== end Frame class ===================*/
    }
}