/*
*
* NetworkManager is where all network operations are done from.
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
        [SerializeField] MultiLayoutNetwork _multiLayoutNetwork;
        [SerializeField] HandheldNetwork _handheldNetwork;
        [SerializeField] GameObject _subnetworkPrefab;
        [SerializeField] OptionsMenu _optionsMenu;
        [SerializeField] FramesArea _framesArea;

        Dictionary<int, BasicSubnetwork> _subnetworks = new();
        Dictionary<int, NodeLinkNetwork> _allNetworks = new();

        public NetworkFilesLoader FileLoader { get { return _fileLoader; } }
        public NetworkGlobal NetworkGlobal { get { return _networkGlobal; } }
        public int? HoveredNetwork { get; private set; }
        public HashSet<int> SelectedNetworks
        {
            get
            {
                return _allNetworks.Values.Where(subn => subn.Selected).Select(subn => subn.ID).ToHashSet();
            }
        }

        // tuple is (subnetworkID, <node|link|community>ID)
        public Dictionary<string, Tuple<int, int>> NodeGUIDToID { get; } = new();
        public Dictionary<string, Tuple<int, int>> LinkGUIDToID { get; } = new();
        public Dictionary<string, Tuple<int, int>> CommunityGUIDToID { get; } = new();

        // tuple is (subnetworkID, <node|link|community>ID)
        public Dictionary<Tuple<int, int>, string> NodeIDToGUID { get; } = new();
        public Dictionary<Tuple<int, int>, string> LinkIDToGUID { get; } = new();
        public Dictionary<Tuple<int, int>, string> CommunityIDToGUID { get; } = new();

        Coroutine _transformMoverCR;

        int _curWorkingSubgraph = -1;

        public HashSet<string> SelectedNodeGUIDs
        {
            get
            {
                return GetSelNodeGUIDs().Values
                    .SelectMany(i => i)
                    .ToHashSet();
            }
        }

        public HashSet<string> SelectedLinkGUIDs
        {
            get
            {
                return GetSelLinkGUIDs().Values
                    .SelectMany(i => i)
                    .ToHashSet();
            }
        }

        public HashSet<string> SelectedCommunityGUIDs
        {
            get
            {
                return GetSelCommGUIDs().Values
                    .SelectMany(i => i)
                    .ToHashSet();
            }
        }

        [Obsolete("SelectedNodes is deprecated, please use SubnSelectedNodes() or SelectedNodeGUIDs.")]
        public HashSet<int> SelectedNodes
        {
            get
            {
                return _allNetworks.Values
                    .Select(subn => subn.SelectedNodes)
                    .SelectMany(i => i)
                    .ToHashSet();
            }
        }

        [Obsolete("SelectedCommunities is deprecated, please use SubnSelectedCommunities() or SelectedCommunityGUIDs.")]
        public HashSet<int> SelectedCommunities
        {
            get
            {
                return _allNetworks.Values
                    .Select(subn => subn.SelectedCommunities)
                    .SelectMany(i => i)
                    .ToHashSet();
            }
        }

        public delegate void SubnetworkCreateEvent(BasicSubnetwork subnetwork);
        public event SubnetworkCreateEvent OnSubnetworkCreate;

        public delegate void SubnetworkDestroyEvent(BasicSubnetwork subnetwork);
        public event SubnetworkDestroyEvent OnSubnetworkDestroy;

        NetworkGlobal _networkGlobal = new();

        NetworkFilesLoader _fileLoader;
        NetworkStorage _storage;
        SurfaceManager _surfaceManager;

        bool _updatingStorage = true;
        bool _updatingRenderElements = true;

        void Start()
        {
            Initialize();

            _storage = GameObject.Find("/Database")?.GetComponent<NetworkStorage>();
            _storage?.InitialStore(_fileLoader.ClusterLayout, _networkGlobal,
                _multiLayoutNetwork.Context, _subnetworks.Values.Select(sn => sn.Context));

            _multiLayoutNetwork.SetStorageUpdateCallback(UpdateStorage);

            _surfaceManager = GameObject.Find("Surface Manager").GetComponent<SurfaceManager>();
        }

        public void Initialize()
        {
            _fileLoader = GetComponent<NetworkFilesLoader>();

            _fileLoader.LoadFiles();
            _networkGlobal.InitNetwork(_fileLoader);

            InitMultiLayoutNetwork();
            _handheldNetwork?.Initialize(_multiLayoutNetwork, _subnetworks);

            _allNetworks[_multiLayoutNetwork.Context.SubnetworkID /* 0 */] = _multiLayoutNetwork;

            // PauseRenderUpdate();

            // SetMLNodeColorEncoding("Degree", 0, 0.1f, "#00FF00");
            // SetMLNodeSizeEncoding("Degree", -0.01f, 0.1f);

            // UnpauseRenderUpdate();
        }

        void InitMultiLayoutNetwork()
        {
            _multiLayoutNetwork.Initialize();

            AddToGUIDMaps(_multiLayoutNetwork);
        }

        void AddToGUIDMaps(NodeLinkNetwork network)
        {
            foreach (var (nodeID, node) in network.Context.Nodes)
            {
                var tup = Tuple.Create(network.ID, nodeID);
                NodeGUIDToID[node.GUID] = tup;
                NodeIDToGUID[tup] = node.GUID;
            }

            foreach (var (linkID, link) in network.Context.Links)
            {
                var tup = Tuple.Create(network.ID, linkID);
                LinkGUIDToID[link.GUID] = tup;
                LinkIDToGUID[tup] = link.GUID;
            }

            foreach (var (communityID, community) in network.Context.Communities)
            {
                var tup = Tuple.Create(network.ID, communityID);
                CommunityGUIDToID[community.GUID] = tup;
                CommunityIDToGUID[tup] = community.GUID;
            }
        }

        void RemoveFromGUIDMaps(NodeLinkNetwork network)
        {
            foreach (var (nodeID, node) in network.Context.Nodes)
            {
                NodeGUIDToID.Remove(node.GUID);
                NodeIDToGUID.Remove(Tuple.Create(network.ID, nodeID));
            }

            foreach (var (linkID, link) in network.Context.Links)
            {
                LinkGUIDToID.Remove(link.GUID);
                LinkIDToGUID.Remove(Tuple.Create(network.ID, linkID));
            }

            foreach (var (communityID, community) in network.Context.Communities)
            {
                CommunityGUIDToID.Remove(community.GUID);
                CommunityIDToGUID.Remove(Tuple.Create(network.ID, communityID));
            }
        }

        public void DrawPreview()
        {
            _multiLayoutNetwork.DrawPreview();
            _handheldNetwork?.DrawPreview();
        }

        public HashSet<string> GetValidOptions()
        {
            HashSet<string> opts = new HashSet<string>();

            if (SelectedNodeGUIDs.Count > 0)
            {
                opts.Add("Bring Node");
                opts.Add("Reset Node(s)");
            }

            if (SelectedCommunityGUIDs.Count > 0)
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
            foreach (var subn in _allNetworks.Values) subn.UpdateStorage();
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
            foreach (var subn in _allNetworks.Values) subn.UpdateRenderElements();
            UpdateHandheld();
        }

        public void ToggleBigNetworkSphericalAndHairball(bool animated = true)
        {
            _multiLayoutNetwork.ToggleSphericalAndHairball(animated);
        }

        // also sets current working subgraph
        public void CreateWorkingSubgraph(IEnumerable<int> nodeIDs)
        {
            if (_curWorkingSubgraph != -1) HideSubnetwork(_curWorkingSubgraph);

            var newSubnID = CreateSubnetwork(nodeIDs,
                    useShell: false,
                    sourceSubnetworkID: _multiLayoutNetwork.ID)
                .SubnetworkID;

            _framesArea.AddFrame(
                ID: newSubnID,
                displayName: $"{newSubnID}",
                onClick: _ =>
                {
                    SwitchToSubnetwork(newSubnID);
                }
            );

            SwitchToSubnetwork(newSubnID);

            SetMLLayout("forcedDir", newSubnID);
        }

        public void HoverNetwork(int subnetworkID)
        {
            HoveredNetwork = subnetworkID;
            TriggerRenderUpdate();
        }

        public void UnhoverNetwork(int networkID)
        {
            HoveredNetwork = null;
            TriggerRenderUpdate();
        }

        public void SetSelectedNetworks(IEnumerable<int> networkIDs, bool selected)
        {
            foreach (var subnID in networkIDs)
            {
                var subnetwork = _allNetworks[subnID];
                subnetwork.SetSelectedNetwork(selected);
                subnetwork.UpdateSelectedElements();
            }

            // if (selected)
            //     _handheldNetwork.PushSelectionEvent(NetworkIDsToNetworkGUIDs(networkIDs, subnetworkID));

            // UpdateHandheld();
            UpdateOptions();
        }

        public void ToggleSelectedNetworks(IEnumerable<int> networkIDs)
        {
            foreach (var subnID in networkIDs)
            {
                var subnetwork = _allNetworks[subnID];

                bool newSelected = subnetwork.ToggleSelectedNetwork();
                subnetwork.UpdateSelectedElements();
            }

            // _handheldNetwork.PushSelectionEvent(NetworkIDsToNetworkGUIDs(newSelecteds, subnetworkID));

            // UpdateHandheld();
            UpdateOptions();
        }

        public void HoverNode(int nodeID)
        {
            _networkGlobal.HoveredNode = _networkGlobal.Nodes[nodeID];
            TriggerRenderUpdate();
        }

        public void UnhoverNode(int nodeID)
        {
            _networkGlobal.HoveredNode = null;
            TriggerRenderUpdate();
        }

        public void SetSelectedNodes(IEnumerable<string> nodeGUIDs, bool selected)
        {
            foreach (var (subnID, nodeIDs) in SortNodeGUIDs(nodeGUIDs))
            {
                _allNetworks[subnID].SetSelectedNodes(nodeIDs, selected);
                _allNetworks[subnID].UpdateSelectedElements();
            }

            if (selected)
                _handheldNetwork.PushSelectionEvent(nodeGUIDs);

            UpdateHandheld();
            UpdateOptions();
        }

        public void SetSelectedNodes(IEnumerable<int> nodeIDs, bool selected, int subnetworkID = 0)
        {
            var subnetwork = _allNetworks[subnetworkID];
            subnetwork.SetSelectedNodes(nodeIDs, selected);
            subnetwork.UpdateSelectedElements();

            if (selected)
                _handheldNetwork.PushSelectionEvent(NodeIDsToNodeGUIDs(nodeIDs, subnetworkID));

            UpdateHandheld();
            UpdateOptions();
        }

        public void ToggleSelectedNodes(IEnumerable<string> nodeGUIDs)
        {
            HashSet<string> newSelecteds = new();

            foreach (var (subnID, nodeIDs) in SortNodeGUIDs(nodeGUIDs))
            {
                var subnetwork = _allNetworks[subnID];

                var newSelected = subnetwork.ToggleSelectedNodes(nodeIDs);
                subnetwork.UpdateSelectedElements();

                newSelecteds.UnionWith(NodeIDsToNodeGUIDs(newSelected, subnID));
            }

            _handheldNetwork.PushSelectionEvent(newSelecteds);

            UpdateHandheld();
            UpdateOptions();
        }

        public void ToggleSelectedNodes(IEnumerable<int> nodeIDs, int subnetworkID = 0)
        {
            var subnetwork = _allNetworks[subnetworkID];

            IEnumerable<int> newSelecteds = subnetwork.ToggleSelectedNodes(nodeIDs);
            subnetwork.UpdateSelectedElements();

            _handheldNetwork.PushSelectionEvent(NodeIDsToNodeGUIDs(newSelecteds, subnetworkID));

            UpdateHandheld();
            UpdateOptions();
        }

        public void SetSelectedLinks(IEnumerable<string> linkGUIDs, bool selected)
        {
            foreach (var (subnID, linkIDs) in SortLinkGUIDs(linkGUIDs))
            {
                _allNetworks[subnID].SetSelectedLinks(linkIDs, selected);
                _allNetworks[subnID].UpdateSelectedElements();
            }

            UpdateHandheld();
            UpdateOptions();
        }

        public void SetSelectedLinks(IEnumerable<int> linkIDs, bool selected, int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].SetSelectedLinks(linkIDs, selected);
            _allNetworks[subnetworkID].UpdateSelectedElements();

            UpdateHandheld();
            UpdateOptions();
        }

        public void ToggleSelectedLinks(IEnumerable<string> linkGUIDs)
        {
            foreach (var (subnID, linkIDs) in SortLinkGUIDs(linkGUIDs))
            {
                _allNetworks[subnID].ToggleSelectedLinks(linkIDs);
                _allNetworks[subnID].UpdateSelectedElements();
            }

            UpdateHandheld();
            UpdateOptions();
        }

        public void ToggleSelectedLinks(IEnumerable<int> linkIDs, int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].ToggleSelectedLinks(linkIDs);
            _allNetworks[subnetworkID].UpdateSelectedElements();

            UpdateHandheld();
            UpdateOptions();
        }

        public void StartMLNodeMove(string nodeGUID)
        {
            StartMLNodesMove(new string[] { nodeGUID });
        }

        public void StartMLNodeMove(int nodeID, int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].StartNodeMove(nodeID);
        }

        public void StartMLNodesMove(IEnumerable<string> nodeGUIDs)
        {
            foreach (var (subnID, nodeIDs) in SortNodeGUIDs(nodeGUIDs)) StartMLNodesMove(nodeIDs, subnID);
        }

        public void StartMLNodesMove(IEnumerable<int> nodeIDs, int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].StartNodesMove(nodeIDs);
        }

        public void EndMLNodesMove()
        {
            foreach (var subn in _allNetworks.Values) subn.EndNodesMove();
            UpdateHandheld();
        }

        public void EndMLNodesMove(int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].EndNodesMove();
            UpdateHandheld();
        }

        public void StartMLNodesFollow(string toFollowGUID, IEnumerable<string> nodeGUIDs)
        {
            _transformMoverCR = StartCoroutine(CRNodesFollow(toFollowGUID, nodeGUIDs));
        }

        public void EndMLNodesFollow()
        {
            CoroutineUtils.StopIfRunning(this, ref _transformMoverCR);
        }

        public void StartMLCommMove(string commGUID)
        {
            StartMLCommsMove(new string[] { commGUID });
        }

        public void StartMLCommMove(int commID, int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].StartCommMove(commID);
        }

        public void StartMLCommsMove(IEnumerable<string> commGUIDs)
        {
            foreach (var (subnID, commIDs) in SortCommunityGUIDs(commGUIDs)) StartMLCommsMove(commIDs, subnID);
        }

        public void StartMLCommsMove(IEnumerable<int> commIDs, int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].StartCommsMove(commIDs);
        }

        public void EndMLCommsMove()
        {
            foreach (var subn in _allNetworks.Values) subn.EndCommsMove();
            UpdateHandheld();
        }

        public void EndMLCommsMove(int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].EndCommsMove();
            UpdateHandheld();
        }

        public void StartMLCommsFollow(string toFollowGUID, IEnumerable<string> commGUIDs)
        {
            _transformMoverCR = StartCoroutine(CRCommsFollow(toFollowGUID, commGUIDs));
        }

        public void EndMLCommsFollow()
        {
            CoroutineUtils.StopIfRunning(this, ref _transformMoverCR);
        }

        public void StartMLNetworkMove(int subnetworkID)
        {
            _allNetworks[subnetworkID].StartNetworkMove();
        }

        public void StartMLNetworksMove(IEnumerable<int> subnetworkIDs)
        {
            foreach (var subn in subnetworkIDs) _allNetworks[subn].StartNetworkMove();
        }

        public void EndMLNetworksMove()
        {
            foreach (var subn in _allNetworks.Values) subn.EndNetworkMove();
        }

        public void EndMLNetworksMove(int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].EndNetworkMove();
            UpdateHandheld();
        }

        public void StartMLNetworksFollow(int toFollowID, IEnumerable<int> networkIDs)
        {
            _transformMoverCR = StartCoroutine(CRNetworksFollow(toFollowID, networkIDs));
        }

        public void EndMLNetworksFollow()
        {
            CoroutineUtils.StopIfRunning(this, ref _transformMoverCR);
        }

        public void HoverCommunity(int commID)
        {
            _networkGlobal.HoveredCommunity = _networkGlobal.Communities[commID];
            TriggerRenderUpdate();
        }

        public void UnhoverCommunity(int commID)
        {
            _networkGlobal.HoveredCommunity = null;
            TriggerRenderUpdate();
        }

        public void SetSelectedCommunities(IEnumerable<string> commGUIDs, bool selected)
        {
            foreach (var (subnID, commIDs) in SortCommunityGUIDs(commGUIDs))
            {
                _allNetworks[subnID].SetSelectedComms(commIDs, selected);
                _allNetworks[subnID].UpdateSelectedElements();
            }

            if (selected)
                _handheldNetwork.PushSelectionEvent(commGUIDs);

            UpdateHandheld();
            UpdateOptions();
        }

        public void SetSelectedCommunities(IEnumerable<int> commIDs, bool selected, int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].SetSelectedComms(commIDs, selected);
            _allNetworks[subnetworkID].UpdateSelectedElements();

            if (selected)
                _handheldNetwork.PushSelectionEvent(CommIDToNodeGUIDs(commIDs, subnetworkID));

            UpdateHandheld();
            UpdateOptions();
        }

        public void ToggleSelectedCommunities(IEnumerable<string> commGUIDs)
        {
            HashSet<string> newSelecteds = new();

            foreach (var (subnID, commIDs) in SortCommunityGUIDs(commGUIDs))
            {
                var subnetwork = _allNetworks[subnID];
                var selectedComms = subnetwork.ToggleSelectedComms(commIDs);
                subnetwork.UpdateSelectedElements();

                newSelecteds.UnionWith(CommIDToNodeGUIDs(selectedComms, subnID));
            }

            _handheldNetwork.PushSelectionEvent(newSelecteds);

            UpdateHandheld();
            UpdateOptions();
        }

        public void ToggleSelectedCommunities(IEnumerable<int> commIDs, int subnetworkID = 0)
        {
            var selectedComms = _allNetworks[subnetworkID].ToggleSelectedComms(commIDs);
            _allNetworks[subnetworkID].UpdateSelectedElements();

            _handheldNetwork.PushSelectionEvent(CommIDToNodeGUIDs(selectedComms, subnetworkID));

            UpdateHandheld();
            UpdateOptions();
        }

        public void ClearSelection()
        {
            ClearSelectedItems();
            foreach (var subn in _allNetworks.Values) subn.UpdateSelectedElements();

            UpdateHandheld();
            UpdateOptions();
        }

        // layout for MultiLayoutNetwork = [spherical, cluster, floor]
        // layout for BasicSubnetwork = [forceddir]
        public void SetMLLayout(IEnumerable<string> commGUIDs, string layout)
        {
            foreach (var (subnID, commIDs) in SortCommunityGUIDs(commGUIDs)) SetMLLayout(commIDs, layout, subnID);
        }

        public void SetMLLayout(IEnumerable<int> commIDs, string layout, int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].SetLayout(commIDs, layout, UpdateHandheld);
        }

        // layout for MultiLayoutNetwork = [spherical, cluster, floor]
        // layout for BasicSubnetwork = [forceddir]
        public void SetMLLayout(string commGUID, string layout)
        {
            var res = SortCommunityGUIDs(new string[] { commGUID }).First();

            var subnID = res.Key;
            var commID = res.Value.First();

            SetMLLayout(commID, layout, subnID);
        }

        public void SetMLLayout(int commID, string layout, int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].SetLayout(new int[] { commID }, layout, UpdateHandheld);
        }

        public void SetMLLayout(string layout, int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].SetLayout(new int[] { }, layout, UpdateHandheld);
        }

        public void BringMLNodes(IEnumerable<string> nodeGUIDs)
        {
            foreach (var (subnID, nodeIDs) in SortNodeGUIDs(nodeGUIDs)) BringMLNodes(nodeIDs, subnID);
        }

        public void BringMLNodes(IEnumerable<int> nodeIDs, int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].BringNodes(nodeIDs, UpdateHandheld);
        }

        public void ReturnMLNodes(IEnumerable<string> nodeGUIDs)
        {
            foreach (var (subnID, nodeIDs) in SortNodeGUIDs(nodeGUIDs)) ReturnMLNodes(nodeIDs, subnID);
        }

        public void ReturnMLNodes(IEnumerable<int> nodeIDs, int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].ReturnNodes(nodeIDs);
        }

        public Transform GetMLNodeTransform(string nodeGUID)
        {
            var res = SortNodeGUIDs(new string[] { nodeGUID }).First();

            var subnID = res.Key;
            var nodeID = res.Value.First();

            return GetMLNodeTransform(nodeID, subnID);
        }

        public Transform GetMLNodeTransform(int nodeID, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].GetNodeTransform(nodeID);
        }

        public Transform GetMLCommTransform(string commGUID)
        {
            var res = SortCommunityGUIDs(new string[] { commGUID }).First();

            var subnID = res.Key;
            var commID = res.Value.First();

            return GetMLCommTransform(commID, subnID);
        }

        public Transform GetMLCommTransform(int commID, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].GetCommTransform(commID);
        }

        public Transform GetMLNetworkTransform(int subnetworkID)
        {
            return _allNetworks[subnetworkID]?.GetNetworkTransform();
        }

        public MultiLayoutContext CreateSubnetwork(IEnumerable<int> nodeIDs, bool useShell = true, int sourceSubnetworkID = 0)
        {
            if (nodeIDs.Count() == 0) return null;

            var subn = BasicSubnetworkUtils.CreateBasicSubnetwork(_subnetworkPrefab, transform, nodeIDs,
                    _allNetworks[sourceSubnetworkID].Context, useShell, out var gameObj);

            subn.SetStorageUpdateCallback(UpdateStorage);
            // mark communities and nodes dirty to be registered in storage update
            DirtySubnetworkElements(subn);

            _subnetworks[subn.ID] = subn;
            _allNetworks[subn.ID] = subn;
            AddToGUIDMaps(subn);

            TriggerStorageUpdate();
            TriggerRenderUpdate();

            OnSubnetworkCreate?.Invoke(subn);

            return subn.Context;
        }

        public void DeleteSubnetwork(int subnetworkID)
        {
            var subn = _subnetworks[subnetworkID];

            OnSubnetworkDestroy?.Invoke(subn);

            // mark communities and nodes dirty to be registered in storage update
            DirtySubnetworkElements(subn);

            TriggerStorageUpdate();
            TriggerRenderUpdate();

            RemoveFromGUIDMaps(subn);

            _subnetworks.Remove(subn.ID);
            _allNetworks.Remove(subn.ID);

            subn.Destroy();
        }

        public void HideSubnetwork(int subnetworkID)
        {
            _subnetworks[subnetworkID].gameObject.SetActive(false);
        }

        public void ShowSubnetwork(int subnetworkID)
        {
            _subnetworks[subnetworkID].gameObject.SetActive(true);
        }

        public void SwitchToSubnetwork(int subnetworkID)
        {
            if (_curWorkingSubgraph != -1)
            {
                HideSubnetwork(_curWorkingSubgraph);
                _framesArea.Frames[_curWorkingSubgraph].SetSelect(false);
            }

            _multiLayoutNetwork.ClearSelection();
            _multiLayoutNetwork.SetSelectedNodes(_subnetworks[subnetworkID].Context.Nodes.Keys, true);
            _multiLayoutNetwork.UpdateSelectedElements();

            ShowSubnetwork(subnetworkID);
            _framesArea.Frames[subnetworkID].SetSelect(true);

            _curWorkingSubgraph = subnetworkID;
        }

        public MultiLayoutContext CreateSurfSubnetwork(IEnumerable<int> nodeIDs, out int surfaceID, int sourceSubnetworkID = 0)
        {
            surfaceID = -1;

            if (nodeIDs.Count() == 0) return null;

            var subn = BasicSubnetworkUtils.CreateBasicSubnetwork(_subnetworkPrefab, transform, nodeIDs,
                    _allNetworks[sourceSubnetworkID].Context, false, out var gameObj);

            subn.SetStorageUpdateCallback(UpdateStorage);
            // mark communities and nodes dirty to be registered in storage update
            DirtySubnetworkElements(subn);

            _subnetworks[subn.ID] = subn;
            _allNetworks[subn.ID] = subn;
            AddToGUIDMaps(subn);

            TriggerStorageUpdate();
            TriggerRenderUpdate();

            OnSubnetworkCreate?.Invoke(subn);

            var surfID = _surfaceManager.SpawnSurfaceFromPointer();

            // TODO only unselect originally selected nodes
            // SetSelectedNodes(nodeIDs, false, sourceSubnetworkID);
            ClearSelection();

            _surfaceManager.AttachNodes(subn.Context.NodeGUIDToID.Keys, surfID);

            return subn.Context;
        }

        public void DeleteSurfSubnetwork(int subnetworkID)
        {
            // RemoveFromGUIDMaps(subn);
            throw new NotImplementedException();
        }

        // TODO perhaps move this somewhere else, or at least move to util file
        public void GetInnerAndOuterLinks(IEnumerable<string> nodeGUIDs, out List<string> innerLinks, out List<string> outerLinks, out List<bool> isStartOuterLinks)
        {
            innerLinks = new();
            outerLinks = new();
            isStartOuterLinks = new();

            Dictionary<int, List<int>> subnAndInnerLinks = new();
            Dictionary<int, List<int>> subnAndOuterLinks = new();

            foreach (var (subnetworkID, nodeIDs) in SortNodeGUIDs(nodeGUIDs))
            {
                subnAndInnerLinks[subnetworkID] = new();
                subnAndOuterLinks[subnetworkID] = new();

                foreach (var nodeID in nodeIDs)
                {
                    foreach (var link in NetworkGlobal.NodeLinkMatrixUndir[nodeID])
                    {
                        if (!_allNetworks[subnetworkID].Context.Links.Keys.Contains(link.ID)) continue;

                        // check other node of this link
                        if (nodeID == link.SourceNodeID)
                        {
                            // check targetnode of link
                            if (nodeIDs.Contains(link.TargetNodeID))
                            {
                                subnAndInnerLinks[subnetworkID].Add(link.ID);
                            }
                            else
                            {
                                subnAndOuterLinks[subnetworkID].Add(link.ID);
                                isStartOuterLinks.Add(true);
                            }
                        }
                        else
                        {
                            // check sourcenode of link
                            if (nodeIDs.Contains(link.SourceNodeID))
                            {
                                subnAndInnerLinks[subnetworkID].Add(link.ID);
                            }
                            else
                            {
                                subnAndOuterLinks[subnetworkID].Add(link.ID);
                                isStartOuterLinks.Add(false);
                            }
                        }
                    }
                }
            }

            innerLinks = LinkIDsToLinkGUIDs(subnAndInnerLinks).ToList();
            outerLinks = LinkIDsToLinkGUIDs(subnAndOuterLinks).ToList();
        }

        public void SetMLNodesSize(IEnumerable<string> nodeGUIDs, float size)
        {
            foreach (var (subnID, nodeIDs) in SortNodeGUIDs(nodeGUIDs)) SetMLNodesSize(nodeIDs, size, subnID);
        }

        public void SetMLNodesSize(IEnumerable<int> nodeIDs, float size, int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].SetNodesSize(nodeIDs, size, _updatingStorage, _updatingRenderElements);
        }

        public void SetMLNodesColor(IEnumerable<string> nodeGUIDs, string color)
        {
            foreach (var (subnID, nodeIDs) in SortNodeGUIDs(nodeGUIDs)) SetMLNodesColor(nodeIDs, color, subnID);
        }

        public void SetMLNodesColor(IEnumerable<int> nodeIDs, string color, int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].SetNodesColor(nodeIDs, color, _updatingStorage, _updatingRenderElements);
        }

        public void SetMLNodesPosition(IEnumerable<string> nodeGUIDs, Vector3 position)
        {
            foreach (var (subnID, nodeIDs) in SortNodeGUIDs(nodeGUIDs)) SetMLNodesPosition(nodeIDs, position, subnID);
        }

        public void SetMLNodesPosition(IEnumerable<int> nodeIDs, Vector3 position, int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].SetNodesPosition(nodeIDs, position, _updatingStorage, _updatingRenderElements);
        }

        public void SetMLNodesPosition(IEnumerable<string> nodeGUIDs, IEnumerable<Vector3> positions)
        {
            foreach (var (subnID, nodeIDs) in SortNodeGUIDs(nodeGUIDs)) SetMLNodesPosition(nodeIDs, positions, subnID);
        }

        public void SetMLNodesPosition(IEnumerable<int> nodeIDs, IEnumerable<Vector3> positions, int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].SetNodesPosition(nodeIDs, positions, _updatingStorage, _updatingRenderElements);
        }

        public void SetMLLinksWidth(IEnumerable<string> linkGUIDs, float width)
        {
            foreach (var (subnID, linkIDs) in SortLinkGUIDs(linkGUIDs)) SetMLLinksWidth(linkIDs, width, subnID);
        }

        public void SetMLLinksWidth(IEnumerable<int> linkIDs, float width, int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].SetLinksWidth(linkIDs, width, _updatingStorage, _updatingRenderElements);
        }

        public void SetMLLinksColorStart(IEnumerable<string> linkGUIDs, string color)
        {
            foreach (var (subnID, linkIDs) in SortLinkGUIDs(linkGUIDs)) SetMLLinksColorStart(linkIDs, color, subnID);
        }

        public void SetMLLinksColorStart(IEnumerable<int> linkIDs, string color, int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].SetLinksColorStart(linkIDs, color, _updatingStorage, _updatingRenderElements);
        }

        public void SetMLLinksColorEnd(IEnumerable<string> linkGUIDs, string color)
        {
            foreach (var (subnID, linkIDs) in SortLinkGUIDs(linkGUIDs)) SetMLLinksColorEnd(linkIDs, color, subnID);
        }

        public void SetMLLinksColorEnd(IEnumerable<int> linkIDs, string color, int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].SetLinksColorEnd(linkIDs, color, _updatingStorage, _updatingRenderElements);
        }

        public void SetMLLinksAlpha(IEnumerable<string> linkGUIDs, float alpha)
        {
            foreach (var (subnID, linkIDs) in SortLinkGUIDs(linkGUIDs)) SetMLLinksAlpha(linkIDs, alpha, subnID);
        }

        public void SetMLLinksAlpha(IEnumerable<int> linkIDs, float alpha, int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].SetLinksAlpha(linkIDs, alpha, _updatingStorage, _updatingRenderElements);
        }

        public void SetMLLinksBundlingStrength(IEnumerable<string> linkGUIDs, float bundlingStrength)
        {
            foreach (var (subnID, linkIDs) in SortLinkGUIDs(linkGUIDs)) SetMLLinksBundlingStrength(linkIDs, bundlingStrength, subnID);
        }

        public void SetMLLinksBundlingStrength(IEnumerable<int> linkIDs, float bundlingStrength, int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].SetLinksBundlingStrength(linkIDs, bundlingStrength, _updatingStorage, _updatingRenderElements);
        }

        public void SetMLLinksBundleStart(IEnumerable<string> linkGUIDs, bool bundleStart)
        {
            foreach (var (subnID, linkIDs) in SortLinkGUIDs(linkGUIDs)) SetMLLinksBundleStart(linkIDs, bundleStart, subnID);
        }

        public void SetMLLinksBundleStart(IEnumerable<int> linkIDs, bool bundleStart, int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].SetLinksBundleStart(linkIDs, bundleStart, _updatingStorage, _updatingRenderElements);
        }

        public void SetMLLinksBundleEnd(IEnumerable<string> linkGUIDs, bool bundleEnd)
        {
            foreach (var (subnID, linkIDs) in SortLinkGUIDs(linkGUIDs)) SetMLLinksBundleEnd(linkIDs, bundleEnd, subnID);
        }

        public void SetMLLinksBundleEnd(IEnumerable<int> linkIDs, bool bundleEnd, int subnetworkID = 0)
        {
            _allNetworks[subnetworkID].SetLinksBundleEnd(linkIDs, bundleEnd, _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLNodeColorEncoding(string prop, float min = 0f, float max = 1f, string color = "#0000FF" /* blue */, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].SetNodeColorEncoding(prop, min, max, color,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLNodeColorEncoding(string prop, Dictionary<string, string> valueToColor, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].SetNodeColorEncoding(prop, valueToColor,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLNodeColorEncoding(string prop, Dictionary<bool?, string> valueToColor, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].SetNodeColorEncoding(prop, valueToColor,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLNodeSizeEncoding(string prop, float min = 0f, float max = 1f, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].SetNodeSizeEncoding(prop, min, max,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLNodeSizeEncoding(string prop, Dictionary<string, float> valueToSize, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].SetNodeSizeEncoding(prop, valueToSize,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLNodeSizeEncoding(string prop, Dictionary<bool?, float> valueToSize, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].SetNodeSizeEncoding(prop, valueToSize,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkWidthEncoding(string prop, float min = 0f, float max = 1f, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].SetLinkWidthEncoding(prop, min, max,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkWidthEncoding(string prop, Dictionary<string, float> valueToWidth, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].SetLinkWidthEncoding(prop, valueToWidth,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkWidthEncoding(string prop, Dictionary<bool?, float> valueToWidth, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].SetLinkWidthEncoding(prop, valueToWidth,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkBundlingStrengthEncoding(string prop, float min = 0f, float max = 1f, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].SetLinkBundlingStrengthEncoding(prop, min, max,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkBundlingStrengthEncoding(string prop, Dictionary<string, float> valueToBundlingStrength, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].SetLinkBundlingStrengthEncoding(prop, valueToBundlingStrength,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkBundlingStrengthEncoding(string prop, Dictionary<bool?, float> valueToBundlingStrength, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].SetLinkBundlingStrengthEncoding(prop, valueToBundlingStrength,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkColorStartEncoding(string prop, float min = 0f, float max = 1f, string colorStart = "#FFFFFF" /* white */, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].SetLinkColorStartEncoding(prop, min, max, colorStart,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkColorStartEncoding(string prop, Dictionary<string, string> valueToColorStart, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].SetLinkColorStartEncoding(prop, valueToColorStart,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkColorStartEncoding(string prop, Dictionary<bool?, string> valueToColorStart, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].SetLinkColorStartEncoding(prop, valueToColorStart,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkColorEndEncoding(string prop, float min = 0f, float max = 1f, string colorEnd = "#FFFFFF" /* white */, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].SetLinkColorEndEncoding(prop, min, max, colorEnd,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkColorEndEncoding(string prop, Dictionary<string, string> valueToColorEnd, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].SetLinkColorEndEncoding(prop, valueToColorEnd,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkColorEndEncoding(string prop, Dictionary<bool?, string> valueToColorEnd, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].SetLinkColorEndEncoding(prop, valueToColorEnd,
                _updatingStorage, _updatingRenderElements);
        }


        public bool SetMLLinkBundleStartEncoding(string prop, Dictionary<string, bool> valueToDoBundle, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].SetLinkBundleStartEncoding(prop, valueToDoBundle,
                _updatingStorage, _updatingRenderElements);
        }


        public bool SetMLLinkBundleStartEncoding(string prop, Dictionary<bool?, bool> valueToDoBundle, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].SetLinkBundleStartEncoding(prop, valueToDoBundle,
                _updatingStorage, _updatingRenderElements);
        }


        public bool SetMLLinkBundleEndEncoding(string prop, Dictionary<string, bool> valueToDoBundle, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].SetLinkBundleEndEncoding(prop, valueToDoBundle,
                _updatingStorage, _updatingRenderElements);
        }


        public bool SetMLLinkBundleEndEncoding(string prop, Dictionary<bool?, bool> valueToDoBundle, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].SetLinkBundleEndEncoding(prop, valueToDoBundle,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkAlphaEncoding(string prop, float min = 0f, float max = 1f, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].SetLinkAlphaEncoding(prop, min, max,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkAlphaEncoding(string prop, Dictionary<string, float> valueToAlpha, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].SetLinkAlphaEncoding(prop, valueToAlpha,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkAlphaEncoding(string prop, Dictionary<bool?, float> valueToAlpha, int subnetworkID = 0)
        {
            return _allNetworks[subnetworkID].SetLinkAlphaEncoding(prop, valueToAlpha,
                _updatingStorage, _updatingRenderElements);
        }

        public HashSet<int> SubnSelectedNodes(int subnetworkID)
        {
            return _allNetworks[subnetworkID].SelectedNodes;
        }

        public HashSet<int> SubnSelectedLinks(int subnetworkID)
        {
            return _allNetworks[subnetworkID].SelectedLinks;
        }

        public HashSet<int> SubnSelectedCommunities(int subnetworkID)
        {
            return _allNetworks[subnetworkID].SelectedCommunities;
        }

        public bool IsNodeSelected(int nodeID, int subnetworkID)
        {
            return _allNetworks[subnetworkID].SelectedNodes.Contains(nodeID);
        }

        public bool IsLinkSelected(int linkID, int subnetworkID)
        {
            return _allNetworks[subnetworkID].SelectedLinks.Contains(linkID);
        }

        public bool IsCommSelected(int commID, int subnetworkID)
        {
            return _allNetworks[subnetworkID].SelectedCommunities.Contains(commID);
        }

        // returns map of subnetworkIDs and an array of its nodeIDs
        public Dictionary<int, HashSet<int>> SortNodeGUIDs(IEnumerable<string> nodeGUIDs)
        {
            Dictionary<int, HashSet<int>> sorted = new();

            foreach (var subnID in _allNetworks.Keys.ToList())
            {
                var guidToId = _allNetworks[subnID].Context.NodeGUIDToID;
                var nodeIDs = guidToId.Keys.Intersect(nodeGUIDs).Select(guid => guidToId[guid]).ToHashSet();

                if (nodeIDs.Count != 0)
                    sorted[subnID] = nodeIDs;
            }

            return sorted;
        }

        // returns map of subnetworkIDs and an array of its linkIDs
        public Dictionary<int, HashSet<int>> SortLinkGUIDs(IEnumerable<string> linkGUIDs)
        {
            Dictionary<int, HashSet<int>> sorted = new();

            foreach (var subnID in _allNetworks.Keys.ToList())
            {
                var guidToId = _allNetworks[subnID].Context.LinkGUIDToID;
                var linkIDs = guidToId.Keys.Intersect(linkGUIDs).Select(guid => guidToId[guid]).ToHashSet();

                if (linkIDs.Count != 0)
                    sorted[subnID] = linkIDs;
            }

            return sorted;
        }

        // returns map of subnetworkIDs and an array of its communityIDs
        public Dictionary<int, HashSet<int>> SortCommunityGUIDs(IEnumerable<string> communityGUIDs)
        {
            Dictionary<int, HashSet<int>> sorted = new();

            foreach (var subnID in _allNetworks.Keys)
            {
                var guidToId = _allNetworks[subnID].Context.CommunityGUIDToID;
                var commIDs = guidToId.Keys.Intersect(communityGUIDs).Select(guid => guidToId[guid]).ToHashSet();

                if (commIDs.Count != 0)
                    sorted[subnID] = commIDs;
            }

            return sorted;
        }

        // reverse of SortNodeGUIDs
        public IEnumerable<string> NodeIDsToNodeGUIDs(Dictionary<int, List<int>> subnNodeIDs)
        {
            return subnNodeIDs
                .Select(subnAndNodes =>
                    NodeIDsToNodeGUIDs(subnAndNodes.Value, subnAndNodes.Key))
                .SelectMany(i => i);
        }

        // reverse of SortLinkGUIDs
        public IEnumerable<string> LinkIDsToLinkGUIDs(Dictionary<int, List<int>> subnLinkIDs)
        {
            return subnLinkIDs
                .Select(subnAndLinks =>
                    LinkIDsToLinkGUIDs(subnAndLinks.Value, subnAndLinks.Key))
                .SelectMany(i => i);
        }

        // reverse of SortCommunityGUIDs
        public IEnumerable<string> CommIDsToCommGUIDs(Dictionary<int, List<int>> subnCommIDs)
        {
            return subnCommIDs
                .Select(subnAndComms =>
                    CommIDsToCommGUIDs(subnAndComms.Value, subnAndComms.Key))
                .SelectMany(i => i);
        }

        public IEnumerable<string> NodeIDsToNodeGUIDs(IEnumerable<int> nodeIDs, int subnetworkID)
        {
            return nodeIDs.Select(nid => NodeIDToGUID[Tuple.Create(subnetworkID, nid)]);
        }

        public IEnumerable<string> LinkIDsToLinkGUIDs(IEnumerable<int> linkIDs, int subnetworkID)
        {
            return linkIDs.Select(lid => LinkIDToGUID[Tuple.Create(subnetworkID, lid)]);
        }

        public IEnumerable<string> CommIDsToCommGUIDs(IEnumerable<int> commIDs, int subnetworkID)
        {
            return commIDs.Select(cid => CommunityIDToGUID[Tuple.Create(subnetworkID, cid)]);
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
            foreach (var subn in _subnetworks.Values) subn.ClearSelection();
        }

        void UpdateHandheld()
        {
            _handheldNetwork.UpdateRenderElements();
        }

        void UpdateOptions()
        {
            int selectedSubnetworkForNodes = -1;

            int selectedSubnetworkForComms = -1;
            bool onlyOneSelectedForComms = false;

            Dictionary<int, HashSet<int>> subnToSelNodes = new();
            Dictionary<int, HashSet<int>> subnToSelComms = new();

            for (int curSubn = 0; curSubn < _allNetworks.Count; curSubn++)
            {
                var selNodes = SubnSelectedNodes(curSubn);
                if (selNodes.Count != 0)
                {
                    if (selectedSubnetworkForNodes == -1)
                    {
                        selectedSubnetworkForNodes = curSubn;
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
            }

            if (selectedSubnetworkForNodes == -1)
            {
                _optionsMenu.ClearOptions();
                return;
            }

            Dictionary<string, Action> callbacks = new();

            // callbacks["Reset node(s)"] = () =>
            // {
            //     foreach (var (subn, subnNodes) in subnToSelNodes)
            //     {
            //         if (subnNodes.Count != 0) ReturnMLNodes(subnNodes, subn);
            //     }
            // };

            // callbacks["Bring node(s)"] = () =>
            // {
            //     foreach (var (subn, subnNodes) in subnToSelNodes)
            //     {
            //         if (subnNodes.Count != 0) BringMLNodes(subnNodes, subn);
            //     }
            // };

            // callbacks["Create subgraph"] = () =>
            // {
            //     // TODO consider scenario of nodes from different subgraphs?
            //     CreateSurfSubnetwork(subnToSelNodes.Values.First(), out var _, subnToSelNodes.Keys.First());
            // };

            // if (onlyOneSelectedForComms && selectedSubnetworkForComms == 0)
            // {
            //     callbacks["Focus comm."] = () =>
            //     {
            //         SetMLLayout(subnToSelComms[0], "cluster");
            //     };

            //     callbacks["Project comm. floor"] = () =>
            //     {
            //         SetMLLayout(subnToSelComms[0], "floor");
            //     };
            // }

            _optionsMenu.SetOptions(callbacks);
        }

        void DirtySubnetworkElements(BasicSubnetwork subnetwork)
        {
            foreach (var comm in subnetwork.Context.Communities.Values) comm.Dirty = true;
            foreach (var node in subnetwork.Context.Nodes.Values) node.Dirty = true;
        }

        Dictionary<int, HashSet<string>> GetSelNodeGUIDs()
        {
            return _allNetworks.Keys.ToDictionary(
                subnID => subnID,
                subnID => _allNetworks[subnID].SelectedNodeGUIDs
            );
        }

        Dictionary<int, HashSet<string>> GetSelLinkGUIDs()
        {
            return _allNetworks.Keys.ToDictionary(
                subnID => subnID,
                subnID => _allNetworks[subnID].SelectedLinkGUIDs
            );
        }

        Dictionary<int, HashSet<string>> GetSelCommGUIDs()
        {
            return _allNetworks.Keys.ToDictionary(
                subnID => subnID,
                subnID => _allNetworks[subnID].SelectedCommunityGUIDs
            );
        }

        IEnumerable<string> CommIDToNodeGUIDs(IEnumerable<int> commIDs, int subnetworkID)
        {
            return commIDs.Select(cid =>
            {
                var ctx = _allNetworks[subnetworkID].Context;
                return ctx.Communities[cid].Nodes.Select(nid => ctx.Nodes[nid].GUID);
            }).SelectMany(i => i);
        }

        IEnumerator CRNodesFollow(string toFollowGUID, IEnumerable<string> nodeGUIDs)
        {
            Vector3 prevPosition = Vector3.positiveInfinity;
            Quaternion prevRotation = Quaternion.identity;

            var grabbedTransform = GetMLNodeTransform(toFollowGUID);
            var otherTransforms = nodeGUIDs.Where(nguid => nguid != toFollowGUID).Select(nguid => GetMLNodeTransform(nguid));

            while (true)
            {
                var curPosition = grabbedTransform.position;
                var curRotation = grabbedTransform.rotation;

                if (float.IsFinite(prevPosition.x))
                {
                    var diff = curPosition - prevPosition;
                    var diffRot = curRotation * Quaternion.Inverse(prevRotation);
                    diffRot.ToAngleAxis(out var angle, out var axis);

                    foreach (var child in otherTransforms)
                    {
                        child.RotateAround(prevPosition, axis, angle);

                        child.position += diff;
                    }
                }

                prevPosition = curPosition;
                prevRotation = curRotation;
                yield return null;
            }
        }

        IEnumerator CRCommsFollow(string toFollowGUID, IEnumerable<string> commGUIDs)
        {
            Vector3 prevPosition = Vector3.positiveInfinity;
            Quaternion prevRotation = Quaternion.identity;

            var grabbedTransform = GetMLCommTransform(toFollowGUID);
            var otherTransforms = commGUIDs.Where(cguid => cguid != toFollowGUID).Select(cguid => GetMLCommTransform(cguid));

            while (true)
            {
                var curPosition = grabbedTransform.position;
                var curRotation = grabbedTransform.rotation;

                if (float.IsFinite(prevPosition.x))
                {
                    var diff = curPosition - prevPosition;
                    var diffRot = curRotation * Quaternion.Inverse(prevRotation);
                    diffRot.ToAngleAxis(out var angle, out var axis);

                    foreach (var child in otherTransforms)
                    {
                        child.RotateAround(prevPosition, axis, angle);

                        child.position += diff;
                    }
                }

                prevPosition = curPosition;
                prevRotation = curRotation;
                yield return null;
            }
        }

        IEnumerator CRNetworksFollow(int toFollowID, IEnumerable<int> networkIDs)
        {
            Vector3 prevPosition = Vector3.positiveInfinity;
            Quaternion prevRotation = Quaternion.identity;

            var grabbedTransform = GetMLNetworkTransform(toFollowID);
            var otherTransforms = networkIDs.Where(cid => cid != toFollowID).Select(cid => GetMLNetworkTransform(cid));

            while (true)
            {
                var curPosition = grabbedTransform.position;
                var curRotation = grabbedTransform.rotation;

                if (float.IsFinite(prevPosition.x))
                {
                    var diff = curPosition - prevPosition;
                    var diffRot = curRotation * Quaternion.Inverse(prevRotation);
                    diffRot.ToAngleAxis(out var angle, out var axis);

                    foreach (var child in otherTransforms)
                    {
                        child.RotateAround(prevPosition, axis, angle);

                        child.position += diff;
                    }
                }

                prevPosition = curPosition;
                prevRotation = curRotation;
                yield return null;
            }
        }
    }
}