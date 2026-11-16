// Unity Editor window (Window > Jev > Desktop Test) for running the real analysis
// pipeline without a headset. Requires Play mode, an active StreamingSampleMic with
// a NetworkManager, Neo4j, and the Python server. Runs on the active command host.

using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Whisper.Samples;

public sealed class JevDesktopTestWindow : EditorWindow
{
    private bool running;

    [MenuItem("Window/Jev/Desktop Test")]
    public static void Open()
    {
        GetWindow<JevDesktopTestWindow>("Jev Test").Show();
    }

    private void OnGUI()
    {
        if (!Application.isPlaying) running = false;
        EditorGUILayout.LabelField("Test Jev without a headset", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(KeyboardCommandTester.JevAnalysisTestCommand, MessageType.Info);
        EditorGUILayout.LabelField("Start Neo4j and the Python server, then enter Play mode and click Run.", EditorStyles.wordWrappedLabel);

        var voice = FindObjectsOfType<StreamingSampleMic>().FirstOrDefault(mic => mic.isActiveAndEnabled && mic._networkManager != null);
        bool ready = Application.isPlaying && voice != null && voice._networkManager != null;
        using (new EditorGUI.DisabledScope(!ready || running))
            if (GUILayout.Button("Run GPA vs aggression analysis", GUILayout.Height(36)))
                voice.StartCoroutine(Run(voice));

        using (new EditorGUI.DisabledScope(!ready))
            if (GUILayout.Button("Cancel analysis")) voice.TryCancelCommand("cancel");

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Enter Play mode first. This window does not require a KeyboardCommandTester component.", MessageType.Info);
        else if (!ready)
            EditorGUILayout.HelpBox("Open the main network scene with an active StreamingSampleMic and NetworkManager.", MessageType.Warning);
        else
            EditorGUILayout.LabelField(running ? "Running… Check the command/statistics display for results." : "Ready.", EditorStyles.wordWrappedLabel);
    }

    // Start a fresh request on the active host and release the running flag even on failure/cancellation.
    private IEnumerator Run(StreamingSampleMic voice)
    {
        running = true;
        Repaint();
        try
        {
            yield return voice.SubmitCommand("new request: " + KeyboardCommandTester.JevAnalysisTestCommand);
        }
        finally
        {
            running = false;
            if (this != null) Repaint();
        }
    }

    private void OnInspectorUpdate() => Repaint();
}
