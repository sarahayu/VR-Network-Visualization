using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VidiGraph
{
    public class BigNetworkInput : NetworkInput
    {
        // TODO there's gotta be a better way to do this
        public XRInputButtonReader LeftGripPress = new XRInputButtonReader("LeftGripPress");
        public XRInputButtonReader LeftTriggerPress = new XRInputButtonReader("LeftTriggerPress");
        public XRInputButtonReader RightGripPress = new XRInputButtonReader("RightGripPress");
        public XRInputButtonReader RightTriggerPress = new XRInputButtonReader("RightTriggerPress");

        NetworkManager _manager;

        Community _hoveredCommunity = null;

        public override void Initialize()
        {
            _manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();

            var renderer = GetComponentInChildren<NetworkRenderer>();

            renderer.OnCommunityHoverEnter += OnCommunityHoverEnter;
            renderer.OnCommunityHoverExit += OnCommunityHoverExit;
        }

        void OnEnable()
        {
            LeftGripPress.EnableDirectActionIfModeUsed();
            LeftTriggerPress.EnableDirectActionIfModeUsed();
            RightGripPress.EnableDirectActionIfModeUsed();
            RightTriggerPress.EnableDirectActionIfModeUsed();
        }

        void Start()
        {
        }

        void Update()
        {
            if (RightGripPress.ReadWasPerformedThisFrame())
            {
                if (_hoveredCommunity != null)
                {
                    _manager.ToggleCommunityFocus(_hoveredCommunity.ID);
                }
            }

            if (LeftGripPress.ReadWasPerformedThisFrame() && LeftTriggerPress.ReadWasPerformedThisFrame())
            {
                if (_manager.CurBigLayout == "spherical")
                {
                    _manager.ChangeToLayout("hairball");
                }
                else if (_manager.CurBigLayout == "hairball")
                {
                    _manager.ChangeToLayout("spherical");
                }
            }
        }

        void OnCommunityHoverEnter(Community community, HoverEnterEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                _hoveredCommunity = community;
            }
        }

        void OnCommunityHoverExit(Community community, HoverExitEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                _hoveredCommunity = null;
            }
        }
    }

}