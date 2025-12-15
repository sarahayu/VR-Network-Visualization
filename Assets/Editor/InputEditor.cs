/*
*
* NetworkEditor is how we can reload network preview from editor mode
*
*/

using UnityEngine;
using UnityEditor;

namespace VidiGraph
{
    [CustomEditor(typeof(InputManager))]

    public class InputEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            InputManager network = (InputManager)target;

            GUILayout.Space(10);

            if (GUILayout.Button("Create Test Subgraph", GUILayout.Height(25)))
            {
                network.CallTestingFunctionWork1();
            }

            if (GUILayout.Button("Make Test Selection", GUILayout.Height(25)))
            {
                network.CallTestingFunctionSelectWomen();
            }

            if (GUILayout.Button("Test Color Selected Red", GUILayout.Height(25)))
            {
                network.CallTestingFunctionColorSelectedRed();
            }

            if (GUILayout.Button("Test Color Selected White", GUILayout.Height(25)))
            {
                network.CallTestingFunctionColorSelectedWhite();
            }

            GUILayout.Space(10);

            DrawDefaultInspector();
        }
    }
}