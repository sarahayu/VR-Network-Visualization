using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
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
        [SerializeField]
        XRInputButtonReader CommandPress = new XRInputButtonReader("CommandPress");

        [SerializeField]
        GameObject _tooltip;
        [SerializeField]
        Transform _buttonsTransform;
        [SerializeField]
        GameObject _optionPrefab;

        [SerializeField]
        Color _btnHighlight = new Color(200f / 255, 200f / 255, 200f / 255);
        [SerializeField]
        Color _btnDefault = new Color(94f / 255, 94f / 255, 94f / 255);

        NetworkManager _networkManager;
        SurfaceManager _surfaceManager;
        XRInteractionManager _xrManager;

        Community _hoveredCommunity = null;
        Node _hoveredNode = null;

        List<Tuple<string, GameObject>> _curOptions = new List<Tuple<string, GameObject>>();

        HashSet<string> _lastOptions = new HashSet<string>();

        string _lastOptionLabel = "";

        bool _cancelUpcomingDeselection = false;

        TextMeshProUGUI _infoCol1;
        TextMeshProUGUI _infoCol2;

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
        Coroutine _dupeListenerCR = null;

        public void Initialize()
        {
            _networkManager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _surfaceManager = GameObject.Find("/Surface Manager").GetComponent<SurfaceManager>();
            _xrManager = GameObject.Find("/XR Interaction Manager").GetComponent<XRInteractionManager>();

            _tooltip.SetActive(false);
            _infoCol1 = _tooltip.GetNamedChild("NodeInfo_1").GetComponent<TextMeshProUGUI>();
            _infoCol2 = _tooltip.GetNamedChild("NodeInfo_2").GetComponent<TextMeshProUGUI>();

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

        void OnEnable()
        {
            LeftGripPress.EnableDirectActionIfModeUsed();
            LeftTriggerPress.EnableDirectActionIfModeUsed();
            RightGripPress.EnableDirectActionIfModeUsed();
            RightTriggerPress.EnableDirectActionIfModeUsed();
            Thumbstick.EnableDirectActionIfModeUsed();
            ThumbstickClick.EnableDirectActionIfModeUsed();

            CommandPress.EnableDirectActionIfModeUsed();
        }

        void Update()
        {
            if (_surfaceManager.IsMovingSurface)
            {
                _cancelUpcomingDeselection = true;
            }

            if (UpdateSelection()) { }
            else if (ToggleSphericalAndHairball()) { }
            else if (CheckSelectionActions()) { }
            else if (RunExperimentalCommand()) { }

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

        public bool CheckSelectionActions()
        {
            var newOpts = _networkManager.GetValidOptions();

            if (!newOpts.SetEquals(_lastOptions))
            {
                CreateNewOptionBtns(newOpts);

                _lastOptions = newOpts;
            }

            CheckThumbstickRotation();

            return CheckThumbstickClick();
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
            var thumbVal = Thumbstick.ReadValue();

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

        bool CheckThumbstickClick()
        {
            if (ThumbstickClick.ReadWasPerformedThisFrame())
            {
                switch (_lastOptionLabel)
                {
                    case "Bring Node":
                        _networkManager.BringMLNodes(_networkManager.SelectedNodes);
                        break;
                    case "Reset Node(s)":
                        _networkManager.ReturnMLNodes(_networkManager.SelectedNodes);
                        break;
                    case "Focus Comm.":
                        _networkManager.SetMLLayout(_networkManager.SelectedCommunities, "cluster");
                        break;
                    case "Project Comm. Floor":
                        _networkManager.SetMLLayout(_networkManager.SelectedCommunities, "floor");
                        break;
                    default:
                        return false;
                }

                return true;
            }

            return false;
        }

        bool UpdateSelection()
        {
            if (!RightGripPress.ReadWasCompletedThisFrame()) return false;

            if (!_cancelUpcomingDeselection)
            {
                if (_hoveredCommunity == null && _hoveredNode == null)
                {
                    _networkManager.ClearSelection();
                }
            }

            _cancelUpcomingDeselection = false;

            return true;
        }

        bool ToggleSphericalAndHairball()
        {
            if (!LeftGripPress.ReadWasPerformedThisFrame()) return false;

            _networkManager.ToggleBigNetworkSphericalAndHairball();

            return true;
        }

        bool RunExperimentalCommand()
        {
            if (!CommandPress.ReadWasPerformedThisFrame()) return false;

            var nodeIDs1 = _networkManager.NetworkGlobal.RealNodes.GetRange(0, 10);
            var nodeIDs2 = _networkManager.NetworkGlobal.RealNodes.GetRange(10, 10);
            var linkIDs1 = _networkManager.NetworkGlobal.Links.Values.ToList().GetRange(0, 10).Select(l => l.ID);
            var linkIDs2 = _networkManager.NetworkGlobal.Links.Values.ToList().GetRange(10, 10).Select(l => l.ID);

            _networkManager.SetMLNodesSize(nodeIDs1, 4);
            _networkManager.SetMLNodesColor(nodeIDs2, "#FF0000");
            _networkManager.SetMLLinksWidth(linkIDs1, 3f);
            _networkManager.SetMLLinksColorStart(linkIDs2, "#00FF00");
            _networkManager.SetMLLinksColorEnd(linkIDs1, "#00FFFF");
            _networkManager.SetMLLinksAlpha(linkIDs2, 0.4f);

            return true;
        }

        void CreateNewOptionBtns(HashSet<string> opts)
        {
            foreach (var (_, go) in _curOptions) Destroy(go);

            _curOptions.Clear();

            if (opts.Count == 0) return;

            float totPhi = 275f;

            float phi = totPhi / opts.Count;
            float curAngle = -totPhi / 2 + phi / 2;

            foreach (var label in opts)
            {
                var btn = ContextMenuUtils.MakeOption(_optionPrefab, _buttonsTransform, label, curAngle);
                _curOptions.Add(new Tuple<string, GameObject>(label, btn));

                curAngle += phi;
            }
        }

        string[] GetPropsStr(Node node, int split)
        {
            var filenodes = _networkManager.FileLoader.SphericalLayout.nodes;

            Dictionary<string, object> labelAndID = new Dictionary<string, object>()
            {
                {"label", node.Label},
                {"id", node.ID},
            };

            var props = labelAndID.Concat(ObjectUtils.AsDictionary(filenodes[node.IdxProcessed].props)).ToDictionary(k => k.Key, k => k.Value);

            int counter = 0;

            var splitProps = props.GroupBy(_ => counter++ % split).Select(d => d.ToDictionary(e => e.Key, e => e.Value));

            return splitProps.Select(splitProp =>
                splitProp.Aggregate("", (propStr, propPair) =>
                {
                    return propStr += "<b><size=70%>" + propPair.Key + "</size></b>\n"
                        + (propPair.Value ?? "<i>no info</i>") + "\n"
                        + "<size=50%> </size>\n";
                })
            ).ToArray();
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

                        _dupeListenerCR = StartCoroutine(CRDupeListen(evt.interactorObject, evt.interactableObject));
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

                    CoroutineUtils.StopIfRunning(this, ref _dupeListenerCR);
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

                    _tooltip.SetActive(true);

                    var halves = GetPropsStr(node, 2);

                    _infoCol1.SetText(halves.Length >= 1 ? halves[0] : "");
                    _infoCol2.SetText(halves.Length >= 2 ? halves[1] : "");
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

                    _tooltip.SetActive(false);
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

                        _dupeListenerCR = StartCoroutine(CRDupeListen(evt.interactorObject, evt.interactableObject));
                    }
                    else
                    {
                        _networkManager.StartMLNodeMove(node.ID);
                        _clickWindowCR = StartCoroutine(CRSelectionWindow());

                        _dupeListenerCR = StartCoroutine(CRDupeListen(evt.interactorObject, evt.interactableObject));

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

                    CoroutineUtils.StopIfRunning(this, ref _dupeListenerCR);
                }
            }
        }

        // sometimes controller moves too fast and we ungrab outside of node,
        // resulting in the unhover not registering due to an invalid state change.
        // account for this by manually unhovering if no re-hovering event is
        // detected after ungrab
        IEnumerator CRDelayUnhoverNode()
        {
            yield return new WaitForSeconds(0.1f);

            _lastState = ActionState.UnhoverNode;
            _tooltip.SetActive(false);

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

        // listen for trigger press that triggers a duplication
        IEnumerator CRDupeListen(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            while (true)
            {
                if (RightTriggerPress.ReadIsPerformed())
                {
                    _xrManager.SelectExit(interactor, interactable);

                    _networkManager.CreateSubnetwork(_networkManager.SubnSelectedNodes(-1), -1);
                    break;
                }

                yield return null;
            }

            _dupeListenerCR = null;
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