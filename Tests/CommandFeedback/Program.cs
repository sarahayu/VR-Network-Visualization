// Standalone receipt/display regression harness using lightweight Unity/graph fixtures.
// Runs production reporting code and plan checks without a headset. Requires the
// SDK targeted by the project and its Newtonsoft.Json assembly reference.
// This does not validate real Unity rendering, Neo4j connectivity, or model quality.

using System.Collections;
using UnityEngine;
using VidiGraph;
using Whisper.Samples;

int checks = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new Exception("FAILED: " + label);
    checks++;
}
var graph = new NetworkManager();
for (int i = 1; i <= 4; i++)
{
    graph.NetworkGlobal.Nodes[i] = new object();
    graph.WorkingSubgraphAllNodeGUIDs.Add("n" + i);
    graph.NodeColors["n" + i] = new Color(0);
}
graph.WorkingSelectedNodeGUIDs.Add("n1");
graph.AddLink(1, 1, 2);
graph.AddLink(2, 1, 2); // Parallel relationships count as two links, one neighbor.
graph.AddLink(3, 3, 1);
graph.AddLink(4, 1, 1); // Self-loop counts once and is not its own neighbor.
graph.AddLink(5, 2, 4); // Unrelated link excluded from single-node denominator.
graph.AddLink(6, 1, 4);
graph.WorkingSubgraphAllLinkGUIDs.Remove("l6"); // Outside current graph.

var links = new CommandStepResult { Operation = "colorLink", Status = "succeeded" };
links.RecordLinks(graph, new[] { "l1", "l1", "l2", "l3" }, "aggression", true, "both");
links.CaptureLinkColors(graph, links.AffectedIds);
graph.LinkColors["l1"] = (new Color(1), new Color(1));
graph.LinkColors["l2"] = (new Color(1), new Color(0));
links.CompleteMetrics(graph);
Check(links.MatchedCount == 3 && links.ChangedCount == 2, "deduplicated matched count versus actual color changes");
Check(links.NeighborCount == 2 && links.IncidentTotal == 4, "distinct neighbors and working-graph denominator");
Check(links.Summary().Contains("75%"), "percentage from actual affected links");
var repeated = new CommandStepResult { Operation = "colorLink", Status = "succeeded" };
repeated.RecordLinks(graph, links.AffectedIds, "aggression", true, "both");
repeated.CaptureLinkColors(graph, links.AffectedIds);
repeated.CompleteMetrics(graph);
Check(repeated.ChangedCount == 0 && repeated.Summary().Contains("already had"), "repeated color reports unchanged");

var incoming = new CommandStepResult { Operation = "selectLink", Status = "succeeded" };
incoming.RecordLinks(graph, new[] { "l3" }, "aggression", true, "incoming");
Check(incoming.IncidentTotal == 2 && incoming.Summary().Contains("50%"), "incoming denominator includes incoming self-loop once");
var outgoing = new CommandStepResult { Operation = "selectLink", Status = "succeeded" };
outgoing.RecordLinks(graph, new[] { "l1", "l2", "l4" }, "aggression", true, "outgoing");
Check(outgoing.IncidentTotal == 3 && outgoing.NeighborCount == 1, "outgoing self-loop and duplicate neighbors");

