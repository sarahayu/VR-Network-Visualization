/*
*
* NetworkManager is where all network operations are done from.
*
*/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace VidiGraph
{
    public class NetworkManager : MonoBehaviour
    {
        [SerializeField] MultiLayoutNetwork _multiLayoutNetwork;
        [SerializeField] HandheldNetwork _handheldNetwork;
        [SerializeField] GameObject _subnetworkPrefab;
        [SerializeField] OptionsMenu _optionsMenu;

        Dictionary<int, BasicSubnetwork> _subnetworks = new();

        public NetworkFilesLoader FileLoader { get { return _fileLoader; } }
        public NetworkGlobal NetworkGlobal { get { return _networkGlobal; } }

        [Obsolete("SelectedNodes is deprecated, please use SubnSelectedNodes().")]
        public HashSet<int> SelectedNodes
        {
            get
            {
                return _subnetworks.Values
                    .Select(subn => subn.SelectedNodes)
                    .SelectMany(i => i)
                    .Union(_multiLayoutNetwork.SelectedNodes)
                    .ToHashSet();
            }
        }

        [Obsolete("SelectedCommunities is deprecated, please use SubnSelectedCommunities().")]
        public HashSet<int> SelectedCommunities
        {
            get
            {
                return _subnetworks.Values
                    .Select(subn => subn.SelectedCommunities)
                    .SelectMany(i => i)
                    .Union(_multiLayoutNetwork.SelectedCommunities)
                    .ToHashSet().ToHashSet();
            }
        }

        NetworkGlobal _networkGlobal = new();

        NetworkFilesLoader _fileLoader;
        NetworkStorage _storage;

        bool _updatingStorage = true;
        bool _updatingRenderElements = true;

        void Start()
        {
            Initialize();

            _storage = GameObject.Find("/Database")?.GetComponent<NetworkStorage>();
            _storage?.InitialStore(_fileLoader.ClusterLayout, _networkGlobal,
                _multiLayoutNetwork.Context, _subnetworks.Values.Select(sn => sn.Context));

            _multiLayoutNetwork.SetStorageUpdateCallback(UpdateStorage);
        }

        public void Initialize()
        {
            _fileLoader = GetComponent<NetworkFilesLoader>();

            _fileLoader.LoadFiles();
            _networkGlobal.InitNetwork(_fileLoader);

            _multiLayoutNetwork.Initialize();
            _handheldNetwork?.Initialize(_multiLayoutNetwork, _subnetworks);

            PauseRenderUpdate();

            SetMLNodeColorEncoding("Degree", 0, 0.1f, "#00FF00");
            SetMLNodeSizeEncoding("Degree", -0.01f, 0.1f);

            UnpauseRenderUpdate();
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
            TriggerStorageUpdate();
        }

        public void TriggerStorageUpdate()
        {
            _multiLayoutNetwork.UpdateStorage();
            foreach (var subn in _subnetworks.Values) subn.UpdateStorage();
        }

        public void PauseRenderUpdate()
        {
            _updatingRenderElements = false;
        }

        public void UnpauseRenderUpdate()
        {
            _updatingRenderElements = true;
            TriggerRenderUpdate();
        }

        public void TriggerRenderUpdate()
        {
            _multiLayoutNetwork.UpdateRenderElements();
            foreach (var subn in _subnetworks.Values) subn.UpdateRenderElements();
            UpdateHandheld();
        }

        public void ToggleBigNetworkSphericalAndHairball(bool animated = true)
        {
            _multiLayoutNetwork.ToggleSphericalAndHairball(animated);
        }

        public void HoverNode(int nodeID)
        {
            _networkGlobal.HoveredNode = _networkGlobal.Nodes[nodeID];
            _multiLayoutNetwork.UpdateRenderElements();
            foreach (var subn in _subnetworks.Values) subn.UpdateRenderElements();
        }

        public void UnhoverNode(int nodeID)
        {
            _networkGlobal.HoveredNode = null;
            _multiLayoutNetwork.UpdateRenderElements();
            foreach (var subn in _subnetworks.Values) subn.UpdateRenderElements();
        }

        public void SetSelectedNodes(IEnumerable<int> nodeIDs, bool selected, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
            {
                _multiLayoutNetwork.SetSelectedNodes(nodeIDs, selected);
                _multiLayoutNetwork.UpdateSelectedElements();
            }
            else
            {
                _subnetworks[subnetworkID].SetSelectedNodes(nodeIDs, selected);
                _subnetworks[subnetworkID].UpdateSelectedElements();
            }

            if (selected)
                _handheldNetwork.PushSelectionEvent(nodeIDs, subnetworkID);

            UpdateHandheld();
            UpdateOptions();
        }

        public void ToggleSelectedNodes(IEnumerable<int> nodeIDs, int subnetworkID = 0)
        {
            IEnumerable<int> newSelecteds;
            if (subnetworkID == 0)
            {
                newSelecteds = _multiLayoutNetwork.ToggleSelectedNodes(nodeIDs);
                _multiLayoutNetwork.UpdateSelectedElements();
            }
            else
            {
                newSelecteds = _subnetworks[subnetworkID].ToggleSelectedNodes(nodeIDs);
                _subnetworks[subnetworkID].UpdateSelectedElements();
            }

            _handheldNetwork.PushSelectionEvent(newSelecteds, subnetworkID);

            UpdateHandheld();
            UpdateOptions();
        }

        public void StartMLNodeMove(int nodeID, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
            {
                _multiLayoutNetwork.StartNodeMove(nodeID);
            }
            else
            {
                _subnetworks[subnetworkID].StartNodeMove(nodeID);
            }
        }

        public void StartMLNodesMove(IEnumerable<int> nodeIDs, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
            {
                _multiLayoutNetwork.StartNodesMove(nodeIDs);
            }
            else
            {
                _subnetworks[subnetworkID].StartNodesMove(nodeIDs);
            }
        }

        public void EndMLNodesMove(int subnetworkID = 0)
        {
            if (subnetworkID == 0)
            {
                _multiLayoutNetwork.EndNodesMove();
            }
            else
            {
                _subnetworks[subnetworkID].EndNodesMove();
            }
            UpdateHandheld();
        }

        public void StartMLCommMove(int commID, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
            {
                _multiLayoutNetwork.StartCommMove(commID);
            }
            else
            {
                _subnetworks[subnetworkID].StartCommMove(commID);
            }
        }

        public void StartMLCommsMove(IEnumerable<int> commIDs, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
            {
                _multiLayoutNetwork.StartCommsMove(commIDs);
            }
            else
            {
                _subnetworks[subnetworkID].StartCommsMove(commIDs);
            }
        }

        public void EndMLCommsMove(int subnetworkID = 0)
        {
            if (subnetworkID == 0)
            {
                _multiLayoutNetwork.EndCommsMove();
            }
            else
            {
                _subnetworks[subnetworkID].EndCommsMove();
            }
            UpdateHandheld();
        }

        public void HoverCommunity(int commID)
        {
            _networkGlobal.HoveredCommunity = _networkGlobal.Communities[commID];
            _multiLayoutNetwork.UpdateRenderElements();
            foreach (var subn in _subnetworks.Values) subn.UpdateRenderElements();
        }

        public void UnhoverCommunity(int commID)
        {
            _networkGlobal.HoveredCommunity = null;
            _multiLayoutNetwork.UpdateRenderElements();
            foreach (var subn in _subnetworks.Values) subn.UpdateRenderElements();
        }

        public void SetSelectedCommunities(IEnumerable<int> commIDs, bool selected, int subnetworkID = 0)
        {
            IEnumerable<int> selectedNodes = new List<int>();
            if (subnetworkID == 0)
            {
                _multiLayoutNetwork.SetSelectedComms(commIDs, selected);
                _multiLayoutNetwork.UpdateSelectedElements();

                if (selected)
                    selectedNodes = commIDs.Select(cid => _multiLayoutNetwork.Context.Communities[cid].Nodes).SelectMany(i => i);
            }
            else
            {
                _subnetworks[subnetworkID].SetSelectedComms(commIDs, selected);
                _subnetworks[subnetworkID].UpdateSelectedElements();

                if (selected)
                    selectedNodes = commIDs.Select(cid => _subnetworks[subnetworkID].Context.Communities[cid].Nodes).SelectMany(i => i);
            }


            _handheldNetwork.PushSelectionEvent(selectedNodes, subnetworkID);

            UpdateHandheld();
            UpdateOptions();
        }

        public void ToggleSelectedCommunities(IEnumerable<int> commIDs, int subnetworkID = 0)
        {
            IEnumerable<int> selectedNodes;
            if (subnetworkID == 0)
            {
                var selectedComms = _multiLayoutNetwork.ToggleSelectedComms(commIDs);
                _multiLayoutNetwork.UpdateSelectedElements();
                selectedNodes = selectedComms.Select(cid => _multiLayoutNetwork.Context.Communities[cid].Nodes).SelectMany(i => i);
            }
            else
            {
                var selectedComms = _subnetworks[subnetworkID].ToggleSelectedComms(commIDs);
                _subnetworks[subnetworkID].UpdateSelectedElements();
                selectedNodes = selectedComms.Select(cid => _subnetworks[subnetworkID].Context.Communities[cid].Nodes).SelectMany(i => i);
            }

            _handheldNetwork.PushSelectionEvent(selectedNodes, subnetworkID);

            UpdateHandheld();
            UpdateOptions();
        }

        public void ClearSelection()
        {
            ClearSelectedItems();
            _multiLayoutNetwork.UpdateSelectedElements();
            foreach (var subn in _subnetworks.Values) subn.UpdateSelectedElements();

            UpdateHandheld();
            UpdateOptions();
        }

        // layout = [spherical, cluster, floor]
        // TODO extend for subnetworks
        public void SetMLLayout(IEnumerable<int> commIDs, string layout, int subnetworkID = 0)
        {
            _multiLayoutNetwork.SetLayout(commIDs, layout, UpdateHandheld);
        }

        // layout = [spherical, cluster, floor]
        // TODO extend for subnetworks
        public void SetMLLayout(int commID, string layout, int subnetworkID = 0)
        {
            _multiLayoutNetwork.SetLayout(new int[] { commID }, layout, UpdateHandheld);
        }

        public void BringMLNodes(IEnumerable<int> nodeIDs, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
            {
                _multiLayoutNetwork.BringNodes(nodeIDs, UpdateHandheld);
            }
            else
            {
                _subnetworks[subnetworkID].BringNodes(nodeIDs, UpdateHandheld);
            }
        }

        public void ReturnMLNodes(IEnumerable<int> nodeIDs, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
            {
                _multiLayoutNetwork.ReturnNodes(nodeIDs, UpdateHandheld);
            }
            else
            {
                // TODO implement
                // _subnetworks[subnetworkID].ReturnNodes(nodeIDs);
            }
        }

        public Transform GetMLNodeTransform(int nodeID, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
            {
                return _multiLayoutNetwork.GetNodeTransform(nodeID);
            }
            else
            {
                return _subnetworks[subnetworkID].GetNodeTransform(nodeID);
            }
        }

        public Transform GetMLCommTransform(int commID, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
            {
                return _multiLayoutNetwork.GetCommTransform(commID);
            }
            else
            {
                return _subnetworks[subnetworkID].GetCommTransform(commID);
            }
        }

        public void CreateSubnetwork(IEnumerable<int> nodeIDs, int sourceSubnetworkID = 0)
        {
            if (nodeIDs.Count() == 0) return;

            BasicSubnetwork subn;

            if (sourceSubnetworkID == 0)
            {
                subn = BasicSubnetworkUtils.CreateBasicSubnetwork(_subnetworkPrefab, transform, nodeIDs,
                    _multiLayoutNetwork.Context);
            }
            else
            {
                subn = BasicSubnetworkUtils.CreateBasicSubnetwork(_subnetworkPrefab, transform, nodeIDs,
                    _subnetworks[sourceSubnetworkID].Context);
            }

            subn.SetStorageUpdateCallback(UpdateStorage);
            // mark communities and nodes dirty to be registered in storage update
            DirtySubnetworkElements(subn);

            _subnetworks[subn.ID] = subn;

            TriggerStorageUpdate();
            TriggerRenderUpdate();
        }

        public void SetMLNodesSize(IEnumerable<int> nodeIDs, float size, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
            {
                _multiLayoutNetwork.SetNodesSize(nodeIDs, size, _updatingStorage, _updatingRenderElements);
            }
            else
            {
                _subnetworks[subnetworkID].SetNodesSize(nodeIDs, size, _updatingStorage, _updatingRenderElements);
            }
        }

        public void SetMLNodesColor(IEnumerable<int> nodeIDs, string color, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
            {
                _multiLayoutNetwork.SetNodesColor(nodeIDs, color, _updatingStorage, _updatingRenderElements);
            }
            else
            {
                _subnetworks[subnetworkID].SetNodesColor(nodeIDs, color, _updatingStorage, _updatingRenderElements);
            }
        }

        public void SetMLNodesPosition(IEnumerable<int> nodeIDs, Vector3 position, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
            {
                _multiLayoutNetwork.SetNodesPosition(nodeIDs, position, _updatingStorage, _updatingRenderElements);
            }
            else
            {
                _subnetworks[subnetworkID].SetNodesPosition(nodeIDs, position, _updatingStorage, _updatingRenderElements);
            }
        }

        public void SetMLNodesPosition(IEnumerable<int> nodeIDs, IEnumerable<Vector3> positions, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
            {
                _multiLayoutNetwork.SetNodesPosition(nodeIDs, positions, _updatingStorage, _updatingRenderElements);
            }
            else
            {
                _subnetworks[subnetworkID].SetNodesPosition(nodeIDs, positions, _updatingStorage, _updatingRenderElements);
            }
        }

        public void SetMLLinksWidth(IEnumerable<int> linkIDs, float width, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
            {
                _multiLayoutNetwork.SetLinksWidth(linkIDs, width, _updatingStorage, _updatingRenderElements);
            }
            else
            {
                _subnetworks[subnetworkID].SetLinksWidth(linkIDs, width, _updatingStorage, _updatingRenderElements);
            }
        }

        public void SetMLLinksColorStart(IEnumerable<int> linkIDs, string color, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
            {
                _multiLayoutNetwork.SetLinksColorStart(linkIDs, color, _updatingStorage, _updatingRenderElements);
            }
            else
            {
                _subnetworks[subnetworkID].SetLinksColorStart(linkIDs, color, _updatingStorage, _updatingRenderElements);
            }
        }

        public void SetMLLinksColorEnd(IEnumerable<int> linkIDs, string color, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
            {
                _multiLayoutNetwork.SetLinksColorEnd(linkIDs, color, _updatingStorage, _updatingRenderElements);
            }
            else
            {
                _subnetworks[subnetworkID].SetLinksColorEnd(linkIDs, color, _updatingStorage, _updatingRenderElements);
            }
        }

        public void SetMLLinksAlpha(IEnumerable<int> linkIDs, float alpha, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
            {
                _multiLayoutNetwork.SetLinksAlpha(linkIDs, alpha, _updatingStorage, _updatingRenderElements);
            }
            else
            {
                _subnetworks[subnetworkID].SetLinksAlpha(linkIDs, alpha, _updatingStorage, _updatingRenderElements);
            }
        }

        public void SetMLLinksBundlingStrength(IEnumerable<int> linkIDs, float bundlingStrength, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
            {
                _multiLayoutNetwork.SetLinksBundlingStrength(linkIDs, bundlingStrength, _updatingStorage, _updatingRenderElements);
            }
            else
            {
                _subnetworks[subnetworkID].SetLinksBundlingStrength(linkIDs, bundlingStrength, _updatingStorage, _updatingRenderElements);
            }
        }

        public void SetMLLinksBundleStart(IEnumerable<int> linkIDs, bool bundleStart, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
            {
                _multiLayoutNetwork.SetLinksBundleStart(linkIDs, bundleStart, _updatingStorage, _updatingRenderElements);
            }
            else
            {
                _subnetworks[subnetworkID].SetLinksBundleStart(linkIDs, bundleStart, _updatingStorage, _updatingRenderElements);
            }
        }

        public void SetMLLinksBundleEnd(IEnumerable<int> linkIDs, bool bundleEnd, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
            {
                _multiLayoutNetwork.SetLinksBundleEnd(linkIDs, bundleEnd, _updatingStorage, _updatingRenderElements);
            }
            else
            {
                _subnetworks[subnetworkID].SetLinksBundleEnd(linkIDs, bundleEnd, _updatingStorage, _updatingRenderElements);
            }
        }

        public bool SetMLNodeColorEncoding(string prop, float min = 0f, float max = 1f, string color = "#0000FF" /* blue */, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
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

        public bool SetMLNodeColorEncoding(string prop, Dictionary<string, string> valueToColor, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
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

        public bool SetMLNodeColorEncoding(string prop, Dictionary<bool?, string> valueToColor, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
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

        public bool SetMLNodeSizeEncoding(string prop, float min = 0f, float max = 1f, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
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

        public bool SetMLNodeSizeEncoding(string prop, Dictionary<string, float> valueToSize, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
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

        public bool SetMLNodeSizeEncoding(string prop, Dictionary<bool?, float> valueToSize, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
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

        public bool SetMLLinkWidthEncoding(string prop, float min = 0f, float max = 1f, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
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

        public bool SetMLLinkWidthEncoding(string prop, Dictionary<string, float> valueToWidth, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
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

        public bool SetMLLinkWidthEncoding(string prop, Dictionary<bool?, float> valueToWidth, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
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

        public bool SetMLLinkBundlingStrengthEncoding(string prop, float min = 0f, float max = 1f, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
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

        public bool SetMLLinkBundlingStrengthEncoding(string prop, Dictionary<string, float> valueToBundlingStrength, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
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

        public bool SetMLLinkBundlingStrengthEncoding(string prop, Dictionary<bool?, float> valueToBundlingStrength, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
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

        public bool SetMLLinkColorStartEncoding(string prop, float min = 0f, float max = 1f, string colorStart = "#FFFFFF" /* white */, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
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

        public bool SetMLLinkColorStartEncoding(string prop, Dictionary<string, string> valueToColorStart, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
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

        public bool SetMLLinkColorStartEncoding(string prop, Dictionary<bool?, string> valueToColorStart, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
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

        public bool SetMLLinkColorEndEncoding(string prop, float min = 0f, float max = 1f, string colorEnd = "#FFFFFF" /* white */, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
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

        public bool SetMLLinkColorEndEncoding(string prop, Dictionary<string, string> valueToColorEnd, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
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

        public bool SetMLLinkColorEndEncoding(string prop, Dictionary<bool?, string> valueToColorEnd, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
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
        public bool SetMLLinkBundleStartEncoding(string prop, Dictionary<string, bool> valueToDoBundle, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
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
        public bool SetMLLinkBundleStartEncoding(string prop, Dictionary<bool?, bool> valueToDoBundle, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
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
        public bool SetMLLinkBundleEndEncoding(string prop, Dictionary<string, bool> valueToDoBundle, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
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
        public bool SetMLLinkBundleEndEncoding(string prop, Dictionary<bool?, bool> valueToDoBundle, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
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

        public bool SetMLLinkAlphaEncoding(string prop, float min = 0f, float max = 1f, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
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

        public bool SetMLLinkAlphaEncoding(string prop, Dictionary<string, float> valueToAlpha, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
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

        public bool SetMLLinkAlphaEncoding(string prop, Dictionary<bool?, float> valueToAlpha, int subnetworkID = 0)
        {
            if (subnetworkID == 0)
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

        public HashSet<int> SubnSelectedNodes(int subnetworkID)
        {
            if (subnetworkID == 0) return _multiLayoutNetwork.SelectedNodes;
            return _subnetworks[subnetworkID].SelectedNodes;
        }

        public HashSet<int> SubnSelectedCommunities(int subnetworkID)
        {
            if (subnetworkID == 0) return _multiLayoutNetwork.SelectedCommunities;
            return _subnetworks[subnetworkID].SelectedCommunities;
        }

        public bool IsNodeSelected(int nodeID, int subnetworkID)
        {
            if (subnetworkID == 0) return _multiLayoutNetwork.SelectedNodes.Contains(nodeID);
            return _subnetworks[subnetworkID].SelectedNodes.Contains(nodeID);
        }

        public bool IsCommSelected(int commID, int subnetworkID)
        {
            if (subnetworkID == 0) return _multiLayoutNetwork.SelectedCommunities.Contains(commID);
            return _subnetworks[subnetworkID].SelectedCommunities.Contains(commID);
        }

        /*=============== start private methods ===================*/

        void UpdateStorage()
        {
            _storage?.UpdateStore(_fileLoader.ClusterLayout, _networkGlobal, _multiLayoutNetwork.Context,
                    _subnetworks.Values.Select(sn => sn.Context));
        }

        // clears both nodes and communities
        void ClearSelectedItems()
        {
            _multiLayoutNetwork.ClearSelection();
            foreach (var subn in _subnetworks.Values) subn.ClearSelection();
        }

        void UpdateHandheld()
        {
            _handheldNetwork.UpdateRenderElements();
        }

        void UpdateOptions()
        {
            int selectedSubnetworkForNodes = -1;
            bool onlyOneSelectedForNodes = false;

            int selectedSubnetworkForComms = -1;
            bool onlyOneSelectedForComms = false;

            Dictionary<int, HashSet<int>> subnToSelNodes = new();
            Dictionary<int, HashSet<int>> subnToSelComms = new();

            int curSubn = 0;
            do
            {
                var selNodes = SubnSelectedNodes(curSubn);
                if (selNodes.Count != 0)
                {
                    if (selectedSubnetworkForNodes == -1)
                    {
                        selectedSubnetworkForNodes = curSubn;
                        onlyOneSelectedForNodes = true;
                    }
                    else
                    {
                        onlyOneSelectedForNodes = false;
                    }

                    subnToSelNodes[curSubn] = selNodes;
                }

                var selComms = SubnSelectedCommunities(curSubn);
                if (selComms.Count != 0)
                {
                    if (selectedSubnetworkForComms == -1)
                    {
                        selectedSubnetworkForComms = curSubn;
                        onlyOneSelectedForComms = true;
                    }
                    else
                    {
                        onlyOneSelectedForComms = false;
                    }

                    subnToSelComms[curSubn] = selComms;
                }
            } while (_subnetworks.ContainsKey(++curSubn));

            if (selectedSubnetworkForNodes == -1)
            {
                _optionsMenu.ClearOptions();
                return;
            }

            Dictionary<string, Action> callbacks = new();

            callbacks["Reset node(s)"] = () =>
            {
                foreach (var (subn, subnNodes) in subnToSelNodes)
                {
                    if (subnNodes.Count != 0) ReturnMLNodes(subnNodes, subn);
                }
            };

            callbacks["Bring node(s)"] = () =>
            {
                foreach (var (subn, subnNodes) in subnToSelNodes)
                {
                    if (subnNodes.Count != 0) BringMLNodes(subnNodes, subn);
                }
            };

            if (onlyOneSelectedForComms && selectedSubnetworkForComms == 0)
            {
                callbacks["Focus comm."] = () =>
                {
                    SetMLLayout(subnToSelComms[0], "cluster");
                };

                callbacks["Project comm. floor"] = () =>
                {
                    SetMLLayout(subnToSelComms[0], "floor");
                };
            }

            _optionsMenu.SetOptions(callbacks);
        }

        void DirtySubnetworkElements(BasicSubnetwork subnetwork)
        {
            foreach (var comm in subnetwork.Context.Communities.Values) comm.Dirty = true;
            foreach (var node in subnetwork.Context.Nodes.Values) node.Dirty = true;
        }
    }
}