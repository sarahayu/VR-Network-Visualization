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
        NetworkDataStructure _data;

        public NetworkFilesLoader FileLoader { get { return _fileLoader; } }
        public NetworkDataStructure Data { get { return _data; } }

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
            _data = GetComponent<NetworkDataStructure>();

            _fileLoader.LoadFiles();
            _data.InitNetwork();

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