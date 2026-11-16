// Lightweight stand-ins for Unity, graph, UI, and database dependencies used by the
// standalone command-feedback harness. These types exist only for tests; they do
// not reproduce Unity frame timing, physics, or real database behavior.

// Minimal graph/appearance boundary for exercising the production receipt logic
// without a running Unity player or Neo4j server. Production assemblies are compiled separately.
namespace UnityEngine
{
    public readonly record struct Color(int Value);
    public sealed class WaitUntil(Func<bool> predicate) : System.Collections.IEnumerator
    {
        public object Current => null;
        public bool MoveNext() => !predicate();
        public void Reset() => throw new NotSupportedException();
    }
    public class MonoBehaviour { }
    public sealed class SerializeField : Attribute { }
    public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string title) { } }
    public sealed class MinAttribute : Attribute { public MinAttribute(int value) { } }
    public static class Debug
    {
        public static void LogException(Exception exception) { }
        public static void LogError(string message, object context) { }
    }
}
namespace UnityEngine.UI { public sealed class Text { public string text = ""; } }
namespace Neo4j.Driver { public sealed class TransientException : Exception { } }
namespace Whisper.Samples
{
    public sealed class StatisticalQuery { public string label, query; }
    public sealed class StreamingSampleMic
    {
        public VidiGraph.NetworkManager _networkManager;
        public VidiGraph.DatabaseStorage _databaseStorage;
        public StatisticalReportDisplay statisticalReportDisplay;
        public LegendFixture legendManager;
        public Action<string> OnMessage;
        public void ShowPlanMessage(string text) => OnMessage?.Invoke(text);
        public Task<bool> EnsureWorkingSessionAsync(string label, string shortLabel) => Task.FromResult(true);
    }
    public sealed class LegendFixture
    {
        public void SetNodeColorLabel(string color, string label) { }
        public void SetEdgeColorLabel(string color, string label) { }
    }
}

namespace VidiGraph
{
    using UnityEngine;
    public sealed class Link { public int SourceNodeID, TargetNodeID; }
    public sealed class NetworkGlobal
    {
        public Dictionary<int, object> Nodes = new();
        public Dictionary<int, Link> Links = new();
    }
    public sealed class DatabaseStorage
    {
        public Queue<Task<double>> Results = new();
        public Task<double> GetValueFromStoreAsync(NetworkGlobal graph, string query, int timeout) => Results.Dequeue();
        public NetworkManager Graph;
        public Dictionary<string, float> NumericValues = new();
        public bool FailNumeric;
        public Task<Dictionary<string, float>> GetNodesWithNumericValuesAsync(NetworkGlobal graph, string attribute) =>
            FailNumeric ? Task.FromException<Dictionary<string, float>>(new Exception("fixture numeric failure")) : Task.FromResult(NumericValues);
        public Task<List<string>> GetLinksFromStoreAsync(NetworkGlobal graph, string query) => Task.FromResult(Graph.LinkGUIDToID.Keys
            .Where(guid => !query.Contains("WHERE") || query.Contains("'" + Graph.LinkTypes[guid] + "'")).ToList());
    }
    public sealed class FileLoaderFixture { public string DatasetName = "school"; }
    public sealed class VisualCommandStep(Action message) : IDisposable
    {
        public System.Collections.IEnumerator Play() { message?.Invoke(); yield return null; }
        public void Dispose() { }
    }
    public sealed class NetworkManager
    {
        public bool HasWorkingSession = true;
        public const int MainNetworkID = 0;
        public int CurWorkingSubgraphID = 1;
        public FileLoaderFixture FileLoader = new();
        public VisualCommandStep CurrentVisualStep;
        public Dictionary<Tuple<int, int>, string> NodeIDToGUID = new(), LinkIDToGUID = new();
        public Dictionary<string, Tuple<int, int>> NodeGUIDToID = new();
        public Dictionary<string, string> LinkTypes = new();
        public NetworkGlobal NetworkGlobal = new();
        public HashSet<string> WorkingSelectedNodeGUIDs = new();
        public HashSet<string> WorkingSubgraphAllNodeGUIDs = new();
        public HashSet<string> WorkingSubgraphAllLinkGUIDs = new();
        public Dictionary<string, Tuple<int, int>> LinkGUIDToID = new();
        public Dictionary<string, Color> NodeColors = new();
        public Dictionary<string, (Color start, Color end)> LinkColors = new();
        public Dictionary<int, HashSet<int>> SortNodeGUIDs(IEnumerable<string> ids) => new()
        {
            [1] = ids.Select(id => int.Parse(id.Substring(1))).ToHashSet()
        };
        public IEnumerable<int> GetCurWorkingSubgraphLinkIDs() => WorkingSubgraphAllLinkGUIDs.Select(guid => LinkGUIDToID[guid].Item2);
        public bool TryGetCommandNodeColor(string guid, out Color color) => NodeColors.TryGetValue(guid, out color);
        public bool TryGetCommandLinkColors(string guid, out Color start, out Color end)
        {
            bool found = LinkColors.TryGetValue(guid, out var pair);
            start = pair.start; end = pair.end; return found;
        }
        public void SetQueryMode(bool enabled) { }
        public HashSet<string> ReplaceWorkingNodeSelection(IEnumerable<string> guids)
        {
            WorkingSelectedNodeGUIDs = new HashSet<string>(guids);
            return new HashSet<string>(guids);
        }
        public void SetMLNodesColor(IEnumerable<string> guids, string color)
        {
            foreach (var guid in guids) NodeColors[guid] = new Color(Convert.ToInt32(color.Substring(1), 16));
        }
        public void SetMLLinksColorStart(IEnumerable<string> guids, string color)
        {
            foreach (var guid in guids) LinkColors[guid] = (new Color(Convert.ToInt32(color.Substring(1), 16)), LinkColors[guid].end);
        }
        public void SetMLLinksColorEnd(IEnumerable<string> guids, string color)
        {
            foreach (var guid in guids) LinkColors[guid] = (LinkColors[guid].start, new Color(Convert.ToInt32(color.Substring(1), 16)));
        }
        public void AddLink(int id, int source, int target)
        {
            string guid = "l" + id;
            NetworkGlobal.Links[id] = new Link { SourceNodeID = source, TargetNodeID = target };
            WorkingSubgraphAllLinkGUIDs.Add(guid);
            LinkGUIDToID[guid] = Tuple.Create(1, id);
            LinkIDToGUID[Tuple.Create(1, id)] = guid;
            LinkTypes[guid] = "aggression";
            LinkColors[guid] = (new Color(0), new Color(0));
        }
    }
}
