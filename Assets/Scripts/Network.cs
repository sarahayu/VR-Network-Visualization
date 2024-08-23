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
        void Start()
        {

        }

        public void Initialize()
        {
            var fileLoader = GetComponent<NetworkFilesLoader>();
            var dataStruct = GetComponent<NetworkDataStructure>();

            bool is2D = fileLoader.is2D;

            fileLoader.LoadFiles();
            dataStruct.InitNetwork(is2D);
        }
    }
}