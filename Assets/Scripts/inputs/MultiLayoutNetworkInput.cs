/*
*
* MultiLayoutNetworkInput takes care of input logic for elements of the main multilayout.
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
    public class MultiLayoutNetworkInput : NetworkInput
    {
        NetworkManager _networkManager;
        InputManager _inputManager;
        XRInteractionManager _xrManager;

        Community _hoveredCommunity = null;
        Node _hoveredNode = null;

        Coroutine _unhoverNodeCR = null;
        Coroutine _unhoverCommCR = null;

        enum ActionState
        {
            HoverNode,
            UnhoverNode,
            GrabNode,
            UngrabNode,
            HoverComm,
            UnhoverComm,
            GrabComm,
            UngrabComm,
            None,
        }

        ActionState _lastState = ActionState.None;

        Coroutine _clickWindowCR = null;
        Coroutine _transformMoverCR = null;

        public void Initialize()
        {
            _networkManager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _inputManager = GameObject.Find("/Input Manager").GetComponent<InputManager>();
            _xrManager = GameObject.Find("/XR Interaction Manager").GetComponent<XRInteractionManager>();

            var renderer = GetComponentInChildren<NetworkRenderer>();

            renderer.OnCommunityHoverEnter += OnCommunityHoverEnter;
            renderer.OnCommunityHoverExit += OnCommunityHoverExit;

            renderer.OnNodeHoverEnter += OnNodeHoverEnter;
            renderer.OnNodeHoverExit += OnNodeHoverExit;

            renderer.OnCommunityGrabEnter += OnCommunityGrabEnter;
            renderer.OnCommunityGrabExit += OnCommunityGrabExit;

            renderer.OnNodeGrabEnter += OnNodeGrabEnter;
            renderer.OnNodeGrabExit += OnNodeGrabExit;
        }

        void Update()
        {
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
        }

        static bool IsUnhoverNode(ActionState state)
        {
            return state == ActionState.UnhoverNode || state == ActionState.HoverComm;
        }

        static bool IsUnhoverComm(ActionState state)
        {
            return state == ActionState.UnhoverComm || state == ActionState.HoverNode;
        }

        void OnCommunityHoverEnter(Community community, HoverEnterEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                CoroutineUtils.StopIfRunning(this, ref _unhoverCommCR);

                if (_lastState == ActionState.None || _lastState == ActionState.UnhoverComm || _lastState == ActionState.UnhoverNode)
                {
                    _lastState = ActionState.HoverComm;

                    _networkManager.HoverCommunity(community.ID);
                    _hoveredCommunity = community;
                }
            }
        }

        void OnCommunityHoverExit(Community community, HoverExitEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                if (_lastState == ActionState.HoverComm || _lastState == ActionState.UngrabComm)
                {
                    _lastState = ActionState.UnhoverComm;
                }
            }
        }

        void OnCommunityGrabEnter(Community community, SelectEnterEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                if (_lastState == ActionState.HoverComm || _lastState == ActionState.UngrabComm)
                {
                    _lastState = ActionState.GrabComm;

                    var selComms = _networkManager.SubnSelectedCommunities(-1);

                    if (selComms.Contains(community.ID))
                    {
                        _transformMoverCR = StartCoroutine(CRAllSelectedComms(community.ID, selComms));
                        _networkManager.StartMLCommsMove(selComms);

                        _clickWindowCR = StartCoroutine(CRSelectionWindow());

                        DupeListen(evt.interactorObject, evt.interactableObject);
                    }
                    else
                    {
                        _networkManager.StartMLCommMove(community.ID);
                        _clickWindowCR = StartCoroutine(CRSelectionWindow());
                    }

                }

            }
        }

        void OnCommunityGrabExit(Community community, SelectExitEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                if (_lastState == ActionState.GrabComm)
                {
                    _lastState = ActionState.UngrabComm;

                    _networkManager.EndMLCommsMove();
                    CoroutineUtils.StopIfRunning(this, ref _transformMoverCR);

                    if (_clickWindowCR != null)
                    {
                        _networkManager.ToggleSelectedCommunities(new List<int> { _hoveredCommunity.ID });

                        CoroutineUtils.StopIfRunning(this, ref _clickWindowCR);
                    }

                    CoroutineUtils.StopIfRunning(this, ref _unhoverCommCR);
                    _unhoverCommCR = StartCoroutine(CRDelayUnhoverComm());
                }
            }
        }

        void OnNodeHoverEnter(Node node, HoverEnterEventArgs evt)
        {
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
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                if (_lastState == ActionState.HoverNode || _lastState == ActionState.UngrabNode)
                {
                    _lastState = ActionState.UnhoverNode;
                }
            }
        }

        void OnNodeGrabEnter(Node node, SelectEnterEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                if (_lastState == ActionState.HoverNode || _lastState == ActionState.UngrabNode)
                {
                    _lastState = ActionState.GrabNode;

                    var selNodes = _networkManager.SubnSelectedNodes(-1);

                    if (selNodes.Contains(node.ID))
                    {
                        _transformMoverCR = StartCoroutine(CRAllSelectedNodes(node.ID, selNodes));
                        _networkManager.StartMLNodesMove(selNodes);

                        _clickWindowCR = StartCoroutine(CRSelectionWindow());

                        DupeListen(evt.interactorObject, evt.interactableObject);
                    }
                    else
                    {
                        _networkManager.StartMLNodeMove(node.ID);
                        _clickWindowCR = StartCoroutine(CRSelectionWindow());

                        DupeListen(evt.interactorObject, evt.interactableObject);

                    }

                }
            }
        }

        void OnNodeGrabExit(Node node, SelectExitEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                if (_lastState == ActionState.GrabNode)
                {
                    _lastState = ActionState.UngrabNode;

                    _networkManager.EndMLNodesMove();
                    CoroutineUtils.StopIfRunning(this, ref _transformMoverCR);

                    if (_clickWindowCR != null)
                    {
                        _networkManager.ToggleSelectedNodes(new List<int> { _hoveredNode.ID });

                        CoroutineUtils.StopIfRunning(this, ref _clickWindowCR);
                    }

                    CoroutineUtils.StopIfRunning(this, ref _unhoverNodeCR);
                    _unhoverNodeCR = StartCoroutine(CRDelayUnhoverNode());
                }
            }
        }

        // listen for trigger press that triggers a duplication
        void DupeListen(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            Action act = null;

            _inputManager.RightTriggerListener += act = () =>
            {
                _xrManager.SelectExit(interactor, interactable);

                _networkManager.CreateSubnetwork(_networkManager.SubnSelectedNodes(-1), -1);

                _inputManager.RightTriggerListener -= act;
            };
        }

        // sometimes controller moves too fast and we ungrab outside of node,
        // resulting in the unhover not registering due to an invalid state change.
        // account for this by manually unhovering if no re-hovering event is
        // detected after ungrab
        IEnumerator CRDelayUnhoverNode()
        {
            yield return new WaitForSeconds(0.1f);

            _lastState = ActionState.UnhoverNode;

            _unhoverNodeCR = null;
        }

        // same thing as above, but with community
        IEnumerator CRDelayUnhoverComm()
        {
            yield return new WaitForSeconds(0.1f);

            _lastState = ActionState.UnhoverComm;

            _unhoverCommCR = null;
        }

        // simple way to detect if we did a click instead of drag
        IEnumerator CRSelectionWindow()
        {
            yield return new WaitForSeconds(0.25f);

            _clickWindowCR = null;
        }

        IEnumerator CRAllSelectedNodes(int grabbedID, IEnumerable<int> nodeIDs)
        {
            Vector3 lastSurfPosition = Vector3.positiveInfinity;
            Quaternion lastSurfRotation = Quaternion.identity;

            var grabbedTransform = _networkManager.GetMLNodeTransform(grabbedID);
            var otherTransforms = nodeIDs.Where(nid => nid != grabbedID).Select(nid => _networkManager.GetMLNodeTransform(nid));

            while (true)
            {
                var curPosition = grabbedTransform.position;
                var curRotation = grabbedTransform.rotation;

                if (float.IsFinite(lastSurfPosition.x))
                {
                    var diff = curPosition - lastSurfPosition;
                    var diffRot = curRotation * Quaternion.Inverse(lastSurfRotation);
                    diffRot.ToAngleAxis(out var angle, out var axis);

                    foreach (var child in otherTransforms)
                    {
                        child.RotateAround(lastSurfPosition, axis, angle);

                        child.position += diff;
                    }
                }

                lastSurfPosition = curPosition;
                lastSurfRotation = curRotation;
                yield return null;
            }
        }

        IEnumerator CRAllSelectedComms(int grabbedID, IEnumerable<int> commIDs)
        {
            Vector3 lastSurfPosition = Vector3.positiveInfinity;
            Quaternion lastSurfRotation = Quaternion.identity;

            var grabbedTransform = _networkManager.GetMLCommTransform(grabbedID);
            var otherTransforms = commIDs.Where(cid => cid != grabbedID).Select(cid => _networkManager.GetMLCommTransform(cid));

            while (true)
            {
                var curPosition = grabbedTransform.position;
                var curRotation = grabbedTransform.rotation;

                if (float.IsFinite(lastSurfPosition.x))
                {
                    var diff = curPosition - lastSurfPosition;
                    var diffRot = curRotation * Quaternion.Inverse(lastSurfRotation);
                    diffRot.ToAngleAxis(out var angle, out var axis);

                    foreach (var child in otherTransforms)
                    {
                        child.RotateAround(lastSurfPosition, axis, angle);

                        child.position += diff;
                    }
                }

                lastSurfPosition = curPosition;
                lastSurfRotation = curRotation;
                yield return null;
            }
        }
    }

}