var selected = new CommandStepResult { Operation = "selectNode", Status = "succeeded" };
selected.RecordSelection(graph, new[] { "n1", "n2", "n2" }, new HashSet<int> { 1, 3 });
Check(selected.MatchedCount == 2 && selected.ChangedCount == 1 && selected.DeselectedCount == 1, "selection additions and removals");
Check(selected.Summary().Contains("50%"), "selection percentage of working graph");
selected.RecordSelection(graph, Array.Empty<string>(), new HashSet<int> { 1, 2 });
Check(selected.Summary().Contains("2 deselected") && !selected.Summary().Contains("nothing changed"), "empty replacement clears prior selection");
var empty = new CommandStepResult { Operation = "colorLink", Status = "no_matches" };
empty.RecordLinks(graph, Array.Empty<string>(), "aggression", true, "both");
empty.CaptureLinkColors(graph, Array.Empty<string>());
empty.CompleteMetrics(graph);
Check(empty.MatchedCount == 0 && empty.ChangedCount == 0 && empty.Summary().Contains("No matching"), "empty link result remains empty");
var nodes = new CommandStepResult { Operation = "colorNode", Status = "succeeded" };
nodes.CaptureNodeColors(graph, new[] { "n1", "n2" });
graph.NodeColors["n1"] = new Color(1);
nodes.CompleteMetrics(graph);
Check(nodes.MatchedCount == 2 && nodes.ChangedCount == 1, "node coloring actual changes");
var grouped = new CommandStepResult { Operation = "colorByAttribute", Status = "succeeded" };
grouped.CaptureNodeColors(graph, new[] { "n1", "n2" });
graph.NodeColors["n1"] = new Color(2);
grouped.CaptureNodeColors(graph, new[] { "n1", "n3" });
graph.NodeColors["n3"] = new Color(2);
grouped.CompleteMetrics(graph);
Check(grouped.MatchedCount == 3 && grouped.ChangedCount == 2, "grouped coloring preserves initial colors and unions assigned nodes");
Check(grouped.Summary().Contains("75%"), "grouped coloring reports graph coverage");
graph.WorkingSelectedNodeGUIDs.Add("n2");
var multiNode = new CommandStepResult { Operation = "colorLink", Status = "succeeded" };
multiNode.RecordLinks(graph, new[] { "l1" }, "aggression", true, "both");
Check(multiNode.NeighborCount == null && multiNode.IncidentTotal == null, "multiple selected nodes do not get a misleading single-node percentage");
graph.WorkingSelectedNodeGUIDs.Remove("n2");

foreach (var predicate in new[] { "n.selected=true", "n.selected = true", "n.selected  =  TRUE" })
{
    string query = $"MATCH (n:Node)-[r:POINTS_TO]->(m) WHERE {predicate} AND r.type='aggression' RETURN r";
    Check(LinkQueryScope.IsSelected(query, "aggression"), "selected predicate recognition");
    Check(!LinkQueryScope.RemoveSelectedPredicate(query).Contains("selected"), "remove graph-local predicate before DB query");
    Check(LinkQueryScope.Direction(query) == "outgoing", "outgoing direction recognition");
}
Check(LinkQueryScope.Direction("MATCH (n:Node)<-[r:POINTS_TO]-(m) RETURN r") == "incoming", "incoming direction recognition");
Check(LinkQueryScope.Direction("MATCH (n)-[r:POINTS_TO]-(m) RETURN r") == "both", "undirected recognition");
Check(!LinkQueryScope.Matches(3, 1, new HashSet<int> { 1 }, "outgoing"), "incoming excluded from outgoing scope");
Check(!LinkQueryScope.Matches(1, 2, new HashSet<int>(), "both"), "empty selected scope matches no links");
Check(LinkQueryScope.Direction("MATCH (m)-[r:POINTS_TO]->(n:Node) RETURN r") == "incoming", "right-hand selected node direction");
Check(LinkQueryScope.ResolveTargets(true, Array.Empty<string>(), new[] { "l1" }, new[] { "l1", "l2" }, true).Count == 0,
    "empty query never falls back to unrelated links");
Check(LinkQueryScope.ResolveTargets(false, Array.Empty<string>(), Array.Empty<string>(), new[] { "l1" }, true).Count == 0,
    "isolated or empty selected node scope never falls back to all links");
Check(LinkQueryScope.ResolveTargets(false, Array.Empty<string>(), Array.Empty<string>(), new[] { "l1" }, false).SetEquals(new[] { "l1" }),
    "unscoped color operation can target all links");

