using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace VidiGraph
{
    [CustomEditor(typeof(Network))]

    public class NetworkFileLoaderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            Network network = (Network)target;

            DrawDefaultInspector();

            // if (DrawDefaultInspector ()) {
            // 	if (mapGen.autoUpdate) {
            // 		mapGen.DrawMapInEditor ();
            // 	}
            // }

            if (GUILayout.Button("Reload"))
            {
                network.Initialize();
            }
        }
    }
}