using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using Whisper.Utils;

using VidiGraph;
using System.Linq;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using Newtonsoft.Json;

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
        public AudioSource audioSource;
        public AudioClip startSpeakingAudio;
        public AudioClip stopSpeakingAudio;
        public AudioClip errorAudio;

        [Header("UI")]
        public Button button;
        [SerializeField]
        XRInputButtonReader CommandPress = new XRInputButtonReader("CommandPress");
        [SerializeField]
        Renderer Indicator;
        public Text buttonText;
        public Text text;
        public ScrollRect scroll;
        public GameObject command_prefab;
        public GameObject command_parent;

        // Reference to the whisper stream
        private WhisperStream _stream;
        private float whisperStartTime;
        private float whisper_currentTime;
        public Dictionary<string, float> buffer_database = new Dictionary<string, float>();


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
            button?.onClick.AddListener(OnButtonPressed);
            microphoneRecord.StartRecord();


            CommandPress.EnableDirectActionIfModeUsed();

        }

        void Update()
        {
            if (CommandPress.ReadWasPerformedThisFrame())
            {
                Debug.Log("calling on button press");
                // Start listening
                _stream.StartStream();
                whisperStartTime = Time.time; // record start time
                // play start speaking audio
                audioSource.PlayOneShot(startSpeakingAudio);


                MaterialPropertyBlock props = new MaterialPropertyBlock();

                Indicator.GetPropertyBlock(props);
                props.SetColor("_Color", ColorUtils.StringToColor("#FF8F00"));
                Indicator.SetPropertyBlock(props);
            }
            if (CommandPress.ReadWasCompletedThisFrame())
            {
                Debug.Log("calling on button release");
                // Stop listening
                _stream.StopStream();
                audioSource.PlayOneShot(stopSpeakingAudio);

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
            if (segment.Result != "")
                text.text = segment.Result;
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
            // Debug.Log("Recognized text input: " + recognizedText);
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
                    loadingIcon.SetLoading(false);
                    audioSource.PlayOneShot(errorAudio);
                }
                else
                {
                    string responseJson = www.downloadHandler.text;

                    // Parse JSON
                    ClassificationResponse classification = JsonConvert.DeserializeObject<ClassificationResponse>(responseJson, new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        MissingMemberHandling = MissingMemberHandling.Ignore
                    });

                    // Show timing info for debugging
                    if (classification.timings != null)
                    {
                        Debug.Log($"Timing - Preprocess Agent: {classification.timings.preprocess_agent}s");
                        Debug.Log($"Timing - General Agent: {classification.timings.general_agent}s");
                        Debug.Log($"Timing - Action Agent: {classification.timings.action_agent}s");
                        Debug.Log($"Timing - Cypher Agent: {classification.timings.cypher_agent}s");
                        Debug.Log($"Timing - Clarify Agent: {classification.timings.clarify_agent}s");
                        Debug.Log($"Timing - Return Code: {classification.timings.return_code}s");
                    }

                    var corrected_input = classification.corrected_input;

                    var _command = Instantiate(command_prefab, command_parent.transform);
                    var _command_text = _command.GetComponent<TMP_Text>();
                    _command_text.text = corrected_input; // showing the correctd command text
                    ScrollToBottom();

                    // Check if clarification is needed
                    if (!string.IsNullOrEmpty(classification.clarify))
                    {
                        Debug.LogWarning("Clarification Needed: " + classification.clarify);
                        var new_command = Instantiate(command_prefab, command_parent.transform);
                        var command_text = new_command.GetComponent<TMP_Text>();
                        command_text.text = "<color=#FF8F00><b>Please say it again</b></color>";
                        ScrollToBottom();
                        audioSource.PlayOneShot(errorAudio);
                    }
                    else
                    {
                        Debug.Log("Classification Response: " + responseJson);
                        var query = classification.queries;
                        // var corrected_input = classification.corrected_input;
                        // [0].Substring(10, classification.query[0].Length - 13);
                        var action = classification.actions;
                        var actions_count = action.Length;
                        Debug.Log("Action: " + action + " Counts:" + actions_count);
                        Debug.Log("Cypher Query: " + query);

                        for (int i = 0; i < actions_count; i++)
                        {
                            switch (action[i][0])
                            {
                                case "selectNode":
                                    Debug.Log("Selecting nodes with query: " + query[i]);
                                    text.text += $"\n<size=18><color=#aaa>{query[i]}</color></size>";
                                    var nodes = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, query[i]);
                                    TimerUtils.StartTime("SetSelectedNodes");
                                    if (_networkManager.OnQueryMode)
                                    {
                                        // have to convert GUIDs to IDs to create a working subgraph
                                        var nodeIDs = _networkManager.SortNodeGUIDs(nodes)[NetworkManager.MainNetworkID];
                                        _networkManager.CreateWorkingSubgraph(nodeIDs, classification.input, classification.input);
                                        _networkManager.SetQueryMode(false);
                                    }
                                    else
                                    {
                                        _networkManager.SetWorkingSelectedNodes(nodes, true);
                                    }
                                    
                                    TimerUtils.EndTime("SetSelectedNodes");
                                    break;
                                case "sizeNode":
                                    // Parse "attribute:scope" format (e.g., "gpa:all" or "gpa:selected")
                                    string sizeParam = action[i][1];
                                    string attributeName = sizeParam.Contains(":") ? sizeParam.Split(':')[0] : sizeParam;

                                    var (minV, maxV) = _databaseStorage.GetMinMaxFromStore(_networkManager.NetworkGlobal, query[i]);
                                    Debug.Log($"Min/Max of {attributeName}: {minV}, {maxV}");
                                    _networkManager.SetMLNodeSizeEncoding(attributeName, minV, maxV, NetworkManager.MainNetworkID);
                                    break;
                                case "selectLink":
                                    Debug.Log("Selecting links with query: " + query[i]);
                                    text.text += $"\n<size=18><color=#aaa>{query[i]}</color></size>";
                                    var links = _databaseStorage.GetLinksFromStore(_networkManager.NetworkGlobal, query[i]);
                                    TimerUtils.StartTime("SetSelectedLinks");
                                    _networkManager.SetWorkingSelectedLinks(links, true);
                                    TimerUtils.EndTime("SetSelectedLinks");
                                    break;
                                case "deselect":
                                    Debug.Log("Deselecting nodes");
                                    TimerUtils.StartTime("Deselect Nodes");
                                    _networkManager.ClearSelection();
                                    TimerUtils.EndTime("Deselect Nodes");
                                    break;
                                case "move":
                                    Debug.Log("Moving selected nodes");
                                    var nodes_move = _networkManager.WorkingSelectedNodeGUIDs;
                                    TimerUtils.StartTime("Move Nodes");
                                    _networkManager.BringMLNodes(nodes_move);
                                    TimerUtils.EndTime("Move Nodes");
                                    break;
                                case "layout":
                                    Debug.Log("Changing layout to: " + action[i][1]);
                                    var comms = _networkManager.WorkingSelectedCommunityGUIDs;
                                    TimerUtils.StartTime("Layout Change");
                                    _networkManager.SetMLLayout(comms, action[i][1]);
                                    TimerUtils.EndTime("Layout Change");
                                    break;
                                case "colorNode":
                                    Debug.Log("Changing color of selected nodes to: " + action[i][1]);
                                    var nodes_color = _networkManager.WorkingSelectedNodeGUIDs;
                                    TimerUtils.StartTime("SetColor");
                                    _networkManager.SetMLNodesColor(nodes_color, action[i][1]); 
                                    TimerUtils.EndTime("SetColor");
                                    break;
                                case "colorLink":
                                    Debug.Log("Changing color of selected links to: " + action[i][1]);
                                    var links_color = _networkManager.WorkingSelectedLinkGUIDs;
                                    TimerUtils.StartTime("SetColor");
                                    _networkManager.SetMLLinksColorStart(links_color, action[i][1]);
                                    _networkManager.SetMLLinksColorEnd(links_color, action[i][1]);
                                    TimerUtils.EndTime("SetColor");
                                    break;
                                case "colorByAttribute":
                                    Debug.Log("Categorical coloring by attribute: " + action[i][1]);
                                    string attributeName_color = action[i][1];

                                    TimerUtils.StartTime("ColorByAttribute");

                                    // Get distinct values from the query result
                                    var distinctValues = _databaseStorage.GetDistinctValuesFromStore(_networkManager.NetworkGlobal, query[i]);
                                    Debug.Log($"Found {distinctValues.Count} distinct values for {attributeName_color}");

                                    // Define 4 allowed colors (no red - reserved for highlighting)
                                    string[] allowedColors = new string[] {
                                        "#7FFFFF",  // cyan
                                        "#7F7FFF",  // blue
                                        "#FFFF7F",  // yellow
                                        "#BF7FBF"   // purple
                                    };


                                    // Warn if more than 6 categories
                                    if (distinctValues.Count > 6)
                                    {
                                        Debug.LogWarning($"Found {distinctValues.Count} categories but only 6 colors available. Colors will repeat.");
                                    }

                                    // Color each category
                                    for (int j = 0; j < distinctValues.Count; j++)
                                    {
                                        string categoryValue = distinctValues[j];
                                        string colorHex = allowedColors[j % allowedColors.Length]; // Cycle through colors

                                        // Build query to select nodes with this category value
                                        string categoryQuery = $"MATCH (n:Node) WHERE n.{attributeName_color} = {categoryValue} RETURN n";

                                        Debug.Log($"  Category '{categoryValue}' → {colorHex}");

                                        // Get nodes for this category
                                        var categoryNodes = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, categoryQuery);

                                        // Color them
                                        _networkManager.SetMLNodesColor(categoryNodes, colorHex);
                                    }

                                    TimerUtils.EndTime("ColorByAttribute");

                                    // Create legend display
                                    var _colorByAttr = Instantiate(command_prefab, command_parent.transform);
                                    var _colorByAttr_text = _colorByAttr.GetComponent<TMP_Text>();

                                    System.Text.StringBuilder legendBuilder = new System.Text.StringBuilder();
                                    legendBuilder.AppendLine($"<b>Colored by {attributeName_color}</b>");

                                    for (int j = 0; j < distinctValues.Count; j++)
                                    {
                                        string categoryValue = distinctValues[j].Replace("'", ""); // Remove quotes for display
                                        string colorHex = allowedColors[j % allowedColors.Length];
                                        string colorName = GetColorName(colorHex);
                                        legendBuilder.AppendLine($"  <color={colorHex}>{colorName}</color> for {categoryValue}");
                                    }

                                    _colorByAttr_text.text = legendBuilder.ToString();
                                    ScrollToBottom();
                                    break;

                                case "shapeByAttribute":
                                    Debug.Log("Categorical shape encoding by attribute: " + action[i][1]);
                                    string attributeName_shape = action[i][1];

                                    TimerUtils.StartTime("ShapeByAttribute");

                                    // Get distinct values from the query result
                                    var distinctShapeValues = _databaseStorage.GetDistinctValuesFromStore(_networkManager.NetworkGlobal, query[i]);
                                    Debug.Log($"Found {distinctShapeValues.Count} distinct values for {attributeName_shape}");

                                    // Define the 3 allowed shapes
                                    string[] allowedShapes = new string[] {
                                        "sphere",
                                        "cube",
                                        "tetrahedron"
                                    };

                                    // Warn if more than 3 categories
                                    if (distinctShapeValues.Count > 3)
                                    {
                                        Debug.LogWarning($"Found {distinctShapeValues.Count} categories but only 3 shapes available. Shapes will repeat.");
                                    }

                                    // Assign shape to each category
                                    for (int j = 0; j < distinctShapeValues.Count; j++)
                                    {
                                        string categoryValue = distinctShapeValues[j];
                                        string shapeName = allowedShapes[j % allowedShapes.Length]; // Cycle through shapes

                                        // Build query to select nodes with this category value
                                        string categoryQuery = $"MATCH (n:Node) WHERE n.{attributeName_shape} = {categoryValue} RETURN n";

                                        Debug.Log($"  Category '{categoryValue}' → {shapeName}");

                                        // Get nodes for this category
                                        var categoryNodes = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, categoryQuery);

                                        // Change their shape
                                        _networkManager.SetMLNodesShape(categoryNodes, shapeName);
                                    }

                                    TimerUtils.EndTime("ShapeByAttribute");

                                    // Create legend display with shape symbols
                                    var _shapeByAttr = Instantiate(command_prefab, command_parent.transform);
                                    var _shapeByAttr_text = _shapeByAttr.GetComponent<TMP_Text>();

                                    System.Text.StringBuilder shapeLegendBuilder = new System.Text.StringBuilder();
                                    shapeLegendBuilder.AppendLine($"<b>Shaped by {attributeName_shape}</b>");

                                    // Define shape symbols for display
                                    string[] shapeSymbols = new string[] {
                                        "●",  // sphere (circle)
                                        "■",  // cube (square)
                                        "▲"   // tetrahedron (triangle)
                                    };

                                    for (int j = 0; j < distinctShapeValues.Count; j++)
                                    {
                                        string categoryValue = distinctShapeValues[j].Replace("'", ""); // Remove quotes for display
                                        string shapeName = allowedShapes[j % allowedShapes.Length];
                                        string shapeSymbol = shapeSymbols[j % shapeSymbols.Length];
                                        shapeLegendBuilder.AppendLine($"  {shapeSymbol} {categoryValue} = {shapeName}");
                                    }

                                    _shapeByAttr_text.text = shapeLegendBuilder.ToString();
                                    ScrollToBottom();
                                    break;

                                case "arithmetic":
                                    Debug.Log("Performing arithmetic operation: " + action[i][1]);
                                    TimerUtils.StartTime("Arithmetic Operation");
                                    var result = _databaseStorage.GetValueFromStore(_networkManager.NetworkGlobal, query[i]);
                                    Debug.Log("Arithmetic Result: " + result);
                                    TimerUtils.EndTime("Arithmetic Operation");
                                    var _result = Instantiate(command_prefab, command_parent.transform);
                                    var _result_text = _result.GetComponent<TMP_Text>();
                                    _result_text.text = "Calculated Result: " + result;
                                    break;
                                default:
                                    // Handle unknown actions
                                    Debug.LogWarning("Unknown action: " + action);
                                    _command_text.text = "Unknown action, " + classification.clarify;
                                    audioSource.PlayOneShot(errorAudio);
                                    break;
                            }
                        }

                        loadingIcon.SetLoading(false); // Done processing
                    }
                }
            }
        }

        private void ScrollToBottom()
        {
            if (scroll != null)
            {
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition = 0f;
                Canvas.ForceUpdateCanvases();
            }
        }

        private string GetColorName(string hexColor)
        {
            return hexColor.ToUpper() switch
            {
                "#7FFFFF" => "cyan",
                "#7F7FFF" => "blue",
                "#FFFF7F" => "yellow",
                "#BF7FBF" => "purple",
                _ => "color"
            };
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
    public string input;
    public string corrected_input;
    public string[] queries;
    public string clarify;
    public Timing timings;
    public string[][] actions;
    
}

[System.Serializable]
public class Timing
{
    public float preprocess_agent;
    public float general_agent;
    public float clarify_agent;
    public float action_agent;
    public float cypher_agent;
    public float return_code;
}



