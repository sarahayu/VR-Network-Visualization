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

        void Start()
        {
            Initialize();

            _storage = GameObject.Find("/Database")?.GetComponent<NetworkStorage>();
            _storage?.InitialStore(_networkGlobal, _multiLayoutNetwork.Context);

            _multiLayoutNetwork.SetStorageUpdateCallback(() => _storage?.UpdateStore(_networkGlobal, _multiLayoutNetwork.Context));
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
                opts.Add("Reset Node");
            }

            if (SelectedCommunities.Count > 0)
            {
                opts.Add("Bring Comm.");
                opts.Add("Reset Comm.");
                opts.Add("Project Comm. Floor");
            }

            return opts;
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
            _multiLayoutNetwork.BringNodes(nodeIDs);
        }

        public void ReturnMLNodes(List<int> nodeIDs)
        {
            _multiLayoutNetwork.ReturnNodes(nodeIDs);
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
        public void SetNodesSize(List<int> nodeIDs, float size)
        {
            _multiLayoutNetwork.SetNodesSize(nodeIDs, size);
        }

        public void SetNodesColor(List<int> nodeIDs, Color color)
        {
            _multiLayoutNetwork.SetNodesColor(nodeIDs, color);
        }

        public void SetNodesPosition(List<int> nodeIDs, Vector3 position, bool updateStorage = true)
        {
            _multiLayoutNetwork.SetNodesPosition(nodeIDs, position, updateStorage);
        }

        public void SetNodesPosition(List<int> nodeIDs, List<Vector3> positions, bool updateStorage = true)
        {
            _multiLayoutNetwork.SetNodesPosition(nodeIDs, positions, updateStorage);
        }

        public void SetLinksWidth(List<int> linkIDs, float width)
        {
            _multiLayoutNetwork.SetLinksWidth(linkIDs, width);
        }

        public void SetLinksColorStart(List<int> linkIDs, Color color)
        {
            _multiLayoutNetwork.SetLinksColorStart(linkIDs, color);
        }

        public void SetLinksColorEnd(List<int> linkIDs, Color color)
        {
            _multiLayoutNetwork.SetLinksColorEnd(linkIDs, color);
        }

        public void SetLinksAlpha(List<int> linkIDs, float alpha)
        {
            _multiLayoutNetwork.SetLinksAlpha(linkIDs, alpha);
        }
    }
}