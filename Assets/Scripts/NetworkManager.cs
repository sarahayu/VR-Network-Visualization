/*
*
* TODO Description goes here
*
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VidiGraph
{
    public class NetworkManager : MonoBehaviour
    {
        [SerializeField]
        MultiLayoutNetwork _multiLayoutNetwork;
        [SerializeField]
        MultiLayoutNetworkInput _multiLayoutNetworkInput;
        [SerializeField]
        HandheldNetwork _handheldNetwork;
        [SerializeField]
        GameObject _subnetworkPrefab;

        Dictionary<int, BasicSubnetwork> _subnetworks = new();

        public NetworkFilesLoader FileLoader { get { return _fileLoader; } }
        public NetworkGlobal NetworkGlobal { get { return _networkGlobal; } }
        public HashSet<int> SelectedNodes { get { return _networkGlobal.SelectedNodes; } }
        public HashSet<int> SelectedCommunities { get { return _networkGlobal.SelectedCommunities; } }

        NetworkFilesLoader _fileLoader;
        NetworkStorage _storage;
        NetworkGlobal _networkGlobal;
        bool _updatingStorage = true;
        bool _updatingRenderElements = true;

        void Start()
        {
            Initialize();

            _storage = GameObject.Find("/Database")?.GetComponent<NetworkStorage>();
            _storage?.InitialStore(_fileLoader.ClusterLayout, _networkGlobal, _multiLayoutNetwork.Context);

            _multiLayoutNetwork.SetStorageUpdateCallback(() => _storage?.UpdateStore(_networkGlobal, _multiLayoutNetwork.Context));

            PauseRenderUpdate();

            SetMLNodeColorEncoding("Degree", 0, 0.1f, "#00FF00");
            SetMLNodeSizeEncoding("Degree", -0.01f, 0.1f);

            UnpauseRenderUpdate();
        }

        public void Initialize()
        {
            _fileLoader = GetComponent<NetworkFilesLoader>();
            _networkGlobal = GetComponent<NetworkGlobal>();

            _fileLoader.LoadFiles();
            _networkGlobal.InitNetwork();

            _multiLayoutNetwork.Initialize();
            _handheldNetwork?.Initialize();
        }

        public void DrawPreview()
        {
            _multiLayoutNetwork.DrawPreview();
            _handheldNetwork?.DrawPreview();
        }

        public HashSet<string> GetValidOptions()
        {
            HashSet<string> opts = new HashSet<string>();

            if (SelectedNodes.Count > 0)
            {
                opts.Add("Bring Node");
                opts.Add("Reset Node(s)");
            }

            if (SelectedCommunities.Count > 0)
            {
                opts.Add("Focus Comm.");
                opts.Add("Project Comm. Floor");
            }

            return opts;
        }

        public void PauseStorageUpdate()
        {
            _updatingStorage = false;
        }

        public void UnpauseStorageUpdate()
        {
            _updatingStorage = true;
            _multiLayoutNetwork.UpdateStorage();
        }

        public void PauseRenderUpdate()
        {
            _updatingRenderElements = false;
        }

        public void UnpauseRenderUpdate()
        {
            _updatingRenderElements = true;
            _multiLayoutNetwork.UpdateRenderElements();
        }

        public void ToggleBigNetworkSphericalAndHairball(bool animated = true)
        {
            _multiLayoutNetwork.ToggleSphericalAndHairball(animated);
        }

        public void HoverNode(int nodeID)
        {
            _networkGlobal.HoveredNode = _networkGlobal.Nodes[nodeID];
            _multiLayoutNetwork.UpdateRenderElements();
        }

        public void UnhoverNode(int nodeID)
        {
            _networkGlobal.HoveredNode = null;
            _multiLayoutNetwork.UpdateRenderElements();
        }

        public void SetSelectedNodes(IEnumerable<int> nodeIDs, bool selected, int subnetworkID = -1)
        {
            _networkGlobal.SetSelectedNodes(nodeIDs, selected, subnetworkID);
            _multiLayoutNetwork.UpdateSelectedElements();
            _handheldNetwork.UpdateRenderElements();
            _multiLayoutNetworkInput.CheckSelectionActions();
        }

        public void ToggleSelectedNodes(IEnumerable<int> nodeIDs, int subnetworkID = -1)
        {
            _networkGlobal.ToggleSelectedNodes(nodeIDs, subnetworkID);
            _multiLayoutNetwork.UpdateSelectedElements();
            _handheldNetwork.UpdateRenderElements();
            _multiLayoutNetworkInput.CheckSelectionActions();
        }

        public void StartMLNodeMove(int nodeID, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                _multiLayoutNetwork.StartNodeMove(nodeID);
            }
            else
            {
                _subnetworks[subnetworkID].StartNodeMove(nodeID);
            }
        }

        public void StartMLNodesMove(IEnumerable<int> nodeIDs, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                _multiLayoutNetwork.StartNodesMove(nodeIDs);
            }
            else
            {
                _subnetworks[subnetworkID].StartNodesMove(nodeIDs);
            }
        }

        public void EndMLNodeMove(int nodeID, Transform transform, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                _multiLayoutNetwork.EndNodeMove(nodeID);
            }
            else
            {
                _subnetworks[subnetworkID].EndNodeMove(nodeID);
            }
        }

        public void EndMLNodesMove(IEnumerable<int> nodeIDs, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                _multiLayoutNetwork.EndNodesMove();
            }
            else
            {
                _subnetworks[subnetworkID].EndNodesMove();
            }
        }

        public void StartMLCommMove(int commID, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                _multiLayoutNetwork.StartCommMove(commID);
            }
            else
            {
                _subnetworks[subnetworkID].StartCommMove(commID);
            }
        }

        public void StartMLCommsMove(IEnumerable<int> commIDs, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                _multiLayoutNetwork.StartCommsMove(commIDs);
            }
            else
            {
                _subnetworks[subnetworkID].StartCommsMove(commIDs);
            }
        }

        public void EndMLCommMove(int commID, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                _multiLayoutNetwork.EndCommMove();
            }
            else
            {
                _subnetworks[subnetworkID].EndCommMove();
            }
        }

        public void EndMLCommsMove(IEnumerable<int> commIDs, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                _multiLayoutNetwork.EndCommsMove();
            }
            else
            {
                _subnetworks[subnetworkID].EndCommsMove();
            }
        }

        public void HoverCommunity(int commID)
        {
            _networkGlobal.HoveredCommunity = _networkGlobal.Communities[commID];
            _multiLayoutNetwork.UpdateRenderElements();
        }

        public void UnhoverCommunity(int commID)
        {
            _networkGlobal.HoveredCommunity = null;
            _multiLayoutNetwork.UpdateRenderElements();
        }

        public void SetSelectedCommunities(IEnumerable<int> commIDs, bool selected, int subnetworkID = -1)
        {
            _networkGlobal.SetSelectedCommunities(commIDs, selected, subnetworkID);
            _multiLayoutNetwork.UpdateSelectedElements();
            _handheldNetwork.UpdateRenderElements();
            _multiLayoutNetworkInput.CheckSelectionActions();
        }

        public void ToggleSelectedCommunities(IEnumerable<int> commIDs, int subnetworkID = -1)
        {
            _networkGlobal.ToggleSelectedCommunities(commIDs, subnetworkID);
            _multiLayoutNetwork.UpdateSelectedElements();
            _handheldNetwork.UpdateRenderElements();
            _multiLayoutNetworkInput.CheckSelectionActions();
        }

        public void ClearSelection()
        {
            _networkGlobal.ClearSelectedItems();
            _multiLayoutNetwork.UpdateSelectedElements();
            _handheldNetwork.UpdateRenderElements();
            _multiLayoutNetworkInput.CheckSelectionActions();
        }

        // layout = [spherical, cluster, floor]
        public void SetMLLayout(IEnumerable<int> commIDs, string layout)
        {
            _multiLayoutNetwork.SetLayout(commIDs, layout);
        }

        // layout = [spherical, cluster, floor]
        public void SetMLLayout(int commID, string layout)
        {
            _multiLayoutNetwork.SetLayout(new int[] { commID }, layout);
        }

        public void BringMLNodes(IEnumerable<int> nodeIDs, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                _multiLayoutNetwork.BringNodes(nodeIDs);
            }
            else
            {
                _subnetworks[subnetworkID].BringNodes(nodeIDs);
            }
        }

        public void ReturnMLNodes(IEnumerable<int> nodeIDs, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                _multiLayoutNetwork.ReturnNodes(nodeIDs);
            }
            else
            {
                _subnetworks[subnetworkID].ReturnNodes(nodeIDs);
            }
        }

        public Transform GetMLNodeTransform(int nodeID, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.GetNodeTransform(nodeID);
            }
            else
            {
                return _subnetworks[subnetworkID].GetNodeTransform(nodeID);
            }
        }

        public Transform GetMLCommTransform(int commID, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.GetCommTransform(commID);
            }
            else
            {
                return _subnetworks[subnetworkID].GetCommTransform(commID);
            }
        }

        public void CreateSubnetwork(IEnumerable<int> nodeIDs, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                var subn = BasicSubnetworkUtils.CreateBasicSubnetwork(_subnetworkPrefab, transform, nodeIDs,
                    _multiLayoutNetwork.Context);
                _subnetworks[subn.ID] = subn;
            }
            else
            {
                var subn = BasicSubnetworkUtils.CreateBasicSubnetwork(_subnetworkPrefab, transform, nodeIDs,
                    _subnetworks[subnetworkID].Context);
                _subnetworks[subn.ID] = subn;
            }
        }

        public void SetMLNodesSize(IEnumerable<int> nodeIDs, float size, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                _multiLayoutNetwork.SetNodesSize(nodeIDs, size, _updatingStorage, _updatingRenderElements);
            }
            else
            {
                _subnetworks[subnetworkID].SetNodesSize(nodeIDs, size, _updatingStorage, _updatingRenderElements);
            }
        }

        public void SetMLNodesColor(IEnumerable<int> nodeIDs, string color, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                _multiLayoutNetwork.SetNodesColor(nodeIDs, color, _updatingStorage, _updatingRenderElements);
            }
            else
            {
                _subnetworks[subnetworkID].SetNodesColor(nodeIDs, color, _updatingStorage, _updatingRenderElements);
            }
        }

        public void SetMLNodesPosition(IEnumerable<int> nodeIDs, Vector3 position, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                _multiLayoutNetwork.SetNodesPosition(nodeIDs, position, _updatingStorage, _updatingRenderElements);
            }
            else
            {
                _subnetworks[subnetworkID].SetNodesPosition(nodeIDs, position, _updatingStorage, _updatingRenderElements);
            }
        }

        public void SetMLNodesPosition(IEnumerable<int> nodeIDs, IEnumerable<Vector3> positions, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                _multiLayoutNetwork.SetNodesPosition(nodeIDs, positions, _updatingStorage, _updatingRenderElements);
            }
            else
            {
                _subnetworks[subnetworkID].SetNodesPosition(nodeIDs, positions, _updatingStorage, _updatingRenderElements);
            }
        }

        public void SetMLLinksWidth(IEnumerable<int> linkIDs, float width, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                _multiLayoutNetwork.SetLinksWidth(linkIDs, width, _updatingStorage, _updatingRenderElements);
            }
            else
            {
                _subnetworks[subnetworkID].SetLinksWidth(linkIDs, width, _updatingStorage, _updatingRenderElements);
            }
        }

        public void SetMLLinksColorStart(IEnumerable<int> linkIDs, string color, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                _multiLayoutNetwork.SetLinksColorStart(linkIDs, color, _updatingStorage, _updatingRenderElements);
            }
            else
            {
                _subnetworks[subnetworkID].SetLinksColorStart(linkIDs, color, _updatingStorage, _updatingRenderElements);
            }
        }

        public void SetMLLinksColorEnd(IEnumerable<int> linkIDs, string color, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                _multiLayoutNetwork.SetLinksColorEnd(linkIDs, color, _updatingStorage, _updatingRenderElements);
            }
            else
            {
                _subnetworks[subnetworkID].SetLinksColorEnd(linkIDs, color, _updatingStorage, _updatingRenderElements);
            }
        }

        public void SetMLLinksAlpha(IEnumerable<int> linkIDs, float alpha, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                _multiLayoutNetwork.SetLinksAlpha(linkIDs, alpha, _updatingStorage, _updatingRenderElements);
            }
            else
            {
                _subnetworks[subnetworkID].SetLinksAlpha(linkIDs, alpha, _updatingStorage, _updatingRenderElements);
            }
        }

        public void SetMLLinksBundlingStrength(IEnumerable<int> linkIDs, float bundlingStrength, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                _multiLayoutNetwork.SetLinksBundlingStrength(linkIDs, bundlingStrength, _updatingStorage, _updatingRenderElements);
            }
            else
            {
                _subnetworks[subnetworkID].SetLinksBundlingStrength(linkIDs, bundlingStrength, _updatingStorage, _updatingRenderElements);
            }
        }

        public void SetMLLinksBundleStart(IEnumerable<int> linkIDs, bool bundleStart, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                _multiLayoutNetwork.SetLinksBundleStart(linkIDs, bundleStart, _updatingStorage, _updatingRenderElements);
            }
            else
            {
                _subnetworks[subnetworkID].SetLinksBundleStart(linkIDs, bundleStart, _updatingStorage, _updatingRenderElements);
            }
        }

        public void SetMLLinksBundleEnd(IEnumerable<int> linkIDs, bool bundleEnd, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                _multiLayoutNetwork.SetLinksBundleEnd(linkIDs, bundleEnd, _updatingStorage, _updatingRenderElements);
            }
            else
            {
                _subnetworks[subnetworkID].SetLinksBundleEnd(linkIDs, bundleEnd, _updatingStorage, _updatingRenderElements);
            }
        }

        public bool SetMLNodeColorEncoding(string prop, float min = 0f, float max = 1f, string color = "#0000FF" /* blue */, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.SetNodeColorEncoding(prop, min, max, color,
                    _updatingStorage, _updatingRenderElements);
            }
            else
            {
                return _subnetworks[subnetworkID].SetNodeColorEncoding(prop, min, max, color,
                    _updatingStorage, _updatingRenderElements);
            }
        }

        public bool SetMLNodeColorEncoding(string prop, Dictionary<string, string> valueToColor, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.SetNodeColorEncoding(prop, valueToColor,
                    _updatingStorage, _updatingRenderElements);
            }
            else
            {
                return _subnetworks[subnetworkID].SetNodeColorEncoding(prop, valueToColor,
                    _updatingStorage, _updatingRenderElements);
            }
        }

        public bool SetMLNodeColorEncoding(string prop, Dictionary<bool?, string> valueToColor, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.SetNodeColorEncoding(prop, valueToColor,
                    _updatingStorage, _updatingRenderElements);
            }
            else
            {
                return _subnetworks[subnetworkID].SetNodeColorEncoding(prop, valueToColor,
                    _updatingStorage, _updatingRenderElements);
            }
        }

        public bool SetMLNodeSizeEncoding(string prop, float min = 0f, float max = 1f, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.SetNodeSizeEncoding(prop, min, max,
                    _updatingStorage, _updatingRenderElements);
            }
            else
            {
                return _subnetworks[subnetworkID].SetNodeSizeEncoding(prop, min, max,
                    _updatingStorage, _updatingRenderElements);
            }
        }

        public bool SetMLNodeSizeEncoding(string prop, Dictionary<string, float> valueToSize, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.SetNodeSizeEncoding(prop, valueToSize,
                    _updatingStorage, _updatingRenderElements);
            }
            else
            {
                return _subnetworks[subnetworkID].SetNodeSizeEncoding(prop, valueToSize,
                    _updatingStorage, _updatingRenderElements);
            }
        }

        public bool SetMLNodeSizeEncoding(string prop, Dictionary<bool?, float> valueToSize, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.SetNodeSizeEncoding(prop, valueToSize,
                    _updatingStorage, _updatingRenderElements);
            }
            else
            {
                return _subnetworks[subnetworkID].SetNodeSizeEncoding(prop, valueToSize,
                    _updatingStorage, _updatingRenderElements);
            }
        }

        public bool SetMLLinkWidthEncoding(string prop, float min = 0f, float max = 1f, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.SetLinkWidthEncoding(prop, min, max,
                    _updatingStorage, _updatingRenderElements);
            }
            else
            {
                return _subnetworks[subnetworkID].SetLinkWidthEncoding(prop, min, max,
                    _updatingStorage, _updatingRenderElements);
            }
        }

        public bool SetMLLinkWidthEncoding(string prop, Dictionary<string, float> valueToWidth, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.SetLinkWidthEncoding(prop, valueToWidth,
                    _updatingStorage, _updatingRenderElements);
            }
            else
            {
                return _subnetworks[subnetworkID].SetLinkWidthEncoding(prop, valueToWidth,
                    _updatingStorage, _updatingRenderElements);
            }
        }

        public bool SetMLLinkWidthEncoding(string prop, Dictionary<bool?, float> valueToWidth, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.SetLinkWidthEncoding(prop, valueToWidth,
                    _updatingStorage, _updatingRenderElements);
            }
            else
            {
                return _subnetworks[subnetworkID].SetLinkWidthEncoding(prop, valueToWidth,
                    _updatingStorage, _updatingRenderElements);
            }
        }

        public bool SetMLLinkBundlingStrengthEncoding(string prop, float min = 0f, float max = 1f, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.SetLinkBundlingStrengthEncoding(prop, min, max,
                    _updatingStorage, _updatingRenderElements);
            }
            else
            {
                return _subnetworks[subnetworkID].SetLinkBundlingStrengthEncoding(prop, min, max,
                    _updatingStorage, _updatingRenderElements);
            }
        }

        public bool SetMLLinkBundlingStrengthEncoding(string prop, Dictionary<string, float> valueToBundlingStrength, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.SetLinkBundlingStrengthEncoding(prop, valueToBundlingStrength,
                    _updatingStorage, _updatingRenderElements);
            }
            else
            {
                return _subnetworks[subnetworkID].SetLinkBundlingStrengthEncoding(prop, valueToBundlingStrength,
                    _updatingStorage, _updatingRenderElements);
            }
        }

        public bool SetMLLinkBundlingStrengthEncoding(string prop, Dictionary<bool?, float> valueToBundlingStrength, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.SetLinkBundlingStrengthEncoding(prop, valueToBundlingStrength,
                    _updatingStorage, _updatingRenderElements);
            }
            else
            {
                return _subnetworks[subnetworkID].SetLinkBundlingStrengthEncoding(prop, valueToBundlingStrength,
                    _updatingStorage, _updatingRenderElements);
            }
        }

        public bool SetMLLinkColorStartEncoding(string prop, float min = 0f, float max = 1f, string colorStart = "#FFFFFF" /* white */, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.SetLinkColorStartEncoding(prop, min, max, colorStart,
                    _updatingStorage, _updatingRenderElements);
            }
            else
            {
                return _subnetworks[subnetworkID].SetLinkColorStartEncoding(prop, min, max, colorStart,
                    _updatingStorage, _updatingRenderElements);
            }
        }

        public bool SetMLLinkColorStartEncoding(string prop, Dictionary<string, string> valueToColorStart, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.SetLinkColorStartEncoding(prop, valueToColorStart,
                    _updatingStorage, _updatingRenderElements);
            }
            else
            {
                return _subnetworks[subnetworkID].SetLinkColorStartEncoding(prop, valueToColorStart,
                    _updatingStorage, _updatingRenderElements);
            }
        }

        public bool SetMLLinkColorStartEncoding(string prop, Dictionary<bool?, string> valueToColorStart, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.SetLinkColorStartEncoding(prop, valueToColorStart,
                    _updatingStorage, _updatingRenderElements);
            }
            else
            {
                return _subnetworks[subnetworkID].SetLinkColorStartEncoding(prop, valueToColorStart,
                    _updatingStorage, _updatingRenderElements);
            }
        }

        public bool SetMLLinkColorEndEncoding(string prop, float min = 0f, float max = 1f, string colorEnd = "#FFFFFF" /* white */, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.SetLinkColorEndEncoding(prop, min, max, colorEnd,
                    _updatingStorage, _updatingRenderElements);
            }
            else
            {
                return _subnetworks[subnetworkID].SetLinkColorEndEncoding(prop, min, max, colorEnd,
                    _updatingStorage, _updatingRenderElements);
            }
        }

        public bool SetMLLinkColorEndEncoding(string prop, Dictionary<string, string> valueToColorEnd, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.SetLinkColorEndEncoding(prop, valueToColorEnd,
                    _updatingStorage, _updatingRenderElements);
            }
            else
            {
                return _subnetworks[subnetworkID].SetLinkColorEndEncoding(prop, valueToColorEnd,
                    _updatingStorage, _updatingRenderElements);
            }
        }

        public bool SetMLLinkColorEndEncoding(string prop, Dictionary<bool?, string> valueToColorEnd, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.SetLinkColorEndEncoding(prop, valueToColorEnd,
                    _updatingStorage, _updatingRenderElements);
            }
            else
            {
                return _subnetworks[subnetworkID].SetLinkColorEndEncoding(prop, valueToColorEnd,
                    _updatingStorage, _updatingRenderElements);
            }
        }

        // TODO untested
        public bool SetMLLinkBundleStartEncoding(string prop, Dictionary<string, bool> valueToDoBundle, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.SetLinkBundleStartEncoding(prop, valueToDoBundle,
                    _updatingStorage, _updatingRenderElements);
            }
            else
            {
                return _subnetworks[subnetworkID].SetLinkBundleStartEncoding(prop, valueToDoBundle,
                    _updatingStorage, _updatingRenderElements);
            }
        }

        // TODO untested
        public bool SetMLLinkBundleStartEncoding(string prop, Dictionary<bool?, bool> valueToDoBundle, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.SetLinkBundleStartEncoding(prop, valueToDoBundle,
                    _updatingStorage, _updatingRenderElements);
            }
            else
            {
                return _subnetworks[subnetworkID].SetLinkBundleStartEncoding(prop, valueToDoBundle,
                    _updatingStorage, _updatingRenderElements);
            }
        }

        // TODO untested
        public bool SetMLLinkBundleEndEncoding(string prop, Dictionary<string, bool> valueToDoBundle, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.SetLinkBundleEndEncoding(prop, valueToDoBundle,
                    _updatingStorage, _updatingRenderElements);
            }
            else
            {
                return _subnetworks[subnetworkID].SetLinkBundleEndEncoding(prop, valueToDoBundle,
                    _updatingStorage, _updatingRenderElements);
            }
        }

        // TODO untested
        public bool SetMLLinkBundleEndEncoding(string prop, Dictionary<bool?, bool> valueToDoBundle, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.SetLinkBundleEndEncoding(prop, valueToDoBundle,
                    _updatingStorage, _updatingRenderElements);
            }
            else
            {
                return _subnetworks[subnetworkID].SetLinkBundleEndEncoding(prop, valueToDoBundle,
                    _updatingStorage, _updatingRenderElements);
            }
        }

        public bool SetMLLinkAlphaEncoding(string prop, float min = 0f, float max = 1f, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.SetLinkAlphaEncoding(prop, min, max,
                    _updatingStorage, _updatingRenderElements);
            }
            else
            {
                return _subnetworks[subnetworkID].SetLinkAlphaEncoding(prop, min, max,
                    _updatingStorage, _updatingRenderElements);
            }
        }

        public bool SetMLLinkAlphaEncoding(string prop, Dictionary<string, float> valueToAlpha, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.SetLinkAlphaEncoding(prop, valueToAlpha,
                    _updatingStorage, _updatingRenderElements);
            }
            else
            {
                return _subnetworks[subnetworkID].SetLinkAlphaEncoding(prop, valueToAlpha,
                    _updatingStorage, _updatingRenderElements);
            }
        }

        public bool SetMLLinkAlphaEncoding(string prop, Dictionary<bool?, float> valueToAlpha, int subnetworkID = -1)
        {
            if (subnetworkID == -1)
            {
                return _multiLayoutNetwork.SetLinkAlphaEncoding(prop, valueToAlpha,
                    _updatingStorage, _updatingRenderElements);
            }
            else
            {
                return _subnetworks[subnetworkID].SetLinkAlphaEncoding(prop, valueToAlpha,
                    _updatingStorage, _updatingRenderElements);
            }
        }
    }
}