/*
 * DemoSequence — keyboard-driven showcase using the working-subgraph system.
 *
 *   B      "Status Struggle" case study.
 *          Wipes all sessions, creates a fresh working subgraph from ALL real nodes
 *          in force-directed layout. No initial encoding — let the layout speak.
 *
 * Each step posts its command to the shared command log in plain language (the
 * way the voice demos caption themselves), then posts what it found underneath.
 *
 *   Enter 1  Top-15 aggressors (most outgoing aggression links) → selected + red.
 *             Reports their avg friendship degree vs the school-wide average.
 *             All other nodes become transparent ghosts; all links fade out.
 *
 *   Enter 2  Encode sex with shapes: sphere = girl, cube = boy.
 *             Color aggression links of the top-15 in orange.
 *
 *   Enter 3  Color friendship links of the top-15 in green.
 *             (Aggression orange and shapes remain — cumulative layering.)
 *
 *   F      Standalone: new subgraph, all nodes colored by friendship degree (blue saturation).
 *   A      Standalone: new subgraph, all nodes colored by aggression degree (blue saturation).
 *
 * Key choice: F/A rather than 1/2 because KeyboardCommandTester (which is live in
 * the scene) already binds Alpha1/Alpha2 to "size by grade"/"size by GPA" — sharing
 * them would fire both actions on one press. All three keys are Inspector-editable.
 *
 * Requires: NetworkManager at "/Network Manager", DatabaseStorage at "/Database"
 */

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VidiGraph;
using Whisper.Samples;

public class DemoSequence : MonoBehaviour
{
    [SerializeField] NetworkManager _networkManager;
    [SerializeField] DatabaseStorage _databaseStorage;

    [Header("Keys")]
    [SerializeField] KeyCode _startDemoKey = KeyCode.B;
    [SerializeField] KeyCode _friendshipDegreeKey = KeyCode.F;
    [SerializeField] KeyCode _aggressionDegreeKey = KeyCode.A;

    [Header("Captions")]
    [Tooltip("The label the demo writes its commands into. One object, rewritten " +
             "each step — this is the display that actually shows up in the scene.")]
    [SerializeField] TextMeshProUGUI _stepLabel;

    [Tooltip("How many commands stay on screen. Adding one past this drops the oldest " +
             "off the top and shifts the rest up.")]
    [SerializeField, Min(1)] int _maxVisibleCommands = 3;

    [Tooltip("Blank lines placed between commands so the steps read as separate blocks.")]
    [SerializeField, Min(0)] int _blankLinesBetweenCommands = 1;

    [Tooltip("Also append each command as a new entry in the scrolling command log. " +
             "Off by default: entries are created correctly but sit outside the visible " +
             "area of that scroll view, so they show up as nothing.")]
    [SerializeField] bool _alsoPostToCommandLog;

    [Tooltip("Command-log panel used by the other demos. Auto-borrowed from StreamingSampleMic if left empty.")]
    [SerializeField] GameObject _commandPrefab;
    [SerializeField] GameObject _commandParent;
    [SerializeField] ScrollRect _commandScroll;
    [Tooltip("Persistent legend panel. Auto-borrowed from StreamingSampleMic if left empty.")]
    [SerializeField] LegendManager _legend;

    int _step = -1;
    int _demoSubnID = -1;
    List<int> _bulliesIDs = new List<int>();
    List<int> _aggrLinkIDs = new List<int>();
    List<int> _friendLinkIDs = new List<int>();
    Coroutine _scrollRoutine;  // pending scroll-to-bottom, cancelled if another entry lands first
    string _pendingAnswer;     // reply to a question command, posted once the step has computed it

    // Everything currently on screen; the label is rewritten from this, so new text
    // appears under the old rather than replacing it. Once the list is full the
    // oldest entry drops off the top and the rest shift up.
    readonly List<string> _captionHistory = new List<string>();

    const float BlueHue = 0.60f;

    // Opacity for de-emphasized elements after the top-15 are selected.
    const float DimNodeAlpha = 0.15f;
    const float DimLinkAlpha = 0.03f;
    // Links that touch a selected node stay visible (but under the fully opaque
    // orange/green highlights of steps 2-3).
    const float FocusLinkAlpha = 0.50f;

    // Hex is the source of truth: the same string colors the geometry and labels
    // the legend, so the two can't drift apart.
    const string RedHex    = "#FF0000";   // matches the legend's red palette slot
    const string OrangeHex = "#D67229";
    const string GreenHex  = "#3B8132";

