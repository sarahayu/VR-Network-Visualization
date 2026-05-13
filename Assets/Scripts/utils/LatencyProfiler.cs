/*
 * LatencyProfiler — press I in Play mode to run all latency benchmarks.
 *
 * Measures:
 *   1. Neo4j query latency for every DatabaseStorage method (3 runs → min/avg/max)
 *   2. HTTP round-trip to localhost:5000 for several command types (5 runs each)
 *   3. Unity-side operations: TranslateToWorkingSubgraphNodeGUIDs, SetMLNodesColor
 *
 * Results are printed to the Unity Console as a formatted table.
 * Requires a running Neo4j instance and LangGraph server before pressing I.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace VidiGraph
{
    public class LatencyProfiler : MonoBehaviour
    {
        [Header("Dependencies")]
        public DatabaseStorage databaseStorage;
        public NetworkManager networkManager;

        [Header("Config")]
        [SerializeField] int _dbRuns = 3;
        [SerializeField] int _httpRuns = 5;
        [SerializeField] string _serverUrl = "http://localhost:5000/classify";

        // Representative Cypher queries — adjust to match your actual dataset attribute names
        [Header("Test Queries")]
        [SerializeField] string _allNodesQuery = "MATCH (n:Node) RETURN n.GUID AS guid";
        [SerializeField] string _filteredNodesQuery = "MATCH (n:Node) WHERE n.GPA > 3.5 RETURN n.GUID AS guid";
        [SerializeField] string _allLinksQuery = "MATCH ()-[r:LINK]->() RETURN r.GUID AS guid";
        [SerializeField] string _minMaxQuery = "MATCH (n:Node) WHERE n.GPA IS NOT NULL RETURN min(n.GPA) AS minValue, max(n.GPA) AS maxValue";
        [SerializeField] string _distinctValuesQuery = "MATCH (n:Node) WHERE n.major IS NOT NULL RETURN DISTINCT n.major AS value";
        [SerializeField] string _groupedAttribute = "major";
        [SerializeField] string _numericAttribute = "GPA";

        // HTTP test payloads
        static readonly (string label, string text)[] _httpPayloads = new[]
        {
            ("simple/select-all",    "show all nodes"),
            ("color-by-attribute",   "color nodes by major"),
            ("color-by-GPA",         "color nodes by GPA"),
            ("show-links",           "show all links"),
        };

        struct TimingSample
        {
            public string Label;
            public double[] RunsMs;
            public double MinMs  => RunsMs.Min();
            public double AvgMs  => RunsMs.Average();
            public double MaxMs  => RunsMs.Max();
        }

        readonly List<TimingSample> _dbResults  = new();
        readonly List<TimingSample> _httpResults = new();
        readonly List<TimingSample> _unityResults = new();

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.I))
                StartCoroutine(RunAll());
        }

        IEnumerator RunAll()
        {
            _dbResults.Clear();
            _httpResults.Clear();
            _unityResults.Clear();

            UnityEngine.Debug.Log("[LatencyProfiler] Starting benchmark — please wait...");

            // ── 1. DB queries (synchronous, measured with Stopwatch) ────────────
            MeasureDB("GetNodesFromStore (all)",
                () => databaseStorage.GetNodesFromStore(networkManager.NetworkGlobal, _allNodesQuery));

            MeasureDB("GetNodesFromStore (filtered)",
                () => databaseStorage.GetNodesFromStore(networkManager.NetworkGlobal, _filteredNodesQuery));

            MeasureDB("GetLinksFromStore (all)",
                () => databaseStorage.GetLinksFromStore(networkManager.NetworkGlobal, _allLinksQuery));

            MeasureDB("GetMinMaxFromStore",
                () => databaseStorage.GetMinMaxFromStore(networkManager.NetworkGlobal, _minMaxQuery));

            MeasureDB("GetDistinctValuesFromStore",
                () => databaseStorage.GetDistinctValuesFromStore(networkManager.NetworkGlobal, _distinctValuesQuery));

            MeasureDB($"GetNodesGroupedByAttribute [{_groupedAttribute}] run 1",
                () => databaseStorage.GetNodesGroupedByAttribute(networkManager.NetworkGlobal, _groupedAttribute));

            MeasureDB($"GetNodesGroupedByAttribute [{_groupedAttribute}] run 2 (repeat)",
                () => databaseStorage.GetNodesGroupedByAttribute(networkManager.NetworkGlobal, _groupedAttribute));

            MeasureDB($"GetNodesWithNumericValues [{_numericAttribute}] run 1",
                () => databaseStorage.GetNodesWithNumericValues(networkManager.NetworkGlobal, _numericAttribute));

            MeasureDB($"GetNodesWithNumericValues [{_numericAttribute}] run 2 (repeat)",
                () => databaseStorage.GetNodesWithNumericValues(networkManager.NetworkGlobal, _numericAttribute));

            // ── 2. Unity-side operations ────────────────────────────────────────
            if (networkManager.HasWorkingSession)
            {
                MeasureUnity("TranslateToWorkingSubgraphNodeGUIDs (all nodes)",
                    () => networkManager.TranslateToWorkingSubgraphNodeGUIDs(
                        networkManager.WorkingSubgraphAllNodeGUIDs));

                // GetNodesGroupedByAttribute gives us a real grouped result to feed SetMLNodesColor
                var grouped = databaseStorage.GetNodesGroupedByAttribute(networkManager.NetworkGlobal, _groupedAttribute);
                if (grouped.Count > 0)
                {
                    var firstKey   = grouped.Keys.First();
                    var firstGuids = networkManager.TranslateToWorkingSubgraphNodeGUIDs(grouped[firstKey]);
                    MeasureUnity($"SetMLNodesColor ({firstGuids?.Count() ?? 0} nodes)",
                        () => networkManager.SetMLNodesColor(firstGuids, "#FF5733"));
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("[LatencyProfiler] No active working session — skipping Unity-side operation tests. Create a subgraph first.");
            }

            // ── 3. HTTP round-trips (async, coroutine) ──────────────────────────
            foreach (var (label, text) in _httpPayloads)
            {
                yield return StartCoroutine(MeasureHTTP(label, text));
            }

            PrintReport();
        }

        void MeasureDB(string label, Action action)
        {
            var runs = new double[_dbRuns];
            var sw   = new Stopwatch();
            for (int i = 0; i < _dbRuns; i++)
            {
                sw.Restart();
                action();
                sw.Stop();
                runs[i] = sw.Elapsed.TotalMilliseconds;
            }
            _dbResults.Add(new TimingSample { Label = label, RunsMs = runs });
        }

        void MeasureUnity(string label, Action action)
        {
            var runs = new double[_dbRuns];
            var sw   = new Stopwatch();
            for (int i = 0; i < _dbRuns; i++)
            {
                sw.Restart();
                action();
                sw.Stop();
                runs[i] = sw.Elapsed.TotalMilliseconds;
            }
            _unityResults.Add(new TimingSample { Label = label, RunsMs = runs });
        }

        IEnumerator MeasureHTTP(string label, string userText)
        {
            var runs = new double[_httpRuns];

            for (int i = 0; i < _httpRuns; i++)
            {
                var body     = JsonConvert.SerializeObject(new { userText });
                var postData = System.Text.Encoding.UTF8.GetBytes(body);

                using var req = new UnityWebRequest(_serverUrl, "POST");
                req.uploadHandler   = new UploadHandlerRaw(postData);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                float start = Time.realtimeSinceStartup;
                yield return req.SendWebRequest();
                runs[i] = (Time.realtimeSinceStartup - start) * 1000.0;

                if (req.result != UnityWebRequest.Result.Success)
                    UnityEngine.Debug.LogWarning($"[LatencyProfiler] HTTP error on '{label}' run {i}: {req.error}");
            }

            _httpResults.Add(new TimingSample { Label = label, RunsMs = runs });
        }

        void PrintReport()
        {
            const int COL_LABEL = 52;
            const int COL_NUM   = 9;

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("╔══════════════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║                     LATENCY PROFILER RESULTS                               ║");
            sb.AppendLine("╠══════════════════════════════════════════════════════════════════════════════╣");

            void Header(string section)
            {
                sb.AppendLine($"║  {section,-74}║");
                sb.AppendLine($"║  {"Operation",-COL_LABEL}{"Min ms",COL_NUM}{"Avg ms",COL_NUM}{"Max ms",COL_NUM}  ║");
                sb.AppendLine($"║  {"─".PadRight(COL_LABEL, '─'),COL_LABEL}{"─────────",COL_NUM}{"─────────",COL_NUM}{"─────────",COL_NUM}  ║");
            }

            void Row(TimingSample s)
            {
                string lbl = s.Label.Length > COL_LABEL ? s.Label[..(COL_LABEL - 1)] + "…" : s.Label;
                sb.AppendLine($"║  {lbl,-COL_LABEL}{s.MinMs,COL_NUM:F1}{s.AvgMs,COL_NUM:F1}{s.MaxMs,COL_NUM:F1}  ║");
            }

            // DB section
            Header($"Neo4j Queries  ({_dbRuns} runs each)");
            foreach (var s in _dbResults) Row(s);

            sb.AppendLine("╠══════════════════════════════════════════════════════════════════════════════╣");

            // HTTP section
            Header($"HTTP → localhost:5000/classify  ({_httpRuns} runs each)");
            foreach (var s in _httpResults) Row(s);

            sb.AppendLine("╠══════════════════════════════════════════════════════════════════════════════╣");

            // Unity section
            if (_unityResults.Count > 0)
            {
                Header($"Unity-side Operations  ({_dbRuns} runs each)");
                foreach (var s in _unityResults) Row(s);
                sb.AppendLine("╠══════════════════════════════════════════════════════════════════════════════╣");
            }

            // Summary: total pipeline estimate
            double avgDB   = _dbResults.Any()   ? _dbResults.Average(s => s.AvgMs)   : 0;
            double avgHTTP = _httpResults.Any()  ? _httpResults.Average(s => s.AvgMs) : 0;
            sb.AppendLine($"║  ESTIMATED PIPELINE (whisper + HTTP + 1 DB query + render)                  ║");
            sb.AppendLine($"║    HTTP avg:  {avgHTTP,7:F0} ms                                                    ║");
            sb.AppendLine($"║    DB avg:    {avgDB,7:F0} ms  (single query)                                    ║");
            sb.AppendLine($"║    Whisper:   ~400–1200 ms  (model-dependent, not measured here)             ║");
            sb.AppendLine($"║    Render:    <1 frame       (~16 ms @ 60 fps)                               ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════════════════════════════╝");

            UnityEngine.Debug.Log(sb.ToString());
        }

        // Minimal request DTO mirroring StreamingSampleMic's ClassificationRequest
        [Serializable]
        class ClassificationRequest { public string userText; }
    }
}
