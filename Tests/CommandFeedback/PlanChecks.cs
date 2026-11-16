// Regression cases for production plan validation, saved sets, execution, and math.
// Uses fixture database/graph state to exercise boundaries such as cancellation,
// missing values, ties, empty sets, graph changes, and partial failure.

using System.Collections;
using Newtonsoft.Json;
using UnityEngine;
using VidiGraph;
using Whisper.Samples;

internal static class PlanChecks
{
    private static AnalysisPlan Example()
    {
        PlannedStep Step(string id, string operation, string output, PlanArguments args, params string[] dependencies) =>
            new() { id = id, operation = operation, description = operation, output = output, args = args, depends_on = dependencies };
        return new AnalysisPlan { plan_id = "fixture-plan", summary = "Rank two nodes, select them, color their links, and compare GPA.", steps = new[] {
            Step("s1", "rank_nodes", "top", new() { input = "scope", relationship = "aggression", direction = "outgoing", limit = 2 }),
            Step("s2", "select_nodes", "chosen", new() { input = "top" }, "s1"),
            Step("s3", "find_links", "links", new() { input = "top", relationship = "aggression", direction = "outgoing" }, "s1"),
            Step("s4", "color_links", "painted", new() { input = "links", color = "#FF0000" }, "s3"),
            Step("s5", "compare_numeric", "comparison", new() { input = "top", attribute = "gpa" }, "s1")
        }};
    }

    private static (StreamingSampleMic host, InteractionContext context) Fixture()
    {
        var graph = new NetworkManager();
        for (int id = 1; id <= 5; id++)
        {
            string guid = "n" + id;
            graph.NetworkGlobal.Nodes[id] = new object();
            graph.WorkingSubgraphAllNodeGUIDs.Add(guid);
            graph.NodeColors[guid] = new Color(0);
            graph.NodeGUIDToID[guid] = Tuple.Create(1, id);
            graph.NodeIDToGUID[Tuple.Create(1, id)] = guid;
        }
        graph.AddLink(1, 1, 2); graph.AddLink(2, 1, 2); graph.AddLink(3, 1, 3);
        graph.AddLink(4, 2, 3); graph.AddLink(5, 3, 4); graph.AddLink(6, 5, 1);
        graph.LinkTypes["l6"] = "friendship";
        var context = new InteractionContext { graph_id = "school:1", working_graph_id = 1, scope_label = "fixture snapshot",
            node_ids = new[] { 1, 2, 3, 4, 5 }, link_ids = new[] { 1, 2, 3, 4, 5, 6 }, selected_node_ids = new[] { 5 },
            numeric_attributes = new[] { "gpa" }, relationship_types = new[] { "aggression", "friendship" }, max_steps = 8 };
        var db = new DatabaseStorage { Graph = graph, NumericValues = new() { ["n1"] = 4, ["n2"] = 2, ["n3"] = 3, ["n5"] = float.NaN } };
        return (new StreamingSampleMic { _networkManager = graph, _databaseStorage = db }, context);
    }

    private static void Drain(IEnumerator routine)
    {
        int frames = 0;
        while (routine.MoveNext()) if (++frames > 1000) throw new Exception("Coroutine did not finish.");
    }

