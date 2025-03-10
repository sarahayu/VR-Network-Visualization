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
        BigNetwork _bigNetwork;
        [SerializeField]
        SmallNetwork _smallNetwork;

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

            _bigNetwork.Initialize();
            _smallNetwork.Initialize();
        }

        public void DrawPreview()
        {
            _bigNetwork.DrawPreview();
            _smallNetwork.DrawPreview();
        }

        public void CycleCommunityFocus(int community, bool animated = true)
        {
            _bigNetwork.CycleCommunityFocus(community, animated);
        }

        public void ToggleBigNetworkSphericalAndHairball(bool animated = true)
        {
            _bigNetwork.ToggleSphericalAndHairball(animated);
        }

        public void HoverNode(int nodeID)
        {
            _networkGlobal.HoveredNode = _networkGlobal.Nodes[nodeID];
            _bigNetwork.UpdateRenderElements();
        }

        public void UnhoverNode(int nodeID)
        {
            _networkGlobal.HoveredNode = null;
            _bigNetwork.UpdateRenderElements();
        }

        public void HoverCommunity(int communityID)
        {
            _networkGlobal.HoveredCommunity = _networkGlobal.Communities[communityID];
            _bigNetwork.UpdateRenderElements();
        }

        public void UnhoverCommunity(int communityID)
        {
            _networkGlobal.HoveredCommunity = null;
            _bigNetwork.UpdateRenderElements();
        }
    }
}