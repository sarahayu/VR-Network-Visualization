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
        NetworkDataStructure _networkData;

        public NetworkFilesLoader FileLoader { get { return _fileLoader; } }
        public NetworkDataStructure NetworkData { get { return _networkData; } }

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
            _networkData = GetComponent<NetworkDataStructure>();

            _fileLoader.LoadFiles();
            _networkData.InitNetwork();

            _bigNetwork.Initialize();
            _smallNetwork.Initialize();
        }

        public void DrawPreview()
        {
            _bigNetwork.DrawPreview();
            _smallNetwork.DrawPreview();
        }

        public void ToggleCommunityFocus(int community, bool animated = true)
        {
            _bigNetwork.ToggleCommunityFocus(community, animated);
        }

        public void ToggleBigNetworkSphericalAndHairball(bool animated = true)
        {
            _bigNetwork.ToggleSphericalAndHairball(animated);
        }
    }
}