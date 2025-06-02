using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Runtime.InteropServices;
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
        [SerializeField]
        XRInputButtonReader CommandPress = new XRInputButtonReader("CommandPress");

        NetworkManager _networkManager;
        SurfaceManager _surfaceManager;

        Community _hoveredCommunity = null;
        Node _hoveredNode = null;

        public TextMeshProUGUI TooltipText;
        public Transform ButtonsTransform;
        public GameObject OptionPrefab;

        [SerializeField]
        Transform RightTransform;

        List<Tuple<string, GameObject>> CurOptions = new List<Tuple<string, GameObject>>();

        HashSet<string> LastOptions = new HashSet<string>();

        string lastOptLabel = "";

        Vector3 _startMovePos = Vector3.positiveInfinity;

        InteractionTimer _nodeHoverExit = new InteractionTimer();
        InteractionTimer _commHoverExit = new InteractionTimer();
        bool _cancelUpcomingDeselection = false;

        public override void Initialize()
        {
            _networkManager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _surfaceManager = GameObject.Find("/Surface Manager").GetComponent<SurfaceManager>();

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

        void Start()
        {
        }

        void Update()
        {
            if (_surfaceManager.IsMovingSurface)
            {
                _cancelUpcomingDeselection = true;
            }


            if (RightGripPress.ReadWasCompletedThisFrame())
            {
                if (!_cancelUpcomingDeselection)
                {
                    if (_hoveredCommunity != null)
                    {
                        _networkManager.ToggleSelectedCommunities(new List<int> { _hoveredCommunity.ID });
                    }
                    else if (_hoveredNode != null)
                    {
                        _networkManager.ToggleSelectedNodes(new List<int> { _hoveredNode.ID });
                    }
                    else
                    {
                        _networkManager.ClearSelection();
                    }
                }

                _cancelUpcomingDeselection = false;
            }
            else if (LeftGripPress.ReadWasPerformedThisFrame())
            {
                _networkManager.ToggleBigNetworkSphericalAndHairball();
            }
            else if (CheckSelectionActions()) { }
            else if (CommandPress.ReadWasPerformedThisFrame())
            {
                var nodeIDs1 = _networkManager.NetworkGlobal.RealNodes.GetRange(0, 10);
                var nodeIDs2 = _networkManager.NetworkGlobal.RealNodes.GetRange(10, 10);
                var linkIDs1 = _networkManager.NetworkGlobal.Links.GetRange(0, 10).Select(l => l.ID).ToList();
                var linkIDs2 = _networkManager.NetworkGlobal.Links.GetRange(10, 10).Select(l => l.ID).ToList();

                _networkManager.SetNodesSize(nodeIDs1, 2);
                _networkManager.SetNodesColor(nodeIDs2, Color.green);
                _networkManager.SetLinksWidth(linkIDs1, 0.5f);
                _networkManager.SetLinksColorStart(linkIDs2, Color.blue);
                _networkManager.SetLinksColorEnd(linkIDs1, Color.red);
                _networkManager.SetLinksAlpha(linkIDs2, 1);
            }

            if (_nodeHoverExit.TickAndCheckDidInteract() && _hoveredNode != null)
            {
                _networkManager.UnhoverNode(_hoveredNode.ID);
                _hoveredNode = null;
            }

            if (_commHoverExit.TickAndCheckDidInteract() && _hoveredCommunity != null)
            {
                _networkManager.UnhoverCommunity(_hoveredCommunity.ID);
                _hoveredCommunity = null;
            }
        }

        bool CheckSelectionActions()
        {
            var curOpts = _networkManager.GetValidOptions();

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

            var thumbVal = Thumbstick.ReadValue();
            string curOptnLabel = "";

            var highlightCol = new Color(200f / 255, 200f / 255, 200f / 255);
            var neutralCol = new Color(94f / 255, 94f / 255, 94f / 255);

            if (thumbVal != Vector2.zero)
            {
                float angle = -Vector2.SignedAngle(Vector2.up, thumbVal);

                int optInd = GetHoveredOpt(angle);

                int curInd = 0;
                foreach (var (label, go) in CurOptions)
                {
                    if (curInd == optInd)
                    {
                        go.GetComponentInChildren<Image>().color = highlightCol;
                        curOptnLabel = label;

                    }
                    else
                    {
                        go.GetComponentInChildren<Image>().color = neutralCol;
                    }

                    curInd++;
                }

                lastOptLabel = curOptnLabel;
            }
            else if (lastOptLabel != curOptnLabel)
            {
                foreach (var (_, go) in CurOptions)
                {
                    go.GetComponentInChildren<Image>().color = neutralCol;

                }
                lastOptLabel = curOptnLabel;
            }

            bool inputAction = false;

            if (ThumbstickClick.ReadWasPerformedThisFrame())
            {
                inputAction = true;
                switch (curOptnLabel)
                {
                    case "Bring Node":
                        _networkManager.BringMLNodes(_networkManager.SelectedNodes.ToList());
                        break;
                    case "Reset Node":
                        _networkManager.ReturnMLNodes(_networkManager.SelectedNodes.ToList());
                        break;
                    case "Bring Comm.":
                        _networkManager.SetMLLayout(_networkManager.SelectedCommunities.ToList(), "cluster");
                        break;
                    case "Reset Comm.":
                        _networkManager.SetMLLayout(_networkManager.SelectedCommunities.ToList(), "spherical");
                        break;
                    case "Project Comm. Floor":
                        _networkManager.SetMLLayout(_networkManager.SelectedCommunities.ToList(), "floor");
                        break;
                    default:
                        inputAction = false;
                        break;
                }
            }

            return inputAction;
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
                _networkManager.HoverCommunity(community.ID);
                _hoveredCommunity = community;
                _commHoverExit.DidCancel();
                _nodeHoverExit.DidInteraction();
            }
        }

        void OnCommunityHoverExit(Community community, HoverExitEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                _commHoverExit.DidInteraction();
            }
        }

        void OnCommunityGrabEnter(Community community, SelectEnterEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                _networkManager.StartMLCommMove(community.ID);
                _commHoverExit.DidCancel();
            }
        }

        void OnCommunityGrabExit(Community community, SelectExitEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                _networkManager.EndMLCommMove(community.ID);
                _commHoverExit.DidInteraction();
            }
        }

        void OnNodeHoverEnter(Node node, HoverEnterEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                _networkManager.HoverNode(node.ID);
                _hoveredNode = node;

                TooltipText.SetText($"{node.Label}\n{node.Degree}");
                _nodeHoverExit.DidCancel();
                _commHoverExit.DidInteraction();
            }
        }

        void OnNodeHoverExit(Node node, HoverExitEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                _nodeHoverExit.DidInteraction();
            }
        }

        void OnNodeGrabEnter(Node node, SelectEnterEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                _networkManager.StartMLNodeMove(node.ID);
                _nodeHoverExit.DidCancel();
            }
        }

        void OnNodeGrabExit(Node node, SelectExitEventArgs evt)
        {
            if (evt.interactorObject.handedness == InteractorHandedness.Right)
            {
                _networkManager.EndMLNodeMove(node.ID, evt.interactableObject.transform);
                _nodeHoverExit.DidInteraction();
            }
        }

        bool GrabInProgress()
        {
            return float.IsFinite(_startMovePos.x);
        }
    }

}