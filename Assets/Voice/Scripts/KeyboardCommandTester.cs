using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;
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

        [Header("Display")]
        public bool showInstructions = true;

        private void Start()
        {
            if (showInstructions)
            {
                PrintInstructions();
            }
        }

        private void Update()
        {
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

            // Help menu
            if (Input.GetKeyDown(KeyCode.H))
            {
                PrintInstructions();
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

                    // Execute the actions
                    StartCoroutine(ExecuteActionsDirectly(classification.actions, classification.queries, userInput));
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
                            // Convert GUIDs to IDs to create a working subgraph
                            var nodeIDs = _networkManager.SortNodeGUIDs(nodes)[VidiGraph.NetworkManager.MainNetworkID];
                            _networkManager.CreateWorkingSubgraph(nodeIDs, originalCommand, originalCommand);
                            _networkManager.SetQueryMode(false);
                            Debug.Log($"  ✓ Working subgraph created");
                        }
                        else
                        {
                            Debug.Log($"  [Selection Mode] Setting working selected nodes");
                            _networkManager.SetWorkingSelectedNodes(nodes, true);
                            Debug.Log($"  ✓ Nodes selected");
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
                        Debug.Log($"  Coloring selected nodes: {actionParam}");
                        var nodes_color = _networkManager.WorkingSelectedNodeGUIDs;
                        _networkManager.SetMLNodesColor(nodes_color, actionParam);
                        Debug.Log($"  ✓ {nodes_color.Count} nodes colored");
                        break;

                    case "colorByAttribute":
                        Debug.Log($"  Categorical coloring by: {actionParam}");
                        string attributeName_color = actionParam;

                        // Get distinct values
                        var distinctValues = _databaseStorage.GetDistinctValuesFromStore(_networkManager.NetworkGlobal, queries[i]);
                        Debug.Log($"  Found {distinctValues.Count} distinct values");

                        // Define 6 allowed colors
                        string[] allowedColors = new string[] {
                            "#FF7F7F",  // red (S=0.5)
                            "#FFB852",  // orange (S=0.5)
                            "#FFFF7F",  // yellow (S=0.5)
                            "#7FFF7F",  // green (S=0.5)
                            "#7F7FFF",  // blue (S=0.5)
                            "#9F4D9F"   // purple (S=0.5)
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

                    case "deselect":
                        Debug.Log($"  Deselecting all nodes");
                        _networkManager.ClearSelection();
                        Debug.Log($"  ✓ Selection cleared");
                        break;

                    case "move":
                        Debug.Log($"  Moving selected nodes");
                        var nodes_move = _networkManager.WorkingSelectedNodeGUIDs;
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
            Debug.Log($"Press [H] - Show this help menu");
            Debug.Log("=".PadRight(60, '='));
            Debug.Log($"Server Mode: {(serverEnabled ? "ENABLED" : "DISABLED (Direct execution)")}");
            Debug.Log("=".PadRight(60, '='));
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
                "#FF7F7F" => "red",
                "#FFB852" => "orange",
                "#FFFF7F" => "yellow",
                "#7FFF7F" => "green",
                "#7F7FFF" => "blue",
                "#9F4D9F" => "purple",
                _ => "color"
            };
        }

    }
}
