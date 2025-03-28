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
        HashSet<int> _selectedCommunities = new HashSet<int>();
        public HashSet<int> SelectedCommunities { get { return _selectedCommunities; } }
        Dictionary<int, HashSet<int>> _selectedNodesInCommunity = new Dictionary<int, HashSet<int>>();


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
            // TODO REFACTOR
            List<int> selectOn = new List<int>();
            List<int> selectOff = new List<int>();

            List<int> selectOnComm = new List<int>();
            List<int> selectOffComm = new List<int>();

            foreach (var nodeID in nodeIDs)
            {
                var commID = _networkGlobal.Nodes[nodeID].CommunityID;

                if (_selectedNodes.Contains(nodeID))
                {
                    selectOff.Add(nodeID);
                    _selectedNodes.Remove(nodeID);

                    _networkGlobal.Nodes[nodeID].Selected = false;
                    _networkGlobal.Nodes[nodeID].Dirty = true;

                    // if we're removing node from community that had all its nodes selected, also deselect that community
                    if (_selectedNodesInCommunity[commID].Count == _networkGlobal.Communities[commID].Nodes.Count)
                    {
                        selectOffComm.Add(commID);
                        _networkGlobal.Communities[commID].Selected = false;
                        _networkGlobal.Communities[commID].Dirty = true;
                    }

                    _selectedNodesInCommunity[commID].Remove(nodeID);
                }
                else
                {
                    selectOn.Add(nodeID);
                    _selectedNodes.Add(nodeID);

                    _networkGlobal.Nodes[nodeID].Selected = true;
                    _networkGlobal.Nodes[nodeID].Dirty = true;

                    if (!_selectedNodesInCommunity.ContainsKey(commID))
                        _selectedNodesInCommunity[commID] = new HashSet<int>();

                    _selectedNodesInCommunity[commID].Add(nodeID);

                    // if we're adding a node to community and it becomes full, also select that community
                    if (_selectedNodesInCommunity[commID].Count == _networkGlobal.Communities[commID].Nodes.Count)
                    {
                        selectOnComm.Add(commID);

                        _networkGlobal.Communities[commID].Selected = true;
                        _networkGlobal.Communities[commID].Dirty = true;
                    }
                }
            }
            _multiLayoutNetwork.SetNodesSelected(selectOn, true);
            _multiLayoutNetwork.SetNodesSelected(selectOff, false);

            _multiLayoutNetwork.SetCommunitiesSelected(selectOnComm, true);
            _multiLayoutNetwork.SetCommunitiesSelected(selectOffComm, false);
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
            // TODO REFACTOR
            List<int> selectOn = new List<int>();
            List<int> selectOff = new List<int>();

            List<int> selectOnNode = new List<int>();
            List<int> selectOffNode = new List<int>();

            foreach (var commID in commIDs)
            {
                if (_selectedCommunities.Contains(commID))
                {
                    selectOff.Add(commID);
                    _selectedCommunities.Remove(commID);

                    _networkGlobal.Communities[commID].Selected = false;
                    _networkGlobal.Communities[commID].Dirty = true;

                    // remove all nodes in the community from selectedNodes, if they appear there
                    var nodeIDs = _networkGlobal.Communities[commID].Nodes.Select(n => n.ID);
                    _selectedNodes.RemoveWhere(n => nodeIDs.Contains(n));
                    _selectedNodesInCommunity[commID].Clear();
                    selectOffNode.AddRange(nodeIDs);

                    foreach (var nodeID in nodeIDs)
                    {
                        _networkGlobal.Nodes[nodeID].Selected = false;
                        _networkGlobal.Nodes[nodeID].Dirty = true;
                    }
                }
                else
                {
                    selectOn.Add(commID);
                    _selectedCommunities.Add(commID);

                    _networkGlobal.Communities[commID].Selected = true;
                    _networkGlobal.Communities[commID].Dirty = true;

                    // add all nodes in the community into selectedNodes, even if they are already there (hashset ensures uniqueness)
                    var nodeIDs = _networkGlobal.Communities[commID].Nodes.Select(n => n.ID);
                    _selectedNodes.UnionWith(nodeIDs);
                    if (!_selectedNodesInCommunity.ContainsKey(commID))
                        _selectedNodesInCommunity[commID] = new HashSet<int>();
                    _selectedNodesInCommunity[commID].UnionWith(nodeIDs);
                    // also select all nodes in community
                    selectOnNode.AddRange(nodeIDs);

                    foreach (var nodeID in nodeIDs)
                    {
                        _networkGlobal.Nodes[nodeID].Selected = true;
                        _networkGlobal.Nodes[nodeID].Dirty = true;
                    }
                }
            }

            _multiLayoutNetwork.SetCommunitiesSelected(selectOn, true);
            _multiLayoutNetwork.SetCommunitiesSelected(selectOff, false);

            _multiLayoutNetwork.SetNodesSelected(selectOnNode, true);
            _multiLayoutNetwork.SetNodesSelected(selectOffNode, false);
            _multiLayoutNetwork.UpdateRenderElements();
        }

        public void ClearSelection()
        {
            foreach (var nodeID in _selectedNodes)
            {
                _networkGlobal.Nodes[nodeID].Selected = false;
                _networkGlobal.Nodes[nodeID].Dirty = true;
            }

            foreach (var commID in _selectedCommunities)
            {
                _networkGlobal.Communities[commID].Selected = false;
                _networkGlobal.Communities[commID].Dirty = true;
            }

            _selectedNodes.Clear();
            _selectedCommunities.Clear();
            _multiLayoutNetwork.ClearSelection();
            _multiLayoutNetwork.UpdateRenderElements();
        }
    }
}