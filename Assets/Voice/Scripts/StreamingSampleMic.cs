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
    
        [Header("UI")] 
        public Button button;
        public Text buttonText;
        public Text text;
        public ScrollRect scroll;

        // Reference to the whisper stream
        private WhisperStream _stream;

        // ====== 1) Classification Endpoint URL ======
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
                        Debug.Log("Command: " + classification.command);

                        if (classification.functions != null)
                        {
                            Debug.Log("Functions: " + string.Join(", ", classification.functions));
                        }

                        if (classification.tasks != null)
                        {
                            Debug.Log("Tasks: " + string.Join(", ", classification.tasks));
                        }

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
                    // highlight a node
                    Debug.Log("Highlight Node operation triggered!");
                    _networkManager.HoverNode(0); // Example: highlight node with ID 0
                    break;

                case "Select Node":
                    // select a node
                    Debug.Log("Select Node operation triggered!");
                    _networkManager.HoverNode(0); // Example: select node with ID 0
                    break;

                case "Highlight Group":
                    // highlight a group
                    Debug.Log("Highlight Group operation triggered!");
                    _networkManager.CycleCommunityFocus(0); // Example: highlight group with ID 0
                    break;
                
                case "Group Nodes":
                    // group nodes
                    Debug.Log("Group Nodes operation triggered!");
                    break;

                case "Change Layout Spherical":
                    // change layout
                    Debug.Log("Change Layout operation [spherical] triggered!");
                    foreach (var commID in _networkManager.SelectedCommunities)
                    {
                        _networkManager.SetLayout(commID, "spherical"); // Example: toggle layout
                    }
                    break;

                case "Change Layout Spider":
                    // change layout
                    Debug.Log("Change Layout operation [spider] triggered!");
                    foreach (var commID in _networkManager.SelectedCommunities)
                    {
                        _networkManager.SetLayout(commID, "spider"); // Example: toggle layout
                    }
                    break;

                case "Change Layout Floor":
                    // change layout
                    Debug.Log("Change Layout operation [floor] triggered!");
                    foreach (var commID in _networkManager.SelectedCommunities)
                    {
                        _networkManager.SetLayout(commID, "floor"); // Example: toggle layout
                    }
                    break;

                case "Make Work Surface":
                    // create a new work surface
                    Debug.Log("Make Work Surface operation triggered!");
                    _networkManager.ToggleBigNetworkSphericalAndHairball(); // Example: create a new work surface
                    break;

                case "Project Nodes":
                    // project nodes onto surface
                    Debug.Log("Project Nodes operation triggered!");
                    _networkManager.ToggleBigNetworkSphericalAndHairball(); // Example: project nodes onto surface
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
        public string[] functions;
        public string[] tasks;
    }

}
