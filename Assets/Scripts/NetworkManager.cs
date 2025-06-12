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

        public void SetSelectedNodes(IEnumerable<int> nodeIDs, bool selected)
        {
            _networkGlobal.SetSelectedNodes(nodeIDs, selected);
            _multiLayoutNetwork.UpdateSelectedElements();
            _handheldNetwork.UpdateRenderElements();
            _multiLayoutNetworkInput.CheckSelectionActions();
        }

        public void ToggleSelectedNodes(IEnumerable<int> nodeIDs)
        {
            _networkGlobal.ToggleSelectedNodes(nodeIDs);
            _multiLayoutNetwork.UpdateSelectedElements();
            _handheldNetwork.UpdateRenderElements();
            _multiLayoutNetworkInput.CheckSelectionActions();
        }

        public void StartMLNodeMove(int nodeID)
        {
            _multiLayoutNetwork.StartNodeMove(nodeID);
        }

        public void StartMLNodesMove(IEnumerable<int> nodeIDs)
        {
            _multiLayoutNetwork.StartNodesMove(nodeIDs);
        }

        public void EndMLNodeMove(int nodeID, Transform transform)
        {
            _multiLayoutNetwork.EndNodeMove(nodeID);
        }

        public void EndMLNodesMove(IEnumerable<int> nodeIDs)
        {
            _multiLayoutNetwork.EndNodesMove();
        }

        public void StartMLCommMove(int commID)
        {
            _multiLayoutNetwork.StartCommMove(commID);
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

        public void SetSelectedCommunities(IEnumerable<int> commIDs, bool selected)
        {
            _networkGlobal.SetSelectedCommunities(commIDs, selected);
            _multiLayoutNetwork.UpdateSelectedElements();
            _handheldNetwork.UpdateRenderElements();
            _multiLayoutNetworkInput.CheckSelectionActions();
        }

        public void ToggleSelectedCommunities(IEnumerable<int> commIDs)
        {
            _networkGlobal.ToggleSelectedCommunities(commIDs);
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

        public void BringMLNodes(IEnumerable<int> nodeIDs)
        {
            _multiLayoutNetwork.BringNodes(nodeIDs);
        }

        public void ReturnMLNodes(IEnumerable<int> nodeIDs)
        {
            _multiLayoutNetwork.ReturnNodes(nodeIDs);
        }

        public Transform GetMLNodeTransform(int nodeID)
        {
            return _multiLayoutNetwork.GetNodeTransform(nodeID);
        }

        public Transform GetMLCommTransform(int commID)
        {
            return _multiLayoutNetwork.GetCommTransform(commID);
        }
        public void SetNodesSize(IEnumerable<int> nodeIDs, float size)
        {
            _multiLayoutNetwork.SetNodesSize(nodeIDs, size, _updatingStorage, _updatingRenderElements);
        }

        public void SetNodesColor(IEnumerable<int> nodeIDs, Color color)
        {
            _multiLayoutNetwork.SetNodesColor(nodeIDs, color, _updatingStorage, _updatingRenderElements);
        }

        public void SetNodesPosition(IEnumerable<int> nodeIDs, Vector3 position)
        {
            _multiLayoutNetwork.SetNodesPosition(nodeIDs, position, _updatingStorage, _updatingRenderElements);
        }

        public void SetNodesPosition(IEnumerable<int> nodeIDs, IEnumerable<Vector3> positions)
        {
            _multiLayoutNetwork.SetNodesPosition(nodeIDs, positions, _updatingStorage, _updatingRenderElements);
        }

        public void SetLinksWidth(IEnumerable<int> linkIDs, float width)
        {
            _multiLayoutNetwork.SetLinksWidth(linkIDs, width, _updatingStorage, _updatingRenderElements);
        }

        public void SetLinksColorStart(IEnumerable<int> linkIDs, Color color)
        {
            _multiLayoutNetwork.SetLinksColorStart(linkIDs, color, _updatingStorage, _updatingRenderElements);
        }

        public void SetLinksColorEnd(IEnumerable<int> linkIDs, Color color)
        {
            _multiLayoutNetwork.SetLinksColorEnd(linkIDs, color, _updatingStorage, _updatingRenderElements);
        }

        public void SetLinksAlpha(IEnumerable<int> linkIDs, float alpha)
        {
            _multiLayoutNetwork.SetLinksAlpha(linkIDs, alpha, _updatingStorage, _updatingRenderElements);
        }

        public void SetLinksBundlingStrength(IEnumerable<int> linkIDs, float bundlingStrength)
        {
            _multiLayoutNetwork.SetLinksBundlingStrength(linkIDs, bundlingStrength, _updatingStorage, _updatingRenderElements);
        }

        public void SetLinksBundleStart(IEnumerable<int> linkIDs, bool bundleStart)
        {
            _multiLayoutNetwork.SetLinksBundleStart(linkIDs, bundleStart, _updatingStorage, _updatingRenderElements);
        }

        public void SetLinksBundleEnd(IEnumerable<int> linkIDs, bool bundleEnd)
        {
            _multiLayoutNetwork.SetLinksBundleEnd(linkIDs, bundleEnd, _updatingStorage, _updatingRenderElements);
        }
    }
}