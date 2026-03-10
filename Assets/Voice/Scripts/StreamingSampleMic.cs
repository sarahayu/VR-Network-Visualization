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
        public TMP_InputField textCommandInput;

        // Reference to the whisper stream
        private WhisperStream _stream;
        private float whisperStartTime;
        private float whisper_currentTime;
        public Dictionary<string, float> buffer_database = new Dictionary<string, float>();

        // Link GUIDs from the last selectLink command — used by colorLink
        private HashSet<string> _lastSelectedLinkGUIDs = new HashSet<string>();


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

            textCommandInput?.onSubmit.AddListener(OnTextCommandSubmit);

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

        private void OnTextCommandSubmit(string inputText)
        {
            if (string.IsNullOrWhiteSpace(inputText)) return;
            textCommandInput.text = "";
            textCommandInput.ActivateInputField(); // keep focus for next command
            StartCoroutine(ClassifyUserCommand(inputText.Trim(), 0f));
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
                                    if (_networkManager.OnQueryMode || !_networkManager.HasWorkingSession)
                                    {
                                        // No session yet — create one with ALL nodes first, then select the queried subset
                                        var allNodesForSel = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, "MATCH (n:Node) RETURN n");
                                        var allNodeIDsForSel = _networkManager.SortNodeGUIDs(allNodesForSel)[NetworkManager.MainNetworkID];
                                        _networkManager.CreateWorkingSubgraph(allNodeIDsForSel, classification.corrected_input, classification.corrected_input);
                                        _networkManager.SetQueryMode(false);
                                        var subnGUIDs = _networkManager.TranslateToWorkingSubgraphNodeGUIDs(nodes);
                                        _networkManager.SetWorkingSelectedNodes(subnGUIDs, true);
                                    }
                                    else
                                    {
                                        // Session exists — select queried nodes within it
                                        var subnGUIDs = _networkManager.TranslateToWorkingSubgraphNodeGUIDs(nodes);
                                        _networkManager.SetWorkingSelectedNodes(subnGUIDs, true);
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
                                    TimerUtils.StartTime("SetSelectedLinks");
                                    var links = _databaseStorage.GetLinksFromStore(_networkManager.NetworkGlobal, query[i]);
                                    _lastSelectedLinkGUIDs = new HashSet<string>(links);

                                    // n.selected=true is Unity state, not stored in Neo4j — filter by selected nodes in Unity
                                    if (query[i].Contains("n.selected = true") && _networkManager.HasWorkingSession
                                        && _networkManager.WorkingSelectedNodeGUIDs.Count > 0)
                                    {
                                        string queryWithoutSelected = query[i]
                                            .Replace("n.selected = true AND ", "")
                                            .Replace(" AND n.selected = true", "");
                                        var allTypeLinks = _databaseStorage.GetLinksFromStore(_networkManager.NetworkGlobal, queryWithoutSelected);
                                        var selectedNodeIDs = _networkManager.SortNodeGUIDs(_networkManager.WorkingSelectedNodeGUIDs)
                                            .Values.SelectMany(x => x).ToHashSet();
                                        _lastSelectedLinkGUIDs = new HashSet<string>(allTypeLinks.Where(linkGuid =>
                                        {
                                            if (!_networkManager.LinkGUIDToID.TryGetValue(linkGuid, out var tup)) return false;
                                            if (!_networkManager.NetworkGlobal.Links.TryGetValue(tup.Item2, out var lnk)) return false;
                                            return selectedNodeIDs.Contains(lnk.SourceNodeID) || selectedNodeIDs.Contains(lnk.TargetNodeID);
                                        }));
                                        Debug.Log($"Filtered to {_lastSelectedLinkGUIDs.Count} links for selected nodes");
                                    }

                                    Debug.Log($"Stored {_lastSelectedLinkGUIDs.Count} link GUIDs for coloring");
                                    TimerUtils.EndTime("SetSelectedLinks");
                                    break;
                                case "deselect":
                                    Debug.Log("Deselecting nodes");
                                    TimerUtils.StartTime("Deselect Nodes");
                                    _networkManager.ClearSelection();
                                    _lastSelectedLinkGUIDs.Clear();
                                    TimerUtils.EndTime("Deselect Nodes");
                                    break;
                                case "move":
                                    Debug.Log("Moving selected nodes");
                                    var nodes_move = _networkManager.HasWorkingSession
                                        ? (_networkManager.WorkingSelectedNodeGUIDs.Count > 0
                                            ? _networkManager.WorkingSelectedNodeGUIDs
                                            : _networkManager.WorkingSubgraphAllNodeGUIDs)
                                        : _networkManager.SelectedNodeGUIDs;
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
                                    Debug.Log("Changing color of nodes to: " + action[i][1]);
                                    HashSet<string> nodes_color;

                                    if (_networkManager.HasWorkingSession)
                                    {
                                        // Session exists — use selected subgraph nodes, fall back to all subgraph nodes
                                        nodes_color = _networkManager.WorkingSelectedNodeGUIDs;
                                        if (nodes_color.Count == 0)
                                            nodes_color = _networkManager.WorkingSubgraphAllNodeGUIDs;
                                    }
                                    else
                                    {
                                        // No session — create one with all nodes then color them
                                        Debug.Log("No session, creating one with all nodes");
                                        string selectAllQuery = "MATCH (n:Node) RETURN n";
                                        var allNodes = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, selectAllQuery);
                                        var nodeIDs = _networkManager.SortNodeGUIDs(allNodes)[NetworkManager.MainNetworkID];
                                        _networkManager.CreateWorkingSubgraph(nodeIDs, "Color all nodes", "Color Nodes");
                                        _networkManager.SetWorkingSelectedNodes(_networkManager.WorkingSubgraphAllNodeGUIDs, true);
                                        nodes_color = _networkManager.WorkingSelectedNodeGUIDs;
                                    }

                                    TimerUtils.StartTime("SetColor");
                                    _networkManager.SetMLNodesColor(nodes_color, action[i][1]);
                                    TimerUtils.EndTime("SetColor");
                                    break;
                                case "colorLink":
                                    Debug.Log($"Coloring links: {action[i][1]}");
                                    TimerUtils.StartTime("SetColor");
                                    HashSet<string> linkGUIDs_color;
                                    if (_networkManager.HasWorkingSession)
                                    {
                                        // Translate DB link GUIDs to working subgraph GUIDs
                                        linkGUIDs_color = _networkManager.TranslateToWorkingSubgraphLinkGUIDs(_lastSelectedLinkGUIDs);
                                        if (linkGUIDs_color.Count == 0)
                                        {
                                            // Fall back to selected nodes' links, not ALL links
                                            var selectedNodeLinks = _networkManager.GetLinksForNodes(_networkManager.WorkingSelectedNodeGUIDs);
                                            linkGUIDs_color = selectedNodeLinks.Count > 0
                                                ? selectedNodeLinks
                                                : _networkManager.WorkingSubgraphAllLinkGUIDs;
                                        }
                                    }
                                    else
                                    {
                                        linkGUIDs_color = _lastSelectedLinkGUIDs;
                                    }
                                    _networkManager.SetMLLinksColorStart(linkGUIDs_color, action[i][1]);
                                    _networkManager.SetMLLinksColorEnd(linkGUIDs_color, action[i][1]);
                                    TimerUtils.EndTime("SetColor");
                                    break;
                                case "colorByAttribute":
                                    Debug.Log("Categorical coloring by attribute: " + action[i][1]);
                                    string attributeName_color = action[i][1];

                                    TimerUtils.StartTime("ColorByAttribute");

                                    // Ensure a working session exists; if not, create one with all nodes
                                    if (!_networkManager.HasWorkingSession)
                                    {
                                        var allNodesForCBA = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, "MATCH (n:Node) RETURN n");
                                        var cbaNodeIDs = _networkManager.SortNodeGUIDs(allNodesForCBA)[NetworkManager.MainNetworkID];
                                        _networkManager.CreateWorkingSubgraph(cbaNodeIDs, $"Color by {attributeName_color}", $"Color by {attributeName_color}");
                                        _networkManager.SetWorkingSelectedNodes(_networkManager.WorkingSubgraphAllNodeGUIDs, true);
                                    }

                                    // GPA uses linear gradient coloring instead of categorical
                                    if (attributeName_color.ToLower() == "gpa")
                                    {
                                        string gpaMinMaxQuery = "MATCH (n:Node) RETURN min(n.gpa) AS minValue, max(n.gpa) AS maxValue";
                                        var (gpaMin, gpaMax) = _databaseStorage.GetMinMaxFromStore(_networkManager.NetworkGlobal, gpaMinMaxQuery);
                                        string[] gpaGradient = { "#E6F2FF", "#99C5FF", "#4499FF", "#0066CC", "#003388" };
                                        float gpaRange = gpaMax - gpaMin; if (gpaRange <= 0f) gpaRange = 1f;
                                        float gpaStep = gpaRange / gpaGradient.Length;
                                        for (int b = 0; b < gpaGradient.Length; b++)
                                        {
                                            float lo = gpaMin + b * gpaStep;
                                            float hi = b == gpaGradient.Length - 1 ? gpaMax + 0.001f : gpaMin + (b + 1) * gpaStep;
                                            var gpaNodes = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal,
                                                $"MATCH (n:Node) WHERE n.gpa >= {lo:F4} AND n.gpa < {hi:F4} RETURN n");
                                            _networkManager.SetMLNodesColor(_networkManager.TranslateToWorkingSubgraphNodeGUIDs(gpaNodes), gpaGradient[b]);
                                        }
                                        TimerUtils.EndTime("ColorByAttribute");
                                        var _gpaLegend = Instantiate(command_prefab, command_parent.transform);
                                        var gpaBuilder = new StringBuilder();
                                        gpaBuilder.Append("<b>gpa</b>  ");
                                        foreach (var c in gpaGradient) gpaBuilder.Append($"<color={c}>■</color>");
                                        gpaBuilder.AppendLine($"  {gpaMin:F1} → {gpaMax:F1}");
                                        _gpaLegend.GetComponent<TMP_Text>().text = gpaBuilder.ToString();
                                        ScrollToBottom();
                                        break;
                                    }

                                    // Get distinct values from the query result
                                    var distinctValues = _databaseStorage.GetDistinctValuesFromStore(_networkManager.NetworkGlobal, query[i]);
                                    Debug.Log($"Found {distinctValues.Count} distinct values for {attributeName_color}");

                                    // Sort for deterministic color assignment regardless of DB return order
                                    distinctValues.Sort();

                                    // Palette for auto-pick when user doesn't specify colors
                                    string[] allowedColors = new string[] {
                                        "#7FFFFF",  // cyan
                                        "#7F7FFF",  // blue
                                        "#FFFF7F",  // yellow
                                        "#BF7FBF"   // purple
                                    };

                                    // Parse any user-specified colors from action params (action[i][2+] = "category:colorHex")
                                    var userColors = new Dictionary<string, string>();
                                    for (int k = 2; k < action[i].Length; k++)
                                    {
                                        var parts = action[i][k].Split(new char[] { ':' }, 2);
                                        if (parts.Length == 2 && parts[1].StartsWith("#"))
                                            userColors[parts[0].Trim()] = parts[1].Trim();
                                    }

                                    // Build color mapping — use user-specified if available, else auto-pick from palette
                                    var colorMapping = new List<(string cypherValue, string colorHex)>();
                                    int autoColorIdx = 0;
                                    foreach (var val in distinctValues)
                                    {
                                        string colorHex = userColors.TryGetValue(val, out string specifiedColor)
                                            ? specifiedColor
                                            : allowedColors[autoColorIdx++ % allowedColors.Length];
                                        colorMapping.Add((val, colorHex));
                                    }

                                    if (userColors.Count == 0 && distinctValues.Count > allowedColors.Length)
                                        Debug.LogWarning($"Found {distinctValues.Count} categories but only {allowedColors.Length} colors. Colors will repeat.");

                                    // Color each category using the mapping
                                    foreach (var (cypherValue, colorHex) in colorMapping)
                                    {
                                        string categoryQuery = $"MATCH (n:Node) WHERE n.{attributeName_color} = {cypherValue} RETURN n";
                                        Debug.Log($"  Category '{cypherValue}' → {colorHex}");
                                        var categoryNodes = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, categoryQuery);
                                        var subnGUIDs = _networkManager.TranslateToWorkingSubgraphNodeGUIDs(categoryNodes);
                                        _networkManager.SetMLNodesColor(subnGUIDs, colorHex);
                                    }

                                    TimerUtils.EndTime("ColorByAttribute");

                                    // Build legend from the SAME mapping — guaranteed to match what was applied
                                    var _colorByAttr = Instantiate(command_prefab, command_parent.transform);
                                    var _colorByAttr_text = _colorByAttr.GetComponent<TMP_Text>();

                                    System.Text.StringBuilder legendBuilder = new System.Text.StringBuilder();
                                    legendBuilder.AppendLine($"<b>Colored by {attributeName_color}</b>");
                                    foreach (var (cypherValue, colorHex) in colorMapping)
                                    {
                                        string displayValue = cypherValue.Replace("'", "");
                                        string colorName = GetColorName(colorHex);
                                        legendBuilder.AppendLine($"  <color={colorHex}>{colorName}</color> for {displayValue}");
                                    }

                                    _colorByAttr_text.text = legendBuilder.ToString();
                                    ScrollToBottom();
                                    break;

                                case "colorByValue":
                                    Debug.Log("Linear color encoding by value: " + action[i][1]);
                                    string cbvAttribute = action[i][1];

                                    TimerUtils.StartTime("ColorByValue");

                                    // Ensure session exists
                                    if (!_networkManager.HasWorkingSession)
                                    {
                                        var allNodesForCBV = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, "MATCH (n:Node) RETURN n");
                                        var cbvNodeIDs = _networkManager.SortNodeGUIDs(allNodesForCBV)[NetworkManager.MainNetworkID];
                                        _networkManager.CreateWorkingSubgraph(cbvNodeIDs, $"Color by {cbvAttribute}", $"Color by {cbvAttribute}");
                                    }

                                    // Get value range from min/max query
                                    var (cbvMin, cbvMax) = _databaseStorage.GetMinMaxFromStore(_networkManager.NetworkGlobal, query[i]);
                                    Debug.Log($"  {cbvAttribute} range: [{cbvMin}, {cbvMax}]");

                                    // Single-hue blue gradient: light = low value, dark = high value
                                    string[] cbvGradient = { "#E6F2FF", "#99C5FF", "#4499FF", "#0066CC", "#003388" };
                                    int cbvBuckets = cbvGradient.Length;
                                    float cbvRange = cbvMax - cbvMin;
                                    if (cbvRange <= 0f) cbvRange = 1f;
                                    float cbvStep = cbvRange / cbvBuckets;

                                    for (int b = 0; b < cbvBuckets; b++)
                                    {
                                        float lo = cbvMin + b * cbvStep;
                                        float hi = b == cbvBuckets - 1 ? cbvMax + 0.001f : cbvMin + (b + 1) * cbvStep;
                                        string bucketQuery = $"MATCH (n:Node) WHERE n.{cbvAttribute} >= {lo:F4} AND n.{cbvAttribute} < {hi:F4} RETURN n";
                                        var bucketNodes = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, bucketQuery);
                                        var cbvSubnGUIDs = _networkManager.TranslateToWorkingSubgraphNodeGUIDs(bucketNodes);
                                        _networkManager.SetMLNodesColor(cbvSubnGUIDs, cbvGradient[b]);
                                        Debug.Log($"  Bucket {b}: [{lo:F2}, {hi:F2}) → {cbvGradient[b]} ({cbvSubnGUIDs.Count} nodes)");
                                    }

                                    TimerUtils.EndTime("ColorByValue");

                                    // Legend: show gradient strip with min/max values
                                    var _cbvLegend = Instantiate(command_prefab, command_parent.transform);
                                    var cbvBuilder = new StringBuilder();
                                    cbvBuilder.AppendLine($"<b>{cbvAttribute}</b>  (lighter = lower, darker = higher)");
                                    cbvBuilder.Append("  ");
                                    foreach (var c in cbvGradient)
                                        cbvBuilder.Append($"<color={c}>■</color>");
                                    cbvBuilder.AppendLine($"  {cbvMin:F1} → {cbvMax:F1}");
                                    _cbvLegend.GetComponent<TMP_Text>().text = cbvBuilder.ToString();
                                    ScrollToBottom();
                                    break;

                                case "shapeByAttribute":
                                    Debug.Log("Categorical shape encoding by attribute: " + action[i][1]);
                                    string attributeName_shape = action[i][1];

                                    TimerUtils.StartTime("ShapeByAttribute");

                                    // If no session yet, create one with all nodes
                                    if (!_networkManager.HasWorkingSession)
                                    {
                                        var allNodesForSBA = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, "MATCH (n:Node) RETURN n");
                                        var sbaNodeIDs = _networkManager.SortNodeGUIDs(allNodesForSBA)[NetworkManager.MainNetworkID];
                                        _networkManager.CreateWorkingSubgraph(sbaNodeIDs, $"Shape by {attributeName_shape}", $"Shape by {attributeName_shape}");
                                    }

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

                                    // Assign shape to each category within the working subgraph
                                    for (int j = 0; j < distinctShapeValues.Count; j++)
                                    {
                                        string categoryValue = distinctShapeValues[j];
                                        string shapeName = allowedShapes[j % allowedShapes.Length]; // Cycle through shapes

                                        // Build query to select nodes with this category value
                                        string categoryQuery = $"MATCH (n:Node) WHERE n.{attributeName_shape} = {categoryValue} RETURN n";

                                        Debug.Log($"  Category '{categoryValue}' → {shapeName}");

                                        var categoryNodes = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, categoryQuery);
                                        var subnShapeGUIDs = _networkManager.TranslateToWorkingSubgraphNodeGUIDs(categoryNodes);
                                        _networkManager.SetMLNodesShape(subnShapeGUIDs, shapeName);
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
                                case "reset":
                                    Debug.Log("Reset — coloring all nodes yellow and links gray");
                                    TimerUtils.StartTime("Reset");
                                    _lastSelectedLinkGUIDs.Clear();
                                    if (!_networkManager.HasWorkingSession)
                                    {
                                        var allNodesForReset = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, "MATCH (n:Node) RETURN n");
                                        var resetNodeIDs = _networkManager.SortNodeGUIDs(allNodesForReset)[NetworkManager.MainNetworkID];
                                        _networkManager.CreateWorkingSubgraph(resetNodeIDs, "Reset", "Reset");
                                        _networkManager.BringMLNodes(_networkManager.WorkingSubgraphAllNodeGUIDs);
                                    }
                                    _networkManager.SetMLNodesColor(_networkManager.WorkingSubgraphAllNodeGUIDs, "#FFFF00");
                                    _networkManager.SetMLLinksColorStart(_networkManager.WorkingSubgraphAllLinkGUIDs, "#808080");
                                    _networkManager.SetMLLinksColorEnd(_networkManager.WorkingSubgraphAllLinkGUIDs, "#808080");
                                    _networkManager.ClearSelection();
                                    TimerUtils.EndTime("Reset");
                                    var _resetMsg = Instantiate(command_prefab, command_parent.transform);
                                    _resetMsg.GetComponent<TMP_Text>().text = "<color=#aaa><i>Reset complete</i></color>";
                                    ScrollToBottom();
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



