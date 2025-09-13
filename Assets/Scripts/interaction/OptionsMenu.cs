using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace VidiGraph
{
    public class OptionsMenu : MonoBehaviour
    {
        [SerializeField] GameObject _optionPrefab;

        [SerializeField] Color _btnHighlight = new(200f / 255, 200f / 255, 200f / 255);
        [SerializeField] Color _btnDefault = new(94f / 255, 94f / 255, 94f / 255);

        XRInputValueReader<Vector2> _rightJoystick;

        List<Tuple<string, GameObject>> _curOptions = new();
        Dictionary<string, Action> _actionCallbacks;

        string _lastOptionLabel = "";

        void OnEnable()
        {
        }

        void Start()
        {
            var inputManager = GameObject.Find("/Input Manager").GetComponent<InputManager>();

            _rightJoystick = inputManager.RightJoystick;

            inputManager.RightJoystickClickListener += Click;

            ClearOptions();
        }

        void Update()
        {
            CheckThumbstickRotation();
        }

        public void ClearOptions()
        {
            foreach (var (_, go) in _curOptions) Destroy(go);

            _curOptions.Clear();
        }

        public void SetOptions(Dictionary<string, Action> actionCallbacks)
        {
            _actionCallbacks = actionCallbacks;
            CreateNewOptionBtns(_actionCallbacks.Keys);
        }

        void Click()
        {
            if (_lastOptionLabel != null)
                _actionCallbacks[_lastOptionLabel]?.Invoke();
        }

        void UnhoverAllButtons()
        {
            foreach (var (_, go) in _curOptions)
            {
                go.GetComponentInChildren<Image>().color = _btnDefault;
            }
        }

        string UpdateHoveredButton(float angle)
        {
            string hoveredBtn = null;

            int optInd = ContextMenuUtils.GetHoveredOpt(angle, _curOptions.Count);

            int curInd = 0;
            foreach (var (label, go) in _curOptions)
            {
                if (curInd == optInd)
                {
                    go.GetComponentInChildren<Image>().color = _btnHighlight;
                    hoveredBtn = label;
                }
                else
                {
                    go.GetComponentInChildren<Image>().color = _btnDefault;
                }

                curInd++;
            }

            return hoveredBtn;
        }

        void CheckThumbstickRotation()
        {
            var thumbVal = _rightJoystick.ReadValue();

            if (thumbVal != Vector2.zero)
            {
                float angle = -Vector2.SignedAngle(Vector2.up, thumbVal);

                _lastOptionLabel = UpdateHoveredButton(angle);
            }
            else if (_lastOptionLabel != null)
            {
                UnhoverAllButtons();
                _lastOptionLabel = null;
            }
        }

        void CreateNewOptionBtns(IEnumerable<string> opts)
        {
            foreach (var (_, go) in _curOptions) Destroy(go);

            _curOptions.Clear();

            if (opts.Count() == 0) return;

            float totPhi = 275f;

            float phi = totPhi / opts.Count();
            float curAngle = -totPhi / 2 + phi / 2;

            foreach (var label in opts)
            {
                var btn = ContextMenuUtils.MakeOption(_optionPrefab, transform, label, curAngle);
                _curOptions.Add(new Tuple<string, GameObject>(label, btn));

                curAngle += phi;
            }
        }
    }
}