    static readonly Color RedAggressor     = ParseHex(RedHex);
    static readonly Color OrangeAggression = ParseHex(OrangeHex);
    static readonly Color GreenFriendship  = ParseHex(GreenHex);

    static Color ParseHex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        return c;
    }

    // The spoken script for each step. These are for the audience, not the engine —
    // they read as things you'd say out loud, so numerals are spelled out and the
    // measured values live in the result line underneath. A step can say more than
    // one thing (an action, then a question about it).
    // One sentence per step — each Enter press is exactly one command.
    static readonly string[] StepCommands =
    {
        "Select the fifteen students who bully the most and color them red.",
        "What is their average number of friends?",
        "Encode gender with shapes.",
        "Show the aggression links of the selected students in orange.",
        "Show their friendship links in green.",
    };

    void Start()
    {
        // Null-safe: a missing Network Manager used to throw here, which aborted the
        // rest of Start() — including the panel wiring — and left the command log
        // silently dead for the whole session.
        if (_networkManager == null)
            _networkManager = GameObject.Find("/Network Manager")?.GetComponent<NetworkManager>();
        if (_databaseStorage == null)
            _databaseStorage = GameObject.Find("/Database")?.GetComponent<DatabaseStorage>();

        if (_networkManager == null)
            Debug.LogError("[DemoSequence] No NetworkManager found — the demo cannot run.");

        ResolveUiPanels();

        // Console only — don't seed the command log with a key hint.
        Debug.Log($"[DemoSequence] Ready — {_startDemoKey} to start, Enter for each step, "
                + $"{_friendshipDegreeKey}/{_aggressionDegreeKey} for the degree demos.");
    }

    void Update()
    {
        // ── Status Struggle setup ─────────────────────────────────────────────
        if (Input.GetKeyDown(_startDemoKey))
        {
            _step = 0;
            ClearCaptions();   // fresh run — don't leave the previous one on screen
            ShowCommand("Show me all the students");
            StartDemoSession("Status Struggle");
            SelectAllNodes();
            return;
        }

        // ── Standalone saturation demos ───────────────────────────────────────
        if (Input.GetKeyDown(_friendshipDegreeKey))
        {
            ClearCaptions();
            ShowCommand("Color the students by how many friends they have");
            StartSaturationDemo("friendship", "Friendship Degree");
            return;
        }

        if (Input.GetKeyDown(_aggressionDegreeKey))
        {
            ClearCaptions();
            ShowCommand("Color the students by how aggressive they are");
            StartSaturationDemo("aggression", "Aggression Degree");
            return;
        }

        // ── Enter: advance B-demo steps ───────────────────────────────────────
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (_step < 0 || _demoSubnID < 0) { Debug.Log("[DemoSequence] Press B first."); return; }

            string command = StepCommands[_step];
            Debug.Log($"[Status Struggle] ── {command} ──");

            // Run first: a question's answer doesn't exist until the step computes
            // it, and a question and its answer share one caption, so it can only
            // be written once both halves are known.
            _pendingAnswer = null;
            RunStep();

            string caption = IsQuestion(command) ? $"Q: {command}" : command;
            if (!string.IsNullOrEmpty(_pendingAnswer)) caption += $"\nA: {_pendingAnswer}";

            ShowCommand(caption);

            _step++;
            if (_step >= StepCommands.Length)
            {
                _step = -1;
                Debug.Log("[Status Struggle] ══ Demo complete. Press B to restart. ══");
            }
        }
    }

    void RunStep()
    {
        switch (_step)
        {
            case 0: SelectTopAggressorsRed(); break;
            case 1: AnswerAverageFriends(); break;
            case 2: EncodeGenderWithShapes(); break;
            case 3: ShowAggressionLinksOrange(); break;
            case 4: ColorFriendshipLinksGreen(); break;
        }
    }

    // ─── Step 1: top-15 aggressors → red; log avg friendship degree ───────────
    // Non-aggressor nodes are swapped onto a transparent material and faded to a
    // ghost gray; all links drop to DimLinkAlpha so the top-15 visually pop.

    void SelectTopAggressorsRed()
    {
        var global = _networkManager.NetworkGlobal;

        var aggrOut = ComputeOutgoingDegree("aggression");
        _bulliesIDs = aggrOut
            .OrderByDescending(kv => kv.Value)
            .Take(15)
            .Select(kv => kv.Key)
            .ToList();

        Debug.Log($"[Status Struggle] Top-15 aggression counts: {string.Join(", ", _bulliesIDs.Select(id => aggrOut[id]))}");

        // Bucket every link incident to the top-15 by type in one pass — steps 2/3
        // read _aggrLinkIDs/_friendLinkIDs back out instead of re-walking the graph.
        var (bullyLinkIDs, aggrLinkIDs, friendLinkIDs) = BucketLinksByType(_bulliesIDs);
        _aggrLinkIDs = aggrLinkIDs;
        _friendLinkIDs = friendLinkIDs;

        // Fade every link into the background, then bring the links that touch a
        // selected (top-15) node back up so their connections stay readable.
        var allLinkIDs  = _networkManager.GetCurWorkingSubgraphLinkIDs().ToList();
        Color linkColor = _networkManager.GetSubgraphLinkDefaultColor(_demoSubnID);
        _networkManager.SetMLLinksColorDirect(allLinkIDs, linkColor, linkColor, _demoSubnID, alpha: DimLinkAlpha);
        _networkManager.SetMLLinksColorDirect(bullyLinkIDs, linkColor, linkColor, _demoSubnID, alpha: FocusLinkAlpha);

        // Nodes directly connected to a top-15 aggressor (via the links just
        // brought back up) stay at full opacity too — only nodes with no visible
        // connection to the top-15 get faded.
        var bulliesSet = new HashSet<int>(_bulliesIDs);
        var neighborSet = bullyLinkIDs
            .SelectMany(linkID => new[] { global.Links[linkID].SourceNodeID, global.Links[linkID].TargetNodeID })
            .Where(id => !bulliesSet.Contains(id))
            .ToHashSet();

        var fadedNodes = global.RealNodes
            .Where(id => !bulliesSet.Contains(id) && !neighborSet.Contains(id))
            .ToList();
        var dimColor = new Color(0.70f, 0.70f, 0.70f, DimNodeAlpha);
        _networkManager.SetMLNodesColorDirect(fadedNodes, dimColor, _demoSubnID);
        _networkManager.SetMLNodesTransparent(fadedNodes, true, _demoSubnID);

        Debug.Log($"[Status Struggle] {fadedNodes.Count} nodes faded, {neighborSet.Count} neighbors kept visible.");

        // Top-15: vivid red, selected, opaque.
        _networkManager.SetSelectedNodes(_bulliesIDs, true, _demoSubnID);
        _networkManager.SetMLNodesColorDirect(_bulliesIDs, RedAggressor, _demoSubnID);
        _legend?.SetNodeColorLabel(RedHex, "Most aggressive students");
        _networkManager.ClearSelection();
    }

    // ─── Step 2: answer the friendship-average question ───────────────────────
    // No visual change — this step exists purely to answer, so the question gets
    // its own beat in the walkthrough.

    void AnswerAverageFriends()
    {
        var global = _networkManager.NetworkGlobal;

        // Sum of undirected degrees = 2 * edge count, so the school-wide average
        // follows directly from the degree map.
        var friendDegree = ComputeUndirectedDegree("friendship");
        float schoolAvg  = global.RealNodes.Count > 0
            ? (float)friendDegree.Values.Sum() / global.RealNodes.Count
            : 0f;
        float bulliesAvg = _bulliesIDs.Count > 0
            ? (float)_bulliesIDs.Sum(id => friendDegree.TryGetValue(id, out var d) ? d : 0) / _bulliesIDs.Count
            : 0f;

        // Kept to one short line — it shares the caption with the question, and the
        // full interpretation goes to the console for narration instead.
        float ratio = schoolAvg > 0f ? bulliesAvg / schoolAvg : 0f;
        _pendingAnswer = $"{bulliesAvg:F1} vs {schoolAvg:F1} school-wide"
                       + (ratio > 1f ? $" — {Mathf.RoundToInt((ratio - 1f) * 100f)}% higher." : ".");

        ReportFinding($"{bulliesAvg:F1} friends on average against {schoolAvg:F1} school-wide. "
                    + "The red students sit in the dense core, not the fringe: aggression here is "
                    + "status competition, not marginality. (Faris & Felmlee 2011, \"Status Struggles.\")");
    }

    // ─── Step 3: shape = gender ────────────────────────────────────────────────
    // Sphere encodes female students, cube encodes male students.

    void EncodeGenderWithShapes()
    {
        // Read the sex attribute straight from the loaded network file (same path
        // the degree computations use) — no DB round-trip, no GUID translation.
        // Node IDs are shared with the subgraph, so we target _demoSubnID directly.
        var global    = _networkManager.NetworkGlobal;
        var fileNodes = _networkManager.FileLoader.SphericalLayout.nodes;

        var femaleIDs = new List<int>();
        var maleIDs   = new List<int>();
        var seenValues = new HashSet<string>();

        foreach (var id in global.RealNodes)
        {
            string raw = fileNodes[global.Nodes[id].IdxProcessed].props?.sex;
            if (!string.IsNullOrEmpty(raw)) seenValues.Add(raw);

            switch (SexOf(id))
            {
                case 'f': femaleIDs.Add(id); break;
                case 'm': maleIDs.Add(id);   break;
            }
        }

        Debug.Log($"[Status Struggle] sex values in file: [{string.Join(", ", seenValues)}]");

        _networkManager.SetMLNodesShape(femaleIDs, "sphere", _demoSubnID);
        _networkManager.SetMLNodesShape(maleIDs, "cube", _demoSubnID);

        // Legend shape slots are ordered [sphere, cube, triangle].
        _legend?.SetShapeMapping(new[] { "Girls", "Boys" });

        // Gender rides on shape rather than color so it stays legible under the red
        // aggressor highlight — a cross-gender act is then one glance: red sphere → cube.
        ReportFinding($"Spheres are girls ({femaleIDs.Count}), cubes are boys ({maleIDs.Count}). "
                    + "Gender is on shape, not color, so it survives the red highlight.");
    }

    // ─── Step 3: aggression links of the top-15 → orange ──────────────────────

    void ShowAggressionLinksOrange()
    {
        // Bucketed back in step 1 — orange at uniform full opacity.
        _networkManager.SetMLLinksColorDirect(_aggrLinkIDs, OrangeAggression, OrangeAggression, _demoSubnID, alpha: 1f);
        _legend?.SetEdgeColorLabel(OrangeHex, "Aggression");
        Debug.Log($"[Status Struggle] {_aggrLinkIDs.Count} aggression links → orange.");

        string finding = "";
        float crossAggr   = CrossGenderShare("aggression");
        float crossFriend = CrossGenderShare("friendship");
        if (crossAggr >= 0f && crossFriend >= 0f)
        {
            finding = $"{crossAggr:F0}% of aggression crosses gender, against only "
                    + $"{crossFriend:F0}% of friendships — ";
        }

        ReportFinding(finding + "aggression breaches the gender boundary that ordinary "
                    + "affiliation respects. (Faris & Felmlee 2011, gender segregation.)");
    }

    // ─── Step 3: friendship links of top-15 → green ───────────────────────────
    // Resets all links to default gray first, then re-applies orange (aggression)
    // and overlays green (friendship) for a clean cumulative view.

    void ColorFriendshipLinksGreen()
    {
        var allSubgraphLinkIDs = _networkManager.GetCurWorkingSubgraphLinkIDs().ToList();

        Color gray = _networkManager.GetSubgraphLinkDefaultColor(_demoSubnID);

        // Uniform alphas: background links stay faded, highlighted links get full
        // opacity — otherwise per-link Alpha differences inherited from the main
        // network make identical colors render as different shades.
        _networkManager.SetMLLinksColorDirect(allSubgraphLinkIDs, gray, gray, _demoSubnID, alpha: DimLinkAlpha);
        if (_aggrLinkIDs.Count > 0)
            _networkManager.SetMLLinksColorDirect(_aggrLinkIDs, OrangeAggression, OrangeAggression, _demoSubnID, alpha: 1f);
        if (_friendLinkIDs.Count > 0)
            _networkManager.SetMLLinksColorDirect(_friendLinkIDs, GreenFriendship, GreenFriendship, _demoSubnID, alpha: 1f);

        _legend?.SetEdgeColorLabel(GreenHex, "Friendship");

        _networkManager.ClearSelection();

        Debug.Log($"[Status Struggle] Links: {allSubgraphLinkIDs.Count} reset to default, {_aggrLinkIDs.Count} orange (aggression), {_friendLinkIDs.Count} green (friendship).");

        // Green and orange occupy the same region and partly coincide — the point
        // of the overlay is that aggression runs between structural equals.
        string finding = "The green friendship web and the orange aggression links share the same region";

        float alongFriendship = AggressionAlongFriendshipShare();
        if (alongFriendship >= 0f)
            finding += $", and partly coincide: {alongFriendship:F0}% of aggressive acts run "
                     + "along a declared friendship tie";

        float sharedRatio = SharedFriendRatio();
        if (sharedRatio > 0f)
            finding += $", and aggressor and victim share friends at {sharedRatio:F1} times "
                     + "the rate of a random pair";

        ReportFinding(finding + ". Aggression arises from amity and equivalence — between peers "
                    + "who move in the same circles. (Faris, Felmlee & McMillan 2020, \"With Friends Like These.\")");
    }

    // ─── Standalone saturation demos ──────────────────────────────────────────

    void StartSaturationDemo(string linkType, string sessionName)
    {
        _step = -1;
        StartDemoSession(sessionName);
        ColorBySaturation(linkType, sessionName);
        SelectAllNodes();
    }

    // Blue-saturation encoding: low degree → near-gray (sat≈0.05), high → vivid (sat≈0.90).
    // Friendship uses undirected degree; aggression uses outgoing degree.

    void ColorBySaturation(string linkType, string label)
    {
        var counts = linkType == "friendship"
            ? ComputeUndirectedDegree(linkType)
            : ComputeOutgoingDegree(linkType);

        int minC  = counts.Values.DefaultIfEmpty(0).Min();
        int maxC  = counts.Values.DefaultIfEmpty(0).Max();
        int range = maxC - minC;

        // Every node gets its own color, so build the whole batch and hand it to
        // NetworkManager in one call — a single render update for all of them,
        // rather than one render pass per node.
        var nodeColors = new Dictionary<int, Color>(counts.Count);
        foreach (var (id, cnt) in counts)
        {
            float t   = range > 0 ? (float)(cnt - minC) / range : 0f;
            float sat = Mathf.Lerp(0.05f, 0.90f, t);
            float val = Mathf.Lerp(0.60f, 0.90f, t);
            nodeColors[id] = Color.HSVToRGB(BlueHue, sat, val);
        }
        _networkManager.SetMLNodesColorDirect(nodeColors, _demoSubnID);

        // Sample the same ramp at five stops so the legend swatch matches the nodes.
        var ramp = new string[5];
        for (int i = 0; i < ramp.Length; i++)
        {
            float t = (float)i / (ramp.Length - 1);
            var c = Color.HSVToRGB(BlueHue, Mathf.Lerp(0.05f, 0.90f, t), Mathf.Lerp(0.60f, 0.90f, t));
            ramp[i] = "#" + ColorUtility.ToHtmlStringRGB(c);
        }
        _legend?.SetNodeGradient(label, ramp);

        ReportFinding($"{counts.Count} students, from {minC} to {maxC} connections each. "
                    + "The brighter the blue, the more connected.");
        Debug.Log($"[DemoSequence] {label}: {counts.Count} nodes, degree range {minC}–{maxC}.");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    // Wipes any existing session and opens a fresh working subgraph over every
    // real node, under the given session/frame name. Shared by the B-demo and
    // the standalone key-1/key-2 saturation demos.
    int StartDemoSession(string sessionName)
    {
        _bulliesIDs.Clear();
        _aggrLinkIDs.Clear();
        _friendLinkIDs.Clear();

        _networkManager.ResetAll();
        _legend?.ResetAll();   // the visuals are gone, so the legend shouldn't still describe them

        var global = _networkManager.NetworkGlobal;
        _networkManager.CreateWorkingSubgraph(global.RealNodes, sessionName, sessionName);
        _demoSubnID = _networkManager.CurWorkingSubgraphID;

        Debug.Log($"[DemoSequence] {sessionName}: subgraph ID={_demoSubnID}, {global.RealNodes.Count} nodes.");
        return _demoSubnID;
    }

    void SelectAllNodes()
    {
        var realNodes = _networkManager.NetworkGlobal.RealNodes;
        _networkManager.SetSelectedNodes(realNodes, true, _demoSubnID);
        Debug.Log($"[DemoSequence] Selected {realNodes.Count} nodes.");
    }

    // Outgoing (directed) degree per real node for one file-data link type.
    Dictionary<int, int> ComputeOutgoingDegree(string linkType)
    {
        var global    = _networkManager.NetworkGlobal;
        var fileLinks = _networkManager.FileLoader.SphericalLayout.links;

        var counts = new Dictionary<int, int>();
        foreach (var id in global.RealNodes) counts[id] = 0;

        foreach (var id in global.RealNodes)
        {
            if (!global.NodeLinkMatrixDir.TryGetValue(id, out var links)) continue;
            foreach (var link in links)
                if (fileLinks[link.IdxProcessed].props?.type == linkType) counts[id]++;
        }
        return counts;
    }

    // Undirected degree per real node for one file-data link type (each edge
    // increments both endpoints).
    Dictionary<int, int> ComputeUndirectedDegree(string linkType)
    {
        var global    = _networkManager.NetworkGlobal;
        var fileLinks = _networkManager.FileLoader.SphericalLayout.links;

        var counts = new Dictionary<int, int>();
        foreach (var id in global.RealNodes) counts[id] = 0;

        foreach (var (_, link) in global.Links)
        {
            if (fileLinks[link.IdxProcessed].props?.type != linkType) continue;
            if (counts.ContainsKey(link.SourceNodeID)) counts[link.SourceNodeID]++;
            if (counts.ContainsKey(link.TargetNodeID)) counts[link.TargetNodeID]++;
        }
        return counts;
    }

    // Walks every link incident to nodeIDs once and buckets the IDs by file-data
    // link type, so callers don't each re-walk NodeLinkMatrixUndir separately.
    (List<int> all, List<int> aggression, List<int> friendship) BucketLinksByType(List<int> nodeIDs)
    {
        var global    = _networkManager.NetworkGlobal;
        var fileLinks = _networkManager.FileLoader.SphericalLayout.links;

        var all = new List<int>();
        var aggression = new List<int>();
        var friendship = new List<int>();
        var seen = new HashSet<int>();

        foreach (var id in nodeIDs)
        {
            if (!global.NodeLinkMatrixUndir.TryGetValue(id, out var links)) continue;
            foreach (var link in links)
            {
                if (!seen.Add(link.ID)) continue;
                all.Add(link.ID);
                switch (fileLinks[link.IdxProcessed].props?.type)
                {
                    case "aggression": aggression.Add(link.ID); break;
                    case "friendship": friendship.Add(link.ID); break;
                }
            }
        }
        return (all, aggression, friendship);
    }

    // Reuse the command log and legend the voice/keyboard demos already drive, so
    // demo entries inherit their existing anchoring, layout, and palette instead of
    // needing their own. Inspector assignments always win; anything left empty is
    // borrowed from whichever script owns it (searching inactive objects too, since
    // the owner may not be enabled when this runs).
    void ResolveUiPanels()
    {
        // The scene holds more than one of these components and not all of them have
        // the panel wired up, so every instance is checked and each field is taken
        // from the first one that actually has it — picking a single "best" object
        // would land on the one with null references.
        foreach (var mic in FindObjectsOfType<StreamingSampleMic>(true))
        {
            if (_commandPrefab == null) _commandPrefab = mic.command_prefab;
            if (_commandParent == null) _commandParent = mic.command_parent;
            if (_commandScroll == null) _commandScroll = mic.scroll;
            if (_legend == null) _legend = mic.legendManager;
        }

        foreach (var tester in FindObjectsOfType<KeyboardCommandTester>(true))
        {
            if (_commandPrefab == null) _commandPrefab = tester.command_prefab;
            if (_commandParent == null) _commandParent = tester.command_parent;
            if (_commandScroll == null) _commandScroll = tester.scroll;
            if (_legend == null) _legend = tester.legendManager;
        }

        if (_legend == null)
            Debug.LogWarning("[DemoSequence] No LegendManager found — the legend won't update. "
                           + "Assign Legend on the component.");

        if (_commandPrefab == null || _commandParent == null)
            Debug.LogWarning("[DemoSequence] No command panel found — captions will only go to the console. "
                           + "Assign Command Prefab / Command Parent on the component.");
        else
            Debug.Log($"[DemoSequence] Captioning into '{_commandParent.name}' "
                    + $"(prefab '{_commandPrefab.name}', {_commandParent.transform.childCount} existing entries).");
    }

    // ─── Narrative statistics ─────────────────────────────────────────────────
    // These are measured from the loaded dataset rather than quoted, so the demo
    // reports what this network actually shows. Each returns -1 when the data
    // needed isn't present, and the caller then drops that clause rather than
    // printing a made-up figure.

    // 'f' / 'm', or '\0' when the node has no usable sex recorded.
    char SexOf(int nodeID)
    {
        var global = _networkManager.NetworkGlobal;
        var fileNodes = _networkManager.FileLoader.SphericalLayout.nodes;

        // NodeCollection indexes by node ID, but only IdToIndex can be probed
        // safely — the indexer throws on an unknown ID.
        if (!global.Nodes.IdToIndex.TryGetValue(nodeID, out int idx)) return '\0';

        string sex = fileNodes[global.Nodes.NodeArray[idx].IdxProcessed].props?.sex;
        if (string.IsNullOrEmpty(sex)) return '\0';

        char first = char.ToLowerInvariant(sex[0]);
        if (first == 'f' || first == 'g' || first == 'w') return 'f';   // female / girl / woman
        if (first == 'm' || first == 'b') return 'm';                   // male / boy
        return '\0';
    }

    // Percentage of links of this type whose endpoints differ in recorded sex.
    // Links missing sex on either end are excluded from both numerator and
    // denominator instead of being counted as same-gender.
    float CrossGenderShare(string linkType)
    {
        var global = _networkManager.NetworkGlobal;
        var fileLinks = _networkManager.FileLoader.SphericalLayout.links;

        int cross = 0, total = 0;
        foreach (var (_, link) in global.Links)
        {
            if (fileLinks[link.IdxProcessed].props?.type != linkType) continue;

            char a = SexOf(link.SourceNodeID), b = SexOf(link.TargetNodeID);
            if (a == '\0' || b == '\0') continue;

            total++;
            if (a != b) cross++;
        }

        return total > 0 ? 100f * cross / total : -1f;
    }

    // Percentage of aggression links whose two students are also declared friends.
    float AggressionAlongFriendshipShare()
    {
        var global = _networkManager.NetworkGlobal;
        var fileLinks = _networkManager.FileLoader.SphericalLayout.links;

        var friendPairs = new HashSet<(int, int)>();
        foreach (var (_, link) in global.Links)
            if (fileLinks[link.IdxProcessed].props?.type == "friendship")
                friendPairs.Add(UnorderedPair(link.SourceNodeID, link.TargetNodeID));

        int along = 0, total = 0;
        foreach (var (_, link) in global.Links)
        {
            if (fileLinks[link.IdxProcessed].props?.type != "aggression") continue;

            total++;
            if (friendPairs.Contains(UnorderedPair(link.SourceNodeID, link.TargetNodeID))) along++;
        }

        return total > 0 ? 100f * along / total : -1f;
    }

    // How much more often an aggressor and their victim share friends than two
    // students picked at random. The baseline is exact rather than sampled: a
    // student with d friends is the mutual friend of exactly C(d,2) pairs, so the
    // mean over all pairs is sum(C(d,2)) / C(n,2) — one pass, no random draws.
    float SharedFriendRatio()
    {
        var global = _networkManager.NetworkGlobal;
        var fileLinks = _networkManager.FileLoader.SphericalLayout.links;

        var friends = new Dictionary<int, HashSet<int>>();
        foreach (var (_, link) in global.Links)
        {
            if (fileLinks[link.IdxProcessed].props?.type != "friendship") continue;
            Befriend(friends, link.SourceNodeID, link.TargetNodeID);
            Befriend(friends, link.TargetNodeID, link.SourceNodeID);
        }

        long n = global.RealNodes.Count;
        if (n < 2) return -1f;

        double totalCommon = 0;
        foreach (var id in global.RealNodes)
        {
            long d = friends.TryGetValue(id, out var set) ? set.Count : 0;
            totalCommon += d * (d - 1) / 2.0;
        }

        double baseline = totalCommon / (n * (n - 1) / 2.0);
        if (baseline <= 0) return -1f;

        var counted = new HashSet<(int, int)>();
        double sharedTotal = 0;
        int pairs = 0;

        foreach (var (_, link) in global.Links)
        {
            if (fileLinks[link.IdxProcessed].props?.type != "aggression") continue;
            if (!counted.Add(UnorderedPair(link.SourceNodeID, link.TargetNodeID))) continue;

            int shared = 0;
            if (friends.TryGetValue(link.SourceNodeID, out var a)
                && friends.TryGetValue(link.TargetNodeID, out var b))
            {
                foreach (var mutual in a)
                    if (b.Contains(mutual)) shared++;
            }

            sharedTotal += shared;
            pairs++;
        }

        return pairs > 0 ? (float)(sharedTotal / pairs / baseline) : -1f;
    }

    static void Befriend(Dictionary<int, HashSet<int>> friends, int of, int with)
    {
        if (!friends.TryGetValue(of, out var set)) friends[of] = set = new HashSet<int>();
        set.Add(with);
    }

    // Friendship is undirected but aggression is directed, so pairs are normalised
    // before comparison — otherwise A→B aggression would miss a B–A friendship.
    static (int, int) UnorderedPair(int a, int b) => a < b ? (a, b) : (b, a);

    // Appends this step's caption underneath everything said so far, so the one
    // label builds up a running history — new text under the old, chatroom style.
    // Writing into the label that's already visible avoids the scroll-view clipping
    // that swallowed separately instantiated entries.
    void ShowCommand(string text)
    {
        _captionHistory.Add(text);

        // Past the limit the first entry is dropped; rebuilding from the trimmed
        // list is what makes the remaining ones appear to shift up.
        while (_captionHistory.Count > _maxVisibleCommands) _captionHistory.RemoveAt(0);

        if (_stepLabel == null) ResolveStepLabel();

        // One newline holds a Q and its A together; the extra blank lines separate
        // one command from the next.
        string separator = "\n" + new string('\n', _blankLinesBetweenCommands);
        if (_stepLabel != null) _stepLabel.text = string.Join(separator, _captionHistory);
        else Debug.LogWarning("[DemoSequence] No text object to write into — assign Step Label "
                            + "on the DemoSequence component.");

        if (_alsoPostToCommandLog) PostToCommandLog(text);
    }

    // Finds the text object to write into when none was assigned: first anything
    // named like the panel's runtime text objects, then any text already living
    // inside the command panel. Logs its full path so it's obvious which object
    // the demo is driving.
    void ResolveStepLabel()
    {
        foreach (var candidate in FindObjectsOfType<TextMeshProUGUI>(true))
        {
            if (!candidate.name.Contains("New Text Instance")) continue;
            _stepLabel = candidate;
            break;
        }

        if (_stepLabel == null && _commandParent != null)
            _stepLabel = _commandParent.GetComponentInChildren<TextMeshProUGUI>(true);

        if (_stepLabel != null)
            Debug.Log($"[DemoSequence] Writing captions into '{PathOf(_stepLabel.transform)}'.");
    }

    static string PathOf(Transform t)
    {
        var path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }

    // Starts the running history over — called when a demo (re)starts, so the panel
    // doesn't still show the previous run.
    void ClearCaptions()
    {
        _captionHistory.Clear();
        if (_stepLabel != null) _stepLabel.text = "";
    }

    static bool IsQuestion(string command) => command.TrimEnd().EndsWith("?");

    // Findings deliberately stay OUT of the command history — that panel shows only
    // the spoken commands. They go to the console so the presenter has the exact
    // measured numbers to narrate from.
    void ReportFinding(string text) => Debug.Log($"[Status Struggle] → {text}");

    // Appends one entry to the shared command log, chat-style: each message is added
    // below the previous one and the view scrolls so the newest is visible, pushing
    // older messages up out of frame. Nothing is overwritten or cleared.
    void PostToCommandLog(string text)
    {
        // Resolve on demand as well as in Start(): if Start() ran before the panel's
        // owner was awake — or bailed early — the refs would otherwise stay null for
        // the whole session and every entry would be silently dropped.
        if (_commandPrefab == null || _commandParent == null) ResolveUiPanels();

        if (_commandPrefab == null || _commandParent == null)
        {
            Debug.LogWarning($"[DemoSequence] No command panel — dropping entry: {text}");
            return;
        }

        // worldPositionStays: false — keeps the prefab's own anchors/offsets/scale
        // relative to the panel instead of re-deriving them from world space, which
        // is what makes the entry land where the panel's layout expects it.
        var msgObj = Instantiate(_commandPrefab, _commandParent.transform, false);
        msgObj.transform.SetAsLastSibling();   // newest entry at the bottom of the history
        msgObj.SetActive(true);                // prefab may ship disabled as a template

        var tmp = msgObj.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.text = text;
        else Debug.LogWarning("[DemoSequence] Command prefab has no TMP_Text — entry will be blank.");

        Debug.Log($"[DemoSequence] Command log now has {_commandParent.transform.childCount} entries.");
        ScrollToNewest();
    }

    // Scrolling has to wait a frame: the layout group / size fitter hasn't measured
    // the entry that was just added, so scrolling now would clamp against a stale
    // content height and stop short of the newest message.
    void ScrollToNewest()
    {
        if (_commandScroll == null || !isActiveAndEnabled) return;

        if (_scrollRoutine != null) StopCoroutine(_scrollRoutine);
        _scrollRoutine = StartCoroutine(ScrollToBottomNextFrame());
    }

    IEnumerator ScrollToBottomNextFrame()
    {
        yield return null;

        if (_commandParent != null && _commandParent.transform is RectTransform contentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

        Canvas.ForceUpdateCanvases();
        _commandScroll.verticalNormalizedPosition = 0f;   // 0 == bottom == newest
        _scrollRoutine = null;
    }
}
