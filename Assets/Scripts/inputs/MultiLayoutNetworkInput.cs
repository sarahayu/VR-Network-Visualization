using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VidiGraph
{
    public class MultiLayoutNetworkInput : NetworkInput
    {
        // TODO there's gotta be a better way to do this
        [SerializeField]
        XRInputButtonReader LeftGripPress = new XRInputButtonReader("LeftGripPress");
        [SerializeField]
        XRInputButtonReader LeftTriggerPress = new XRInputButtonReader("LeftTriggerPress");
        [SerializeField]
        XRInputButtonReader RightGripPress = new XRInputButtonReader("RightGripPress");
        [SerializeField]
        XRInputButtonReader RightTriggerPress = new XRInputButtonReader("RightTriggerPress");
        [SerializeField]
        XRInputValueReader<Vector2> Thumbstick = new XRInputValueReader<Vector2>("Thumbstick");
        [SerializeField]
        XRInputButtonReader ThumbstickClick = new XRInputButtonReader("ThumbstickClick");

        NetworkManager _manager;

        Community _hoveredCommunity = null;
        Node _hoveredNode = null;

        public TextMeshProUGUI TooltipText;
        public Transform ButtonsTransform;
        public GameObject OptionPrefab;

        List<Tuple<string, GameObject>> CurOptions = new List<Tuple<string, GameObject>>();

        HashSet<string> LastOptions = new HashSet<string>();

        string lastOptLabel = "";

        public override void Initialize()
        {
            _manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();

            var renderer = GetComponentInChildren<NetworkRenderer>();

            renderer.OnCommunityHoverEnter += OnCommunityHoverEnter;
            renderer.OnCommunityHoverExit += OnCommunityHoverExit;

            renderer.OnNodeHoverEnter += OnNodeHoverEnter;
            renderer.OnNodeHoverExit += OnNodeHoverExit;
        }

        void OnEnable()
        {
            LeftGripPress.EnableDirectActionIfModeUsed();
            LeftTriggerPress.EnableDirectActionIfModeUsed();
            RightGripPress.EnableDirectActionIfModeUsed();
            RightTriggerPress.EnableDirectActionIfModeUsed();
            Thumbstick.EnableDirectActionIfModeUsed();
            ThumbstickClick.EnableDirectActionIfModeUsed();
        }

        void Start()
        {
            // ContextMenuUtils.MakeOption(OptionPrefab, ButtonsTransform, "test1", -45);
            // ContextMenuUtils.MakeOption(OptionPrefab, ButtonsTransform, "test2", 45);
        }

        void Update()
        {
            if (RightGripPress.ReadWasPerformedThisFrame())
            {
                if (_hoveredCommunity != null)
                {
                    // _manager.CycleCommunityFocus(_hoveredCommunity.ID);

                    _manager.ToggleSelectedCommunities(new List<int> { _hoveredCommunity.ID });
                }
                else if (_hoveredNode != null)
                {
                    _manager.ToggleSelectedNodes(new List<int> { _hoveredNode.ID });
                }
                else
                {
                    _manager.ClearSelection();
                }
            }

            var curOpts = _manager.GetValidOptions();

            if (!curOpts.SetEquals(LastOptions))
            {
                foreach (var (_, go) in CurOptions)
                {
                    UnityEngine.Object.Destroy(go);
                }

                CurOptions.Clear();

                CreateOptionBtns(curOpts);

                LastOptions = curOpts;
            }

            // if (RightSecondaryButton.ReadWasPerformedThisFrame())
            // {
            //     foreach (var commID in _manager.SelectedCommunities)
            //     {
            //         _manager.SetLayout(commID, "spherical");
            //     }
            // }

            if (LeftGripPress.ReadWasPerformedThisFrame())
            {
                _manager.ToggleBigNetworkSphericalAndHairball();
            }

            var thumbVal = Thumbstick.ReadValue();
            string curOptnLabel = "";

            if (thumbVal != Vector2.zero)
            {
                float angle = -Vector2.SignedAngle(Vector2.up, thumbVal);

                int optInd = GetHoveredOpt(angle);

                int curInd = 0;
                foreach (var (label, go) in CurOptions)
                {
                    if (curInd == optInd)
                    {
                        go.GetComponentInChildren<Image>().color = new Color(200f / 255, 200f / 255, 200f / 255);
                        curOptnLabel = label;

                    }
                    else
                    {
                        go.GetComponentInChildren<Image>().color = new Color(94f / 255, 94f / 255, 94f / 255);
                    }

                    curInd++;
                }

                lastOptLabel = curOptnLabel;
            }
            else if (lastOptLabel != curOptnLabel)
            {
                foreach (var (_, go) in CurOptions)
                {
                    go.GetComponentInChildren<Image>().color = new Color(94f / 255, 94f / 255, 94f / 255);

                }
                lastOptLabel = curOptnLabel;
            }

            if (ThumbstickClick.ReadWasPerformedThisFrame())
            {
                switch (curOptnLabel)
                {
                    case "Bring Node":
                        _manager.BringNodes(_manager.SelectedNodes.ToList());
                        break;
                    case "Return Node":
                        _manager.ReturnNodes(_manager.SelectedNodes.ToList());
                        break;
                    case "Bring Comm.":
                        _manager.SetLayout(_manager.SelectedCommunities.ToList(), "cluster");
                        break;
                    case "Return Comm.":
                        _manager.SetLayout(_manager.SelectedCommunities.ToList(), "spherical");
                        break;
                    case "Project Comm. Floor":
                        _manager.SetLayout(_manager.SelectedCommunities.ToList(), "floor");
                        break;
                }
            }
        }

        int GetHoveredOpt(float angle)
        {
            if (CurOptions.Count == 0) return -1;

            float totPhi = 275f;

            float phi = totPhi / CurOptions.Count;
            float curAngle = -totPhi / 2 + phi / 2;

            float minAnglDiff = 10000f;
            int optionMinDiff = -1;

            for (int i = 0; i < CurOptions.Count; i++)
            {
                float curDiff = Math.Abs(angle - curAngle);
                if (curDiff < minAnglDiff)
                {
                    minAnglDiff = curDiff;
                    optionMinDiff = i;
                }

                curAngle += phi;
            }

            return optionMinDiff;
        }

        void CreateOptionBtns(HashSet<string> opts)
        {
            if (opts.Count == 0) return;

            float totPhi = 275f;

            float phi = totPhi / opts.Count;
            float curAngle = -totPhi / 2 + phi / 2;

            foreach (var label in opts)
            {
                var btn = ContextMenuUtils.MakeOption(OptionPrefab, ButtonsTransform, label, curAngle);
                CurOptions.Add(new Tuple<string, GameObject>(label, btn));

                curAngle += phi;
            }
        }

        void OnCommunityHoverEnter(Community community, HoverEnterEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                _manager.HoverCommunity(community.ID);
                _hoveredCommunity = community;
            }
        }

        void OnCommunityHoverExit(Community community, HoverExitEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                _manager.UnhoverCommunity(community.ID);
                _hoveredCommunity = null;
            }
        }

        void OnNodeHoverEnter(Node node, HoverEnterEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                _manager.HoverNode(node.ID);
                _hoveredNode = node;

                TooltipText.SetText(node.Label);
            }
        }

        void OnNodeHoverExit(Node node, HoverExitEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                _manager.UnhoverNode(node.ID);
                _hoveredNode = null;
            }
        }
    }

}