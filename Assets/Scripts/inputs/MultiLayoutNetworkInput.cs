using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VidiGraph
{
    public class MultiLayoutNetworkInput : NetworkInput
    {
        // TODO there's gotta be a better way to do this
        public XRInputButtonReader LeftGripPress = new XRInputButtonReader("LeftGripPress");
        public XRInputButtonReader LeftTriggerPress = new XRInputButtonReader("LeftTriggerPress");
        public XRInputButtonReader RightGripPress = new XRInputButtonReader("RightGripPress");
        public XRInputButtonReader RightTriggerPress = new XRInputButtonReader("RightTriggerPress");
        public XRInputButtonReader RightPrimaryButton = new XRInputButtonReader("RightPrimaryButton");

        NetworkManager _manager;

        Community _hoveredCommunity = null;
        Node _hoveredNode = null;

        public TextMeshProUGUI TooltipText;

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
            RightPrimaryButton.EnableDirectActionIfModeUsed();
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
                    _manager.CycleCommunityFocus(_hoveredCommunity.ID);
                }
                else if (_hoveredNode != null)
                {
                    _manager.ToggleFocusNodes(new int[] { _hoveredNode.ID });
                }
            }

            if (LeftGripPress.ReadWasPerformedThisFrame())
            {
                _manager.ToggleBigNetworkSphericalAndHairball();
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