/*
*
* NodeLinkNetworkInteraction takes care of interaction logic for elements of MultiLayoutNetwork and BasicSubnetwork.
*
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VidiGraph
{
    public class HoverInteraction : NetworkInteraction
    {
        NetworkManager _networkManager;
        InputManager _inputManager;
        XRInteractionManager _xrManager;

        Community _hoveredCommunity = null;
        Node _hoveredNode = null;
        bool _hoveredNetwork = false;

        Coroutine _unhoverNodeCR = null;
        Coroutine _unhoverCommCR = null;
        Coroutine _unhoverNetworkCR = null;

        enum ActionState
        {
            HoverNode,
            UnhoverNode,
            HoverComm,
            UnhoverComm,
            HoverNetwork,
            UnhoverNetwork,

            None,
        }

        ActionState _lastState = ActionState.None;

        int _subnetworkID;

        void Awake()
        {
            _networkManager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _inputManager = GameObject.Find("/Input Manager").GetComponent<InputManager>();
            _xrManager = GameObject.Find("/XR Interaction Manager").GetComponent<XRInteractionManager>();
        }

        void Start()
        {
            _subnetworkID = GetComponent<Network>().ID;

            var renderer = GetComponentInChildren<NetworkRenderer>();

            renderer.OnCommunityHoverEnter += OnCommunityHoverEnter;
            renderer.OnCommunityHoverExit += OnCommunityHoverExit;

            renderer.OnNodeHoverEnter += OnNodeHoverEnter;
            renderer.OnNodeHoverExit += OnNodeHoverExit;

            renderer.OnNetworkHoverEnter += OnNetworkHoverEnter;
            renderer.OnNetworkHoverExit += OnNetworkHoverExit;
        }

        void OnDestroy()
        {
            var renderer = GetComponentInChildren<NetworkRenderer>();

            renderer.OnCommunityHoverEnter -= OnCommunityHoverEnter;
            renderer.OnCommunityHoverExit -= OnCommunityHoverExit;

            renderer.OnNodeHoverEnter -= OnNodeHoverEnter;
            renderer.OnNodeHoverExit -= OnNodeHoverExit;

            renderer.OnNetworkHoverEnter -= OnNetworkHoverEnter;
            renderer.OnNetworkHoverExit -= OnNetworkHoverExit;
        }

        void Update()
        {
            if (!Enabled) return;

            if (IsUnhoverNode(_lastState) && _hoveredNode != null)
            {
                _networkManager.UnhoverNode(_hoveredNode.ID);
                _hoveredNode = null;
            }

            if (IsUnhoverComm(_lastState) && _hoveredCommunity != null)
            {
                _networkManager.UnhoverCommunity(_hoveredCommunity.ID);
                _hoveredCommunity = null;
            }

            if (IsUnhoverNetwork(_lastState) && _hoveredNetwork)
            {
                _networkManager.UnhoverNetwork(_subnetworkID);
                _hoveredNetwork = false;
            }
        }

        static bool IsUnhoverNode(ActionState state)
        {
            return state == ActionState.UnhoverNode || state == ActionState.HoverComm;
        }

        static bool IsUnhoverComm(ActionState state)
        {
            return state == ActionState.UnhoverComm || state == ActionState.HoverNode || state == ActionState.HoverNetwork;
        }

        static bool IsUnhoverNetwork(ActionState state)
        {
            return state == ActionState.UnhoverNetwork || state == ActionState.HoverComm;
        }

        void OnCommunityHoverEnter(Community community, HoverEnterEventArgs evt)
        {
            if (!Enabled) return;

            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                CoroutineUtils.StopIfRunning(this, ref _unhoverCommCR);

                if (_lastState == ActionState.None || _lastState == ActionState.UnhoverComm || _lastState == ActionState.UnhoverNode || _lastState == ActionState.UnhoverNetwork)
                {
                    _lastState = ActionState.HoverComm;

                    _networkManager.HoverCommunity(community.ID);
                    _hoveredCommunity = community;
                }
            }
        }

        void OnCommunityHoverExit(Community community, HoverExitEventArgs evt)
        {
            if (!Enabled) return;

            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                if (_lastState == ActionState.HoverComm)
                {
                    _lastState = ActionState.UnhoverComm;
                }
            }
        }

        void OnNodeHoverEnter(Node node, HoverEnterEventArgs evt)
        {
            if (!Enabled) return;

            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                CoroutineUtils.StopIfRunning(this, ref _unhoverNodeCR);

                if (_lastState == ActionState.None || _lastState == ActionState.UnhoverNode || _lastState == ActionState.UnhoverComm)
                {
                    _lastState = ActionState.HoverNode;

                    _networkManager.HoverNode(node.ID);
                    _hoveredNode = node;
                }
            }
        }

        void OnNodeHoverExit(Node node, HoverExitEventArgs evt)
        {
            if (!Enabled) return;

            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                if (_lastState == ActionState.HoverNode)
                {
                    _lastState = ActionState.UnhoverNode;
                }
            }
        }

        void OnNetworkHoverEnter(NetworkContext network, HoverEnterEventArgs evt)
        {
            if (!Enabled) return;

            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                CoroutineUtils.StopIfRunning(this, ref _unhoverNetworkCR);

                if (_lastState == ActionState.None || _lastState == ActionState.UnhoverNetwork || _lastState == ActionState.UnhoverComm)
                {
                    _lastState = ActionState.HoverNetwork;

                    _networkManager.HoverNetwork(_subnetworkID);
                    _hoveredNetwork = true;
                }
            }
        }

        void OnNetworkHoverExit(NetworkContext network, HoverExitEventArgs evt)
        {
            if (!Enabled) return;

            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                if (_lastState == ActionState.HoverNetwork)
                {
                    _lastState = ActionState.UnhoverNetwork;
                }
            }
        }
    }

}