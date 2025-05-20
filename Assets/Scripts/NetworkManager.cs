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
        HandheldNetwork _handheldNetwork;


        NetworkFilesLoader _fileLoader;
        NetworkStorage _storage;
        public NetworkFilesLoader FileLoader { get { return _fileLoader; } }

        NetworkGlobal _networkGlobal;
        public NetworkGlobal NetworkGlobal { get { return _networkGlobal; } }

        public HashSet<int> SelectedNodes { get { return _networkGlobal.SelectedNodes; } }
        public HashSet<int> SelectedCommunities { get { return _networkGlobal.SelectedCommunities; } }



        void Awake()
        {
        }

        void Start()
        {
            Initialize();

            _storage = GameObject.Find("/Database").GetComponent<NetworkStorage>();
            _storage.InitialStore(_networkGlobal, _multiLayoutNetwork.Context);
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

        public HashSet<string> GetValidOptions()
        {
            HashSet<string> opts = new HashSet<string>();

            if (SelectedNodes.Count > 0)
            {
                opts.Add("Bring Node");
                opts.Add("Return Node");
            }

            if (SelectedCommunities.Count > 0)
            {
                opts.Add("Bring Comm.");
                opts.Add("Return Comm.");
                opts.Add("Project Comm. Floor");
            }

            return opts;
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

        public void SetSelectedNodes(List<int> nodeIDs, bool selected)
        {
            _networkGlobal.SetSelectedNodes(nodeIDs, selected);
            _multiLayoutNetwork.UpdateSelectedElements();
        }

        public void ToggleSelectedNodes(List<int> nodeIDs)
        {
            _networkGlobal.ToggleSelectedNodes(nodeIDs);
            _multiLayoutNetwork.UpdateSelectedElements();
        }

        public void StartMLNodeMove(int nodeID)
        {
            _multiLayoutNetwork.StartNodeMove(nodeID, _multiLayoutNetwork.GetNodeTransform(nodeID));
        }

        public void StartMLNodesMove(List<int> nodeIDs)
        {
            _multiLayoutNetwork.StartNodesMove(nodeIDs, nodeIDs.Select(id => _multiLayoutNetwork.GetNodeTransform(id)).ToList());
        }

        public void EndMLNodeMove(int nodeID, Transform transform)
        {
            _multiLayoutNetwork.EndNodeMove(nodeID);

        }

        public void EndMLNodesMove(List<int> nodeIDs)
        {
            _multiLayoutNetwork.EndNodesMove();

        }

        public void StartMLCommMove(int commID)
        {
            _multiLayoutNetwork.StartCommMove(commID, _multiLayoutNetwork.GetCommTransform(commID));
        }

        public void EndMLCommMove(int commID)
        {
            _multiLayoutNetwork.EndCommMove();

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

        public void SetSelectedCommunities(List<int> commIDs, bool selected)
        {
            _networkGlobal.SetSelectedCommunities(commIDs, selected);
            _multiLayoutNetwork.UpdateSelectedElements();
        }

        public void ToggleSelectedCommunities(List<int> commIDs)
        {
            _networkGlobal.ToggleSelectedCommunities(commIDs);
            _multiLayoutNetwork.UpdateSelectedElements();
        }

        public void ClearSelection()
        {
            _networkGlobal.ClearSelectedItems();
            _multiLayoutNetwork.UpdateSelectedElements();

        }

        public void StartCommunityMove(int communityID, Transform transform)
        {

        }

        public void EndCommunityMove(int communityID, Transform transform)
        {

        }

        // layout = [spherical, cluster, floor]
        public void SetMLLayout(List<int> commIDs, string layout)
        {
            _multiLayoutNetwork.SetLayout(commIDs, layout);
        }

        // layout = [spherical, cluster, floor]
        public void SetMLLayout(int commID, string layout)
        {
            _multiLayoutNetwork.SetLayout(new List<int> { commID }, layout);
        }

        public void BringMLNodes(List<int> nodeIDs)
        {
            _multiLayoutNetwork.SetNodesBrought(nodeIDs, true);
        }

        public void ReturnMLNodes(List<int> nodeIDs)
        {
            _multiLayoutNetwork.SetNodesBrought(nodeIDs, false);
        }

        public void SetMLNodeSizeEncoding(Func<VidiGraph.Node, float> func)
        {
            _multiLayoutNetwork.SetNodeSizeEncoding(func);
        }

        public Transform GetMLNodeTransform(int nodeID)
        {
            return _multiLayoutNetwork.GetNodeTransform(nodeID);
        }

        public Transform GetMLCommTransform(int commID)
        {
            return _multiLayoutNetwork.GetCommTransform(commID);
        }
    }
}