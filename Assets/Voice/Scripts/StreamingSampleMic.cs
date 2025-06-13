using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using Whisper.Utils;

using VidiGraph;
using System.Linq;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace Whisper.Samples
{
    /// <summary>
    /// Stream transcription from microphone input,
    /// then classify recognized text into a command.
    /// </summary>
    public class StreamingSampleMic : MonoBehaviour
    {
        public WhisperManager whisper;
        public MicrophoneRecord microphoneRecord;
        public NetworkManager _networkManager;
        public DatabaseStorage _databaseStorage;
        public LoadingIcon loadingIcon;
        public Query _query;

        [Header("UI")]
        public Button button;
        [SerializeField]
        XRInputButtonReader CommandPress = new XRInputButtonReader("CommandPress");
        [SerializeField]
        Renderer Indicator;
        public Text buttonText;
        public Text text;
        public ScrollRect scroll;

        // Reference to the whisper stream
        private WhisperStream _stream;
        private float whisperStartTime;
        private float whisper_currentTime;


        // Classification server URL
        private string serverUrl = "http://localhost:5000/classify";


        private async void Start()
        {
            // Create a whisper stream from the microphone
            _stream = await whisper.CreateStream(microphoneRecord);
            // OnButtonPressed();

            // Subscribe to events
            _stream.OnResultUpdated += OnResult;
            _stream.OnSegmentUpdated += OnSegmentUpdated;
            _stream.OnSegmentFinished += OnSegmentFinished;
            _stream.OnStreamFinished += OnFinished;

            microphoneRecord.OnRecordStop += OnRecordStop;
            button.onClick.AddListener(OnButtonPressed);


            CommandPress.EnableDirectActionIfModeUsed();

        }

        void Update()
        {
            if (CommandPress.ReadWasPerformedThisFrame())
            {
                Debug.Log("calling on button press");
                // Start listening
                _stream.StartStream();
                microphoneRecord.StartRecord();
                whisperStartTime = Time.time; // record start time


                MaterialPropertyBlock props = new MaterialPropertyBlock();

                Indicator.GetPropertyBlock(props);
                props.SetColor("_Color", ColorUtils.StringToColor("#FF8F00"));
                Indicator.SetPropertyBlock(props);
            }
            if (CommandPress.ReadWasCompletedThisFrame())
            {
                Debug.Log("calling on button release");
                // Stop listening
                microphoneRecord.StopRecord();

                MaterialPropertyBlock props = new MaterialPropertyBlock();

                Indicator.GetPropertyBlock(props);
                props.SetColor("_Color", ColorUtils.StringToColor("#C0C0C0"));
                Indicator.SetPropertyBlock(props);
            }

        }

        private void OnButtonPressed()
        {
            if (!microphoneRecord.IsRecording)
            {
                // Start listening
                _stream.StartStream();
                microphoneRecord.StartRecord();
                whisperStartTime = Time.time; // record start time
            }
            else
            {
                // Stop listening
                microphoneRecord.StopRecord();
            }
        }

        private void OnRecordStop(AudioChunk recordedAudio)
        {
            // Reset button label to "Record"
            buttonText.text = "Record";
        }

        /// Called whenever Whisper produces new recognized text.
        private void OnResult(string result)
        {
            // Debug.Log($"Result: {result}");
        }


        private void OnSegmentUpdated(WhisperResult segment)
        {
            // This is partial text as it’s recognized
            // Debug.Log($"Segment updated: {segment.Result}");
        }

        private void OnSegmentFinished(WhisperResult segment)
        {
            // Debug.Log($"Segment finished: {segment.Result}");
            float recognitionTime = Time.time - whisperStartTime;
            whisperStartTime = Time.time;
            StartCoroutine(ClassifyUserCommand(segment.Result, recognitionTime));
            // reset start time for next recording
        }

        private void OnFinished(string finalResult)
        {
            // Debug.Log("Stream finished!");
        }

        // Classification Main Function
        private IEnumerator ClassifyUserCommand(string recognizedText, float whisperTime)
        {
            loadingIcon.SetLoading(true);
            Debug.Log("Recognized text input: " + recognizedText);
            // Debug.Log($"Whisper took {whisperTime:F3} seconds to recognize.");
            ClassificationRequest requestBody = new ClassificationRequest { userText = recognizedText };
            string jsonBody = JsonUtility.ToJson(requestBody);

            byte[] postData = Encoding.UTF8.GetBytes(jsonBody);

            using (UnityWebRequest www = new UnityWebRequest(serverUrl, "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(postData);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError("Classification Error: " + www.error);
                }
                else
                {
                    string responseJson = www.downloadHandler.text;

                    // Parse JSON
                    ClassificationResponse classification = JsonUtility.FromJson<ClassificationResponse>(responseJson);

                    // Show timing info for debugging
                    if (classification.timings != null)
                    {
                        Debug.Log($"Timing - General Agent: {classification.timings.general_agent}s");
                        Debug.Log($"Timing - Execute Agent: {classification.timings.execute_agent}s");
                        Debug.Log($"Timing - Clarify Agent: {classification.timings.clarify_agent}s");
                        Debug.Log($"Timing - Return Code: {classification.timings.return_code}s");
                    }

                    // Check if clarification is needed
                    if (!string.IsNullOrEmpty(classification.clarify))
                    {
                        Debug.LogWarning("Clarification Needed: " + classification.clarify);
                        // Add the voice for classification later
                    }
                    else
                    {
                        var query = classification.query.Substring(10, classification.query.Length - 13);
                        Debug.Log("Cypher Query: " + query);
                        var nodes = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, query);

                        _networkManager.SetSelectedNodes(nodes.Select(n => n.ID), true);


                        loadingIcon.SetLoading(false);
                    }
                }
            }
        }

    }



}



// Classes for JSON serialization/deserialization
[System.Serializable]
public class ClassificationRequest
{
    public string userText;
}

[System.Serializable]
public class ClassificationResponse
{
    public string query;
    public string clarify;
    public Timing timings;
}

[System.Serializable]
public class Timing
{
    public float general_agent;
    public float clarify_agent;
    public float execute_agent;
    public float return_code;
}



