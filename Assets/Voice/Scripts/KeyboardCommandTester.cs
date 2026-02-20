using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Net;
using System.Threading;
using UnityEngine.Networking;
using Newtonsoft.Json;
using TMPro;

namespace Whisper.Samples
{
    /// <summary>
    /// Keyboard-based testing for voice commands without VR
    /// Simulates sending commands to the LangGraph server and executing actions
    /// </summary>
    public class KeyboardCommandTester : MonoBehaviour
    {
        [Header("References")]
        public StreamingSampleMic streamingSampleMic;
        public GameObject command_prefab;
        public GameObject command_parent;
        public UnityEngine.UI.ScrollRect scroll;

        [Header("Server Settings")]
        public string serverUrl = "http://localhost:5000/classify";
        public bool serverEnabled = true;

        [Header("Keyboard Shortcuts")]
        public KeyCode sizeByDegreeKey = KeyCode.Alpha1;
        public KeyCode sizeByGPAKey = KeyCode.Alpha2;
        public KeyCode colorByGradeKey = KeyCode.Alpha3;
        public KeyCode colorBySexKey = KeyCode.Alpha4;
        public KeyCode shapeByGradeKey = KeyCode.Alpha5;
        public KeyCode selectFemaleKey = KeyCode.Alpha6;
        public KeyCode deselectAllKey = KeyCode.Alpha7;
        public KeyCode colorSelectedRedKey = KeyCode.Alpha8;
        public KeyCode moveSelectedKey = KeyCode.Alpha9;
        public KeyCode resetKey = KeyCode.R;

        [Header("Text Input")]
        [TextArea] public string testCommand;
        public KeyCode submitCommandKey = KeyCode.T;

        [Header("Display")]
        public bool showInstructions = true;

        // Demo mode state
        private bool _demoRunning = false;
        private int _demoStep = 0;
        private int _demoTotalSteps = 0;
        private int _activeDemoId = 0; // 0 = demo P, 1 = demo L

        // External command listener
        [Header("External Command Listener")]
        public int listenerPort = 5001;
        public bool listenerEnabled = true;
        private HttpListener _httpListener;
        private Thread _listenerThread;
        private ConcurrentQueue<string> _commandQueue = new ConcurrentQueue<string>();
        private bool _isProcessingCommand = false;

        private void Start()
        {
            if (showInstructions)
            {
                PrintInstructions();
            }

            if (listenerEnabled)
            {
                StartHttpListener();
            }

        }

        private void OnDestroy()
        {
            StopHttpListener();
        }

        private void StartHttpListener()
        {
            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add($"http://localhost:{listenerPort}/");
                _httpListener.Start();

                _listenerThread = new Thread(ListenForRequests);
                _listenerThread.IsBackground = true;
                _listenerThread.Start();

                Debug.Log($"[LISTENER] External command listener started on port {listenerPort}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LISTENER] Failed to start: {e.Message}");
            }
        }

        private void StopHttpListener()
        {
            if (_httpListener != null && _httpListener.IsListening)
            {
                _httpListener.Stop();
                _httpListener.Close();
            }
            if (_listenerThread != null && _listenerThread.IsAlive)
            {
                _listenerThread.Join(1000);
            }
        }

