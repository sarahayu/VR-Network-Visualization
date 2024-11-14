using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace VidiGraph
{
    [CustomEditor(typeof(NetworkManager))]

    public class NetworkFileLoaderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            NetworkManager network = (NetworkManager)target;

            DrawDefaultInspector();

            if (GUILayout.Button("Reload Preview"))
            {
                network.Initialize();
                network.DrawPreview();
            }
        }
    }
}