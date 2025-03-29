/*
*
* TODO Description goes here
*
*/

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
        HandheldNetwork _handheldNetwork;

        NetworkFilesLoader _fileLoader;
        NetworkGlobal _networkGlobal;

        public NetworkFilesLoader FileLoader { get { return _fileLoader; } }
        public NetworkGlobal NetworkGlobal { get { return _networkGlobal; } }

        HashSet<int> _selectedNodes = new HashSet<int>();
        public HashSet<int> SelectedNodes { get { return _selectedNodes; } }
        // HashSet<int> _selectedCommunities = new HashSet<int>();
        // public HashSet<int> SelectedCommunities { get { return _selectedCommunities; } }
        // Dictionary<int, HashSet<int>> _selectedNodesInCommunity = new Dictionary<int, HashSet<int>>();
        public HashSet<int> SelectedCommunities { get { return GetCompleteCommunities(SelectedNodes); } }


        void Awake()
        {
        }

        void Start()
        {
            Initialize();
        }

        void Update()
        {
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

        public void CycleCommunityFocus(int community, bool animated = true)
        {
            _multiLayoutNetwork.CycleCommunityFocus(community, animated);
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

        public void SetSelectedNodes(List<int> nodeIDs)
        {

        }

        public void ToggleSelectedNodes(List<int> nodeIDs)
        {
            // TODO move this to NetworkGlobal
            // we'll only keep track of nodes that have changed
            List<int> selectOn = new List<int>();
            List<int> selectOff = new List<int>();

            foreach (var nodeID in nodeIDs)
            {
                if (_selectedNodes.Contains(nodeID))
                {
                    selectOff.Add(nodeID);
                    _selectedNodes.Remove(nodeID);

                    _networkGlobal.Nodes[nodeID].Selected = false;
                    _networkGlobal.Nodes[nodeID].Dirty = true;
                }
                else
                {
                    selectOn.Add(nodeID);
                    _selectedNodes.Add(nodeID);

                    _networkGlobal.Nodes[nodeID].Selected = true;
                    _networkGlobal.Nodes[nodeID].Dirty = true;
                }
            }
            _multiLayoutNetwork.SetNodesSelected(selectOn, true);
            _multiLayoutNetwork.SetNodesSelected(selectOff, false);

            UpdateSelectedCommunities();
            _multiLayoutNetwork.UpdateRenderElements();
        }

        public void HoverCommunity(int communityID)
        {
            _networkGlobal.HoveredCommunity = _networkGlobal.Communities[communityID];
            _multiLayoutNetwork.UpdateRenderElements();
        }

        public void UnhoverCommunity(int communityID)
        {
            _networkGlobal.HoveredCommunity = null;
            _multiLayoutNetwork.UpdateRenderElements();
        }

        public void ToggleSelectedCommunities(List<int> commIDs)
        {
            var oldSelectedNodes = new HashSet<int>(_selectedNodes);

            _selectedNodes.Clear();

            foreach (var commID in commIDs)
            {
                var globalComm = _networkGlobal.Communities[commID];
                if (globalComm.Selected)
                {
                    globalComm.Selected = false;
                    _multiLayoutNetwork.SetCommunitySelected(commID, false);
                    _selectedNodes.ExceptWith(globalComm.Nodes.Select(n => n.ID));
                }
                else
                {
                    globalComm.Selected = true;
                    _multiLayoutNetwork.SetCommunitySelected(commID, true);
                    _selectedNodes.UnionWith(globalComm.Nodes.Select(n => n.ID));
                }
                globalComm.Dirty = true;
            }

            var newSelected = _selectedNodes.Except(oldSelectedNodes);
            var removed = oldSelectedNodes.Except(_selectedNodes);

            _multiLayoutNetwork.SetNodesSelected(newSelected.ToList(), true);
            _multiLayoutNetwork.SetNodesSelected(removed.ToList(), false);
            _multiLayoutNetwork.UpdateRenderElements();
        }

        public void ClearSelection()
        {
            foreach (var nodeID in _selectedNodes)
            {
                _networkGlobal.Nodes[nodeID].Selected = false;
                _networkGlobal.Nodes[nodeID].Dirty = true;
            }

            _selectedNodes.Clear();

            foreach (var (_, comm) in _networkGlobal.Communities)
            {
                comm.Selected = false;
                // just mark all of them dirty, there usually isn't many communities
                comm.Dirty = true;
            }

            _multiLayoutNetwork.ClearSelection();
            _multiLayoutNetwork.UpdateRenderElements();
        }

        // return a list of communityIDs of communities where all of its nodes are selected.
        // this helps us select a community by also selecting all of its nodes.
        // hashsets ensure no duplicates.
        HashSet<int> GetCompleteCommunities(HashSet<int> nodeIDs)
        {
            Dictionary<int, int> curCommSize = new Dictionary<int, int>();

            foreach (var nodeID in nodeIDs)
            {
                int commID = _networkGlobal.Nodes[nodeID].CommunityID;

                if (!curCommSize.ContainsKey(commID))
                    curCommSize[commID] = 0;

                curCommSize[commID] += 1;
            }

            HashSet<int> completeComm = new HashSet<int>();

            foreach (var (commID, size) in curCommSize)
            {
                if (size == _networkGlobal.Communities[commID].Nodes.Count)
                    completeComm.Add(commID);
            }

            return completeComm;
        }

        void UpdateSelectedCommunities()
        {
            // we'll just calculate complete communities every time since there usually isn't a lot of communities.
            var completeComms = GetCompleteCommunities(_selectedNodes);
            var incompleteComms = _networkGlobal.Communities.Keys.ToHashSet().Except(completeComms);

            _multiLayoutNetwork.SetCommunitiesSelected(completeComms.ToList(), true);
            _multiLayoutNetwork.SetCommunitiesSelected(incompleteComms.ToList(), false);

            foreach (var comm in completeComms)
            {
                _networkGlobal.Communities[comm].Selected = true;
                _networkGlobal.Communities[comm].Dirty = true;
            }

            foreach (var comm in incompleteComms)
            {
                _networkGlobal.Communities[comm].Selected = false;
                _networkGlobal.Communities[comm].Dirty = true;
            }
        }
    }
}