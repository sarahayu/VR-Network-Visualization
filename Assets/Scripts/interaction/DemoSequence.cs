/*
 * DemoSequence — keyboard-driven showcase using the working-subgraph system.
 *
 *   B      "Status Struggle" case study.
 *          Wipes all sessions, creates a fresh working subgraph from ALL real nodes
 *          in force-directed layout. No initial encoding — let the layout speak.
 *
 *   Enter 1  Top-15 aggressors (most outgoing aggression links) → selected + red.
 *             Shows their avg friendship degree vs school-wide avg on the label.
 *             All other nodes become transparent ghosts; all links fade out.
 *
 *   Enter 2  Encode sex with shapes: sphere = girl, cube = boy.
 *             Color aggression links of the top-15 in orange.
 *
 *   Enter 3  Color friendship links of the top-15 in green.
 *             (Aggression orange and shapes remain — cumulative layering.)
 *
 *   1      Standalone: new subgraph, all nodes colored by friendship degree (blue saturation).
 *   2      Standalone: new subgraph, all nodes colored by aggression degree (blue saturation).
 *
 * Requires: NetworkManager at "/Network Manager", DatabaseStorage at "/Database"
 */

using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using VidiGraph;

public class DemoSequence : MonoBehaviour
{
    [SerializeField] NetworkManager _networkManager;
    [SerializeField] DatabaseStorage _databaseStorage;
    [SerializeField] TextMeshProUGUI _stepLabel;

    int _step = -1;
    int _demoSubnID = -1;
    List<int> _bulliesIDs = new List<int>();
    List<int> _aggrLinkIDs = new List<int>();
    List<int> _friendLinkIDs = new List<int>();
    string _lastResult = "";   // finding from the step that just ran, shown on the label

    const float BlueHue = 0.60f;

    // Opacity for de-emphasized elements after the top-15 are selected.
    const float DimNodeAlpha = 0.15f;
    const float DimLinkAlpha = 0.03f;
    // Links that touch a selected node stay visible (but under the fully opaque
    // orange/green highlights of steps 2-3).
    const float FocusLinkAlpha = 0.50f;

    static readonly Color OrangeAggression = ParseHex("#D67229");
    static readonly Color GreenFriendship  = ParseHex("#3B8132");

