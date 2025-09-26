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
    public class TactileEditInteraction : NetworkInteraction
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

        // We need a state system because xrinteraction input is wacky.
        // When hovering to a community, then hovering to a node within, then back to the enclosing community, it follows the following events:
        //     1. hover over community: CommunityHoverEnter
        //     2. hover to the node inside: CommunityHoverExit -> NodeHoverEnter
        //     3. hover back to enclosing community: NodeHoverExit -> CommunityHoverEnter
        // HOWEVER, when switching from hovering a node to grabbing it, it follows the following:
        //     1. hover over node: NodeHoverEnter
        //     2. grab the hovered node: NodeGrabEnter -> NodeHoverExit
        //     3. ungrab the node: NodeHoverEnter -> NodeGrabExit
        // Notice that in step 2, GrabEnter precedes HoverExit, which is COUNTERINTUITIVE to hovering community -> hovering node event sequence,
        //     where NodeHoverExit precedes CommunityHoverEnter.
        // So, we'll use action states to keep track of the order of actions (e.g. step 2, hovering a node immediately after grabbing it wouldn't make sense)

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
            HoverNetwork,
            UnhoverNetwork,
            GrabNetwork,
            UngrabNetwork,
            None,
        }

        ActionState _lastState = ActionState.None;

        Coroutine _clickWindowCR = null;
        Action _dupeCB = null;
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

            renderer.OnCommunityGrabEnter += OnCommunityGrabEnter;
            renderer.OnCommunityGrabExit += OnCommunityGrabExit;

            renderer.OnNodeGrabEnter += OnNodeGrabEnter;
            renderer.OnNodeGrabExit += OnNodeGrabExit;

            renderer.OnNetworkHoverEnter += OnNetworkHoverEnter;
            renderer.OnNetworkHoverExit += OnNetworkHoverExit;

            renderer.OnNetworkGrabEnter += OnNetworkGrabEnter;
            renderer.OnNetworkGrabExit += OnNetworkGrabExit;
        }

        void OnDestroy()
        {
            var renderer = GetComponentInChildren<NetworkRenderer>();

            renderer.OnCommunityHoverEnter -= OnCommunityHoverEnter;
            renderer.OnCommunityHoverExit -= OnCommunityHoverExit;

            renderer.OnNodeHoverEnter -= OnNodeHoverEnter;
            renderer.OnNodeHoverExit -= OnNodeHoverExit;

            renderer.OnCommunityGrabEnter -= OnCommunityGrabEnter;
            renderer.OnCommunityGrabExit -= OnCommunityGrabExit;

            renderer.OnNodeGrabEnter -= OnNodeGrabEnter;
            renderer.OnNodeGrabExit -= OnNodeGrabExit;

            renderer.OnNetworkHoverEnter -= OnNetworkHoverEnter;
            renderer.OnNetworkHoverExit -= OnNetworkHoverExit;

            renderer.OnNetworkGrabEnter -= OnNetworkGrabEnter;
            renderer.OnNetworkGrabExit -= OnNetworkGrabExit;
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
                if (_lastState == ActionState.HoverComm || _lastState == ActionState.UngrabComm)
                {
                    _lastState = ActionState.UnhoverComm;
                }
            }
        }

        void OnCommunityGrabEnter(Community community, SelectEnterEventArgs evt)
        {
            if (!Enabled) return;

            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                if (_lastState == ActionState.HoverComm || _lastState == ActionState.UngrabComm)
                {
                    _lastState = ActionState.GrabComm;

                    var commGUID = _networkManager.CommunityIDToGUID[Tuple.Create(_subnetworkID, community.ID)];

                    if (_networkManager.SelectedCommunityGUIDs.Contains(commGUID))
                    {
                        _networkManager.StartMLCommsFollow(
                            commGUID,
                            _networkManager.SelectedCommunityGUIDs);
                        _networkManager.StartMLCommsMove(_networkManager.SelectedCommunityGUIDs);

                        _clickWindowCR = StartCoroutine(CRSelectionWindow());

                        DupeListen(evt.interactorObject, evt.interactableObject, DoCommunityGrabExit);
                    }
                    else
                    {
                        _networkManager.StartMLCommMove(community.ID, _subnetworkID);
                        _clickWindowCR = StartCoroutine(CRSelectionWindow());
                    }

                }

            }
        }

        void OnCommunityGrabExit(Community community, SelectExitEventArgs evt)
        {
            if (!Enabled) return;

            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                if (_lastState == ActionState.GrabComm)
                {
                    DoCommunityGrabExit();
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
                if (_lastState == ActionState.HoverNode || _lastState == ActionState.UngrabNode)
                {
                    _lastState = ActionState.UnhoverNode;
                }
            }
        }

        void OnNodeGrabEnter(Node node, SelectEnterEventArgs evt)
        {
            if (!Enabled) return;

            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                if (_lastState == ActionState.HoverNode || _lastState == ActionState.UngrabNode)
                {
                    _lastState = ActionState.GrabNode;

                    var nodeGUID = _networkManager.NodeIDToGUID[Tuple.Create(_subnetworkID, node.ID)];

                    if (_networkManager.SelectedNodeGUIDs.Contains(nodeGUID))
                    {
                        _networkManager.StartMLNodesFollow(
                            nodeGUID,
                            _networkManager.SelectedNodeGUIDs);
                        _networkManager.StartMLNodesMove(_networkManager.SelectedNodeGUIDs);

                        _clickWindowCR = StartCoroutine(CRSelectionWindow());

                        DupeListen(evt.interactorObject, evt.interactableObject, DoNodeGrabExit);
                    }
                    else
                    {
                        _networkManager.StartMLNodeMove(nodeGUID);
                        _clickWindowCR = StartCoroutine(CRSelectionWindow());

                        DupeListen(evt.interactorObject, evt.interactableObject, DoNodeGrabExit);

                    }

                }
            }
        }

        void OnNodeGrabExit(Node node, SelectExitEventArgs evt)
        {
            if (!Enabled) return;

            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                if (_lastState == ActionState.GrabNode)
                {
                    DoNodeGrabExit();
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
                if (_lastState == ActionState.HoverNetwork || _lastState == ActionState.UngrabNetwork)
                {
                    _lastState = ActionState.UnhoverNetwork;
                }
            }
        }

        void OnNetworkGrabEnter(NetworkContext network, SelectEnterEventArgs evt)
        {
            if (!Enabled) return;

            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                if (_lastState == ActionState.HoverNetwork || _lastState == ActionState.UngrabNetwork)
                {
                    _lastState = ActionState.GrabNetwork;

                    // TODO support multiple selected subgraph movement? would have to figure out cross-graph node/comm movement...
                    if (_networkManager.SelectedNetworks.Contains(_subnetworkID))
                    {

                        _networkManager.StartMLNetworksFollow(
                            _subnetworkID,
                            _networkManager.SelectedNetworks);
                        _networkManager.StartMLNetworksMove(_networkManager.SelectedNetworks);

                        _clickWindowCR = StartCoroutine(CRSelectionWindow());

                        DupeListen(evt.interactorObject, evt.interactableObject, DoNetworkGrabExit);
                    }
                    else
                    {
                        _networkManager.StartMLNetworkMove(_subnetworkID);
                        _clickWindowCR = StartCoroutine(CRSelectionWindow());

                        DupeListen(evt.interactorObject, evt.interactableObject, DoNetworkGrabExit);

                    }

                }
            }
        }

        void OnNetworkGrabExit(NetworkContext network, SelectExitEventArgs evt)
        {
            if (!Enabled) return;

            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                if (_lastState == ActionState.GrabNetwork)
                {
                    DoNetworkGrabExit();
                }
            }
        }

        void DoCommunityGrabExit()
        {
            _lastState = ActionState.UngrabComm;

            _networkManager.EndMLCommsFollow();
            _networkManager.EndMLCommsMove();

            if (_clickWindowCR != null)
            {
                _networkManager.ToggleSelectedCommunities(new List<int> { _hoveredCommunity.ID }, _subnetworkID);

                CoroutineUtils.StopIfRunning(this, ref _clickWindowCR);
            }

            CoroutineUtils.StopIfRunning(this, ref _unhoverCommCR);
            _unhoverCommCR = StartCoroutine(CRDelayUnhoverComm());

            if (_dupeCB != null)
            {
                _inputManager.RightTriggerListener -= _dupeCB;
                _dupeCB = null;
            }
        }

        void DoNodeGrabExit()
        {
            _lastState = ActionState.UngrabNode;

            _networkManager.EndMLNodesFollow();
            _networkManager.EndMLNodesMove();

            if (_clickWindowCR != null)
            {
                _networkManager.ToggleSelectedNodes(new List<int> { _hoveredNode.ID }, _subnetworkID);

                CoroutineUtils.StopIfRunning(this, ref _clickWindowCR);
            }

            CoroutineUtils.StopIfRunning(this, ref _unhoverNodeCR);
            _unhoverNodeCR = StartCoroutine(CRDelayUnhoverNode());

            if (_dupeCB != null)
            {
                _inputManager.RightTriggerListener -= _dupeCB;
                _dupeCB = null;
            }
        }

        void DoNetworkGrabExit()
        {
            _lastState = ActionState.UngrabNetwork;

            _networkManager.EndMLNetworksFollow();
            _networkManager.EndMLNetworksMove();

            if (_clickWindowCR != null)
            {
                _networkManager.ToggleSelectedNetworks(new List<int> { _subnetworkID });

                CoroutineUtils.StopIfRunning(this, ref _clickWindowCR);
            }

            CoroutineUtils.StopIfRunning(this, ref _unhoverNetworkCR);
            _unhoverNetworkCR = StartCoroutine(CRDelayUnhoverNetwork());

            if (_dupeCB != null)
            {
                _inputManager.RightTriggerListener -= _dupeCB;
                _dupeCB = null;
            }
        }

        // listen for trigger press that triggers a duplication
        void DupeListen(IXRSelectInteractor interactor, IXRSelectInteractable interactable, Action unselect)
        {
            _inputManager.RightTriggerListener += _dupeCB = () =>
            {
                var selecteds = _networkManager.SubnSelectedNodes(_subnetworkID);

                unselect.Invoke();

                var newSubnetworkID = _networkManager.CreateSubnetwork(selecteds, useShell: true, sourceSubnetworkID: _subnetworkID).SubnetworkID;

                var subnInteractable = _networkManager
                    .GetMLNetworkTransform(newSubnetworkID)
                    .gameObject
                    .GetComponent<XRGrabInteractable>();

                var rightCont = _inputManager.RightController.GetComponentInChildren<NearFarInteractor>();

                // TODO only deselect the ones that were originally selected...
                _networkManager.ClearSelection();
                // _networkManager.SetSelectedNodes(selecteds, false, _subnetworkID);

                _networkManager.SetSelectedNodes(selecteds, true, newSubnetworkID);
                _xrManager.HoverEnter(rightCont, (IXRHoverInteractable)subnInteractable);
                _xrManager.SelectEnter(rightCont, (IXRSelectInteractable)subnInteractable);

                // have to do selectexit for the original object last to avoid the above selectenter to not yank the selectable to the controller
                _xrManager.SelectExit(interactor, interactable);

                _inputManager.RightTriggerListener -= _dupeCB;
                _dupeCB = null;
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

        // same thing as above, but with network
        IEnumerator CRDelayUnhoverNetwork()
        {
            yield return new WaitForSeconds(0.1f);

            _lastState = ActionState.UnhoverNetwork;

            _unhoverNetworkCR = null;
        }

        // simple way to detect if we did a click instead of drag
        IEnumerator CRSelectionWindow()
        {
            yield return new WaitForSeconds(0.25f);

            _clickWindowCR = null;
        }
    }

}