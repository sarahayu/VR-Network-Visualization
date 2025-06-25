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

        public void SetMLNodesSize(IEnumerable<int> nodeIDs, float size)
        {
            _multiLayoutNetwork.SetNodesSize(nodeIDs, size, _updatingStorage, _updatingRenderElements);
        }

        public void SetMLNodesColor(IEnumerable<int> nodeIDs, Color color)
        {
            _multiLayoutNetwork.SetNodesColor(nodeIDs, color, _updatingStorage, _updatingRenderElements);
        }

        public void SetMLNodesPosition(IEnumerable<int> nodeIDs, Vector3 position)
        {
            _multiLayoutNetwork.SetNodesPosition(nodeIDs, position, _updatingStorage, _updatingRenderElements);
        }

        public void SetMLNodesPosition(IEnumerable<int> nodeIDs, IEnumerable<Vector3> positions)
        {
            _multiLayoutNetwork.SetNodesPosition(nodeIDs, positions, _updatingStorage, _updatingRenderElements);
        }

        public void SetMLLinksWidth(IEnumerable<int> linkIDs, float width)
        {
            _multiLayoutNetwork.SetLinksWidth(linkIDs, width, _updatingStorage, _updatingRenderElements);
        }

        public void SetMLLinksColorStart(IEnumerable<int> linkIDs, Color color)
        {
            _multiLayoutNetwork.SetLinksColorStart(linkIDs, color, _updatingStorage, _updatingRenderElements);
        }

        public void SetMLLinksColorEnd(IEnumerable<int> linkIDs, Color color)
        {
            _multiLayoutNetwork.SetLinksColorEnd(linkIDs, color, _updatingStorage, _updatingRenderElements);
        }

        public void SetMLLinksAlpha(IEnumerable<int> linkIDs, float alpha)
        {
            _multiLayoutNetwork.SetLinksAlpha(linkIDs, alpha, _updatingStorage, _updatingRenderElements);
        }

        public void SetMLLinksBundlingStrength(IEnumerable<int> linkIDs, float bundlingStrength)
        {
            _multiLayoutNetwork.SetLinksBundlingStrength(linkIDs, bundlingStrength, _updatingStorage, _updatingRenderElements);
        }

        public void SetMLLinksBundleStart(IEnumerable<int> linkIDs, bool bundleStart)
        {
            _multiLayoutNetwork.SetLinksBundleStart(linkIDs, bundleStart, _updatingStorage, _updatingRenderElements);
        }

        public void SetMLLinksBundleEnd(IEnumerable<int> linkIDs, bool bundleEnd)
        {
            _multiLayoutNetwork.SetLinksBundleEnd(linkIDs, bundleEnd, _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLNodeColorEncoding(string prop, float min = 0f, float max = 1f, string color = "#0000FF" /* blue */)
        {
            return _multiLayoutNetwork.SetNodeColorEncoding(prop, min, max, color,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLNodeColorEncoding(string prop, Dictionary<string, string> valueToColor)
        {
            return _multiLayoutNetwork.SetNodeColorEncoding(prop, valueToColor,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLNodeColorEncoding(string prop, Dictionary<bool?, string> valueToColor)
        {
            return _multiLayoutNetwork.SetNodeColorEncoding(prop, valueToColor,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLNodeSizeEncoding(string prop, float min = 0f, float max = 1f)
        {
            return _multiLayoutNetwork.SetNodeSizeEncoding(prop, min, max,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLNodeSizeEncoding(string prop, Dictionary<string, float> valueToSize)
        {
            return _multiLayoutNetwork.SetNodeSizeEncoding(prop, valueToSize,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLNodeSizeEncoding(string prop, Dictionary<bool?, float> valueToSize)
        {
            return _multiLayoutNetwork.SetNodeSizeEncoding(prop, valueToSize,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkWidthEncoding(string prop, float min = 0f, float max = 1f)
        {
            return _multiLayoutNetwork.SetLinkWidthEncoding(prop, min, max,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkWidthEncoding(string prop, Dictionary<string, float> valueToWidth)
        {
            return _multiLayoutNetwork.SetLinkWidthEncoding(prop, valueToWidth,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkWidthEncoding(string prop, Dictionary<bool?, float> valueToWidth)
        {
            return _multiLayoutNetwork.SetLinkWidthEncoding(prop, valueToWidth,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkBundlingStrengthEncoding(string prop, float min = 0f, float max = 1f)
        {
            return _multiLayoutNetwork.SetLinkBundlingStrengthEncoding(prop, min, max,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkBundlingStrengthEncoding(string prop, Dictionary<string, float> valueToBundlingStrength)
        {
            return _multiLayoutNetwork.SetLinkBundlingStrengthEncoding(prop, valueToBundlingStrength,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkBundlingStrengthEncoding(string prop, Dictionary<bool?, float> valueToBundlingStrength)
        {
            return _multiLayoutNetwork.SetLinkBundlingStrengthEncoding(prop, valueToBundlingStrength,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkColorStartEncoding(string prop, float min = 0f, float max = 1f, string colorStart = "#FFFFFF" /* white */)
        {
            return _multiLayoutNetwork.SetLinkColorStartEncoding(prop, min, max, colorStart,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkColorStartEncoding(string prop, Dictionary<string, string> valueToColorStart)
        {
            return _multiLayoutNetwork.SetLinkColorStartEncoding(prop, valueToColorStart,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkColorStartEncoding(string prop, Dictionary<bool?, string> valueToColorStart)
        {
            return _multiLayoutNetwork.SetLinkColorStartEncoding(prop, valueToColorStart,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkColorEndEncoding(string prop, float min = 0f, float max = 1f, string colorEnd = "#FFFFFF" /* white */)
        {
            return _multiLayoutNetwork.SetLinkColorEndEncoding(prop, min, max, colorEnd,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkColorEndEncoding(string prop, Dictionary<string, string> valueToColorEnd)
        {
            return _multiLayoutNetwork.SetLinkColorEndEncoding(prop, valueToColorEnd,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkColorEndEncoding(string prop, Dictionary<bool?, string> valueToColorEnd)
        {
            return _multiLayoutNetwork.SetLinkColorEndEncoding(prop, valueToColorEnd,
                _updatingStorage, _updatingRenderElements);
        }

        // TODO untested
        public bool SetMLLinkBundleStartEncoding(string prop, Dictionary<string, bool> valueToDoBundle)
        {
            return _multiLayoutNetwork.SetLinkBundleStartEncoding(prop, valueToDoBundle,
                _updatingStorage, _updatingRenderElements);
        }

        // TODO untested
        public bool SetMLLinkBundleStartEncoding(string prop, Dictionary<bool?, bool> valueToDoBundle)
        {
            return _multiLayoutNetwork.SetLinkBundleStartEncoding(prop, valueToDoBundle,
                _updatingStorage, _updatingRenderElements);
        }

        // TODO untested
        public bool SetMLLinkBundleEndEncoding(string prop, Dictionary<string, bool> valueToDoBundle)
        {
            return _multiLayoutNetwork.SetLinkBundleEndEncoding(prop, valueToDoBundle,
                _updatingStorage, _updatingRenderElements);
        }

        // TODO untested
        public bool SetMLLinkBundleEndEncoding(string prop, Dictionary<bool?, bool> valueToDoBundle)
        {
            return _multiLayoutNetwork.SetLinkBundleEndEncoding(prop, valueToDoBundle,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkAlphaEncoding(string prop, float min = 0f, float max = 1f)
        {
            return _multiLayoutNetwork.SetLinkAlphaEncoding(prop, min, max,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkAlphaEncoding(string prop, Dictionary<string, float> valueToAlpha)
        {
            return _multiLayoutNetwork.SetLinkAlphaEncoding(prop, valueToAlpha,
                _updatingStorage, _updatingRenderElements);
        }

        public bool SetMLLinkAlphaEncoding(string prop, Dictionary<bool?, float> valueToAlpha)
        {
            return _multiLayoutNetwork.SetLinkAlphaEncoding(prop, valueToAlpha,
                _updatingStorage, _updatingRenderElements);
        }

    }
}