/*
*
* TODO Description goes here
*
*/

using System.Collections;
using System.Collections.Generic;
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

        public void ToggleFocusNodes(int[] nodeIDs)
        {
            _multiLayoutNetwork.ToggleFocusNodes(nodeIDs);
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
    }
}