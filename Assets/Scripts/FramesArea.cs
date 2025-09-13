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

        public Frame AddFrame(int ID,
            string displayName = null,
            Action<Frame> onClick = null)
        {
            if (Frames.ContainsKey(ID)) throw new System.Exception($"Frame exists with key {ID}");

            var frame = Instantiate(_framesPrefab, transform).GetComponent<Frame>();

            frame.transform.position += _curPosOffset;
            frame.GetComponentInChildren<TextMeshPro>().SetText($"{displayName ?? ID.ToString()}");
            frame.GetComponent<XRGrabInteractable>().selectExited.AddListener(_ => onClick?.Invoke(frame));
            frame.GetComponent<XRGrabInteractable>().hoverEntered.AddListener(_ => frame.SetHover(true));
            frame.GetComponent<XRGrabInteractable>().hoverExited.AddListener(_ => frame.SetHover(false));

            Frames[ID] = frame;

            _curPosOffset += Spacing;

            return frame;
        }

        public void RemoveFrame(int ID)
        {
            Destroy(Frames[ID].gameObject);

            Frames.Remove(ID);
        }
    }
}