using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using Whisper.Utils;

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
    
        [Header("UI")] 
        public Button button;
        public Text buttonText;
        public Text text;
        public ScrollRect scroll;

        // Reference to the whisper stream
        private WhisperStream _stream;

        // ====== 1) Classification Endpoint URL ======
        // Replace with your actual server endpoint (local or remote).
        private string serverUrl = "http://localhost:5000/classify";


        private async void Start()
        {
            // Create a whisper stream from the microphone
            _stream = await whisper.CreateStream(microphoneRecord);

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
        
            buttonText.text = microphoneRecord.IsRecording ? "Stop" : "Record";
        }
    
        private void OnRecordStop(AudioChunk recordedAudio)
        {
            // Reset button label to "Record"
            buttonText.text = "Record";
        }
    
        /// <summary>
        /// Called whenever Whisper produces new recognized text.
        /// </summary>
        private void OnResult(string result)
        {
            // Display transcribed text
            text.text = result;
            UiUtils.ScrollDown(scroll);

            // ====== 2) Send text for classification ======
            StartCoroutine(ClassifyUserCommand(result));
        }
        
        private void OnSegmentUpdated(WhisperResult segment)
        {
            // This is partial text as it’s recognized
            Debug.Log($"Segment updated: {segment.Result}");
        }
        
        private void OnSegmentFinished(WhisperResult segment)
        {
            Debug.Log($"Segment finished: {segment.Result}");
        }
        
        private void OnFinished(string finalResult)
        {
            Debug.Log("Stream finished!");
        }

        // ====== 3) Classification Logic ======
        private IEnumerator ClassifyUserCommand(string recognizedText)
        {
            // Prepare JSON body for the request
            ClassificationRequest requestBody = new ClassificationRequest { userText = recognizedText };
            string jsonBody = JsonUtility.ToJson(requestBody);
            
            // Convert to raw bytes
            byte[] postData = Encoding.UTF8.GetBytes(jsonBody);

            // Create the UnityWebRequest for POST
            using (UnityWebRequest www = new UnityWebRequest(serverUrl, "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(postData);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                // Send request
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.ConnectionError
                    || www.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError("Classification Error: " + www.error);
                }
                else
                {
                    // Handle server response
                    string responseJson = www.downloadHandler.text;
                    Debug.Log("Classification response: " + responseJson);

                    // Parse JSON into our response class
                    ClassificationResponse classification = JsonUtility.FromJson<ClassificationResponse>(responseJson);

                    if (classification != null)
                    {
                        // We have a command. Execute it.
                        Debug.Log("Command: " + classification.command);
                        ExecuteOperation(classification.command);
                    }
                }
            }
        }

        // ====== 4) Execute the Classified Command ======
        private void ExecuteOperation(string command)
        {
            switch (command)
            {
                case "Highlight Node":
                    // Insert your logic to highlight a node
                    Debug.Log("Highlight Node operation triggered!");
                    break;

                case "Select Node":
                    // Insert your logic to select a node
                    Debug.Log("Select Node operation triggered!");
                    break;

                case "Highlight Group":
                    // Insert your logic for highlighting a group
                    Debug.Log("Highlight Group operation triggered!");
                    break;
                
                case "Group Nodes":
                    // Insert your logic to group nodes
                    Debug.Log("Group Nodes operation triggered!");
                    break;

                case "Change Layout":
                    // Insert logic to update or change layout
                    Debug.Log("Change Layout operation triggered!");
                    break;

                case "Make Work Surface":
                    // Insert logic to create a new work surface
                    Debug.Log("Make Work Surface operation triggered!");
                    break;

                case "Project Nodes":
                    // Insert logic to project nodes onto surface
                    Debug.Log("Project Nodes operation triggered!");
                    break;

                default:
                    // If no valid command is recognized:
                    Debug.LogWarning("Unknown Command: " + command);
                    break;
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
        public string command;
    }
}
