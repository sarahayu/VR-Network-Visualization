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
        public VidiGraph.NetworkLoadingComet loadingComet;
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
        public TMP_Text LegendText;
        public LegendManager legendManager;
        public ScrollRect scroll;
        public GameObject command_prefab;
        public GameObject command_parent;
        public TMP_InputField textCommandInput;

        // Reference to the whisper stream
        private WhisperStream _stream;
        private float whisperStartTime;
        private float _pipelineStartTime;

        // Link GUIDs from the last selectLink command — used by colorLink
        private HashSet<string> _lastSelectedLinkGUIDs = new HashSet<string>();

        // Session-long caches: keyed by attribute name.
        // Node-attribute groupings are static for the scene lifetime (nodes don't change), so no invalidation needed.
        private readonly Dictionary<string, Dictionary<string, List<string>>> _groupedNodesCache = new();
        private readonly Dictionary<string, Dictionary<string, float>> _numericValuesCache = new();
        // Track last selectLink type for legend labeling
        private string _lastLinkSelectLabel = "Links";
        // Track last selectNode parameter for legend labeling
        private string _lastSelectNodeLabel = "";

        private int _snapshotCount = 0;
        private int _demoStep = 0;


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

            loadingComet ??= FindObjectOfType<VidiGraph.NetworkLoadingComet>();
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

            if (Input.GetKeyDown(KeyCode.Return))
            {
                RunDemoStep();
            }

            if (Input.GetKeyDown(KeyCode.T)) loadingComet?.Show();
            if (Input.GetKeyUp(KeyCode.T))   loadingComet?.Hide();

            if (Input.GetKeyDown(KeyCode.S))
            {
                _snapshotCount++;
                string saveName = $"Snapshot {_snapshotCount}";
                if (_networkManager.HasWorkingSession && _networkManager.WorkingSelectedNodeGUIDs.Count > 0)
                {
                    _networkManager.SaveSelectedNodesAsSession(saveName);
                }
                else
                {
                    if (!EnsureWorkingSession(saveName, saveName)) return;
                }
                var msg = Instantiate(command_prefab, command_parent.transform);
                msg.GetComponent<TMP_Text>().text = $"<b>Saved:</b> {saveName}";
                ScrollToBottom();
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
            textCommandInput.ActivateInputField();
            _pipelineStartTime = Time.time;
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
            float recognitionTime = Time.time - whisperStartTime;
            whisperStartTime = Time.time;
            _pipelineStartTime = Time.time;
            StartCoroutine(ClassifyUserCommand(segment.Result, recognitionTime));
        }

        private void OnFinished(string finalResult)
        {
            // Debug.Log("Stream finished!");
        }

        // Classification Main Function
        private IEnumerator ClassifyUserCommand(string recognizedText, float whisperTime)
        {
            loadingIcon.SetLoading(true);
            loadingComet?.Show();
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

                TimerUtils.StartTime("HTTP/LangGraph");
                yield return www.SendWebRequest();
                TimerUtils.EndTime("HTTP/LangGraph");

                if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError("Classification Error: " + www.error);
                    loadingIcon.SetLoading(false);
                    loadingComet?.Hide();
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
                                    _lastSelectNodeLabel = LegendManager.ToShortLabel(action[i][1]);
                                    var nodes = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, query[i]);
                                    TimerUtils.StartTime("SetSelectedNodes");
                                    if (_networkManager.OnQueryMode || !_networkManager.HasWorkingSession)
                                    {
                                        // No session yet — create one with ALL nodes first, then select the queried subset
                                        var allNodesForSel = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, "MATCH (n:Node) RETURN n");
                                        var sortedSel = _networkManager.SortNodeGUIDs(allNodesForSel);
                                        if (!sortedSel.ContainsKey(NetworkManager.MainNetworkID)) break;
                                        _networkManager.CreateWorkingSubgraph(sortedSel[NetworkManager.MainNetworkID], classification.corrected_input, classification.corrected_input);
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
                                    // Track link type for legend
                                    if (action[i][1] == "all") _lastLinkSelectLabel = "All links";
                                    else if (action[i][1].Contains("aggression")) _lastLinkSelectLabel = "Aggression links";
                                    else if (action[i][1].Contains("friendship")) _lastLinkSelectLabel = "Friendship links";
                                    else _lastLinkSelectLabel = action[i][1] + " links";
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
                                    if (!EnsureWorkingSession("Color all nodes", "Color Nodes", selectAll: true)) break;
                                    HashSet<string> nodes_color = _networkManager.WorkingSelectedNodeGUIDs.Count > 0
                                        ? _networkManager.WorkingSelectedNodeGUIDs
                                        : _networkManager.WorkingSubgraphAllNodeGUIDs;

                                    TimerUtils.StartTime("SetColor");
                                    _networkManager.SetMLNodesColor(nodes_color, action[i][1]);
                                    legendManager?.SetNodeColorLabel(action[i][1], _lastSelectNodeLabel);
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
                                    legendManager?.SetEdgeColorLabel(action[i][1], _lastLinkSelectLabel);
                                    TimerUtils.EndTime("SetColor");
                                    break;
                                case "colorByAttribute":
                                    Debug.Log("Categorical coloring by attribute: " + action[i][1]);
                                    string attributeName_color = action[i][1];

                                    TimerUtils.StartTime("ColorByAttribute");

                                    if (!EnsureWorkingSession($"Color by {attributeName_color}", $"Color by {attributeName_color}", selectAll: true)) break;

                                    // Single DB round trip: get all nodes grouped by attribute value (cached per session).
                                    if (!_groupedNodesCache.TryGetValue(attributeName_color, out var grouped_color))
                                    {
                                        grouped_color = _databaseStorage.GetNodesGroupedByAttribute(_networkManager.NetworkGlobal, attributeName_color);
                                        _groupedNodesCache[attributeName_color] = grouped_color;
                                    }
                                    var distinctValues = grouped_color.Keys.OrderBy(v => v).ToList();
                                    Debug.Log($"Found {distinctValues.Count} distinct values for {attributeName_color}");

                                    // Palette for auto-pick when user doesn't specify colors
                                    string[] allowedColors = { "#7FFFFF", "#7F7FFF", "#FFFF7F", "#BF7FBF" };

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

                                    // Apply colors using pre-grouped data — zero additional DB calls
                                    foreach (var (cypherValue, colorHex) in colorMapping)
                                    {
                                        Debug.Log($"  Category '{cypherValue}' → {colorHex}");
                                        var subnGUIDs = _networkManager.TranslateToWorkingSubgraphNodeGUIDs(grouped_color[cypherValue]);
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
                                        string displayValue = LegendManager.PrettifyLabel(attributeName_color, cypherValue);
                                        string colorName = GetColorName(colorHex);
                                        legendBuilder.AppendLine($"  <color={colorHex}>{colorName}</color> for {displayValue}");
                                    }

                                    _colorByAttr_text.text = legendBuilder.ToString();
                                    legendManager?.SetNodeColorMapping(colorMapping.Select(cm => (LegendManager.PrettifyLabel(attributeName_color, cm.cypherValue), cm.colorHex)));
                                    ScrollToBottom();
                                    break;

                                case "colorByGPA":
                                    Debug.Log("Linear GPA coloring");

                                    TimerUtils.StartTime("ColorByGPA");

                                    if (!EnsureWorkingSession("Color by GPA", "Color by GPA", selectAll: true)) break;

                                    {
                                        string[] gpaGradient = { "#E3F2FD", "#90CAF9", "#42A5F5", "#1976D2", "#1565C0" };

                                        // Single DB round trip: all nodes with their GPA values (cached).
                                        // Client-side bucketing replaces the previous 1 min/max + 5 bucket queries.
                                        if (!_numericValuesCache.TryGetValue("gpa", out var gpaValues))
                                        {
                                            gpaValues = _databaseStorage.GetNodesWithNumericValues(_networkManager.NetworkGlobal, "gpa");
                                            _numericValuesCache["gpa"] = gpaValues;
                                        }

                                        if (gpaValues.Count > 0)
                                        {
                                            float gpaMin = gpaValues.Values.Min();
                                            float gpaMax = gpaValues.Values.Max();
                                            if (gpaMax > gpaMin)
                                            {
                                                float step = (gpaMax - gpaMin) / gpaGradient.Length;
                                                for (int b = 0; b < gpaGradient.Length; b++)
                                                {
                                                    float low = gpaMin + b * step;
                                                    float high = b == gpaGradient.Length - 1 ? gpaMax + 0.001f : gpaMin + (b + 1) * step;
                                                    var bucketGuids = gpaValues
                                                        .Where(kv => kv.Value >= low && kv.Value < high)
                                                        .Select(kv => kv.Key);
                                                    var bucketGUIDs = _networkManager.TranslateToWorkingSubgraphNodeGUIDs(bucketGuids);
                                                    _networkManager.SetMLNodesColor(bucketGUIDs, gpaGradient[b]);
                                                }
                                            }
                                        }

                                        TimerUtils.EndTime("ColorByGPA");

                                        var _gpaLegend = Instantiate(command_prefab, command_parent.transform);
                                        var gpaLegendBuilder = new StringBuilder();
                                        gpaLegendBuilder.Append("<b>gpa</b>   low  ");
                                        foreach (var c in gpaGradient)
                                            gpaLegendBuilder.Append($"<color={c}>■</color>");
                                        gpaLegendBuilder.Append("  high");
                                        _gpaLegend.GetComponent<TMP_Text>().text = gpaLegendBuilder.ToString();
                                        ScrollToBottom();
                                    }
                                    break;

                                case "colorByValue":
                                    Debug.Log("Linear color encoding by value: " + action[i][1]);
                                    string cbvAttribute = action[i][1];

                                    TimerUtils.StartTime("ColorByValue");

                                    if (!EnsureWorkingSession($"Color by {cbvAttribute}", $"Color by {cbvAttribute}", selectAll: true)) break;

                                    // Get value range from min/max query
                                    var (cbvMin, cbvMax) = _databaseStorage.GetMinMaxFromStore(_networkManager.NetworkGlobal, query[i]);
                                    Debug.Log($"  {cbvAttribute} range: [{cbvMin}, {cbvMax}]");

                                    // Linear encoding: same hue, lighter = lower, darker = higher
                                    string cbvLinearHue = "#003388";
                                    bool cbvEncodingSuccess = _networkManager.SetMLNodeColorEncoding(
                                        cbvAttribute, cbvMin, cbvMax, cbvLinearHue);

                                    if (!cbvEncodingSuccess)
                                    {
                                        // Fallback: client-side bucketing using a single cached DB query.
                                        // Replaces the previous 5 separate bucket queries.
                                        string[] cbvGradient = { "#E6F2FF", "#99C5FF", "#4499FF", "#0066CC", "#003388" };
                                        if (!_numericValuesCache.TryGetValue(cbvAttribute, out var cbvNodeValues))
                                        {
                                            cbvNodeValues = _databaseStorage.GetNodesWithNumericValues(_networkManager.NetworkGlobal, cbvAttribute);
                                            _numericValuesCache[cbvAttribute] = cbvNodeValues;
                                        }
                                        float cbvRange = cbvMax - cbvMin;
                                        if (cbvRange <= 0f) cbvRange = 1f;
                                        float cbvStep = cbvRange / cbvGradient.Length;
                                        for (int b = 0; b < cbvGradient.Length; b++)
                                        {
                                            float lo = cbvMin + b * cbvStep;
                                            float hi = b == cbvGradient.Length - 1 ? cbvMax + 0.001f : cbvMin + (b + 1) * cbvStep;
                                            var bucketGuids = cbvNodeValues
                                                .Where(kv => kv.Value >= lo && kv.Value < hi)
                                                .Select(kv => kv.Key);
                                            _networkManager.SetMLNodesColor(_networkManager.TranslateToWorkingSubgraphNodeGUIDs(bucketGuids), cbvGradient[b]);
                                        }
                                    }

                                    TimerUtils.EndTime("ColorByValue");

                                    // Legend: gradient strip with lighter/darker meaning (no min/max numbers)
                                    var _cbvLegend = Instantiate(command_prefab, command_parent.transform);
                                    var cbvBuilder = new StringBuilder();
                                    string[] cbvGradientDisplay = { "#E6F2FF", "#99C5FF", "#4499FF", "#0066CC", "#003388" };
                                    cbvBuilder.Append($"<b>{cbvAttribute}</b>   low  ");
                                    foreach (var c in cbvGradientDisplay)
                                        cbvBuilder.Append($"<color={c}>■</color>");
                                    cbvBuilder.Append("  high");
                                    _cbvLegend.GetComponent<TMP_Text>().text = cbvBuilder.ToString();
                                    ScrollToBottom();
                                    break;

                                case "shapeByAttribute":
                                    Debug.Log("Categorical shape encoding by attribute: " + action[i][1]);
                                    string attributeName_shape = action[i][1];

                                    TimerUtils.StartTime("ShapeByAttribute");

                                    if (!EnsureWorkingSession($"Shape by {attributeName_shape}", $"Shape by {attributeName_shape}")) break;

                                    // Single DB round trip: get all nodes grouped by attribute value (cached).
                                    if (!_groupedNodesCache.TryGetValue(attributeName_shape, out var grouped_shape))
                                    {
                                        grouped_shape = _databaseStorage.GetNodesGroupedByAttribute(_networkManager.NetworkGlobal, attributeName_shape);
                                        _groupedNodesCache[attributeName_shape] = grouped_shape;
                                    }
                                    var distinctShapeValues = grouped_shape.Keys.OrderBy(v => v).ToList();
                                    Debug.Log($"Found {distinctShapeValues.Count} distinct values for {attributeName_shape}");

                                    string[] allowedShapes = { "sphere", "cube", "tetrahedron" };

                                    if (distinctShapeValues.Count > 3)
                                        Debug.LogWarning($"Found {distinctShapeValues.Count} categories but only 3 shapes available. Shapes will repeat.");

                                    // Apply shapes using pre-grouped data — zero additional DB calls
                                    for (int j = 0; j < distinctShapeValues.Count; j++)
                                    {
                                        string categoryValue = distinctShapeValues[j];
                                        string shapeName = allowedShapes[j % allowedShapes.Length];
                                        Debug.Log($"  Category '{categoryValue}' → {shapeName}");
                                        var subnShapeGUIDs = _networkManager.TranslateToWorkingSubgraphNodeGUIDs(grouped_shape[categoryValue]);
                                        _networkManager.SetMLNodesShape(subnShapeGUIDs, shapeName);
                                    }

                                    TimerUtils.EndTime("ShapeByAttribute");

                                    var _shapeByAttr = Instantiate(command_prefab, command_parent.transform);
                                    var _shapeByAttr_text = _shapeByAttr.GetComponent<TMP_Text>();

                                    string[] shapeSymbols = { "●", "■", "▲" };
                                    var shapeLegendBuilder = new System.Text.StringBuilder();
                                    shapeLegendBuilder.AppendLine($"<b>Shaped by {attributeName_shape}</b>");
                                    for (int j = 0; j < distinctShapeValues.Count; j++)
                                    {
                                        string displayVal = distinctShapeValues[j].Replace("'", "");
                                        shapeLegendBuilder.AppendLine($"  {shapeSymbols[j % shapeSymbols.Length]} {displayVal} = {allowedShapes[j % allowedShapes.Length]}");
                                    }

                                    _shapeByAttr_text.text = shapeLegendBuilder.ToString();
                                    legendManager?.SetShapeMapping(distinctShapeValues.Select(v => LegendManager.PrettifyLabel(attributeName_shape, v)));
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
                                    bool resetHadSession = _networkManager.HasWorkingSession;
                                    if (!EnsureWorkingSession("Reset", "Reset")) break;
                                    if (!resetHadSession)
                                        _networkManager.BringMLNodes(_networkManager.WorkingSubgraphAllNodeGUIDs);
                                    _networkManager.SetMLNodesColor(_networkManager.WorkingSubgraphAllNodeGUIDs, "#FFFF00");
                                    _networkManager.SetMLLinksColorStart(_networkManager.WorkingSubgraphAllLinkGUIDs, "#808080");
                                    _networkManager.SetMLLinksColorEnd(_networkManager.WorkingSubgraphAllLinkGUIDs, "#808080");
                                    _networkManager.ClearSelection();
                                    legendManager?.ResetAll();
                                    TimerUtils.EndTime("Reset");
                                    var _resetMsg = Instantiate(command_prefab, command_parent.transform);
                                    _resetMsg.GetComponent<TMP_Text>().text = "<color=#aaa><i>Reset complete</i></color>";
                                    ScrollToBottom();
                                    break;
                                case "deleteSession":
                                    Debug.Log("Deleting current session");
                                    if (!_networkManager.HasWorkingSession)
                                    {
                                        var _noSession = Instantiate(command_prefab, command_parent.transform);
                                        _noSession.GetComponent<TMP_Text>().text = "<color=#aaa><i>No active session to delete.</i></color>";
                                        ScrollToBottom();
                                        break;
                                    }
                                    _networkManager.DeleteCurWorkingGraph();
                                    var _delMsg = Instantiate(command_prefab, command_parent.transform);
                                    _delMsg.GetComponent<TMP_Text>().text = "<color=#f88><b>Session deleted.</b></color>";
                                    ScrollToBottom();
                                    break;
                                case "saveSession":
                                    string saveName = action[i].Length > 1 && !string.IsNullOrWhiteSpace(action[i][1])
                                        ? action[i][1]
                                        : "Snapshot";
                                    Debug.Log($"Saving session as: {saveName}");
                                    if (_networkManager.HasWorkingSession && _networkManager.WorkingSelectedNodeGUIDs.Count > 0)
                                    {
                                        _networkManager.SaveSelectedNodesAsSession(saveName);
                                    }
                                    else
                                    {
                                        // No selection yet — save all nodes as the new session
                                        if (!EnsureWorkingSession(saveName, saveName)) break;
                                    }
                                    var _saveCmd = Instantiate(command_prefab, command_parent.transform);
                                    _saveCmd.GetComponent<TMP_Text>().text = $"<b>Saved:</b> {saveName}";
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

                        Debug.Log($"<color=cyan>[TotalPipeline]</color> {(Time.time - _pipelineStartTime) * 1000:F0}ms end-to-end (whisper:{whisperTime:F2}s + server + execution)");
                        loadingIcon.SetLoading(false);
                        StartCoroutine(HideCometWhenReady());
                    }
                }
            }
        }


        private IEnumerator HideCometWhenReady()
        {
            while (_networkManager.IsLayoutAnimating)
                yield return null;
            loadingComet?.Hide();
        }

        private void RunDemoStep()
        {
            switch (_demoStep)
            {
                case 0:
                    // Step 1 — create a working session and select ALL nodes inside it
                    if (!EnsureWorkingSession("Demo Selection", "Demo")) return;
                    _networkManager.SetWorkingSelectedNodes(_networkManager.WorkingSubgraphAllNodeGUIDs, true);
                    ShowDemoMessage("<b>[Demo 1/3]</b> Working session created — all nodes selected.\n<color=#aaa>Press Enter to save snapshot →</color>");
                    _demoStep++;
                    break;

                case 1:
                    // Step 2 — save the selection as a named snapshot without switching view
                    if (!_networkManager.HasWorkingSession || _networkManager.WorkingSelectedNodeGUIDs.Count == 0)
                    {
                        ShowDemoMessage("<color=#f88>[Demo]</color> No nodes selected — press Enter to restart.");
                        _demoStep = 0;
                        return;
                    }
                    _networkManager.SaveSelectedNodesAsSession("Demo Snapshot");
                    ShowDemoMessage("<b>[Demo 2/3]</b> Snapshot saved → watch the cyan dots fly to the wall.\n<color=#aaa>Press Enter to finish →</color>");
                    _demoStep++;
                    break;

                case 2:
                    // Step 3 — reset demo counter
                    _demoStep = 0;
                    ShowDemoMessage("<b>[Demo 3/3]</b> Demo complete. Press Enter to run again.");
                    break;
            }
        }

        private void ShowDemoMessage(string msg)
        {
            var obj = Instantiate(command_prefab, command_parent.transform);
            obj.GetComponent<TMP_Text>().text = msg;
            ScrollToBottom();
        }

        private void ScrollToBottom()
        {
            if (scroll != null)
            {
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition = 0f;
            }
        }

        // Creates a working session with all nodes if one doesn't exist yet.
        // Returns false when the main network is not found — caller should break/return.
        // selectAll: also mark all subgraph nodes as selected after creation.
        private bool EnsureWorkingSession(string sessionLabel, string shortLabel, bool selectAll = false)
        {
            if (_networkManager.HasWorkingSession) return true;
            var allNodes = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, "MATCH (n:Node) RETURN n");
            var sorted = _networkManager.SortNodeGUIDs(allNodes);
            if (!sorted.ContainsKey(NetworkManager.MainNetworkID)) return false;
            _networkManager.CreateWorkingSubgraph(sorted[NetworkManager.MainNetworkID], sessionLabel, shortLabel);
            if (selectAll) _networkManager.SetWorkingSelectedNodes(_networkManager.WorkingSubgraphAllNodeGUIDs, true);
            return true;
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



