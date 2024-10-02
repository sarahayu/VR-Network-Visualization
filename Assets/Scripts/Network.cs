/*
*
* This should be central component for Network object
*
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class Network : MonoBehaviour
    {
        NetworkFilesLoader fileLoader;
        NetworkDataStructure dataStruct;
        NetworkRenderer networkRenderer;

        void Awake()
        {
        }

        void Start()
        {
            Initialize();
        }

        void Update()
        {
            Draw();
        }

        public void Initialize()
        {
            fileLoader = GetComponent<NetworkFilesLoader>();
            dataStruct = GetComponent<NetworkDataStructure>();
            networkRenderer = GetComponentInChildren<NetworkRenderer>();

            fileLoader.LoadFiles();
            dataStruct.InitNetwork();
            networkRenderer.Initialize();
        }

        public void Draw()
        {
            networkRenderer.DrawNetwork();
        }
    }
}