        private void ListenForRequests()
        {
            while (_httpListener != null && _httpListener.IsListening)
            {
                try
                {
                    var context = _httpListener.GetContext();
                    var request = context.Request;
                    var response = context.Response;

                    if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/command")
                    {
                        using (var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding))
                        {
                            string body = reader.ReadToEnd();
                            var json = JsonConvert.DeserializeObject<Dictionary<string, string>>(body);
                            if (json != null && json.ContainsKey("userText"))
                            {
                                string cmd = json["userText"];
                                _commandQueue.Enqueue(cmd);
                                Debug.Log($"[LISTENER] Queued command: {cmd}");

                                byte[] responseBytes = Encoding.UTF8.GetBytes("{\"status\": \"queued\"}");
                                response.ContentType = "application/json";
                                response.StatusCode = 200;
                                response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                            }
                            else
                            {
                                byte[] responseBytes = Encoding.UTF8.GetBytes("{\"error\": \"missing userText\"}");
                                response.ContentType = "application/json";
                                response.StatusCode = 400;
                                response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                            }
                        }
                    }
                    else if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/ping")
                    {
                        byte[] responseBytes = Encoding.UTF8.GetBytes("{\"status\": \"ready\"}");
                        response.ContentType = "application/json";
                        response.StatusCode = 200;
                        response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                    }
                    else
                    {
                        response.StatusCode = 404;
                    }

                    response.Close();
                }
                catch (HttpListenerException)
                {
                    break; // Listener was stopped
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[LISTENER] Error: {e.Message}");
                }
            }
        }

        private void Update()
        {
            // Process external commands from the HTTP listener queue
            if (!_isProcessingCommand && _commandQueue.TryDequeue(out string externalCommand))
            {
                Debug.Log($"[LISTENER] Processing external command: {externalCommand}");
                _isProcessingCommand = true;
                StartCoroutine(SendToServerAndExecuteExternal(externalCommand));
            }

            // Submit typed inspector command
            if (Input.GetKeyDown(submitCommandKey) && !string.IsNullOrWhiteSpace(testCommand))
            {
                Debug.Log($"[TEXT INPUT] Submitting: {testCommand}");
                StartCoroutine(SendToServerAndExecute(testCommand.Trim()));
            }

            // Size by grade (stored property - works)
            if (Input.GetKeyDown(sizeByDegreeKey))
            {
                Debug.Log("[KEYBOARD] Triggered: Size nodes by grade");
                ExecuteDirectActionWithSelection("sizeNode", "grade:all");
            }

            // Size by GPA (needs selection first)
            if (Input.GetKeyDown(sizeByGPAKey))
            {
                Debug.Log("[KEYBOARD] Triggered: Size nodes by GPA");
                ExecuteDirectActionWithSelection("sizeNode", "gpa:all");
            }

            // Color by grade (categorical - works on all nodes, no prior selection needed)
            if (Input.GetKeyDown(colorByGradeKey))
            {
                Debug.Log("[KEYBOARD] Triggered: Color by attribute (grade)");
                ExecuteDirectAction("colorByAttribute", "grade");
            }

            // Color by sex (categorical - works on all nodes, no prior selection needed)
            if (Input.GetKeyDown(colorBySexKey))
            {
                Debug.Log("[KEYBOARD] Triggered: Color by attribute (sex)");
                ExecuteDirectAction("colorByAttribute", "sex");
            }

            // Shape by grade (categorical shape encoding - works on all nodes)
            if (Input.GetKeyDown(shapeByGradeKey))
            {
                Debug.Log("[KEYBOARD] Triggered: Shape by attribute (grade)");
                ExecuteDirectAction("shapeByAttribute", "grade");
            }

            // Select female nodes
            if (Input.GetKeyDown(selectFemaleKey))
            {
                Debug.Log("[KEYBOARD] Triggered: Select female students");
                ExecuteDirectSelectAction("n.sex = 'F'");
            }

            // Deselect all
            if (Input.GetKeyDown(deselectAllKey))
            {
                Debug.Log("[KEYBOARD] Triggered: Deselect all");
                ExecuteDirectSimpleAction("deselect", "");
            }

            // Color selected nodes red
            if (Input.GetKeyDown(colorSelectedRedKey))
            {
                Debug.Log("[KEYBOARD] Triggered: Color selected nodes red");
                ExecuteDirectSimpleAction("colorNode", "#FF0000");
            }

            // Move selected nodes
            if (Input.GetKeyDown(moveSelectedKey))
            {
                Debug.Log("[KEYBOARD] Triggered: Move selected nodes");
                ExecuteDirectSimpleAction("move", "here");
            }

            // Reset all annotations and selections
            if (Input.GetKeyDown(resetKey))
            {
                Debug.Log("[KEYBOARD] Triggered: Reset all");
                var _networkManager = streamingSampleMic._networkManager;
                _networkManager.ResetAll();
            }

            // Help menu
            if (Input.GetKeyDown(KeyCode.H))
            {
                PrintInstructions();
            }

            // Demo mode: Press P or L to start, Enter to advance
            if (Input.GetKeyDown(KeyCode.P) && !_demoRunning)
            {
                Debug.Log("[DEMO P] Starting demo sequence...");
                _demoRunning = true;
                _activeDemoId = 0;
                _demoStep = 0;
                _demoTotalSteps = 3;
                RunDemoStep(_activeDemoId, _demoStep);
            }

            if (Input.GetKeyDown(KeyCode.L) && !_demoRunning)
            {
                Debug.Log("[DEMO L] Starting demo sequence...");
                _demoRunning = true;
                _activeDemoId = 1;
                _demoStep = 0;
                _demoTotalSteps = 2;
                RunDemoStep(_activeDemoId, _demoStep);
            }

            if (Input.GetKeyDown(KeyCode.O) && !_demoRunning)
            {
                Debug.Log("[DEMO O] Starting demo sequence...");
                _demoRunning = true;
                _activeDemoId = 2;
                _demoStep = 0;
                _demoTotalSteps = 3;
                RunDemoStep(_activeDemoId, _demoStep);
            }

            if (Input.GetKeyDown(KeyCode.Return) && _demoRunning)
            {
                _demoStep++;
                if (_demoStep < _demoTotalSteps)
                {
                    RunDemoStep(_activeDemoId, _demoStep);
                }
                else
                {
                    _demoRunning = false;
                    Debug.Log("[DEMO] Demo complete!");
                }
            }
        }

        private void ExecuteCommand(string commandText)
        {
            if (serverEnabled)
            {
                StartCoroutine(SendToServerAndExecute(commandText));
            }
            else
            {
                Debug.LogWarning("Server is disabled. Enable 'Server Enabled' to test with LangGraph server.");
            }
        }

        private void ExecuteDirectActionWithSelection(string actionName, string parameter)
        {
            // For actions that need all nodes selected first (like degree sizing)
            Debug.Log($"[DIRECT WITH SELECTION] Executing: {actionName}({parameter})");

            // Create select all + action
            string[][] actions;
            string[] queries;
            string commandDescription;

            // First select all nodes, then perform the action
            actions = new string[][] {
                new string[] { "selectNode", "true" },  // Select all nodes
                new string[] { actionName, parameter }
            };

            queries = new string[] {
                "MATCH (n:Node) RETURN n",  // Select all
                actionName == "sizeNode"
                    ? $"MATCH (n:Node) RETURN min(n.{parameter.Split(':')[0]}) AS minValue, max(n.{parameter.Split(':')[0]}) AS maxValue"
                    : ""
            };

            commandDescription = $"select all then {actionName} by {parameter}";

            StartCoroutine(ExecuteActionsDirectly(actions, queries, commandDescription));
        }

        private void ExecuteDirectAction(string actionName, string parameter)
        {
            // Directly create and execute action without going through server
            Debug.Log($"[DIRECT] Executing action: {actionName}({parameter})");

            // Create mock response
            string[][] actions;
            string[] queries;
            string commandDescription;

            switch (actionName)
            {
                case "colorByAttribute":
                    actions = new string[][] { new string[] { "colorByAttribute", parameter } };
                    queries = new string[] { $"MATCH (n:Node) RETURN DISTINCT n.{parameter} AS value ORDER BY value" };
                    commandDescription = $"color by {parameter}";
                    break;

                case "shapeByAttribute":
                    actions = new string[][] { new string[] { "shapeByAttribute", parameter } };
                    queries = new string[] { $"MATCH (n:Node) RETURN DISTINCT n.{parameter} AS value ORDER BY value" };
                    commandDescription = $"shape by {parameter}";
                    break;

                case "sizeNode":
                    actions = new string[][] { new string[] { "sizeNode", parameter } };
                    queries = new string[] { $"MATCH (n:Node) RETURN min(n.{parameter.Split(':')[0]}) AS minValue, max(n.{parameter.Split(':')[0]}) AS maxValue" };
                    commandDescription = $"size by {parameter}";
                    break;

                default:
                    Debug.LogError($"Unknown direct action: {actionName}");
                    return;
            }

            // Execute directly via StreamingSampleMic's logic
            StartCoroutine(ExecuteActionsDirectly(actions, queries, commandDescription));
        }

        private void ExecuteDirectSelectAction(string whereClause)
        {
            // For selection commands with WHERE clause
            Debug.Log($"[DIRECT SELECT] Executing: selectNode with WHERE {whereClause}");

            string[][] actions = new string[][] {
                new string[] { "selectNode", whereClause }
            };

            string[] queries = new string[] {
                $"MATCH (n:Node) WHERE {whereClause} RETURN n"
            };

            string commandDescription = $"select nodes where {whereClause}";

            StartCoroutine(ExecuteActionsDirectly(actions, queries, commandDescription));
        }

        private void ExecuteDirectSimpleAction(string actionName, string parameter)
        {
            // For simple single-action commands (deselect, colorNode, move, etc.)
            Debug.Log($"[DIRECT SIMPLE] Executing: {actionName}({parameter})");

            string[][] actions = new string[][] {
                new string[] { actionName, parameter }
            };

            string[] queries = new string[] { "" };  // Most simple actions don't need queries

            string commandDescription = $"{actionName} {parameter}";

            StartCoroutine(ExecuteActionsDirectly(actions, queries, commandDescription));
        }

        private IEnumerator SendToServerAndExecuteExternal(string userInput)
        {
            yield return StartCoroutine(SendToServerAndExecute(userInput));
            _isProcessingCommand = false;
        }

        private IEnumerator SendToServerAndExecute(string userInput)
        {
            Debug.Log($"[SERVER] Sending to server: {userInput}");

            ClassificationRequest requestBody = new ClassificationRequest { userText = userInput };
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
                    Debug.LogError($"[SERVER ERROR] {www.error}");
                    Debug.LogError("Make sure the Python server is running: python langgraph_server.py");
                    ShowSystemMessage("Please say it again");
                }
                else
                {
                    string responseJson = www.downloadHandler.text;
                    Debug.Log($"[SERVER RESPONSE] {responseJson}");

                    ClassificationResponse classification = JsonConvert.DeserializeObject<ClassificationResponse>(responseJson, new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        MissingMemberHandling = MissingMemberHandling.Ignore
                    });

                    // Check if clarification is needed (server couldn't understand)
                    if (!string.IsNullOrEmpty(classification.clarify))
                    {
                        Debug.LogWarning($"[SERVER] Clarification needed: {classification.clarify}");
                        ShowSystemMessage("Please say it again");
                    }
                    else
                    {
                        // Execute the actions
                        StartCoroutine(ExecuteActionsDirectly(classification.actions, classification.queries, userInput));
                    }
                }
            }
        }

        private IEnumerator ExecuteActionsDirectly(string[][] actions, string[] queries, string originalCommand)
        {
            if (streamingSampleMic == null)
            {
                Debug.LogError("StreamingSampleMic reference is null! Please assign it in the Inspector.");
                yield break;
            }

            var _networkManager = streamingSampleMic._networkManager;
            var _databaseStorage = streamingSampleMic._databaseStorage;

            // Store link GUIDs from selectLink so colorLink can use them without marking links as Selected
            HashSet<string> _lastQueriedLinkGUIDs = new HashSet<string>();

            if (_networkManager == null || _databaseStorage == null)
            {
                Debug.LogError("NetworkManager or DatabaseStorage is null!");
                yield break;
            }

            Debug.Log($"[EXECUTE] Processing {actions.Length} action(s) for: {originalCommand}");

            // Display command in UI (if UI elements are assigned)
            if (command_prefab != null && command_parent != null)
            {
                var _commandUI = Instantiate(command_prefab, command_parent.transform);
                var _commandUI_text = _commandUI.GetComponent<TMP_Text>();
                _commandUI_text.text = $"<b>User:</b> {originalCommand}";
                ScrollToBottom();
            }

            for (int i = 0; i < actions.Length; i++)
            {
                string actionName = actions[i][0];
                string actionParam = actions[i][1];

                Debug.Log($"[ACTION {i + 1}/{actions.Length}] {actionName}({actionParam})");

                switch (actionName)
                {
                    case "selectNode":
                        Debug.Log($"  Selecting nodes with query: {queries[i]}");
                        var nodes = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, queries[i]);

                        if (_networkManager.OnQueryMode)
                        {
                            Debug.Log($"  [Query Mode] Creating working subgraph");
                            var nodeIDs = _networkManager.SortNodeGUIDs(nodes)[VidiGraph.NetworkManager.MainNetworkID];
                            _networkManager.CreateWorkingSubgraph(nodeIDs, originalCommand, originalCommand);
                            _networkManager.SetQueryMode(false);
                            _networkManager.SetWorkingSelectedNodes(_networkManager.WorkingSubgraphAllNodeGUIDs, true);
                            Debug.Log($"  ✓ Working subgraph created with {_networkManager.WorkingSubgraphAllNodeGUIDs.Count} nodes");
                        }
                        else if (!_networkManager.HasWorkingSession)
                        {
                            Debug.Log($"  [No Session] Creating working subgraph");
                            var nodeIDs = _networkManager.SortNodeGUIDs(nodes)[VidiGraph.NetworkManager.MainNetworkID];
                            _networkManager.CreateWorkingSubgraph(nodeIDs, originalCommand, originalCommand);
                            _networkManager.SetWorkingSelectedNodes(_networkManager.WorkingSubgraphAllNodeGUIDs, true);
                            Debug.Log($"  ✓ Working subgraph created with {_networkManager.WorkingSubgraphAllNodeGUIDs.Count} nodes");
                        }
                        else
                        {
                            Debug.Log($"  [Session Exists] Selecting within working subgraph");
                            var subnGUIDs = _networkManager.TranslateToWorkingSubgraphNodeGUIDs(nodes);
                            _networkManager.SetWorkingSelectedNodes(subnGUIDs, true);
                            Debug.Log($"  ✓ {subnGUIDs.Count} nodes selected in working subgraph");
                        }
                        break;

                    case "sizeNode":
                        string sizeParam = actionParam;
                        string attributeName = sizeParam.Contains(":") ? sizeParam.Split(':')[0] : sizeParam;
                        string scope = sizeParam.Contains(":") ? sizeParam.Split(':')[1] : "all";

                        Debug.Log($"  Sizing nodes by: {attributeName} (scope: {scope})");
                        var (minV, maxV) = _databaseStorage.GetMinMaxFromStore(_networkManager.NetworkGlobal, queries[i]);
                        Debug.Log($"  Min/Max: {minV}, {maxV}");
                        _networkManager.SetMLNodeSizeEncoding(attributeName, minV, maxV, VidiGraph.NetworkManager.MainNetworkID);
                        Debug.Log($"  ✓ Size encoding applied");
                        break;

                    case "colorNode":
                        Debug.Log($"  Coloring nodes: {actionParam}");
                        HashSet<string> nodes_color;
                        if (_networkManager.HasWorkingSession)
                        {
                            nodes_color = _networkManager.WorkingSelectedNodeGUIDs;
                            if (nodes_color.Count == 0)
                                nodes_color = _networkManager.WorkingSubgraphAllNodeGUIDs;
                        }
                        else
                        {
                            nodes_color = _networkManager.SelectedNodeGUIDs;
                        }
                        Debug.Log($"  Found {nodes_color.Count} nodes to color");
                        _networkManager.SetMLNodesColor(nodes_color, actionParam);
                        Debug.Log($"  ✓ {nodes_color.Count} nodes colored");
                        break;

                    case "colorByAttribute":
                        Debug.Log($"  Categorical coloring by: {actionParam}");
                        string attributeName_color = actionParam;

                        // Get distinct values
                        var distinctValues = _databaseStorage.GetDistinctValuesFromStore(_networkManager.NetworkGlobal, queries[i]);
                        Debug.Log($"  Found {distinctValues.Count} distinct values");

                        // Define 4 allowed colors (no red - reserved for highlighting)
                        string[] allowedColors = new string[] {
                            "#7FFFFF",  // cyan
                            "#7F7FFF",  // blue
                            "#FFFF7F",  // yellow
                            "#BF7FBF"   // purple
                        };





                        if (distinctValues.Count > 6)
                        {
                            Debug.LogWarning($"  ⚠ {distinctValues.Count} categories but only 6 colors - colors will repeat");
                        }

                        // Color each category
                        for (int j = 0; j < distinctValues.Count; j++)
                        {
                            string categoryValue = distinctValues[j];
                            string colorHex = allowedColors[j % allowedColors.Length];

                            string categoryQuery = $"MATCH (n:Node) WHERE n.{attributeName_color} = {categoryValue} RETURN n";
                            Debug.Log($"    Category {categoryValue} → {colorHex}");

                            var categoryNodes = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, categoryQuery);
                            _networkManager.SetMLNodesColor(categoryNodes, colorHex);
                        }

                        // Print color legend to console
                        Debug.Log($"  ✓ Categorical coloring complete");
                        Debug.Log($"  === Color Legend for '{attributeName_color}' ===");
                        for (int j = 0; j < distinctValues.Count; j++)
                        {
                            string categoryValue = distinctValues[j].Replace("'", "");
                            string colorHex = allowedColors[j % allowedColors.Length];
                            Debug.Log($"    ■ {categoryValue} = {colorHex}");
                        }

                        // Create UI legend display (if UI elements are assigned)
                        if (command_prefab != null && command_parent != null)
                        {
                            var _colorLegend = Instantiate(command_prefab, command_parent.transform);
                            var _colorLegend_text = _colorLegend.GetComponent<TMP_Text>();

                            System.Text.StringBuilder uiLegendBuilder = new System.Text.StringBuilder();
                            uiLegendBuilder.AppendLine($"<b>Colored by {attributeName_color}</b>");

                            for (int j = 0; j < distinctValues.Count; j++)
                            {
                                string categoryValue = distinctValues[j].Replace("'", "");
                                string colorHex = allowedColors[j % allowedColors.Length];
                                string colorName = GetColorName(colorHex);
                                uiLegendBuilder.AppendLine($"  <color={colorHex}>{colorName}</color> for {categoryValue}");
                            }

                            _colorLegend_text.text = uiLegendBuilder.ToString();
                            ScrollToBottom();
                        }
                        break;

                    case "shapeByAttribute":
                        Debug.Log($"  Categorical shape encoding by: {actionParam}");
                        string attributeName_shape = actionParam;

                        // Get distinct values
                        var distinctShapeValues = _databaseStorage.GetDistinctValuesFromStore(_networkManager.NetworkGlobal, queries[i]);
                        Debug.Log($"  Found {distinctShapeValues.Count} distinct values");

                        // Define 3 allowed shapes
                        string[] allowedShapes = new string[] {
                            "sphere",
                            "cube",
                            "tetrahedron"
                        };

                        if (distinctShapeValues.Count > 3)
                        {
                            Debug.LogWarning($"  ⚠ {distinctShapeValues.Count} categories but only 3 shapes - shapes will repeat");
                        }

                        // Assign shape to each category
                        for (int j = 0; j < distinctShapeValues.Count; j++)
                        {
                            string categoryValue = distinctShapeValues[j];
                            string shapeName = allowedShapes[j % allowedShapes.Length];

                            string categoryQuery = $"MATCH (n:Node) WHERE n.{attributeName_shape} = {categoryValue} RETURN n";
                            Debug.Log($"    Category {categoryValue} → {shapeName}");

                            var categoryNodes = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, categoryQuery);
                            _networkManager.SetMLNodesShape(categoryNodes, shapeName);
                        }

                        // Print shape legend to console
                        Debug.Log($"  ✓ Categorical shape encoding complete");
                        Debug.Log($"  === Shape Legend for '{attributeName_shape}' ===");

                        string[] shapeSymbols = new string[] { "●", "■", "▲" };
                        for (int j = 0; j < distinctShapeValues.Count; j++)
                        {
                            string categoryValue = distinctShapeValues[j].Replace("'", "");
                            string shapeName = allowedShapes[j % allowedShapes.Length];
                            string shapeSymbol = shapeSymbols[j % shapeSymbols.Length];
                            Debug.Log($"    {shapeSymbol} {categoryValue} = {shapeName}");
                        }

                        // Create UI legend display (if UI elements are assigned)
                        if (command_prefab != null && command_parent != null)
                        {
                            var _shapeLegend = Instantiate(command_prefab, command_parent.transform);
                            var _shapeLegend_text = _shapeLegend.GetComponent<TMP_Text>();

                            System.Text.StringBuilder uiShapeLegendBuilder = new System.Text.StringBuilder();
                            uiShapeLegendBuilder.AppendLine($"<b>Shaped by {attributeName_shape}</b>");

                            for (int j = 0; j < distinctShapeValues.Count; j++)
                            {
                                string categoryValue = distinctShapeValues[j].Replace("'", "");
                                string shapeName = allowedShapes[j % allowedShapes.Length];
                                string shapeSymbol = shapeSymbols[j % shapeSymbols.Length];
                                uiShapeLegendBuilder.AppendLine($"  {shapeSymbol} {categoryValue} = {shapeName}");
                            }

                            _shapeLegend_text.text = uiShapeLegendBuilder.ToString();
                            ScrollToBottom();
                        }
                        break;

                    case "selectLink":
                        Debug.Log($"  Querying links with: {queries[i]}");
                        var links = _databaseStorage.GetLinksFromStore(_networkManager.NetworkGlobal, queries[i]);
                        _lastQueriedLinkGUIDs = new HashSet<string>(links);
                        Debug.Log($"  ✓ Found {_lastQueriedLinkGUIDs.Count} links from query");
                        break;

                    case "colorLink":
                        Debug.Log($"  Coloring links: {actionParam}");
                        HashSet<string> linkGUIDs_color;
                        if (_networkManager.HasWorkingSession)
                        {
                            linkGUIDs_color = _networkManager.TranslateToWorkingSubgraphLinkGUIDs(_lastQueriedLinkGUIDs);
                            if (linkGUIDs_color.Count == 0)
                                linkGUIDs_color = _networkManager.WorkingSubgraphAllLinkGUIDs;
                        }
                        else
                        {
                            if (_lastQueriedLinkGUIDs.Count == 0)
                            {
                                string selectAllLinksQuery = "MATCH ()-[r:POINTS_TO]-() RETURN r";
                                var allLinks = _databaseStorage.GetLinksFromStore(_networkManager.NetworkGlobal, selectAllLinksQuery);
                                _lastQueriedLinkGUIDs = new HashSet<string>(allLinks);
                            }
                            linkGUIDs_color = _lastQueriedLinkGUIDs;
                        }
                        _networkManager.SetMLLinksColorStart(linkGUIDs_color, actionParam);
                        _networkManager.SetMLLinksColorEnd(linkGUIDs_color, actionParam);
                        Debug.Log($"  ✓ {linkGUIDs_color.Count} links colored {actionParam}");
                        break;

                    case "reset":
                        Debug.Log($"  Resetting all annotations and selections");
                        _networkManager.ResetAll();
                        _lastQueriedLinkGUIDs.Clear();
                        Debug.Log($"  ✓ Reset complete");
                        break;

                    case "deselect":
                        Debug.Log($"  Deselecting all nodes");
                        _networkManager.ClearSelection();
                        _lastQueriedLinkGUIDs.Clear();
                        Debug.Log($"  ✓ Selection cleared");
                        break;

                    case "move":
                        Debug.Log($"  Moving selected nodes");
                        var nodes_move = _networkManager.SelectedNodeGUIDs;
                        _networkManager.BringMLNodes(nodes_move);
                        Debug.Log($"  ✓ {nodes_move.Count} nodes moved");
                        break;

                    case "layout":
                        Debug.Log($"  Changing layout to: {actionParam}");
                        var comms = _networkManager.WorkingSelectedCommunityGUIDs;
                        _networkManager.SetMLLayout(comms, actionParam);
                        Debug.Log($"  ✓ Layout changed");
                        break;

                    default:
                        Debug.LogWarning($"  ⚠ Unknown action: {actionName}");
                        break;
                }

                // Small delay between actions for visibility
                yield return new WaitForSeconds(0.1f);
            }

            Debug.Log($"[COMPLETE] All actions executed for: {originalCommand}");
        }

        // Natural language commands for each demo step (sent to server)
        private static readonly string[][] DemoCommands = new string[][] {
            // Demo P (demoId 0): Friendship highlight demo
            new string[] {
                "Highlight the top 3 nodes that have the most friendship links",
                "Color the nodes by grade",
                "Color their aggression links for highlighted nodes"
            },
            // Demo L (demoId 1): Smoker/drinker + aggression demo
            new string[] {
                "Color nodes by smoker or drinker",
                "Color their aggression links in red"
            },
            // Demo O (demoId 2): Gender + aggression targets + friendship links
            new string[] {
                "Color nodes by gender",
                "Select the nodes with many incoming aggression links",
                "Color their friendship links in blue"
            }
        };

        private void RunDemoStep(int demoId, int step)
        {
            // Ensure QueryMode is off so selectNode doesn't create subgraphs during demos
            var _networkManager = streamingSampleMic._networkManager;
            var _databaseStorage = streamingSampleMic._databaseStorage;

            if (_networkManager.OnQueryMode)
            {
                _networkManager.SetQueryMode(false);
            }

            // Determine demo label
            string demoLabel = demoId == 0 ? "P" : demoId == 1 ? "L" : "O";

            // Create working subgraph session if this is the first step
            if (step == 0)
            {
                Debug.Log($"[DEMO] Creating session for demo {demoLabel}");
                string selectAllQuery = "MATCH (n:Node) RETURN n";
                var allNodes = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, selectAllQuery);
                var nodeIDs = _networkManager.SortNodeGUIDs(allNodes)[VidiGraph.NetworkManager.MainNetworkID];

                _networkManager.CreateWorkingSubgraph(nodeIDs, $"Demo {demoLabel}", $"Demo {demoLabel}");
            }

            if (demoId < 0 || demoId >= DemoCommands.Length || step < 0 || step >= DemoCommands[demoId].Length)
            {
                Debug.LogError($"[DEMO] Invalid demoId={demoId} step={step}");
                return;
            }

            string command = DemoCommands[demoId][step];
            Debug.Log($"[DEMO {demoLabel} - Step {step + 1}] {command}");

            if (serverEnabled)
            {
                // Send natural language to server → server returns actions/queries → Unity executes
                StartCoroutine(SendToServerAndExecute(command));
            }
            else
            {
                // Fallback: direct execution with hardcoded actions
                RunDemoStepDirect(demoId, step);
            }
        }

        private void RunDemoStepDirect(int demoId, int step)
        {
            string[][] actions;
            string[] queries;
            string commandDescription;

            if (demoId == 0)
            {
                switch (step)
                {
                    case 0:
                        actions = new string[][] {
                            new string[] { "selectNode", "top3friendship" },
                            new string[] { "colorNode", "#FF0000" }
                        };
                        queries = new string[] {
                            "MATCH (n:Node)-[r:POINTS_TO]-(m) WHERE r.type = 'friendship' WITH n, COUNT(r) AS degree ORDER BY degree DESC LIMIT 3 RETURN n",
                            ""
                        };
                        commandDescription = "Highlight top 3 nodes with most friendship links";
                        StartCoroutine(ExecuteActionsDirectly(actions, queries, commandDescription));
                        break;
                    case 1:
                        actions = new string[][] {
                            new string[] { "colorByAttribute", "grade" }
                        };
                        queries = new string[] {
                            "MATCH (n:Node) RETURN DISTINCT n.grade AS value ORDER BY value"
                        };
                        commandDescription = "Color nodes by grade";
                        StartCoroutine(ExecuteActionsDirectly(actions, queries, commandDescription));
                        break;
                    case 2:
                        actions = new string[][] {
                            new string[] { "selectLink", "aggression" },
                            new string[] { "colorLink", "#FF0000" }
                        };
                        queries = new string[] {
                            "MATCH (n:Node)-[r:POINTS_TO]-(m) WHERE n.selected = true AND r.type = 'aggression' RETURN r",
                            ""
                        };
                        commandDescription = "Color aggression links for highlighted nodes";
                        StartCoroutine(ExecuteActionsDirectly(actions, queries, commandDescription));
                        break;
                }
            }
            else if (demoId == 1)
            {
                switch (step)
                {
                    case 0:
                        actions = new string[][] {
                            new string[] { "colorByAttribute", "smoker" }
                        };
                        queries = new string[] {
                            "MATCH (n:Node) RETURN DISTINCT n.smoker AS value ORDER BY value"
                        };
                        commandDescription = "Color nodes by smoker";
                        StartCoroutine(ExecuteActionsDirectly(actions, queries, commandDescription));
                        break;
                    case 1:
                        actions = new string[][] {
                            new string[] { "selectLink", "aggression" },
                            new string[] { "colorLink", "#FF0000" }
                        };
                        queries = new string[] {
                            "MATCH ()-[r:POINTS_TO]-() WHERE r.type = 'aggression' RETURN r",
                            ""
                        };
                        commandDescription = "Color aggression links in red";
                        StartCoroutine(ExecuteActionsDirectly(actions, queries, commandDescription));
                        break;
                }
            }
            else if (demoId == 2)
            {
                switch (step)
                {
                    case 0:
                        actions = new string[][] {
                            new string[] { "colorByAttribute", "sex" }
                        };
                        queries = new string[] {
                            "MATCH (n:Node) RETURN DISTINCT n.sex AS value ORDER BY value"
                        };
                        commandDescription = "Color nodes by gender";
                        StartCoroutine(ExecuteActionsDirectly(actions, queries, commandDescription));
                        break;
                    case 1:
                        actions = new string[][] {
                            new string[] { "selectNode", "topAggression" },
                            new string[] { "colorNode", "#FF0000" }
                        };
                        queries = new string[] {
                            "MATCH (n:Node)<-[r:POINTS_TO]-(m) WHERE r.type = 'aggression' WITH n, COUNT(r) AS inDegree ORDER BY inDegree DESC LIMIT 3 RETURN n",
                            ""
                        };
                        commandDescription = "Select nodes with most incoming aggression links";
                        StartCoroutine(ExecuteActionsDirectly(actions, queries, commandDescription));
                        break;
                    case 2:
                        actions = new string[][] {
                            new string[] { "selectLink", "friendship" },
                            new string[] { "colorLink", "#7F7FFF" }
                        };
                        queries = new string[] {
                            "MATCH (n:Node)-[r:POINTS_TO]-(m) WHERE n.selected = true AND r.type = 'friendship' RETURN r",
                            ""
                        };
                        commandDescription = "Color friendship links for selected nodes in blue";
                        StartCoroutine(ExecuteActionsDirectly(actions, queries, commandDescription));
                        break;
                }
            }
        }

        private void PrintInstructions()
        {
            Debug.Log("=".PadRight(60, '='));
            Debug.Log("KEYBOARD COMMAND TESTER - Instructions");
            Debug.Log("=".PadRight(60, '='));
            Debug.Log($"Press [{sizeByDegreeKey}] - Size nodes by grade (numeric)");
            Debug.Log($"Press [{sizeByGPAKey}] - Size nodes by GPA");
            Debug.Log($"Press [{colorByGradeKey}] - Color nodes by grade (categorical)");
            Debug.Log($"Press [{colorBySexKey}] - Color nodes by sex (categorical)");
            Debug.Log($"Press [{shapeByGradeKey}] - Shape nodes by grade (categorical)");
            Debug.Log($"Press [{selectFemaleKey}] - Select female students");
            Debug.Log($"Press [{deselectAllKey}] - Deselect all");
            Debug.Log($"Press [{colorSelectedRedKey}] - Color selected nodes red");
            Debug.Log($"Press [{moveSelectedKey}] - Move selected nodes");
            Debug.Log($"Press [{resetKey}] - Reset all annotations and selections");
            Debug.Log($"Press [H] - Show this help menu");
            Debug.Log("---");
            Debug.Log($"Press [P] - Demo: highlight top 3 → color by grade → color aggression links");
            Debug.Log($"Press [L] - Demo: color by smoker → color aggression links");
            Debug.Log($"Press [O] - Demo: color by gender → select aggression targets → color friendship links blue");
            Debug.Log($"Press [Enter] - Next demo step");
            Debug.Log("=".PadRight(60, '='));
            Debug.Log($"Server Mode: {(serverEnabled ? "ENABLED" : "DISABLED (Direct execution)")}");
            Debug.Log("=".PadRight(60, '='));
        }

        private void ShowSystemMessage(string message)
        {
            Debug.Log($"[SYSTEM] {message}");
            if (command_prefab != null && command_parent != null)
            {
                var msgObj = Instantiate(command_prefab, command_parent.transform);
                var msgText = msgObj.GetComponent<TMP_Text>();
                msgText.text = $"<color=#FF8F00><b>{message}</b></color>";
                ScrollToBottom();
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