var report = CommandExecutionReport.FromLegacy(new[] { new[] { "selectLink", "aggression" }, new[] { "colorLink", "red" } }, new[] { "query", "" });
Check(report.Steps[1].DependsOn.Single() == report.Steps[0].StepId, "legacy adapter assigns dependency IDs");
report.Steps[0] = incoming;
report.Steps[1] = links;
incoming.AffectedIds = links.AffectedIds;
Check(!report.Summary().Contains("Found"), "combined summary suppresses redundant preparatory query");

bool disposed = false, downstreamRan = false;
IEnumerator FailingNested() { yield return null; throw new InvalidOperationException("fixture failure"); }
IEnumerator Sequence(CommandExecutionReport execution)
{
    try
    {
        execution.Steps[0].Status = "succeeded";
        execution.Steps[1].Status = "running";
        yield return FailingNested();
        downstreamRan = true;
    }
    finally { disposed = true; }
}
var failure = CommandExecutionReport.FromLegacy(new[] { new[] { "selectNode", "all" }, new[] { "colorNode", "red" }, new[] { "move", "" } }, new[] { "", "", "" });
var runner = CommandExecutionRunner.Run(Sequence(failure), failure);
while (runner.MoveNext()) { }
Check(disposed && !downstreamRan, "nested failure disposes execution and prevents downstream actions");
Check(failure.Steps[0].Status == "succeeded" && failure.Steps[1].Status == "failed" && failure.Steps[2].Status == "skipped", "partial failure preserves successful results");
Check(failure.Summary().Contains("fixture failure") && failure.Summary().Contains("skipped"), "failure explanation");

var display = new StatisticalReportDisplay();
var output = new UnityEngine.UI.Text();
var database = new DatabaseStorage();
void SetDisplayField(string field, object value) => typeof(StatisticalReportDisplay)
    .GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(display, value);
SetDisplayField("outputText", output);
SetDisplayField("networkManager", graph);
SetDisplayField("databaseStorage", database);
display.DisplayCommandReport(report, null);
Check(output.text.Contains("Command result") && output.text.Contains("75%"), "automatic report without requested statistics");
database.Results.Enqueue(Task.FromResult(4.5));
display.DisplayCommandReport(report, new[] { new StatisticalQuery { label = "Average GPA", query = "fixture" } });
Check(output.text.Contains("75%") && output.text.Contains("Average GPA: 4.5"), "explicit analysis preserves automatic feedback");
database.Results.Enqueue(Task.FromResult(0.0));
database.Results.Enqueue(Task.FromResult(double.NaN));
database.Results.Enqueue(Task.FromException<double>(new Exception("fixture database failure")));
display.DisplayCommandReport(report, new[] {
    new StatisticalQuery { label = "Count", query = "zero" },
    new StatisticalQuery { label = "Average", query = "no data" },
    new StatisticalQuery { label = "Other", query = "failure" }
});
Check(output.text.Contains("Count: 0") && output.text.Contains("Average: no data") && output.text.Contains("Other: unable to calculate"),
    "zero, no data, and failure remain distinct");
var pending = new TaskCompletionSource<double>();
database.Results.Enqueue(pending.Task);
display.DisplayReports(new[] { new StatisticalQuery { label = "Old report", query = "slow" } });
display.BeginCommand();
display.DisplayCommandReport(failure, null);
string latest = output.text;
pending.SetResult(99);
await Task.Yield();
Check(output.text == latest && !output.text.Contains("99"), "older asynchronous statistics cannot overwrite new command feedback");
SetDisplayField("clearPreviousReport", false);
database.Results.Enqueue(Task.FromResult(3.0));
display.DisplayReports(new[] { new StatisticalQuery { label = "New result", query = "fast" } });
Check(output.text.Contains(latest) && output.text.Contains("New result: 3") && !output.text.Contains("Calculating"),
    "append mode retains history without duplicating loading text");
PlanChecks.Run(Check);
Console.WriteLine($"Passed {checks} command-feedback and planning checks.");