    static Color ParseHex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        return c;
    }

    static readonly string[] StepLabels =
    {
        "Step 1 / 3 — Top-15 aggressors → red; others dimmed",
        "Step 2 / 3 — Encode gender (sphere=girl, cube=boy) + aggression links orange",
        "Step 3 / 3 — Friendship links of top-15 → green",
    };

    void Start()
    {
        if (_networkManager == null)
            _networkManager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
        if (_databaseStorage == null)
            _databaseStorage = GameObject.Find("/Database")?.GetComponent<DatabaseStorage>();

        ShowLabel("Press B — Status Struggle demo");
        Debug.Log("[DemoSequence] Ready — press B to start, Enter for each step.");
    }

    void Update()
    {
        // ── B: Status Struggle setup ───────────────────────────────────────────
        if (Input.GetKeyDown(KeyCode.B))
        {
            _step = 0;
            StartDemoSession("Status Struggle");
            SelectAllNodes();
            ShowLabel("Press Enter — " + StepLabels[0]);
            return;
        }

        // ── Key 1 / Key 2: standalone saturation demos ─────────────────────────
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            StartSaturationDemo("friendship", "Friendship Degree", "Demo 1 — friendship degree");
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            StartSaturationDemo("aggression", "Aggression Degree", "Demo 2 — aggression degree");
            return;
        }

        // ── Enter: advance B-demo steps ───────────────────────────────────────
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (_step < 0 || _demoSubnID < 0) { Debug.Log("[DemoSequence] Press B first."); return; }

            Debug.Log($"[Status Struggle] ── {StepLabels[_step]} ──");
            _lastResult = "";
            RunStep();

            _step++;
            string resultLine = string.IsNullOrEmpty(_lastResult) ? "" : _lastResult + "\n";
            if (_step >= StepLabels.Length)
            {
                _step = -1;
                ShowLabel(resultLine + "Demo complete — press B to restart");
                Debug.Log("[Status Struggle] ══ Demo complete. Press B to restart. ══");
            }
            else
            {
                ShowLabel(resultLine + "Press Enter — " + StepLabels[_step]);
                Debug.Log($"[Status Struggle] → Next: {StepLabels[_step]}");
            }
        }
    }

    void RunStep()
    {
        switch (_step)
        {
            case 0: SelectTopAggressorsRed(); break;
            case 1: EncodeGenderAndAggressionLinks(); break;
            case 2: ColorFriendshipLinksGreen(); break;
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

        // Compare against school-wide friendship degree. Sum of undirected degrees
        // = 2 * edge count, so the average follows directly from the degree map.
        var friendDegree = ComputeUndirectedDegree("friendship");
        float schoolAvg  = global.RealNodes.Count > 0 ? (float)friendDegree.Values.Sum() / global.RealNodes.Count : 0f;
        float bulliesAvg = _bulliesIDs.Count > 0
            ? (float)_bulliesIDs.Sum(id => friendDegree.TryGetValue(id, out var d) ? d : 0) / _bulliesIDs.Count
            : 0f;

        _lastResult = $"Top-15 avg friends: {bulliesAvg:F1} vs school-wide: {schoolAvg:F1}";
        Debug.Log($"[Status Struggle] {_lastResult}");
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
        _networkManager.SetMLNodesColorDirect(_bulliesIDs, new Color(1f, 0.067f, 0.067f, 1f), _demoSubnID);
        _networkManager.ClearSelection();
    }

    // ─── Step 2: shape = gender; aggression links = orange ─────────────────────
    // Sphere encodes female students, cube encodes male students.
    // Aggression links of the top-15 are colored orange.

    void EncodeGenderAndAggressionLinks()
    {
        // Encode sex via shapes. Read the sex attribute straight from the loaded
        // network file (same path the degree computations use) — no DB round-trip,
        // no GUID translation. Node IDs are shared with the subgraph, so we target
        // _demoSubnID directly.
        var global    = _networkManager.NetworkGlobal;
        var fileNodes = _networkManager.FileLoader.SphericalLayout.nodes;

        var femaleIDs = new List<int>();
        var maleIDs   = new List<int>();
        var seenValues = new HashSet<string>();

        foreach (var id in global.RealNodes)
        {
            string sex = fileNodes[global.Nodes[id].IdxProcessed].props?.sex;
            if (string.IsNullOrEmpty(sex)) continue;
            seenValues.Add(sex);

            char first = char.ToLowerInvariant(sex[0]);
            if (first == 'f' || first == 'g' || first == 'w') femaleIDs.Add(id);       // female / girl / woman
            else if (first == 'm' || first == 'b')            maleIDs.Add(id);         // male / boy
        }

        Debug.Log($"[Status Struggle] sex values in file: [{string.Join(", ", seenValues)}]");

        _networkManager.SetMLNodesShape(femaleIDs, "sphere", _demoSubnID);
        _networkManager.SetMLNodesShape(maleIDs, "cube", _demoSubnID);
        Debug.Log($"[Status Struggle] Shapes: {femaleIDs.Count} girls → sphere, {maleIDs.Count} boys → cube.");

        // Aggression links of the top-15 (bucketed back in step 1) → orange at
        // uniform full opacity.
        _networkManager.SetMLLinksColorDirect(_aggrLinkIDs, OrangeAggression, OrangeAggression, _demoSubnID, alpha: 1f);
        Debug.Log($"[Status Struggle] {_aggrLinkIDs.Count} aggression links → orange.");

        _lastResult = $"Sphere = girl ({femaleIDs.Count}), cube = boy ({maleIDs.Count}); {_aggrLinkIDs.Count} aggression links orange";
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

        _networkManager.ClearSelection();
        _lastResult = $"{_friendLinkIDs.Count} friendship links green, {_aggrLinkIDs.Count} aggression links orange";
        Debug.Log($"[Status Struggle] Links: {allSubgraphLinkIDs.Count} reset to default, {_aggrLinkIDs.Count} orange (aggression), {_friendLinkIDs.Count} green (friendship).");
    }

    // ─── Key 1 / Key 2: standalone saturation demos ───────────────────────────

    void StartSaturationDemo(string linkType, string sessionName, string label)
    {
        _step = -1;
        StartDemoSession(sessionName);
        ColorBySaturation(linkType, label);
        SelectAllNodes();
        ShowLabel(label);
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

    void ShowLabel(string text)
    {
        if (_stepLabel != null) _stepLabel.text = text;
    }
}
