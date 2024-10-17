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
        NetworkFilesLoader _fileLoader;
        NetworkDataStructure _dataStruct;
        NetworkInput _input;
        NetworkRenderer _renderer;
        NetworkLayout _layout;

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
            _fileLoader = GetComponent<NetworkFilesLoader>();
            _dataStruct = GetComponent<NetworkDataStructure>();
            _input = GetComponent<NetworkInput>();
            _layout = GetComponentInChildren<NetworkLayout>();
            _renderer = GetComponentInChildren<NetworkRenderer>();

            _fileLoader.LoadFiles();
            _dataStruct.InitNetwork();
            _input.Initialize();
            _layout.Initialize();
            _renderer.Initialize();

            _layout.ApplyLayout();
            _renderer.UpdateRenderElements();
        }

        public void Draw()
        {
            _renderer.Draw();
        }
    }
}