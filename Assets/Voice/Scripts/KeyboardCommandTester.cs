using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading;
using UnityEngine.Networking;
using Newtonsoft.Json;
using TMPro;
using VidiGraph;

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
        public LegendManager legendManager;
        public Camera demoDCamera;
        public GameObject command_prefab;
        public GameObject command_parent;
        public UnityEngine.UI.ScrollRect scroll;

        [Header("Demo Settings")]
        [Tooltip("Node size used in Demo E smoker subgraph (default 1.0)")]
        public float demoENodeSize = 1.5f;

        [Header("Server Settings")]
        public string serverUrl = "http://localhost:5000/classify";
        public bool serverEnabled = true;

        [Header("Keyboard Shortcuts")]
        public KeyCode sizeByDegreeKey = KeyCode.Alpha1;
        public KeyCode sizeByGPAKey = KeyCode.Alpha2;
        public KeyCode colorByGradeKey = KeyCode.Alpha3;
        public KeyCode colorBySexKey = KeyCode.Alpha4;
        public KeyCode colorByGPAKey = KeyCode.G;
        public KeyCode shapeByGradeKey = KeyCode.Alpha5;
        public KeyCode selectFemaleKey = KeyCode.Alpha6;
        public KeyCode deselectAllKey = KeyCode.Alpha7;
        public KeyCode colorSelectedRedKey = KeyCode.Alpha8;
        public KeyCode moveSelectedKey = KeyCode.Alpha9;
        public KeyCode resetKey = KeyCode.R;

        [Header("Text Input")]
        [TextArea] public string testCommand;
        public KeyCode submitCommandKey = KeyCode.T;

        public const string JevAnalysisTestCommand =
            "Select the five students with the most outgoing aggression links, color them red, and compare their average GPA with everyone else.";
        public bool IsJevAnalysisTestRunning { get; private set; }

        private StreamingSampleMic FindJevTestHost()
        {
            if (streamingSampleMic != null && streamingSampleMic.isActiveAndEnabled && streamingSampleMic._networkManager != null)
                return streamingSampleMic;
            return FindObjectsOfType<StreamingSampleMic>().FirstOrDefault(mic => mic.isActiveAndEnabled && mic._networkManager != null);
        }

        public bool CanRunJevAnalysisTest => Application.isPlaying && !IsJevAnalysisTestRunning && FindJevTestHost() != null;

        public void RunJevAnalysisTest()
        {
            if (!Application.isPlaying || IsJevAnalysisTestRunning) return;
            streamingSampleMic = FindJevTestHost();
            if (streamingSampleMic == null || streamingSampleMic._networkManager == null)
            {
                Debug.LogError("Assign a StreamingSampleMic with a NetworkManager before running the Jev test.", this);
                return;
            }
            // The Tests object may intentionally be inactive. Run on the active
            // command host rather than enabling unrelated test components.
            streamingSampleMic.StartCoroutine(RunJevAnalysisTestRoutine());
        }

        private IEnumerator RunJevAnalysisTestRoutine()
        {
            IsJevAnalysisTestRunning = true;
            try
            {
                // A fresh request avoids answering a previous pending clarification.
                yield return streamingSampleMic.SubmitCommand("new request: " + JevAnalysisTestCommand);
            }
            finally { IsJevAnalysisTestRunning = false; }
        }

        public void CancelJevAnalysisTest()
        {
            streamingSampleMic?.TryCancelCommand("cancel");
        }

        [Header("Display")]
        public bool showInstructions = true;
        public bool showBackgroundNetwork = false;

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
            if (legendManager == null)
            {
                legendManager = FindObjectOfType<LegendManager>();
                if (legendManager == null)
                    Debug.LogWarning("[KeyboardCommandTester] No LegendManager found in scene — legend panel will not update.");
                else
                    Debug.Log("[KeyboardCommandTester] LegendManager auto-discovered.");
            }

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
            // Cancellation must bypass the normal command queue while a plan is running.
            if (_isProcessingCommand && _commandQueue.TryPeek(out string queuedCommand)
                && StreamingSampleMic.IsCancellation(queuedCommand))
            {
                _commandQueue.TryDequeue(out queuedCommand);
                streamingSampleMic?.TryCancelCommand(queuedCommand);
            }
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

            // Color by GPA (linear blue gradient — reads from in-memory data, no server needed)
            if (Input.GetKeyDown(colorByGPAKey))
            {
                Debug.Log("[KEYBOARD] Triggered: Color by GPA");
                ExecuteDirectAction("colorByGPA", "gpa");
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

            if (Input.GetKeyDown(KeyCode.Q) && !_demoRunning)
            {
                Debug.Log("[DEMO Q] Starting continuous demo sequence...");
                _demoRunning = true;
                _activeDemoId = 3;
                StartCoroutine(RunDemoQContinuous());
            }

            if (Input.GetKeyDown(KeyCode.W) && !_demoRunning)
            {
                Debug.Log("[DEMO W] Starting continuous demo sequence...");
                _demoRunning = true;
                _activeDemoId = 4;
                StartCoroutine(RunDemoWContinuous());
            }

            if (Input.GetKeyDown(KeyCode.K) && !_demoRunning)
            {
                Debug.Log("[DEMO K] Starting demo sequence (smoker + cyan aggression)...");
                _demoRunning = true;
                _activeDemoId = 3;
                _demoStep = 0;
                _demoTotalSteps = 2;
                RunDemoStep(_activeDemoId, _demoStep);
            }

            if (Input.GetKeyDown(KeyCode.Z) && !_demoRunning)
            {
                Debug.Log("[DEMO Z] Starting demo sequence (smoker purple + aggression pink + friendship green)...");
                _demoRunning = true;
                _activeDemoId = 4;
                _demoStep = 0;
                _demoTotalSteps = 2;
                RunDemoStep(_activeDemoId, _demoStep);
            }

            if (Input.GetKeyDown(KeyCode.E) && !_demoRunning)
            {
                Debug.Log("[DEMO E] Starting continuous demo sequence...");
                _demoRunning = true;
                _activeDemoId = 5;
                StartCoroutine(RunDemoEContinuous());
            }

            if (Input.GetKeyDown(KeyCode.S) && !_demoRunning)
            {
                Debug.Log("[DEMO S] Starting continuous demo sequence...");
                _demoRunning = true;
                _activeDemoId = 6;
                StartCoroutine(RunDemoSContinuous());
            }

            if (Input.GetKeyDown(KeyCode.D) && !_demoRunning)
            {
                Debug.Log("[DEMO D] Starting step-by-step smoker demo...");
                _demoRunning = true;
                _activeDemoId = 7;
                _demoStep = 0;
                _demoTotalSteps = 4;
                RunDemoStep(_activeDemoId, _demoStep);
            }

            // Player movement — always available (arrow keys), not just during a demo
            UpdatePlayerMovement();

            if (_demoRunning && _activeDemoId == 7 && Input.GetKeyDown(KeyCode.Escape))
            {
                _demoRunning = false;
                Debug.Log("[DEMO D] Exited.");
            }

            if (Input.GetKeyDown(KeyCode.Return) && _demoRunning)
            {
                _demoStep++;
                if (_demoStep < _demoTotalSteps)
                {
                    RunDemoStep(_activeDemoId, _demoStep);
                }
                else if (_activeDemoId == 7)
                {
                    // Demo D keeps running so movement stays active; Escape to exit
                    Debug.Log("[DEMO D] All steps complete. Press Escape to exit.");
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
                case "colorByGPA":
                    actions = new string[][] { new string[] { "colorByGPA", "gpa" } };
                    queries = new string[] { "" };
                    commandDescription = "color by GPA";
                    break;

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
            if (streamingSampleMic == null || streamingSampleMic._networkManager == null) yield break;
            yield return streamingSampleMic.SubmitCommand(userInput);
        }

        private IEnumerator ExecuteActionsDirectly(string[][] actions, string[] queries, string originalCommand, string legendNodeLabel = null)
        {
            if (streamingSampleMic == null || streamingSampleMic._networkManager == null) yield break;
            // Desktop demos share the same queue as spoken commands.
            yield return streamingSampleMic._networkManager.VisualScheduler.Schedule(
                ExecuteActionSteps(actions, queries, originalCommand, legendNodeLabel));
        }

        private IEnumerator ExecuteActionSteps(string[][] actions, string[] queries, string originalCommand, string legendNodeLabel = null)
        {
            if (streamingSampleMic == null) yield break;
            if (!string.IsNullOrEmpty(originalCommand)) ShowSystemMessage(originalCommand);
            yield return streamingSampleMic.ExecuteActionSteps(new ClassificationResponse
            {
                actions = actions,
                queries = queries
            }, originalCommand, legendNodeLabel);
        }

        // Natural language commands for each demo step (sent to server)
        private static readonly string[][] DemoCommands = new string[][] {
            // Demo P (demoId 0): Friendship highlight demo
            new string[] {
                "Highlight the top 3 nodes that have the most friendship links",
                "Color the nodes by grade",
                "Color their aggression links for highlighted nodes"
            },
            // Demo L (demoId 1): Smoker highlight + aggression demo
            new string[] {
                "Color all nodes where smoker is true in purple",
                "Color the aggression links of the smoker nodes pink and friendship link in green"
            },
            // Demo O (demoId 2): Gender + aggression targets + friendship links
            new string[] {
                "Color nodes by gender",
                "Select the top 3 nodes with the most incoming aggression links",
                "Color their friendship links in blue"
            },
            // Demo K (demoId 3): Smoker nodes steel blue, gray all links, smoker aggression links light orange
            new string[] {
                "Color all nodes where smoker is true in steel blue",
                "Color the aggression links of smoker nodes in light orange"
            },
            // Demo Z (demoId 4): Smoker nodes purple, then color aggression links pink + friendship links green
            new string[] {
                "Color all smoker nodes in purple",
                "Color the aggression links of smoker nodes in pink and friendship links in green"
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

            // Demo D uses its own setup/step logic
            if (demoId == 7)
            {
                if (step == 0)
                    StartCoroutine(RunDemoDSetup());
                else
                    RunDemoDStepDirect(step);
                return;
            }

            // Determine demo label
            string demoLabel = demoId == 0 ? "P" : demoId == 1 ? "L" : demoId == 2 ? "O" : demoId == 3 ? "K" : demoId == 4 ? "Z" : "Q";

            // Create working subgraph session if this is the first step
            if (step == 0)
            {
                if (legendManager == null)
                    Debug.LogWarning("[DEMO] legendManager is not assigned on KeyboardCommandTester — legend will not update. Assign it in the Inspector.");
                Debug.Log($"[DEMO] Creating session for demo {demoLabel}");
                legendManager?.ResetAll();
                string selectAllQuery = "MATCH (n:Node) RETURN n";
                var allNodes = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, selectAllQuery);
                var sortedDemo = _networkManager.SortNodeGUIDs(allNodes);
                if (!sortedDemo.ContainsKey(VidiGraph.NetworkManager.MainNetworkID)) return;

                _networkManager.CreateWorkingSubgraph(sortedDemo[VidiGraph.NetworkManager.MainNetworkID], $"Demo {demoLabel}", $"Demo {demoLabel}");
            }

            if (demoId < 0 || demoId >= DemoCommands.Length || step < 0 || step >= DemoCommands[demoId].Length)
            {
                Debug.LogError($"[DEMO] Invalid demoId={demoId} step={step}");
                return;
            }

            string command = DemoCommands[demoId][step];
            Debug.Log($"[DEMO {demoLabel} - Step {step + 1}] {command}");

            // Demo K step 1 always runs directly — it requires multi-step logic (gray all links,
            // color aggression orange, then select only aggression in the working subgraph)
            // that the server cannot produce from a single natural-language command.
            if (demoId == 3 && step == 1)
            {
                StartCoroutine(RunDemoKStep1());
            }
            else if (serverEnabled)
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
                        StartCoroutine(ExecuteActionsDirectly(actions, queries, commandDescription, legendNodeLabel: "Top 3 Friendship"));
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
                            new string[] { "selectNode", "smoker" },
                            new string[] { "colorNode", "#A000FF" },
                            new string[] { "deselect", "" }
                        };
                        queries = new string[] {
                            "MATCH (n:Node) WHERE n.smoker = true RETURN n",
                            "",
                            ""
                        };
                        commandDescription = "Color all nodes where smoker is true in purple";
                        StartCoroutine(ExecuteActionsDirectly(actions, queries, commandDescription, legendNodeLabel: "Smokers"));
                        break;
                    case 1:
                        actions = new string[][] {
                            new string[] { "selectLink", "all" },
                            new string[] { "colorLink", "#808080" },
                            new string[] { "selectLink", "aggression" },
                            new string[] { "colorLink", "#FFB6C1" },
                            new string[] { "deselect", "" }
                        };
                        queries = new string[] {
                            "MATCH (n:Node)-[r:POINTS_TO]-(m:Node) RETURN r",
                            "",
                            "MATCH (n:Node)-[r:POINTS_TO]-(m:Node) WHERE n.smoker = true AND r.type = 'aggression' RETURN r",
                            "",
                            ""
                        };
                        commandDescription = "Color all links gray, color smoker aggression links pink";
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
            else if (demoId == 3)
            {
                // Demo K: smoker nodes blue, gray all links, smoker aggression links light orange
                switch (step)
                {
                    case 0:
                        actions = new string[][] {
                            new string[] { "selectNode", "smoker" },
                            new string[] { "colorNode", "#4682B4" },
                            new string[] { "deselect", "" }
                        };
                        queries = new string[] {
                            "MATCH (n:Node) WHERE n.smoker = true RETURN n",
                            "",
                            ""
                        };
                        commandDescription = "Color all smoker nodes in steel blue";
                        StartCoroutine(ExecuteActionsDirectly(actions, queries, commandDescription, legendNodeLabel: "Smokers"));
                        break;
                    case 1:
                        StartCoroutine(RunDemoKStep1());
                        break;
                }
            }
            else if (demoId == 4)
            {
                // Demo Z: smoker nodes purple, then aggression links pink + friendship links green
                switch (step)
                {
                    case 0:
                        actions = new string[][] {
                            new string[] { "selectNode", "smoker" },
                            new string[] { "colorNode", "#800080" },
                            new string[] { "deselect", "" }
                        };
                        queries = new string[] {
                            "MATCH (n:Node) WHERE n.smoker = true RETURN n",
                            "",
                            ""
                        };
                        commandDescription = "Color all smoker nodes in purple";
                        StartCoroutine(ExecuteActionsDirectly(actions, queries, commandDescription, legendNodeLabel: "Smoker"));
                        break;
                    case 1:
                        actions = new string[][] {
                            new string[] { "selectLink", "aggression" },
                            new string[] { "colorLink", "#FF69B4" },
                            new string[] { "selectLink", "friendship" },
                            new string[] { "colorLink", "#00FF00" }
                        };
                        queries = new string[] {
                            "MATCH (n:Node)-[r:POINTS_TO]-(m:Node) WHERE (n.smoker = true OR m.smoker = true) AND r.type = 'aggression' RETURN r",
                            "",
                            "MATCH (n:Node)-[r:POINTS_TO]-(m:Node) WHERE (n.smoker = true OR m.smoker = true) AND r.type = 'friendship' RETURN r",
                            ""
                        };
                        commandDescription = "Color aggression links of smoker nodes pink, friendship links green";
                        StartCoroutine(ExecuteActionsDirectly(actions, queries, commandDescription));
                        break;
                }
            }
        }

        private IEnumerator RunDemoKStep1()
        {
            var _networkManager = streamingSampleMic._networkManager;
            var _databaseStorage = streamingSampleMic._databaseStorage;

            const string allLinksQuery = "MATCH (n:Node)-[r:POINTS_TO]-(m:Node) RETURN r";
            const string aggrQuery     = "MATCH (n:Node)-[r:POINTS_TO]-(m:Node) WHERE n.smoker = true AND r.type = 'aggression' RETURN r";

            // Color all links dark gray, then color smoker aggression links light orange
            // (selectLink/colorLink only sets _lastQueriedLinkGUIDs — does not touch Unity selection state)
            yield return StartCoroutine(ExecuteActionsDirectly(
                new string[][] {
                    new string[] { "selectLink", "all" },
                    new string[] { "colorLink",  "#404040" },
                    new string[] { "selectLink", "aggression" },
                    new string[] { "colorLink",  "#FFA040" }
                },
                new string[] { allLinksQuery, "", aggrQuery, "" },
                "Color all links dark gray, smoker aggression links light orange"
            ));

            // Clear all link selections, then select ONLY smoker aggression links
            // using working-subgraph GUIDs so the selection color blends with the light orange
            _networkManager.SetWorkingSelectedLinks(_networkManager.WorkingSubgraphAllLinkGUIDs, false);
            var aggrLinksGlobal  = _databaseStorage.GetLinksFromStore(_networkManager.NetworkGlobal, aggrQuery);
            var aggrLinksWorking = _networkManager.TranslateToWorkingSubgraphLinkGUIDs(aggrLinksGlobal);
            _networkManager.SetWorkingSelectedLinks(aggrLinksWorking, true);
        }

        private IEnumerator RunDemoQContinuous()
        {
            var _networkManager = streamingSampleMic._networkManager;
            var _databaseStorage = streamingSampleMic._databaseStorage;

            if (_networkManager.OnQueryMode)
                _networkManager.SetQueryMode(false);

            // Create session with all nodes
            Debug.Log("[DEMO Q] Creating session with all nodes...");
            var allNodes = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, "MATCH (n:Node) RETURN n");
            var sorted = _networkManager.SortNodeGUIDs(allNodes);
            if (!sorted.ContainsKey(VidiGraph.NetworkManager.MainNetworkID))
            {
                Debug.LogError("[DEMO Q] No main network nodes found.");
                _demoRunning = false;
                yield break;
            }
            _networkManager.CreateWorkingSubgraph(sorted[VidiGraph.NetworkManager.MainNetworkID], "Demo Q", "Demo Q");
            legendManager?.ResetAll();

            // Step 1: Highlight top-3 nodes with most friendship links
            Debug.Log("[DEMO Q - Step 1/3] Highlight top-3 nodes with most friendship links");
            yield return StartCoroutine(ExecuteActionsDirectly(
                new string[][] {
                    new string[] { "selectNode", "top3friendship" },
                    new string[] { "colorNode", "#FFD700" },
                    new string[] { "deselect", "" }
                },
                new string[] {
                    "MATCH (n:Node)-[r:POINTS_TO]-(m) WHERE r.type = 'friendship' WITH n, COUNT(r) AS degree ORDER BY degree DESC LIMIT 3 RETURN n",
                    "",
                    ""
                },
                "Highlight top 3 nodes with most friendship links",
                legendNodeLabel: "Top 3 Friendship"
            ));

            yield return new WaitForSeconds(1.5f);

            // Step 2: Color nodes by GPA
            Debug.Log("[DEMO Q - Step 2/3] Color nodes by GPA");
            yield return StartCoroutine(ExecuteActionsDirectly(
                new string[][] { new string[] { "colorByGPA", "gpa" } },
                new string[] { "" },
                "Color nodes by GPA"
            ));

            yield return new WaitForSeconds(1.5f);

            // Step 3: Color ALL aggression links in red
            Debug.Log("[DEMO Q - Step 3/3] Color aggression links in red");
            yield return StartCoroutine(ExecuteActionsDirectly(
                new string[][] {
                    new string[] { "selectLink", "aggression" },
                    new string[] { "colorLink", "#FF0000" }
                },
                new string[] {
                    "MATCH (n:Node)-[r:POINTS_TO]-(m) WHERE r.type = 'aggression' RETURN r",
                    ""
                },
                "Color aggression links in red"
            ));

            _demoRunning = false;
            Debug.Log("[DEMO Q] Continuous demo complete!");
        }

        private IEnumerator RunDemoWContinuous()
        {
            var _networkManager = streamingSampleMic._networkManager;
            var _databaseStorage = streamingSampleMic._databaseStorage;

            if (_networkManager.OnQueryMode)
                _networkManager.SetQueryMode(false);

            // Create session with all nodes
            Debug.Log("[DEMO W] Creating session with all nodes...");
            var allNodes = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, "MATCH (n:Node) RETURN n");
            var sorted = _networkManager.SortNodeGUIDs(allNodes);
            if (!sorted.ContainsKey(VidiGraph.NetworkManager.MainNetworkID))
            {
                Debug.LogError("[DEMO W] No main network nodes found.");
                _demoRunning = false;
                yield break;
            }
            _networkManager.CreateWorkingSubgraph(sorted[VidiGraph.NetworkManager.MainNetworkID], "Demo W", "Demo W");
            legendManager?.ResetAll();

            // Step 1: Highlight top-5 nodes with most friendship links
            Debug.Log("[DEMO W - Step 1/3] Highlight top-5 nodes with most friendship links");
            yield return StartCoroutine(ExecuteActionsDirectly(
                new string[][] {
                    new string[] { "selectNode", "top5friendship" },
                    new string[] { "colorNode", "#FF0000" }
                },
                new string[] {
                    "MATCH (n:Node)-[r:POINTS_TO]-(m) WHERE r.type = 'friendship' WITH n, COUNT(r) AS degree ORDER BY degree DESC LIMIT 5 RETURN n",
                    ""
                },
                "Highlight top 5 nodes with most friendship links",
                legendNodeLabel: "Top 5 Friendship"
            ));

            yield return new WaitForSeconds(1.5f);

            // Step 2: Shape nodes by gender
            Debug.Log("[DEMO W - Step 2/3] Shape nodes by gender");
            yield return StartCoroutine(ExecuteActionsDirectly(
                new string[][] { new string[] { "shapeByAttribute", "sex" } },
                new string[] { "MATCH (n:Node) RETURN DISTINCT n.sex AS value ORDER BY value" },
                "Shape nodes by gender"
            ));

            yield return new WaitForSeconds(1.5f);

            // Step 3: Color aggression links for highlighted nodes in red
            Debug.Log("[DEMO W - Step 3/3] Color aggression links for highlighted nodes in red");
            yield return StartCoroutine(ExecuteActionsDirectly(
                new string[][] {
                    new string[] { "selectLink", "aggression" },
                    new string[] { "colorLink", "#FF0000" }
                },
                new string[] {
                    "MATCH (n:Node)-[r:POINTS_TO]-(m) WHERE n.selected = true AND r.type = 'aggression' RETURN r",
                    ""
                },
                "Color aggression links for highlighted nodes in red"
            ));

            _demoRunning = false;
            Debug.Log("[DEMO W] Continuous demo complete!");
        }

        private IEnumerator RunDemoEContinuous()
        {
            var _networkManager = streamingSampleMic._networkManager;
            var _databaseStorage = streamingSampleMic._databaseStorage;

            if (_networkManager.OnQueryMode)
                _networkManager.SetQueryMode(false);

            // Create first session with all nodes
            Debug.Log("[DEMO E] Creating first session with all nodes...");
            var allNodes = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, "MATCH (n:Node) RETURN n");
            var sorted = _networkManager.SortNodeGUIDs(allNodes);
            if (!sorted.ContainsKey(VidiGraph.NetworkManager.MainNetworkID))
            {
                Debug.LogError("[DEMO E] No main network nodes found.");
                _demoRunning = false;
                yield break;
            }
            _networkManager.CreateWorkingSubgraph(sorted[VidiGraph.NetworkManager.MainNetworkID], "Demo E - Top 3 Friendship", "Demo E");
            if (showBackgroundNetwork) _networkManager.ShowMainNetwork();
            legendManager?.ResetAll();

            // Step 1: Highlight top-3 nodes with most friendship links
            Debug.Log("[DEMO E - Step 1/2] Highlight top-3 nodes with most friendship links");
            yield return StartCoroutine(ExecuteActionsDirectly(
                new string[][] {
                    new string[] { "selectNode", "top3friendship" },
                    new string[] { "colorNode", "#FFD700" }
                },
                new string[] {
                    "MATCH (n:Node)-[r:POINTS_TO]-(m) WHERE r.type = 'friendship' WITH n, COUNT(r) AS degree ORDER BY degree DESC LIMIT 3 RETURN n",
                    ""
                },
                "Highlight the top 3 nodes with the most friendship links",
                legendNodeLabel: "Top 3 Friendship"
            ));

            yield return new WaitForSeconds(1.5f);

            // Step 2: Save this session and create a new session with smoker nodes
            Debug.Log("[DEMO E - Step 2/2] Saving current session, creating new smokers session");

            // Show the command in history panel
            if (command_prefab != null && command_parent != null)
            {
                var cmdObj = Instantiate(command_prefab, command_parent.transform);
                cmdObj.GetComponent<TMP_Text>().text = "<b>User:</b> Save this session and create a new session of select all smokers nodes";
                ScrollToBottom();
            }

            ShowSystemMessage("Session 'Demo E' saved. Creating new smokers session...");

            // Query smoker nodes and create a new dedicated session
            var smokerNodes = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, "MATCH (n:Node) WHERE n.smoker = true RETURN n");
            var sortedSmokers = _networkManager.SortNodeGUIDs(smokerNodes);
            if (!sortedSmokers.ContainsKey(VidiGraph.NetworkManager.MainNetworkID) || !sortedSmokers[VidiGraph.NetworkManager.MainNetworkID].Any())
            {
                Debug.LogError("[DEMO E] No smoker nodes found.");
                ShowSystemMessage("No smoker nodes found.");
                _demoRunning = false;
                yield break;
            }

            _networkManager.CreateWorkingSubgraph(
                sortedSmokers[VidiGraph.NetworkManager.MainNetworkID],
                "Smokers Session",
                "Smokers"
            );
            if (showBackgroundNetwork) _networkManager.ShowMainNetwork();

            // Make nodes bigger in the smokers subgraph (adjustable via Demo Settings in Inspector)
            _networkManager.SetMLNodesSize(_networkManager.WorkingSubgraphAllNodeGUIDs, demoENodeSize);

            // Color smoker nodes dodger blue, color ALL links white
            yield return StartCoroutine(ExecuteActionsDirectly(
                new string[][] {
                    new string[] { "colorNode",  "#1e90ff" },
                    new string[] { "selectLink", "all" },
                    new string[] { "colorLink",  "#FFFFFF" }
                },
                new string[] {
                    "",
                    "MATCH (n:Node)-[r:POINTS_TO]-(m:Node) RETURN r",
                    ""
                },
                "Color smokers nodes dodger blue, all links bright white",
                legendNodeLabel: "Smokers"
            ));

            // Select ALL links in the working subgraph so the selection color blends with white
            _networkManager.SetWorkingSelectedLinks(_networkManager.WorkingSubgraphAllLinkGUIDs, true);

            _demoRunning = false;
            Debug.Log("[DEMO E] Continuous demo complete!");
        }

        private IEnumerator RunDemoSContinuous()
        {
            var _networkManager = streamingSampleMic._networkManager;
            var _databaseStorage = streamingSampleMic._databaseStorage;

            if (_networkManager.OnQueryMode)
                _networkManager.SetQueryMode(false);

            // Create session with all nodes so all links are accessible
            Debug.Log("[DEMO S] Creating session with all nodes...");
            var allNodes = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, "MATCH (n:Node) RETURN n");
            var sorted = _networkManager.SortNodeGUIDs(allNodes);
            if (!sorted.ContainsKey(VidiGraph.NetworkManager.MainNetworkID))
            {
                Debug.LogError("[DEMO S] No main network nodes found.");
                _demoRunning = false;
                yield break;
            }
            _networkManager.CreateWorkingSubgraph(sorted[VidiGraph.NetworkManager.MainNetworkID], "Demo S", "Demo S");
            if (showBackgroundNetwork) _networkManager.ShowMainNetwork();
            legendManager?.ResetAll();

            // Step 1: Color smokers steel blue
            Debug.Log("[DEMO S - Step 1/2] Color smokers steel blue");
            yield return StartCoroutine(ExecuteActionsDirectly(
                new string[][] {
                    new string[] { "selectNode", "smoker" },
                    new string[] { "colorNode", "#4682B4" },
                    new string[] { "deselect", "" }
                },
                new string[] {
                    "MATCH (n:Node) WHERE n.smoker = true RETURN n",
                    "",
                    ""
                },
                "Color smokers in steel blue",
                legendNodeLabel: "Smokers"
            ));

            yield return new WaitForSeconds(1.5f);

            // Step 2: Dim all links to dark gray, then highlight smokers' aggression links in vivid orange
            Debug.Log("[DEMO S - Step 2/2] Dim all links, highlight smokers' aggression links in vivid orange");
            yield return StartCoroutine(ExecuteActionsDirectly(
                new string[][] {
                    new string[] { "selectLink", "all" },
                    new string[] { "colorLink", "#404040" },
                    new string[] { "selectLink", "aggression" },
                    new string[] { "colorLink", "#FF5500" }
                },
                new string[] {
                    "MATCH (n:Node)-[r:POINTS_TO]-(m:Node) RETURN r",
                    "",
                    "MATCH (n:Node)-[r:POINTS_TO]-(m:Node) WHERE n.smoker = true AND r.type = 'aggression' RETURN r",
                    ""
                },
                "Color smokers' aggression links in vivid orange"
            ));

            // Select the smoker aggression links so they are highlighted
            var aggrLinks = _databaseStorage.GetLinksFromStore(_networkManager.NetworkGlobal,
                "MATCH (n:Node)-[r:POINTS_TO]-(m:Node) WHERE n.smoker = true AND r.type = 'aggression' RETURN r");
            _networkManager.SetSelectedLinks(aggrLinks, true);
            yield return null;

            _demoRunning = false;
            Debug.Log("[DEMO S] Continuous demo complete!");
        }

        private IEnumerator RunDemoDSetup()
        {
            var _networkManager = streamingSampleMic._networkManager;
            var _databaseStorage = streamingSampleMic._databaseStorage;

            if (_networkManager.OnQueryMode)
                _networkManager.SetQueryMode(false);

            Debug.Log("[DEMO D] Querying smoker nodes...");
            var smokerNodes = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal,
                "MATCH (n:Node) WHERE n.smoker = true RETURN n");
            var sorted = _networkManager.SortNodeGUIDs(smokerNodes);
            if (!sorted.ContainsKey(VidiGraph.NetworkManager.MainNetworkID) || !sorted[VidiGraph.NetworkManager.MainNetworkID].Any())
            {
                Debug.LogError("[DEMO D] No smoker nodes found.");
                _demoRunning = false;
                yield break;
            }

            _networkManager.CreateWorkingSubgraph(sorted[VidiGraph.NetworkManager.MainNetworkID], "Demo D - Smokers", "Smokers");
            _networkManager.ShowMainNetwork();
            legendManager?.ResetAll();

            // Select all nodes so they are grabbable
            _networkManager.SetWorkingSelectedNodes(_networkManager.WorkingSubgraphAllNodeGUIDs, true);

            ShowDemoMessage("Select only smokers nodes");
            Debug.Log("[DEMO D] Setup complete. Press Enter to advance steps. Movement keys active.");
            yield return null;
        }

        private void RunDemoDStepDirect(int step)
        {
            switch (step)
            {
                case 1:
                    ShowDemoMessage("Color smokers in blue");
                    StartCoroutine(ExecuteActionsDirectly(
                        new string[][] {
                            new string[] { "colorNode", "#0892d0" }
                        },
                        new string[] { "" },
                        null,
                        legendNodeLabel: "Smokers"
                    ));
                    break;

                case 2:
                    ShowDemoMessage("Color the aggression links in pink and friendship in blue");
                    StartCoroutine(ExecuteActionsDirectly(
                        new string[][] {
                            new string[] { "selectLink", "aggression" },
                            new string[] { "colorLink",  "#FF69B4" },
                            new string[] { "selectLink", "friendship" },
                            new string[] { "colorLink",  "#0000FF" }
                        },
                        new string[] {
                            "MATCH (n:Node)-[r:POINTS_TO]-(m:Node) WHERE n.smoker = true AND r.type = 'aggression' RETURN r",
                            "",
                            "MATCH (n:Node)-[r:POINTS_TO]-(m:Node) WHERE n.smoker = true AND r.type = 'friendship' RETURN r",
                            ""
                        },
                        null
                    ));
                    break;

                case 3:
                    ShowDemoMessage("Shape nodes by gender");
                    StartCoroutine(ExecuteActionsDirectly(
                        new string[][] {
                            new string[] { "shapeByAttribute", "sex" }
                        },
                        new string[] {
                            "MATCH (n:Node) RETURN DISTINCT n.sex AS value ORDER BY value"
                        },
                        null
                    ));
                    break;
            }
        }

        // Free-look player movement. Always active (not gated to a demo), so the
        // camera can be repositioned at any time while recording.
        //   Up / Down     — walk forward / back
        //   Left / Right  — turn left / right, so you can face a direction and then
        //                   walk into it (movement follows where you're looking)
        //   , / .         — strafe left / right without turning
        // NOTE: turning is not on Q/E because those are demo trigger keys (demos 3
        // and 5) — sharing them would fire a demo every time you turned.
        private void UpdatePlayerMovement()
        {
            var cam = demoDCamera;
            if (cam == null) return;

            const float moveSpeed = 1.5f;
            const float rotSpeed  = 60f;

            float moveF = 0f;
            float moveH = 0f;
            float rot   = 0f;

            if (Input.GetKey(KeyCode.UpArrow))    moveF += moveSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.DownArrow))  moveF -= moveSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.LeftArrow))  rot   -= rotSpeed  * Time.deltaTime;
            if (Input.GetKey(KeyCode.RightArrow)) rot   += rotSpeed  * Time.deltaTime;
            if (Input.GetKey(KeyCode.Comma))      moveH -= moveSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.Period))     moveH += moveSpeed * Time.deltaTime;

            if (moveF == 0f && moveH == 0f && rot == 0f) return;

            // Move along camera's facing/right directions (projected flat on XZ plane)
            Transform t = cam.transform;
            Vector3 forward = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
            Vector3 right   = Vector3.ProjectOnPlane(t.right,   Vector3.up).normalized;
            t.position += forward * moveF + right * moveH;

            // Rotate around world Y axis (turn left / right)
            t.Rotate(Vector3.up, rot, Space.World);
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
            Debug.Log($"Press [{colorByGPAKey}] - Color nodes by GPA (linear blue gradient)");
            Debug.Log($"Press [{shapeByGradeKey}] - Shape nodes by grade (categorical)");
            Debug.Log($"Press [{selectFemaleKey}] - Select female students");
            Debug.Log($"Press [{deselectAllKey}] - Deselect all");
            Debug.Log($"Press [{colorSelectedRedKey}] - Color selected nodes red");
            Debug.Log($"Press [{moveSelectedKey}] - Move selected nodes");
            Debug.Log($"Press [{resetKey}] - Reset all annotations and selections");
            Debug.Log($"Press [H] - Show this help menu");
            Debug.Log("---");
            Debug.Log($"Press [P] - Demo: highlight top 3 → color by grade → color aggression links");
            Debug.Log($"Press [L] - Demo: highlight smoker nodes orange → color all aggression links red");
            Debug.Log($"Press [O] - Demo: color by gender → select aggression targets → color friendship links blue");
            Debug.Log($"Press [Q] - Demo (continuous): highlight top 3 friendship → color by GPA → color aggression links red");
            Debug.Log($"Press [W] - Demo (continuous): highlight top 5 friendship → shape by gender → color all aggression links red");
            Debug.Log($"Press [K] - Demo: smoker nodes steel blue → color smoker aggression links light orange");
            Debug.Log($"Press [Z] - Demo: color smoker nodes purple → color smoker aggression links pink + friendship links green");
            Debug.Log($"Press [E] - Demo (continuous): highlight top 3 friendship → save session → new session with all smokers selected");
            Debug.Log($"Press [S] - Demo (continuous): select smokers → color steel blue → color smokers' aggression links pink");
            Debug.Log($"Press [D] - Demo: smoker subgraph → [Enter] color #0892d0 → [Enter] aggression pink + friendship blue → [Enter] shape by gender | ↑↓←→ Q/E to move camera, Escape to exit");
            Debug.Log($"Press [Enter] - Next demo step (P/L/O/K)");
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
                msgText.text = $"<b>{message}</b>";
                ScrollToBottom();
            }
        }

        // Shows plain text in the command history panel without any prefix
        private void ShowDemoMessage(string message)
        {
            if (command_prefab != null && command_parent != null)
            {
                var msgObj = Instantiate(command_prefab, command_parent.transform);
                msgObj.GetComponent<TMP_Text>().text = message;
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

        // Converts a selectNode actionParam to a short human-readable legend label
        private static string ToShortLabel(string actionParam) =>
            LegendManager.ToShortLabel(actionParam);

    }
}
