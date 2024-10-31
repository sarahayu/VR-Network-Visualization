using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VidiGraph
{
    public class NetworkInput : MonoBehaviour
    {
        // TODO there's gotta be a better way to do this
        public XRInputButtonReader LeftGripPress = new XRInputButtonReader("LeftGripPress");
        public XRInputButtonReader LeftTriggerPress = new XRInputButtonReader("LeftTriggerPress");
        public XRInputButtonReader RightGripPress = new XRInputButtonReader("RightGripPress");
        public XRInputButtonReader RightTriggerPress = new XRInputButtonReader("RightTriggerPress");

        Network _network;

        Community _hoveredCommunity = null;

        public void Initialize()
        {
            _network = GetComponent<Network>();

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
                    _network.ToggleCommunityFocus(_hoveredCommunity.communityIdx);
                }
            }

            if (LeftGripPress.ReadWasPerformedThisFrame() && LeftTriggerPress.ReadWasPerformedThisFrame())
            {
                if (_network.CurLayout == "spherical")
                {
                    _network.ChangeToLayout("hairball");
                }
                else if (_network.CurLayout == "hairball")
                {
                    _network.ChangeToLayout("spherical");
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