    public static void Run(Action<bool, string> check)
    {
        var (host, context) = Fixture();
        var plan = JsonConvert.DeserializeObject<AnalysisPlan>(JsonConvert.SerializeObject(Example()));
        var execution = new SequentialPlanExecutor(host, plan, context, 8);
        host.OnMessage = message => {
            if (message.Contains("find_links")) host._networkManager.WorkingSelectedNodeGUIDs = new HashSet<string> { "n5" };
        };
        Drain(CommandExecutionRunner.Run(execution.Execute(() => false), execution.Report));
        check(execution.Report.Steps.All(s => s.Status == "succeeded"), "full typed plan completes");
        check(execution.Report.Steps[0].ResultNodeIds.SequenceEqual(new[] { 1, 2 }), "rank includes deterministic node-ID tie break");
        check(execution.Report.Steps[3].ResultLinkIds.Order().SequenceEqual(new[] { 1, 2, 3, 4 }), "saved group survives hand selection changes");
        check(host._networkManager.LinkColors["l1"].start.Value == 0xFF0000 && host._networkManager.LinkColors["l6"].start.Value == 0,
            "only requested relationship type is colored");
        var comparison = execution.Report.Steps[4].Comparison;
        check(comparison.selected.total == 2 && comparison.selected.mean == 3 && comparison.remaining.valid == 1 && comparison.remaining.missing == 2,
            "comparison uses saved group and excludes missing/non-finite values");
        check(comparison.difference == 0 && comparison.Describe().Contains("no significance test"), "descriptive comparison labels evidence limits");
        var repeat = new SequentialPlanExecutor(host, Example(), context, 8);
        Drain(CommandExecutionRunner.Run(repeat.Execute(() => false), repeat.Report));
        check(repeat.Report.Steps[3].ChangedCount == 0, "repeated plan reports unchanged link colors");

        var (filterHost, filterContext) = Fixture();
        var filteredPlan = Example();
        filteredPlan.steps[0].operation = "filter_numeric";
        filteredPlan.steps[0].args = new PlanArguments { input = "scope", attribute = "gpa", comparison = "gt", value = 2 };
        var filtered = new SequentialPlanExecutor(filterHost, filteredPlan, filterContext, 8);
        Drain(CommandExecutionRunner.Run(filtered.Execute(() => false), filtered.Report));
        check(filtered.Report.Steps[0].ResultNodeIds.SequenceEqual(new[] { 1, 3 }), "numeric filter excludes missing and non-finite attributes");

        foreach (Action<AnalysisPlan> corrupt in new Action<AnalysisPlan>[] {
            p => p.steps[4].args.attribute = "unknown",
            p => p.steps[3].args.input = "top",
            p => p.steps[1].depends_on = Array.Empty<string>(),
            p => p.steps[0].args.input = "future",
            p => p.steps[1].output = "scope",
            p => p.steps[3].operation = "execute_cypher",
            p => p.steps[0].args.limit = 0,
            p => p.steps[3].args.color = "invalid"
        })
        {
            var bad = Example(); corrupt(bad);
            bool rejected = false;
            try { new SequentialPlanExecutor(host, bad, context, 8); } catch (ArgumentException) { rejected = true; }
            check(rejected, "client rejects invalid plan before execution");
        }
        bool exceeded = false;
        try { PlanValidation.Validate(Example(), context, 4); } catch (ArgumentException) { exceeded = true; }
        check(exceeded, "client enforces step limit");
        var extra = JsonConvert.DeserializeObject<AnalysisPlan>(JsonConvert.SerializeObject(Example()).Replace("\"input\":\"scope\"", "\"input\":\"scope\",\"query\":\"DELETE n\""));
        bool extraRejected = false;
        try { PlanValidation.Validate(extra, context, 8); } catch (ArgumentException) { extraRejected = true; }
        check(extraRejected, "client rejects extra executable arguments from JSON");

        var (cancelHost, cancelContext) = Fixture();
        var cancelled = new SequentialPlanExecutor(cancelHost, Example(), cancelContext, 8);
        Drain(CommandExecutionRunner.Run(cancelled.Execute(() => cancelled.Report.Steps[1].Status == "succeeded"), cancelled.Report));
        check(cancelled.Report.Steps[2].Status == "cancelled" && cancelled.Report.Steps[3].Status == "skipped", "cancellation stops between steps");
        check(cancelHost._networkManager.WorkingSelectedNodeGUIDs.SetEquals(new[] { "n1", "n2" }), "cancellation retains completed selection");
        check(cancelHost._networkManager.LinkColors.Values.All(c => c.start.Value == 0), "cancelled color step has no effect");

        var (failedHost, failedContext) = Fixture();
        failedHost._databaseStorage.FailNumeric = true;
        var failed = new SequentialPlanExecutor(failedHost, Example(), failedContext, 8);
        Drain(CommandExecutionRunner.Run(failed.Execute(() => false), failed.Report));
        check(failed.Report.Steps[3].Status == "succeeded" && failed.Report.Steps[4].Status == "failed", "query failure preserves earlier step results");
        check(failed.Report.Summary().Contains("fixture numeric failure"), "query error reaches user feedback");

        var (emptyHost, emptyContext) = Fixture();
        emptyContext.selected_node_ids = Array.Empty<int>();
        var emptyPlan = Example();
        emptyPlan.steps[0].args.input = "selection";
        var empty = new SequentialPlanExecutor(emptyHost, emptyPlan, emptyContext, 8);
        Drain(CommandExecutionRunner.Run(empty.Execute(() => false), empty.Report));
        check(empty.Report.Steps[3].MatchedCount == 0 && emptyHost._networkManager.LinkColors.Values.All(c => c.start.Value == 0), "empty saved group never expands to all links");
        check(empty.Report.Steps[4].Comparison.selected.mean == null && empty.Report.Steps[4].Comparison.difference == null, "empty group is no data, not zero");

        var (changedHost, changedContext) = Fixture();
        var changed = new SequentialPlanExecutor(changedHost, Example(), changedContext, 8);
        changedHost._networkManager.CurWorkingSubgraphID = 2;
        Drain(CommandExecutionRunner.Run(changed.Execute(() => false), changed.Report));
        check(changed.Report.Steps[0].Status == "failed" && changed.Report.Steps[1].Status == "skipped", "session changes invalidate execution");

        var selfLinks = new[] { (1, 1, 1), (2, 2, 1), (2, 2, 1) };
        check(PlanMath.Rank(new[] { 1, 2, 3 }, selfLinks, "both", 3).SequenceEqual(new[] { 1, 2, 3 }), "ranking deduplicates link IDs and includes isolated nodes");
        check(PlanMath.Rank(new[] { 1, 2 }, selfLinks, "incoming", 1).Single() == 1, "incoming ranking respects direction");
    }
}
