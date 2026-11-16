// Custom Inspector exposing real Jev analysis and cancellation buttons.
// The test uses the active command host even if the test object is inactive;
// Play mode, Neo4j, and the Python server are still required.

using UnityEditor;
using UnityEngine;
using Whisper.Samples;

[CustomEditor(typeof(KeyboardCommandTester))]
public sealed class KeyboardCommandTesterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var tester = (KeyboardCommandTester)target;
        bool previousEnabled = GUI.enabled;
        // Unity disables the Inspector GUI for inactive objects; this test runs
        // on the active command host and does not require enabling Tests.
        GUI.enabled = true;
        EditorGUILayout.LabelField("Jev desktop test", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(KeyboardCommandTester.JevAnalysisTestCommand, MessageType.Info);
        EditorGUILayout.LabelField("Uses the real Jev pipeline. Neo4j and the Python server must be running.", EditorStyles.wordWrappedLabel);

        using (new EditorGUI.DisabledScope(!tester.CanRunJevAnalysisTest))
            if (GUILayout.Button("Run GPA vs aggression analysis", GUILayout.Height(32)))
                tester.RunJevAnalysisTest();

        using (new EditorGUI.DisabledScope(!Application.isPlaying || !tester.IsJevAnalysisTestRunning))
            if (GUILayout.Button("Cancel analysis")) tester.CancelJevAnalysisTest();

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Enter Play mode to enable the button. No headset, typing, or microphone is needed.", MessageType.None);
        else if (tester.IsJevAnalysisTestRunning)
            EditorGUILayout.LabelField("Running… Results appear in the command/statistics display.");
        else if (!tester.CanRunJevAnalysisTest)
            EditorGUILayout.HelpBox("No active StreamingSampleMic with a NetworkManager was found in the scene.", MessageType.Warning);

        EditorGUILayout.Space();
        GUI.enabled = previousEnabled;
        DrawDefaultInspector();
        if (tester.IsJevAnalysisTestRunning) Repaint();
    }
}
