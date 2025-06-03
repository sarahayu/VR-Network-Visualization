using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using Whisper.Utils;

using VidiGraph;

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
        public Query _query;

        [Header("UI")]
        public Button button;
        public Text buttonText;
        public Text text;
        public ScrollRect scroll;

        // Reference to the whisper stream
        private WhisperStream _stream;
        // Timer for detecting pause
        private float lastRecognizedTime = 0f;

        // Buffer to store what is currently being transcribed
        private string currentBuffer = "";

        // ====== 1) Classification Endpoint URL ======
        private string serverUrl = "http://localhost:5000/classify";


        private async void Start()
        {
            // Create a whisper stream from the microphone
            _stream = await whisper.CreateStream(microphoneRecord);
            OnButtonPressed();

            // Subscribe to events
            _stream.OnResultUpdated += OnResult;
            _stream.OnSegmentUpdated += OnSegmentUpdated;
            _stream.OnSegmentFinished += OnSegmentFinished;
            _stream.OnStreamFinished += OnFinished;

            microphoneRecord.OnRecordStop += OnRecordStop;
            button.onClick.AddListener(OnButtonPressed);


        }

        private void OnButtonPressed()
        {
            if (!microphoneRecord.IsRecording)
            {
                // Start listening
                _stream.StartStream();
                microphoneRecord.StartRecord();
            }
            else
            {
                // Stop listening
                microphoneRecord.StopRecord();
            }

            // buttonText.text = microphoneRecord.IsRecording ? "Stop" : "Record";
        }

        private void OnRecordStop(AudioChunk recordedAudio)
        {
            // Reset button label to "Record"
            buttonText.text = "Record";
        }

        /// Called whenever Whisper produces new recognized text.
        private void OnResult(string result)
        {
            float currentTime = Time.time;

            if (currentTime - lastRecognizedTime > 3.0f)
            {
                // More than 2 seconds passed → treat as new sentence
                currentBuffer = result;
            }
            else
            {
                // Within 2 seconds → keep appending
                currentBuffer += " " + result;
            }

            lastRecognizedTime = currentTime;

            // Display current transcription
            text.text = currentBuffer;
            UiUtils.ScrollDown(scroll);

            StartCoroutine(ClassifyUserCommand(currentBuffer));
            currentBuffer = "";
        }


        private void OnSegmentUpdated(WhisperResult segment)
        {
            // This is partial text as it’s recognized
            // Debug.Log($"Segment updated: {segment.Result}");
        }

        private void OnSegmentFinished(WhisperResult segment)
        {
            // Debug.Log($"Segment finished: {segment.Result}");
        }

        private void OnFinished(string finalResult)
        {
            // Debug.Log("Stream finished!");
        }

        // ====== 3) Classification Logic ======
        private IEnumerator ClassifyUserCommand(string recognizedText)
        {
            Debug.Log("Recognized TEXT: " + recognizedText);
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
                    // Debug.Log("GPT Response: " + responseJson);

                    ClassificationResponse classification = JsonUtility.FromJson<ClassificationResponse>(responseJson);

                    _databaseStorage.InteractStore(responseJson);
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
}


