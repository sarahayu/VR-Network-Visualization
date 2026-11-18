using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using Whisper.Utils;

using VidiGraph;
using System.Linq;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.XR.Hands;

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
        [Tooltip("Hold this key to speak; release to finish the voice command.")]
        public KeyCode desktopTalkKey = KeyCode.F8;
        [Header("Left hand push-to-talk")]
        public bool enableLeftFistVoice = true;
        [Range(20, 100)] public float fistBendDegrees = 50f;
        [Min(0.05f)] public float fistHoldSeconds = 0.15f;
        [Tooltip("Maximum palm distance below eye level when arming voice, in metres.")]
        [Min(0)] public float fistMaxBelowEyes = 0.55f;
        private HandNodeInteractor _handInteractor;
        private RightHandNodeMover _handMover;
        private TwoHandBoxSelector _handBox;
        private Camera _handCamera;

        private readonly List<XRHandSubsystem> _handSubsystems = new List<XRHandSubsystem>();
        private bool _fistTalking, _fistLatched, _fistNeedsOpen = true;
        private float _fistHeld;
        private bool _suppressFistUntilOpen;
        public bool IsVoiceListening => _voiceListening;
        public bool IsFistTalking => _fistTalking && _voiceListening;
        public bool IsVoiceFinishing => _voiceFinishing;
        public Vector3 VoiceHandPosition { get; private set; }
        public void SuppressFistUntilOpen() { _suppressFistUntilOpen = true; }
        [SerializeField]
        XRInputButtonReader CommandPress = new XRInputButtonReader("CommandPress");
        [SerializeField]
        Renderer Indicator;
        public Text buttonText;
        public Text text;
        public TMP_Text LegendText;
        public LegendManager legendManager;
        public StatisticalReportDisplay statisticalReportDisplay;
        public ScrollRect scroll;
        public GameObject command_prefab;
        public GameObject command_parent;
        public TMP_InputField textCommandInput;

        [Tooltip("Legacy 3-step snapshot demo on the Enter key (creates a session, saves a " +
                 "snapshot with the fly-to-wall animation). Leave OFF — Enter now advances " +
                 "the Status Struggle demo in DemoSequence.")]
        public bool enableEnterSnapshotDemo = false;

        // Reference to the whisper stream
        private WhisperStream _stream;
        private bool _voiceListening;
        private bool _voiceFinishing;
        private bool _keyboardTalking;
        private float whisperStartTime;
        private float _pipelineStartTime;
        private string _completedTranscript = "";

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


        [Header("Jev planning")]
        [Range(1, 12)] public int maxPlanSteps = 8;
        private readonly string conversationId = System.Guid.NewGuid().ToString("N");
        private string pendingClarificationId;
        private InteractionContext pendingContext;
        private bool commandInFlight, cancelRequested;

        public static bool IsCancellation(string text)
        {
            string command = (text ?? "").Trim().TrimEnd('.', '!', '?').ToLowerInvariant();
            return command == "cancel" || command == "stop" || command == "cancel plan" || command == "stop analysis";
        }

        public bool TryCancelCommand(string text)
        {
            if (!IsCancellation(text)) return false;
            bool hadPending = pendingContext != null;
            pendingClarificationId = null;
            pendingContext = null;
            if (commandInFlight) cancelRequested = true;
            ShowPlanMessage(commandInFlight ? "Cancellation requested. The current step will finish; remaining steps will stop."
                : hadPending ? "Clarification cancelled." : "No command is running.");
            return true;
        }

        // Command Travels to Classify
        public IEnumerator SubmitCommand(string text) => ClassifyUserCommand(text, 0f);

        // Parses the Commands
        // Saves the command state of the current command
        private InteractionContext CaptureInteractionContext()
        {
            var manager = _networkManager;
            int graph = manager.CurWorkingSubgraphID;
            
            var real = new HashSet<int>(manager.NetworkGlobal.RealNodes);
            var ids = manager.HasWorkingSession
                ? manager.SortNodeGUIDs(manager.WorkingSubgraphAllNodeGUIDs).Values.SelectMany(x => x).Where(real.Contains).ToArray()
                : real.ToArray();
            
            var nodeSet = new HashSet<int>(ids);
            var selection = manager.HasWorkingSession ? manager.WorkingSelectedNodeGUIDs : manager.SelectedNodeGUIDs;
            
            var selectionGroups = manager.SortNodeGUIDs(selection);
            int selectionGraph = manager.HasWorkingSession ? graph : NetworkManager.MainNetworkID;
            
            var selected = selectionGroups.TryGetValue(selectionGraph, out var selectedIds)
                ? selectedIds.Where(nodeSet.Contains).Distinct().ToArray() : System.Array.Empty<int>();
            
            var file = manager.FileLoader.SphericalLayout;
            var properties = file.nodes.FirstOrDefault(n => n.props != null)?.props.GetType().GetProperties()
                ?? System.Array.Empty<System.Reflection.PropertyInfo>();
            
            var numeric = properties.Where(prop =>
            {
                var type = System.Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                return type == typeof(float) || type == typeof(double) || type == typeof(int) || type == typeof(long);
            }).Select(prop => prop.Name).ToArray();
            
            var links = manager.HasWorkingSession ? manager.GetCurWorkingSubgraphLinkIDs() : manager.NetworkGlobal.Links.Keys;
            
            return new InteractionContext
            {
                graph_id = manager.FileLoader.DatasetName + ":" + graph,
                working_graph_id = graph,
                scope_label = manager.HasWorkingSession ? "working graph at command start" : "dataset at command start",
                node_ids = ids, selected_node_ids = selected,
                link_ids = links.Where(id => nodeSet.Contains(manager.NetworkGlobal.Links[id].SourceNodeID)
                    && nodeSet.Contains(manager.NetworkGlobal.Links[id].TargetNodeID)).Distinct().ToArray(),
                attributes = properties.Select(prop => prop.Name).ToArray(), numeric_attributes = numeric,
                relationship_types = file.links.Where(l => l.props != null).Select(l => l.props.type)
                    .Where(type => !string.IsNullOrWhiteSpace(type)).Distinct().ToArray(),
                direct_operations = new[] { "selectNode", "selectLink", "colorNode", "colorLink", "colorByAttribute", "colorByGPA",
                    "colorByValue", "shapeByAttribute", "sizeNode", "widthLink", "move", "layout", "deselect", "reset", "saveSession", "deleteSession", "arithmetic" },
                max_steps = Mathf.Clamp(maxPlanSteps, 1, 12)
            };
        }

        private ClassificationRequest BuildClassificationRequest(string text)
        {
            if (text.StartsWith("new request:", System.StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring("new request:".Length).Trim();
                pendingContext = null;
                pendingClarificationId = null;
            }
            var current = CaptureInteractionContext();
            if (pendingContext != null && pendingContext.graph_id != current.graph_id)
            {
                pendingContext = null;
                pendingClarificationId = null;
                throw new System.InvalidOperationException("The graph changed. Please repeat the complete request.");
            }
            return new ClassificationRequest { userText = text, conversationId = conversationId,
                clarificationId = pendingClarificationId, context = pendingContext ?? current };
        }

        private async void Start()
        {
            _handInteractor = FindObjectOfType<HandNodeInteractor>();
            _handMover = FindObjectOfType<RightHandNodeMover>();
            _handBox = FindObjectOfType<TwoHandBoxSelector>();
            _handCamera = Camera.main;
            // Typed commands and visual feedback do not depend on voice initialization.
            textCommandInput?.onSubmit.AddListener(OnTextCommandSubmit);
            CommandPress.EnableDirectActionIfModeUsed();
            loadingComet ??= FindObjectOfType<VidiGraph.NetworkLoadingComet>();

            try
            {
                await InitializeVoiceAsync();
            }
            catch (System.Exception exception)
            {
                if (this != null)
                    Debug.LogError($"Voice initialization failed: {exception}. Typed commands remain available.", this);
            }
        }

        private async Task InitializeVoiceAsync()
        {
            if (whisper == null || microphoneRecord == null)
            {
                Debug.LogError("Voice requires assigned WhisperManager and MicrophoneRecord components. Typed commands remain available.", this);
                return;
            }

            // Ensure the microphone is ready before proceeding
            if (!await EnsureMicrophoneReady())
                return;
            if (this == null || whisper == null || microphoneRecord == null) return;

            // Push-to-talk already defines the speech window. Stream VAD can
            // misclassify quiet headset input and discard every audio chunk.
            whisper.useVad = false;

            // Create a whisper stream from the microphone
            var stream = await whisper.CreateStream(microphoneRecord);
            if (this == null || whisper == null || microphoneRecord == null) return;
            if (stream == null)
            {
                Debug.LogError(
                    $"Whisper could not create a voice stream. Check the earlier model-loading error and " +
                    $"the WhisperManager model path '{whisper.ModelPath}'. Model weights are excluded from Git " +
                    "and must be installed locally. Typed commands remain available.", this);
                return;
            }
            _stream = stream;

            // Subscribe to events
            _stream.OnResultUpdated += OnResult;
            _stream.OnSegmentUpdated += OnSegmentUpdated;
            _stream.OnSegmentFinished += OnSegmentFinished;
            _stream.OnStreamFinished += OnFinished;

            microphoneRecord.OnRecordStop += OnRecordStop;
            button?.onClick.AddListener(OnButtonPressed);
            microphoneRecord.StartRecord();

            // Microphone.Start can succeed while the device position remains at zero.
            // Verify that real samples are arriving before accepting voice commands.
            await Task.Delay(500);
            if (this == null || microphoneRecord == null) return;
            int microphonePosition = Microphone.GetPosition(microphoneRecord.RecordStartMicDevice);
            if (microphonePosition <= 0)
            {
                Debug.LogError(
                    "Microphone recording did not start: the sample position is still zero. " +
                    "Check the selected input device, OS/Quest microphone permission, and hardware mute state."
                );
            }
            else
            {
                string deviceName = string.IsNullOrEmpty(microphoneRecord.RecordStartMicDevice)
                    ? "Default microphone"
                    : microphoneRecord.RecordStartMicDevice;
                Debug.Log($"Microphone active: {deviceName}, sample position {microphonePosition}.");
            }

        }

        private void OnDestroy()
        {
            textCommandInput?.onSubmit.RemoveListener(OnTextCommandSubmit);
            button?.onClick.RemoveListener(OnButtonPressed);
            CommandPress.DisableDirectActionIfModeUsed();
            if (_stream != null)
            {
                _stream.OnResultUpdated -= OnResult;
                _stream.OnSegmentUpdated -= OnSegmentUpdated;
                _stream.OnSegmentFinished -= OnSegmentFinished;
                _stream.OnStreamFinished -= OnFinished;
                if (_voiceListening) _stream.StopStream();
            }
            if (microphoneRecord != null)
            {
                microphoneRecord.OnRecordStop -= OnRecordStop;
                if (_stream != null && microphoneRecord.IsRecording) microphoneRecord.StopRecord();
            }
        }

        // Ensure the microphone is ready before starting the voice command
        // Async method to ensure the microphone is ready before starting the voice command
        private async Task<bool> EnsureMicrophoneReady()
        {
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                Debug.Log("Requesting microphone permission...");
                AsyncOperation permissionRequest =
                    Application.RequestUserAuthorization(UserAuthorization.Microphone);
                while (!permissionRequest.isDone)
                    await Task.Yield();
            }

            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                Debug.LogError("Microphone permission was denied. Enable microphone access for the app, then restart it.");
                return false;
            }

            string[] devices = Microphone.devices;
            if (devices == null || devices.Length == 0)
            {
                Debug.LogError("Unity found no microphone devices. Check the OS/Quest input and privacy settings.");
                return false;
            }

            Debug.Log("Available microphones: " + string.Join(", ", devices));
            return true;
        }

        async void Update()
        {
            UpdateLeftFistVoice();
            if (CommandPress.ReadWasPerformedThisFrame()) StartVoiceCommand();
            if (CommandPress.ReadWasCompletedThisFrame()) StopVoiceCommand();
            if (Input.GetKeyDown(desktopTalkKey) && !_voiceListening)
            {
                StartVoiceCommand();
                _keyboardTalking = _voiceListening;
            }
            if (Input.GetKeyUp(desktopTalkKey) && _keyboardTalking)
            {
                _keyboardTalking = false;
                StopVoiceCommand();
            }

            // Off by default: Enter drives DemoSequence's Status Struggle walkthrough.
            // Leaving this on ran a second demo on the same key — it created its own
            // working session and fired the snapshot-save animation on every step.
            if (enableEnterSnapshotDemo && Input.GetKeyDown(KeyCode.Return))
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
                    if (!await EnsureWorkingSessionAsync(saveName, saveName)) return;
                }
                var msg = Instantiate(command_prefab, command_parent.transform);
                msg.GetComponent<TMP_Text>().text = $"<b>Saved:</b> {saveName}";
                ScrollToBottom();
            }

        }

        // In case the application loses focus, stop the voice command to prevent missed key releases
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) ResetFistVoice();
            // A key release can be missed when switching away from the Game view.
            if (hasFocus || !_keyboardTalking) return;
            _keyboardTalking = false;
            StopVoiceCommand();
        }

        private void OnButtonPressed()
        {
            if (_voiceListening) StopVoiceCommand();
            else StartVoiceCommand();
        }

        private void UpdateLeftFistVoice()
        {
            if (!enableLeftFistVoice || !Application.isFocused) { ResetFistVoice(); return; }
            SubsystemManager.GetSubsystems(_handSubsystems);
            XRHandSubsystem running = null;
            foreach (var subsystem in _handSubsystems)
                if (subsystem.running) { running = subsystem; break; }
            if (running == null || !running.leftHand.isTracked) { ResetFistVoice(); return; }
            var hand = running.leftHand;
            float index, middle, ring, little;
            bool valid = FingerBend(hand, XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, out index)
                & FingerBend(hand, XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, out middle)
                & FingerBend(hand, XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, out ring)
                & FingerBend(hand, XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, out little);
            if (!valid) { ResetFistVoice(); return; }
            if (_handCamera == null) _handCamera = Camera.main;
            Transform trackingSpace = _handInteractor != null ? _handInteractor.TrackingSpace : null;
            if (trackingSpace == null || _handCamera == null
                || !hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var palm))
            { ResetFistVoice(); return; }
            Vector3 palmWorld = trackingSpace.TransformPoint(palm.position);
            VoiceHandPosition = palmWorld;
            Vector3 viewport = _handCamera.WorldToViewportPoint(palmWorld);
            bool raisedInView = palmWorld.y >= _handCamera.transform.position.y - fistMaxBelowEyes
                && viewport.z > 0.1f && viewport.x > 0f && viewport.x < 1f
                && viewport.y > 0f && viewport.y < 1f;
            if ((_handMover != null && _handMover.IsMoving)
                || (_handBox != null && _handBox.IsSelecting)
                || (_networkManager != null && _networkManager.DraggedNodeGUID != null))
            {
                _suppressFistUntilOpen = true;
                ResetFistVoice();
                return;
            }
            float threshold = _fistLatched ? fistBendDegrees - 10f : fistBendDegrees;
            bool fist = index >= threshold && middle >= threshold && ring >= threshold && little >= threshold;
            if (!fist)
            {
                bool fullyOpen = index < 30 && middle < 30 && ring < 30 && little < 30;
                if (fullyOpen) _suppressFistUntilOpen = false;
                if (_fistTalking) StopVoiceCommand();
                _fistTalking = _fistLatched = false;
                _fistHeld = 0;
                if (fullyOpen) _fistNeedsOpen = false;
                return;
            }
            if (_fistLatched) return; // Lowering the hand after activation is allowed.
            if (_suppressFistUntilOpen || _fistNeedsOpen || !raisedInView)
            { _fistHeld = 0; return; }
            _fistHeld += Time.unscaledDeltaTime;
            if (_fistHeld < fistHoldSeconds) return;
            _fistLatched = true;
            if (!_voiceListening)
            {
                StartVoiceCommand();
                _fistTalking = _voiceListening;
            }
        }

        private static bool FingerBend(XRHand hand, XRHandJointID a, XRHandJointID b, XRHandJointID c, out float bend)
        {
            bend = 0;
            if (!hand.GetJoint(a).TryGetPose(out var first) || !hand.GetJoint(b).TryGetPose(out var middle)
                || !hand.GetJoint(c).TryGetPose(out var last)) return false;
            Vector3 u = middle.position - first.position, v = last.position - middle.position;
            if (u.sqrMagnitude < 0.000001f || v.sqrMagnitude < 0.000001f) return false;
            bend = Vector3.Angle(u, v);
            return true;
        }

        private void ResetFistVoice()
        {
            if (_fistTalking) StopVoiceCommand();
            _fistTalking = _fistLatched = false;
            _fistNeedsOpen = true;
            _fistHeld = 0;
        }

        private void OnDisable() { ResetFistVoice(); }

        private void StartVoiceCommand()
        {
            if (_stream == null || _voiceListening || _voiceFinishing
                || microphoneRecord == null || !microphoneRecord.IsRecording) return;
            _completedTranscript = "";
            if (text != null) text.text = "";
            whisperStartTime = Time.time;
            _voiceListening = true;
            _stream.StartStream();
            SetVoiceFeedback(true);
        }

        private void StopVoiceCommand()
        {
            _fistTalking = false;
            if (_stream == null || !_voiceListening) return;
            _voiceListening = false;
            _voiceFinishing = true;
            SetVoiceFeedback(false);
            _stream.StopStream();
        }

        private void SetVoiceFeedback(bool listening)
        {
            if (buttonText != null) buttonText.text = listening ? "Stop & send" : "Talk";
            var clip = listening ? startSpeakingAudio : stopSpeakingAudio;
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
            if (Indicator != null)
            {
                var props = new MaterialPropertyBlock();
                Indicator.GetPropertyBlock(props);
                props.SetColor("_Color", ColorUtils.StringToColor(listening ? "#FF8F00" : "#C0C0C0"));
                Indicator.SetPropertyBlock(props);
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
            if (buttonText != null) buttonText.text = "Talk";
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
            if (text != null && !string.IsNullOrWhiteSpace(segment.Result))
                text.text = _completedTranscript + segment.Result;
        }

        private void OnSegmentFinished(WhisperResult segment)
        {
            if (!string.IsNullOrWhiteSpace(segment.Result))
            {
                _completedTranscript += segment.Result.Trim() + " ";
                if (text != null)
                    text.text = _completedTranscript.TrimEnd();
            }

            float recognitionTime = Time.time - whisperStartTime;
            whisperStartTime = Time.time;
            _pipelineStartTime = Time.time;
            StartCoroutine(ClassifyUserCommand(segment.Result, recognitionTime));
        }

        private void OnFinished(string finalResult)
        {
            _fistTalking = false;
            _voiceFinishing = false;
            _voiceListening = false;
            // Debug.Log("Stream finished!");
        }

        // Classification Main Function
        private IEnumerator ClassifyUserCommand(string recognizedText, float whisperTime)
        {
            if (TryCancelCommand(recognizedText)) yield break;
            // Keep voice and typed requests in arrival order while earlier steps animate.
            
            // Voice commands enter a schedular, enters a queue to make wait for animation to complete before execution.
            yield return _networkManager.VisualScheduler.Schedule(ExecuteClassifiedCommand(recognizedText, whisperTime));
        }

        /*
            Gives command to classification server,
            Parses the response,

            Then executes the classified command based on the parsed response.
            Also handles visual steps related to the command execution.
            Ensures proper cleanup and error handling throughout the process.

            Sends statistical reports and clears any temporary state related to the command execution.
        */
        private IEnumerator ExecuteClassifiedCommand(string recognizedText, float whisperTime)
        {
            commandInFlight = true;
            cancelRequested = false;
            loadingIcon?.SetLoading(true);
            loadingComet?.Show();
            try
            {
                ClassificationRequest requestBody = null;
                string preparationError = null;
                try { requestBody = BuildClassificationRequest(recognizedText); }
                catch (System.Exception exception) { preparationError = exception.Message; }
                
                if (requestBody == null) { ShowPlanMessage(preparationError); yield break; }
                ShowPlanMessage(requestBody.userText);
                
                using (var www = new UnityWebRequest(serverUrl, "POST"))
                {
                    www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(requestBody)));
                    www.downloadHandler = new DownloadHandlerBuffer();
                    www.SetRequestHeader("Content-Type", "application/json");
                    www.timeout = 120;
                    
                    yield return www.SendWebRequest();
                    if (cancelRequested) { ShowPlanMessage("Command cancelled before execution."); yield break; }
                    
                    ClassificationResponse response = null;
                    
                    try { response = JsonConvert.DeserializeObject<ClassificationResponse>(www.downloadHandler.text); }
                    catch (JsonException exception) { Debug.LogWarning(exception.Message); }
                    if (response == null || !string.IsNullOrEmpty(response.error) ||
                        (string.IsNullOrEmpty(response.route) && response.actions == null && response.stats == null))
                    {
                        ShowPlanMessage(response?.error ?? "Jev could not be reached. No command was executed.");
                        yield break;
                    }
                    
                    yield return HandleClassification(response, requestBody.context);
                }
            }
            finally
            {
                commandInFlight = false;
                _networkManager.CurrentVisualStep = null;
                loadingIcon?.SetLoading(false);
                loadingComet?.Hide();
            }
        }
        
        // Output Response based on Response Clarification or Plan or Unspported
        private IEnumerator HandleClassification(ClassificationResponse response, InteractionContext context)
        {
            if (response.route == "clarify" || !string.IsNullOrEmpty(response.clarify))
            {
                pendingClarificationId = response.clarification_id;
                pendingContext = context;
                ShowPlanMessage(response.clarify + " Reply with your answer, or say 'cancel'.");
                yield break;
            }

            pendingClarificationId = null;
            pendingContext = null;
            
            if (response.route == "unsupported") 
            { 
                ShowPlanMessage(response.message ?? "That operation is not supported."); 
                yield break; 
            }

            if (response.route == "plan")
            {
                SequentialPlanExecutor execution = null;
                string validationError = null;
                try 
                { 
                    execution = new SequentialPlanExecutor(this, response.plan, context, Mathf.Min(maxPlanSteps, context.max_steps)); 
                }
                catch (System.Exception exception) 
                { 
                    validationError = exception.Message;
                }
                if (execution == null) 
                { 
                    ShowPlanMessage("Plan rejected before execution: " + validationError); 
                    yield break; 
                }
                
                LastExecutionReport = execution.Report;
                statisticalReportDisplay?.BeginCommand();
                // Supported plans execute internally; only alternative proposals
                // are shown through the clarification route before acceptance.
                try { yield return CommandExecutionRunner.Run(execution.Execute(() => cancelRequested), execution.Report); }
                finally
                {
                    execution.Report.Finish();
                    statisticalReportDisplay?.DisplayCommandReport(execution.Report, null);
                    Debug.Log(JsonConvert.SerializeObject(execution.Report));
                }
                if (!cancelRequested) yield return SummarizeExecution(execution.Report);
                yield break;
            }
            if (!string.IsNullOrEmpty(response.route) && response.route != "direct")
            { 
                ShowPlanMessage("Unknown Jev route; no action was executed."); 
                yield break;
            }
            
            yield return ExecuteActionSteps(response, response.corrected_input);
        }

        // Give a Summarization Report for the output
        private IEnumerator SummarizeExecution(CommandExecutionReport report)
        {
            using (var www = new UnityWebRequest(serverUrl.Replace("/classify", "/summarize"), "POST"))
            {
                string payload = JsonConvert.SerializeObject(new
                {
                    command_id = report.CommandId,
                    steps = report.Steps.Select(step => new { step.StepId, step.Operation, step.Status,
                        step.SummaryText, step.MatchedCount, step.ChangedCount, step.ScopeTotal, step.NeighborCount,
                        step.IncidentTotal, step.Comparison, step.Error })
                });
                
                www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.timeout = 20;
                yield return www.SendWebRequest();
                
                if (www.result != UnityWebRequest.Result.Success || cancelRequested) yield break;
                
                JObject result = null;
                try { result = JObject.Parse(www.downloadHandler.text); }
                catch (JsonException exception) { Debug.LogWarning(exception.Message); }
                
                if (result != null && (string)result["command_id"] == report.CommandId)
                    statisticalReportDisplay?.AppendExplanation(report.CommandId, (string)result["summary"]);
            }
        }

        public void ShowPlanMessage(string message)
        {
            ShowDemoMessage((message ?? "").Replace("<", "‹").Replace(">", "›"));
        }

        public CommandExecutionReport LastExecutionReport { get; private set; }

        // Call from the existing visual scheduler. Both voice and desktop use this path
        public IEnumerator ExecuteActionSteps(ClassificationResponse classification, string originalCommand, string legendNodeLabel = null)
        {
            bool ownsCommandState = !commandInFlight;
            if (ownsCommandState) { commandInFlight = true; cancelRequested = false; }
            var report = CommandExecutionReport.FromLegacy(classification.actions, classification.queries);
            
            LastExecutionReport = report;
            statisticalReportDisplay?.BeginCommand();
            _lastSelectNodeLabel = legendNodeLabel ?? string.Empty;
            
            try
            {
                yield return CommandExecutionRunner.Run(ExecuteActionStepsCore(classification, originalCommand, report, legendNodeLabel), report);
            }
            finally
            {
                report.Finish();
                _networkManager.CurrentVisualStep = null;
                statisticalReportDisplay?.DisplayCommandReport(report, report.HasFailure ? null : classification.stats);
                Debug.Log(JsonConvert.SerializeObject(report));
                if (ownsCommandState) commandInFlight = false;
            }
        }
        
        // Execution --> executeActionSteps
        private IEnumerator ExecuteActionStepsCore(ClassificationResponse classification, string originalCommand, CommandExecutionReport report, string legendNodeLabel)
        {
            var query = classification.queries ?? System.Array.Empty<string>();
            var action = classification.actions ?? System.Array.Empty<string[]>();
            var actions_count = action.Length;
            
            _lastSelectedLinkGUIDs.Clear();
            
            bool hasLinkQuery = false;
            bool linkScopeSelected = false;
            string linkDirection = "both";
            
            HashSet<string> commandSelectedNodes = null; // Keep explicit query scope, even when empty.

            Debug.Log("Action: " + action + " Counts:" + actions_count);
            Debug.Log("Cypher Query: " + query);

            for (int i = 0; i < actions_count; i++)
            {
                while (_networkManager.HasActiveVisualAnimation || _networkManager.HasPendingVisualInitialization)
                    yield return null;
                var receipt = report.Steps[i];
                if (cancelRequested) { receipt.Status = "cancelled"; yield break; }
                receipt.Status = "running";
                // Reject malformed action records before indexing parsed fields.
                if (action[i] == null || action[i].Length < 2 || i >= query.Length)
                {
                    throw new System.InvalidOperationException($"Malformed action {i + 1}.");
                }
                var previouslySelected = _networkManager.SortNodeGUIDs(_networkManager.WorkingSelectedNodeGUIDs).Values.SelectMany(ids => ids).ToHashSet();
                var stepLabel = DescribeVisualAction(action[i][0], action[i][1]);

                // Execute the visual step for the current action. (Animation)
                using (var visualStep = new VisualCommandStep(() => ShowDemoMessage(stepLabel)))
                {
                    _networkManager.CurrentVisualStep = visualStep;
                    try
                    {
                        // Cases for each type of visual commands / actions
                        switch (action[i][0])
                        {
                            case "selectNode":
                                Debug.Log("Selecting nodes with query: " + query[i]);
                                if (text != null) text.text += $"\n<size=18><color=#aaa>{query[i]}</color></size>";
                                _lastSelectNodeLabel = LegendManager.ToShortLabel(action[i][1]);
                                var nodesTask = _databaseStorage.GetNodesFromStoreAsync(_networkManager.NetworkGlobal, query[i]);
                                yield return new WaitUntil(() => nodesTask.IsCompleted);
                                if (!TryGetTaskResult(nodesTask, out var nodes)) throw new System.InvalidOperationException("The operation could not be completed.");
                                TimerUtils.StartTime("SetSelectedNodes");
                                if (_networkManager.OnQueryMode || !_networkManager.HasWorkingSession)
                                {
                                    // No session yet — create one with ALL nodes first, then select the queried subset
                                    var allNodesTask = _databaseStorage.GetNodesFromStoreAsync(_networkManager.NetworkGlobal, "MATCH (n:Node) RETURN n");
                                    yield return new WaitUntil(() => allNodesTask.IsCompleted);
                                    if (!TryGetTaskResult(allNodesTask, out var allNodesForSel)) throw new System.InvalidOperationException("The operation could not be completed.");

                                    var sortedSel = _networkManager.SortNodeGUIDs(allNodesForSel);
                                    if (!sortedSel.ContainsKey(NetworkManager.MainNetworkID)) throw new System.InvalidOperationException("The dataset contains no selectable nodes.");

                                    _networkManager.CreateWorkingSubgraph(sortedSel[NetworkManager.MainNetworkID], originalCommand, originalCommand);
                                    _networkManager.SetQueryMode(false);

                                    var subnGUIDs = _networkManager.TranslateToWorkingSubgraphNodeGUIDs(nodes);
                                    commandSelectedNodes = _networkManager.ReplaceWorkingNodeSelection(subnGUIDs);
                                }
                                else
                                {
                                    // Session exists — select queried nodes within it
                                    var subnGUIDs = _networkManager.TranslateToWorkingSubgraphNodeGUIDs(nodes);
                                    commandSelectedNodes = _networkManager.ReplaceWorkingNodeSelection(subnGUIDs);
                                }

                                receipt.RecordSelection(_networkManager, commandSelectedNodes, previouslySelected);
                                TimerUtils.EndTime("SetSelectedNodes");
                                break;

                            case "sizeNode":
                                // Handle node size adjustment based on the specified attribute and scope
                                // Parse "attribute:scope" format (e.g., "gpa:all" or "gpa:selected")
                                string sizeParam = action[i][1];
                                string attributeName = sizeParam.Contains(":") ? sizeParam.Split(':')[0] : sizeParam;

                                var minMaxTask = _databaseStorage.GetMinMaxFromStoreAsync(_networkManager.NetworkGlobal, query[i]);

                                yield return new WaitUntil(() => minMaxTask.IsCompleted);
                                if (!TryGetTaskResult(minMaxTask, out var sizeRange)) throw new System.InvalidOperationException("The operation could not be completed.");

                                var (minV, maxV) = sizeRange;
                                Debug.Log($"Min/Max of {attributeName}: {minV}, {maxV}");

                                _networkManager.SetMLNodeSizeEncoding(attributeName, minV, maxV, NetworkManager.MainNetworkID);
                                break;
                            case "selectLink":
                                hasLinkQuery = true;
                                _lastSelectedLinkGUIDs.Clear();
                                _lastLinkSelectLabel = action[i][1].Split(':')[0];
                                linkScopeSelected = LinkQueryScope.IsSelected(query[i], action[i][1]);
                                linkDirection = LinkQueryScope.Direction(query[i]);
                                string linkQuery = LinkQueryScope.RemoveSelectedPredicate(query[i]);
                                var linksTask = _databaseStorage.GetLinksFromStoreAsync(_networkManager.NetworkGlobal, linkQuery);
                                yield return new WaitUntil(() => linksTask.IsCompleted);
                                if (!TryGetTaskResult(linksTask, out var links)) throw new System.InvalidOperationException("The operation could not be completed.");
                                _lastSelectedLinkGUIDs = new HashSet<string>(links);
                                if (linkScopeSelected)
                                {
                                    var selectedIDs = _networkManager.SortNodeGUIDs(_networkManager.WorkingSelectedNodeGUIDs)
                                        .Values.SelectMany(ids => ids).ToHashSet();
                                    _lastSelectedLinkGUIDs.RemoveWhere(guid =>
                                        !_networkManager.LinkGUIDToID.TryGetValue(guid, out var id) ||
                                        !_networkManager.NetworkGlobal.Links.TryGetValue(id.Item2, out var link) ||
                                        !LinkQueryScope.Matches(link.SourceNodeID, link.TargetNodeID, selectedIDs, linkDirection));
                                }
                                var matchedLinks = _networkManager.HasWorkingSession
                                    ? _networkManager.TranslateToWorkingSubgraphLinkGUIDs(_lastSelectedLinkGUIDs)
                                    : _lastSelectedLinkGUIDs;
                                receipt.RecordLinks(_networkManager, matchedLinks, _lastLinkSelectLabel, linkScopeSelected, linkDirection);
                                break;

                            case "deselect":
                                Debug.Log("Deselecting nodes");

                                TimerUtils.StartTime("Deselect Nodes");
                                _networkManager.ClearSelection();

                                _lastSelectedLinkGUIDs.Clear();
                                hasLinkQuery = false;
                                commandSelectedNodes = null;
                                TimerUtils.EndTime("Deselect Nodes");

                                break;
                            case "move":
                                Debug.Log("Moving selected nodes");

                                // Determine which nodes to move based on the current session and selection
                                var nodes_move = _networkManager.HasWorkingSession
                                    ? (_networkManager.WorkingSelectedNodeGUIDs.Count > 0
                                        ? _networkManager.WorkingSelectedNodeGUIDs
                                        : _networkManager.WorkingSubgraphAllNodeGUIDs)
                                    : _networkManager.SelectedNodeGUIDs;

                                TimerUtils.StartTime("Move Nodes");

                                _networkManager.BringMLNodes(nodes_move);
                                TimerUtils.EndTime("Move Nodes");

                                break;
                            case "widthLink":
                                if (!float.TryParse(action[i][1], System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out var width) || width <= 0)
                                    throw new System.ArgumentException("Link width must be positive.");
                                _networkManager.SetSubnetworkGlobalLinkWidth(width, _networkManager.HasWorkingSession
                                    ? _networkManager.WorkingSubgraphID : NetworkManager.MainNetworkID);
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
                                var colorSessionTask = EnsureWorkingSessionAsync("Color all nodes", "Color Nodes", selectAll: true);

                                yield return new WaitUntil(() => colorSessionTask.IsCompleted);
                                if (!TryGetTaskResult(colorSessionTask, out bool colorSessionReady) || !colorSessionReady) throw new System.InvalidOperationException("The operation could not be completed.");

                                HashSet<string> nodes_color = commandSelectedNodes ?? (_networkManager.WorkingSelectedNodeGUIDs.Count > 0
                                    ? _networkManager.WorkingSelectedNodeGUIDs
                                    : _networkManager.WorkingSubgraphAllNodeGUIDs);

                                TimerUtils.StartTime("SetColor");
                                if (!ColorUtility.TryParseHtmlString(action[i][1], out _)) throw new System.ArgumentException("Invalid node color.");
                                receipt.CaptureNodeColors(_networkManager, nodes_color);
                                _networkManager.SetMLNodesColor(nodes_color, action[i][1]);

                                legendManager?.SetNodeColorLabel(action[i][1], legendNodeLabel ?? _lastSelectNodeLabel);
                                TimerUtils.EndTime("SetColor");

                                break;
                            case "colorLink":
                                Debug.Log($"Coloring links: {action[i][1]}");
                                TimerUtils.StartTime("SetColor");

                                // Link color handling for the current action
                                HashSet<string> linkGUIDs_color;
                                if (_networkManager.HasWorkingSession)
                                {
                                    var nodeScope = commandSelectedNodes ?? (_networkManager.WorkingSelectedNodeGUIDs.Count > 0
                                        ? _networkManager.WorkingSelectedNodeGUIDs : null);
                                    if (!hasLinkQuery) linkScopeSelected = nodeScope != null;
                                    linkGUIDs_color = LinkQueryScope.ResolveTargets(hasLinkQuery,
                                        _networkManager.TranslateToWorkingSubgraphLinkGUIDs(_lastSelectedLinkGUIDs),
                                        nodeScope != null ? _networkManager.GetLinksForNodes(nodeScope) : new HashSet<string>(),
                                        _networkManager.WorkingSubgraphAllLinkGUIDs, nodeScope != null);
                                }
                                else
                                {
                                    linkGUIDs_color = hasLinkQuery ? _lastSelectedLinkGUIDs : _networkManager.LinkGUIDToID
                                        .Where(pair => pair.Value.Item1 == NetworkManager.MainNetworkID).Select(pair => pair.Key).ToHashSet();
                                }

                                if (!ColorUtility.TryParseHtmlString(action[i][1], out _)) throw new System.ArgumentException("Invalid link color.");
                                receipt.RecordLinks(_networkManager, linkGUIDs_color, hasLinkQuery ? _lastLinkSelectLabel : "all", linkScopeSelected, linkDirection);
                                receipt.CaptureLinkColors(_networkManager, linkGUIDs_color);
                                _networkManager.SetMLLinksColorStart(linkGUIDs_color, action[i][1]);
                                _networkManager.SetMLLinksColorEnd(linkGUIDs_color, action[i][1]);

                                legendManager?.SetEdgeColorLabel(action[i][1], _lastLinkSelectLabel);
                                TimerUtils.EndTime("SetColor");

                                break;
                            case "colorByAttribute":
                                Debug.Log("Categorical coloring by attribute: " + action[i][1]);
                                string attributeName_color = action[i][1];

                                TimerUtils.StartTime("ColorByAttribute");

                                var colorAttributeSessionTask = EnsureWorkingSessionAsync($"Color by {attributeName_color}", $"Color by {attributeName_color}", selectAll: true);
                                yield return new WaitUntil(() => colorAttributeSessionTask.IsCompleted);
                                if (!TryGetTaskResult(colorAttributeSessionTask, out bool colorAttributeSessionReady) || !colorAttributeSessionReady) throw new System.InvalidOperationException("The operation could not be completed.");
                                receipt.CaptureNodeColors(_networkManager, System.Array.Empty<string>());

                                // Single DB round trip: get all nodes grouped by attribute value (cached per session).
                                if (!_groupedNodesCache.TryGetValue(attributeName_color, out var grouped_color))
                                {
                                    var groupedColorTask = _databaseStorage.GetNodesGroupedByAttributeAsync(_networkManager.NetworkGlobal, attributeName_color);
                                    yield return new WaitUntil(() => groupedColorTask.IsCompleted);
                                    if (!TryGetTaskResult(groupedColorTask, out grouped_color)) throw new System.InvalidOperationException("The operation could not be completed.");
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
                                    receipt.CaptureNodeColors(_networkManager, subnGUIDs);
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

                                var gpaSessionTask = EnsureWorkingSessionAsync("Color by GPA", "Color by GPA", selectAll: true);
                                yield return new WaitUntil(() => gpaSessionTask.IsCompleted);
                                if (!TryGetTaskResult(gpaSessionTask, out bool gpaSessionReady) || !gpaSessionReady) throw new System.InvalidOperationException("The operation could not be completed.");
                                receipt.CaptureNodeColors(_networkManager, System.Array.Empty<string>());

                                {
                                    string[] gpaGradient = { "#E3F2FD", "#90CAF9", "#42A5F5", "#1976D2", "#1565C0" };
                                    // A preceding selection limits every GPA bucket, including an empty selection.
                                    var gpaScope = commandSelectedNodes ?? (_networkManager.WorkingSelectedNodeGUIDs.Count > 0
                                        ? _networkManager.WorkingSelectedNodeGUIDs : _networkManager.WorkingSubgraphAllNodeGUIDs);

                                    // Single DB round trip: all nodes with their GPA values (cached).
                                    // Client-side bucketing replaces the previous 1 min/max + 5 bucket queries.
                                    if (!_numericValuesCache.TryGetValue("gpa", out var gpaValues))
                                    {
                                        var gpaValuesTask = _databaseStorage.GetNodesWithNumericValuesAsync(_networkManager.NetworkGlobal, "gpa");
                                        yield return new WaitUntil(() => gpaValuesTask.IsCompleted);
                                        if (!TryGetTaskResult(gpaValuesTask, out gpaValues)) throw new System.InvalidOperationException("The operation could not be completed.");
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

                                                bucketGUIDs.IntersectWith(gpaScope);
                                                receipt.CaptureNodeColors(_networkManager, bucketGUIDs);
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

                                var colorValueSessionTask = EnsureWorkingSessionAsync($"Color by {cbvAttribute}", $"Color by {cbvAttribute}", selectAll: true);
                                yield return new WaitUntil(() => colorValueSessionTask.IsCompleted);
                                if (!TryGetTaskResult(colorValueSessionTask, out bool colorValueSessionReady) || !colorValueSessionReady) throw new System.InvalidOperationException("The operation could not be completed.");
                                receipt.CaptureNodeColors(_networkManager, System.Array.Empty<string>());

                                // Get value range from min/max query
                                var cbvRangeTask = _databaseStorage.GetMinMaxFromStoreAsync(_networkManager.NetworkGlobal, query[i]);
                                yield return new WaitUntil(() => cbvRangeTask.IsCompleted);
                                if (!TryGetTaskResult(cbvRangeTask, out var cbvRangeResult)) throw new System.InvalidOperationException("The operation could not be completed.");
                                var (cbvMin, cbvMax) = cbvRangeResult;
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
                                        var cbvValuesTask = _databaseStorage.GetNodesWithNumericValuesAsync(_networkManager.NetworkGlobal, cbvAttribute);
                                        yield return new WaitUntil(() => cbvValuesTask.IsCompleted);
                                        if (!TryGetTaskResult(cbvValuesTask, out cbvNodeValues)) throw new System.InvalidOperationException("The operation could not be completed.");
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
                                        var bucketTargets = _networkManager.TranslateToWorkingSubgraphNodeGUIDs(bucketGuids);
                                        receipt.CaptureNodeColors(_networkManager, bucketTargets);
                                        _networkManager.SetMLNodesColor(bucketTargets, cbvGradient[b]);
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

                                var shapeSessionTask = EnsureWorkingSessionAsync($"Shape by {attributeName_shape}", $"Shape by {attributeName_shape}");
                                yield return new WaitUntil(() => shapeSessionTask.IsCompleted);
                                if (!TryGetTaskResult(shapeSessionTask, out bool shapeSessionReady) || !shapeSessionReady) throw new System.InvalidOperationException("The operation could not be completed.");

                                // Single DB round trip: get all nodes grouped by attribute value (cached).
                                if (!_groupedNodesCache.TryGetValue(attributeName_shape, out var grouped_shape))
                                {
                                    var groupedShapeTask = _databaseStorage.GetNodesGroupedByAttributeAsync(_networkManager.NetworkGlobal, attributeName_shape);
                                    yield return new WaitUntil(() => groupedShapeTask.IsCompleted);
                                    if (!TryGetTaskResult(groupedShapeTask, out grouped_shape)) throw new System.InvalidOperationException("The operation could not be completed.");
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
                                // Arithmetic cases are specific statistical commands
                                Debug.Log("Performing arithmetic operation: " + action[i][1]);
                                TimerUtils.StartTime("Arithmetic Operation");

                                var arithmeticTask = _databaseStorage.GetValueFromStoreAsync(_networkManager.NetworkGlobal, query[i]);
                                yield return new WaitUntil(() => arithmeticTask.IsCompleted);

                                if (!TryGetTaskResult(arithmeticTask, out var result)) throw new System.InvalidOperationException("The operation could not be completed.");

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
                                var resetSessionTask = EnsureWorkingSessionAsync("Reset", "Reset");

                                yield return new WaitUntil(() => resetSessionTask.IsCompleted);

                                if (!TryGetTaskResult(resetSessionTask, out bool resetSessionReady) || !resetSessionReady) throw new System.InvalidOperationException("The operation could not be completed.");
                                if (!resetHadSession)
                                    _networkManager.BringMLNodes(_networkManager.WorkingSubgraphAllNodeGUIDs);

                                // Set to default colors
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
                                    var saveSessionTask = EnsureWorkingSessionAsync(saveName, saveName);
                                    yield return new WaitUntil(() => saveSessionTask.IsCompleted);
                                    if (!TryGetTaskResult(saveSessionTask, out bool saveSessionReady) || !saveSessionReady) throw new System.InvalidOperationException("The operation could not be completed.");
                                }
                                var _saveCmd = Instantiate(command_prefab, command_parent.transform);
                                _saveCmd.GetComponent<TMP_Text>().text = $"<b>Saved:</b> {saveName}";
                                ScrollToBottom();
                                break;
                            default:
                                // Handle unknown actions
                                Debug.LogWarning("Unknown action: " + action);
                                throw new System.InvalidOperationException("Unsupported action: " + action[i][0]);
                        }
                        receipt.CompleteMetrics(_networkManager);
                        // Existing movement animations finish before the next action starts.
                        while (_networkManager.HasActiveVisualAnimation) yield return null;
                        yield return visualStep.Play();
                        receipt.Status = receipt.MatchedCount == 0 ? "no_matches" : "succeeded";
                    }
                    finally { _networkManager.CurrentVisualStep = null; }
                }
            }

        }

        // Retained for reference: queued steps now wait for animations and clean up in finally.
        // private IEnumerator HideCometWhenReady()
        // {
        //     while (_networkManager.IsLayoutAnimating)
        //         yield return null;
        //     loadingComet?.Hide();
        // }

        private async void RunDemoStep()
        {
            switch (_demoStep)
            {
                case 0:
                    // Step 1 — create a working session and select ALL nodes inside it
                    if (!await EnsureWorkingSessionAsync("Demo Selection", "Demo")) return;
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
            if (command_prefab == null || command_parent == null) return;
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

        public async Task<bool> EnsureWorkingSessionAsync(
            string sessionLabel,
            string shortLabel,
            bool selectAll = false)
        {
            if (_networkManager.HasWorkingSession)
                return await WaitForWorkingSessionVisualsAsync();

            List<string> allNodes = await _databaseStorage.GetNodesFromStoreAsync(
                _networkManager.NetworkGlobal,
                "MATCH (n:Node) RETURN n"
            );

            var sorted = _networkManager.SortNodeGUIDs(allNodes);
            if (!sorted.ContainsKey(NetworkManager.MainNetworkID))
                return false;

            _networkManager.CreateWorkingSubgraph(
                sorted[NetworkManager.MainNetworkID],
                sessionLabel,
                shortLabel
            );
            if (selectAll)
                _networkManager.SetWorkingSelectedNodes(
                    _networkManager.WorkingSubgraphAllNodeGUIDs,
                    true
                );

            return await WaitForWorkingSessionVisualsAsync();
        }

        private async Task<bool> WaitForWorkingSessionVisualsAsync()
        {
            // Creating a session starts an asynchronous layout. Do not expose a color
            // command's target values to that renderer before its own fade begins.
            while (this != null && isActiveAndEnabled && _networkManager != null)
            {
                if (!_networkManager.HasActiveVisualAnimation && !_networkManager.HasPendingVisualInitialization)
                    return _networkManager.HasWorkingSession;
                await Task.Yield();
            }
            return false;
        }

        public static string DescribeVisualAction(string action, string parameter)
        {
            // Keep server action names out of the visible step explanation.
            string label = action switch
            {
                "selectNode" => "Select nodes", "selectLink" => "Select links",
                "deselect" => "Clear selection", "move" => "Move nodes",
                "layout" => "Change layout", "sizeNode" => "Size nodes",
                "colorNode" => "Color nodes", "colorLink" => "Color links",
                "widthLink" => "Change link width",
                "colorByAttribute" => "Color nodes by", "colorByValue" => "Color nodes by",
                "colorByGPA" => "Color nodes by GPA", "shapeByAttribute" => "Change node shapes by",
                "reset" => "Reset appearance", "saveSession" => "Save session",
                "deleteSession" => "Delete session", "arithmetic" => "Calculate",
                _ => "Update network"
            };
            return $"<b>{label}</b>: {parameter}";
        }

        private static bool TryGetTaskResult<T>(Task<T> task, out T result)
        {
            if (task.IsCanceled)
            {
                Debug.LogError("Database operation was canceled.");
                result = default;
                return false;
            }

            if (task.IsFaulted)
            {
                Debug.LogError($"Database operation failed: {task.Exception?.GetBaseException().Message}");
                result = default;
                return false;
            }

            result = task.Result;
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
    public string conversationId, clarificationId;
    public Whisper.Samples.InteractionContext context;
}

[System.Serializable]
public class ClassificationResponse
{
    public string route, message, error, clarification_id;
    public Whisper.Samples.AnalysisPlan plan;
    public string input;
    public string corrected_input;
    public string[] queries;
    public string clarify;
    public Timing timings;
    public string[][] actions;
    public StatisticalQuery[] stats;

}

[System.Serializable]
[JsonConverter(typeof(StatisticalQueryConverter))]
public class StatisticalQuery
{
    public string label;
    public string query;
}

/// <summary>
/// Accepts both the current { label, query } format and legacy bare query strings.
/// </summary>
public class StatisticalQueryConverter : JsonConverter
{
    public override bool CanConvert(System.Type objectType)
    {
        return objectType == typeof(StatisticalQuery);
    }

    public override object ReadJson(
        JsonReader reader,
        System.Type objectType,
        object existingValue,
        JsonSerializer serializer)
    {
        JToken token = JToken.Load(reader);

        if (token.Type == JTokenType.String)
        {
            return new StatisticalQuery
            {
                label = "Statistical report",
                query = token.Value<string>()
            };
        }

        if (token.Type == JTokenType.Object)
        {
            return new StatisticalQuery
            {
                label = token.Value<string>("label"),
                query = token.Value<string>("query")
            };
        }

        return null;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var statistic = (StatisticalQuery)value;
        writer.WriteStartObject();
        writer.WritePropertyName("label");
        writer.WriteValue(statistic.label);
        writer.WritePropertyName("query");
        writer.WriteValue(statistic.query);
        writer.WriteEndObject();
    }
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
