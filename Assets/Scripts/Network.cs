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
        NetworkLayout networkLayout;

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
            networkLayout = GetComponentInChildren<NetworkLayout>();
            networkRenderer = GetComponentInChildren<NetworkRenderer>();

            fileLoader.LoadFiles();
            dataStruct.InitNetwork();
            networkLayout.Initialize();
            networkRenderer.Initialize();

            networkLayout.ApplyLayout();
            networkRenderer.Update();
        }

        public void Draw()
        {
            networkRenderer.Draw();
        }
    }
}