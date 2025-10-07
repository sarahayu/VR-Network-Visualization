/*
*
* NetworkEditor is how we can reload network preview from editor mode
*
*/

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

            GUILayout.Space(10);

            if (GUILayout.Button("Reload Preview", GUILayout.Height(25)))
            {
                network.Initialize();
                network.DrawPreview();
            }

            GUILayout.Space(10);

            DrawDefaultInspector();
        }
    }
}