// Executes validated Jev operations sequentially against the command-start snapshot.
// Named outputs preserve intermediate sets independently of live selection.
// Cancellation, failure, or a graph/session change stops later steps; completed
// changes remain. The host owns final reporting and evidence-based summarization.

/*  
    SequentialPlanExecutor

    Execute Planned Steps Sequentily against a saved previous state

*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using VidiGraph;

namespace Whisper.Samples
{
    /// <summary>Runs only validated, typed operations; saved sets never follow live selection.</summary>
    public sealed class SequentialPlanExecutor
    {
        private sealed class SavedSet
        {
            public int[] Nodes = Array.Empty<int>(), Links = Array.Empty<int>();
            public string Relationship = "all", Direction = "both";
        }
        private readonly StreamingSampleMic host;
        private readonly AnalysisPlan plan;
        private readonly InteractionContext context;
        private readonly Dictionary<string, SavedSet> outputs = new();
        private readonly Dictionary<string, int[]> linkCache = new();
        private readonly Dictionary<string, Dictionary<int, double>> numericCache = new();
        private readonly HashSet<int> universe, scopeLinks;
        private int expectedWorkingGraph;
        public CommandExecutionReport Report { get; }

        // Validate before any effect, seed immutable starting sets, and allocate pending receipts.
        public SequentialPlanExecutor(StreamingSampleMic host, AnalysisPlan plan, InteractionContext context, int maxSteps)
        {
            PlanValidation.Validate(plan, context, maxSteps); // Entire plan before any effect.
            this.host = host; this.plan = plan; this.context = context;
            
            universe = new HashSet<int>(context.node_ids);
            scopeLinks = new HashSet<int>(context.link_ids);
            
            expectedWorkingGraph = context.working_graph_id;
            
            outputs["scope"] = new SavedSet { Nodes = universe.OrderBy(id => id).ToArray() };
            outputs["selection"] = new SavedSet { Nodes = context.selected_node_ids.Where(universe.Contains).Distinct().ToArray() };
            
            Report = new CommandExecutionReport { CommandId = plan.plan_id };
            // Execute
            foreach (var step in plan.steps)
                Report.Steps.Add(new CommandStepResult { StepId = step.id, DependsOn = step.depends_on,
                    Operation = step.operation switch { "select_nodes" => "selectNode", "color_nodes" => "colorNode",
                        "find_links" => "selectLink", "color_links" => "colorLink", _ => step.operation }, Scope = context.scope_label });
        }

        private NetworkManager Manager => host._networkManager;

        // Reject dataset/session changes. Creating the initial working session is tracked separately.
        private void CheckGraph()
        {
            if (Manager.FileLoader.DatasetName + ":" + context.working_graph_id != context.graph_id ||
                Manager.CurWorkingSubgraphID != expectedWorkingGraph)
                throw new InvalidOperationException("The graph session changed. Submit the command again.");
        }
        
        // Calls Check Graph sto see if we can actually execute
        // Create a working graph only when a mutating operation needs it; update the expected session.
        private async Task EnsureSession()
        {
            CheckGraph();
            if (!await host.EnsureWorkingSessionAsync("Planned analysis", "Analysis"))
                throw new InvalidOperationException("Could not create a working graph.");
            // Creating the initial graph is the only permitted session change during a plan.
            expectedWorkingGraph = Manager.CurWorkingSubgraphID;
            Manager.SetQueryMode(false);
        }

        // Translate saved application node IDs to GUIDs in the expected working graph; fail on stale IDs.
        private HashSet<string> NodeGuids(IEnumerable<int> ids)
        {
            var result = new HashSet<string>();
            foreach (int id in ids)
            {
                if (!Manager.NodeIDToGUID.TryGetValue(Tuple.Create(expectedWorkingGraph, id), out var guid))
                    throw new InvalidOperationException("A saved node is no longer in the working graph.");
                result.Add(guid);
            }
            return result;
        }

        // Translate saved relationship IDs in the current graph without falling back to unrelated links.
        private HashSet<string> LinkGuids(IEnumerable<int> ids)
        {
            var result = new HashSet<string>();
            int graph = expectedWorkingGraph == -1 ? NetworkManager.MainNetworkID : expectedWorkingGraph;
            foreach (int id in ids)
            {
                if (!Manager.LinkIDToGUID.TryGetValue(Tuple.Create(graph, id), out var guid))
                    throw new InvalidOperationException("A saved link is no longer in the graph.");
                result.Add(guid);
            }
            return result;
        }

        // Read each relationship type once per plan and restrict the cached IDs to snapshot links.
        private async Task<int[]> Links(string relationship)
        {
            if (linkCache.TryGetValue(relationship, out var cached)) return cached;
            string predicate = relationship == "all" ? "" : " WHERE r.type='" + relationship.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
            
            var guids = await host._databaseStorage.GetLinksFromStoreAsync(Manager.NetworkGlobal,
                "MATCH ()-[r:POINTS_TO]->()" + predicate + " RETURN DISTINCT r");
            
            var ids = guids.Where(Manager.LinkGUIDToID.ContainsKey).Select(g => Manager.LinkGUIDToID[g].Item2)
                .Where(scopeLinks.Contains).Distinct().ToArray();
            linkCache[relationship] = ids;
            return ids;
        }

        // Cache finite numeric values per attribute, restricted to the original node universe.
        private async Task<Dictionary<int, double>> Numeric(string attribute)
        {
            if (numericCache.TryGetValue(attribute, out var cached)) return cached;
            var values = await host._databaseStorage.GetNodesWithNumericValuesAsync(Manager.NetworkGlobal, attribute);
            var result = new Dictionary<int, double>();
            foreach (var pair in values)
                if (Manager.NodeGUIDToID.TryGetValue(pair.Key, out var id) && universe.Contains(id.Item2) &&
                    !float.IsNaN(pair.Value) && !float.IsInfinity(pair.Value)) result[id.Item2] = pair.Value;
            numericCache[attribute] = result;
            return result;
        }

        /*
            For each plan step
            1. Check whetehr if we are canceling
            2/ If not ten make the plan
            3. Figure out what type of operation 
            4. Execute said command after 
        */
        // Consume steps in order using named inputs. Check cancellation at step boundaries,
        // recheck graph identity after reads, and save each output with its execution receipt.
        public IEnumerator Execute(Func<bool> cancelled)
        {
            for (int i = 0; i < plan.steps.Length; i++)
            {
                if (cancelled())
                {
                    Report.Steps[i].Status = "cancelled";
                    Report.Steps[i].Details = "Cancelled before this step; completed changes were retained.";
                    yield break;
                }
                var step = plan.steps[i];
                var receipt = Report.Steps[i];
                receipt.Status = "running";
                CheckGraph();
                // Keep supported execution steps internal; display the final receipts.
                var source = outputs[step.args.input];
                var result = new SavedSet { Nodes = source.Nodes.ToArray(), Links = source.Links.ToArray(),
                    Relationship = source.Relationship, Direction = source.Direction };
                if (step.operation == "rank_nodes" || step.operation == "find_links")
                {
                    var task = Links(step.args.relationship);
                    yield return new WaitUntil(() => task.IsCompleted);
                    var ids = task.GetAwaiter().GetResult();
                    CheckGraph();
                    if (step.operation == "rank_nodes")
                    {
                        result.Nodes = PlanMath.Rank(source.Nodes, ids.Select(id => (id, Manager.NetworkGlobal.Links[id].SourceNodeID,
                            Manager.NetworkGlobal.Links[id].TargetNodeID)), step.args.direction, step.args.limit.Value);
                        receipt.Details = $"Saved {result.Nodes.Length} highest-ranked nodes by {step.args.direction} {step.args.relationship} link count in {context.scope_label}. Ties resolved by ascending node ID; zero-count nodes included.";
                    }
                    else
                    {
                        var nodeSet = new HashSet<int>(source.Nodes);
                        result.Links = ids.Where(id => LinkQueryScope.Matches(Manager.NetworkGlobal.Links[id].SourceNodeID,
                            Manager.NetworkGlobal.Links[id].TargetNodeID, nodeSet, step.args.direction)).ToArray();
                        result.Relationship = step.args.relationship;
                        result.Direction = step.args.direction;
                        receipt.RecordLinks(Manager, LinkGuids(result.Links), result.Relationship, true, result.Direction, source.Nodes);
                    }
                }
                else if (step.operation == "filter_numeric" || step.operation == "compare_numeric")
                {
                    var task = Numeric(step.args.attribute);
                    yield return new WaitUntil(() => task.IsCompleted);
                    var values = task.GetAwaiter().GetResult();
                    CheckGraph();
                    if (step.operation == "filter_numeric")
                    {
                        result.Nodes = source.Nodes.Where(id => values.TryGetValue(id, out var value) &&
                            PlanMath.Compare(value, step.args.comparison, step.args.value.Value)).ToArray();
                        receipt.Details = $"Saved {result.Nodes.Length} of {source.Nodes.Length} nodes matching the {step.args.attribute} filter; {source.Nodes.Count(id => !values.ContainsKey(id))} missing/non-finite values excluded.";
                    }
                    else
                    {
                        receipt.Comparison = NumericComparison.Calculate(step.args.attribute, context.scope_label, source.Nodes, universe, values);
                        receipt.Details = receipt.Comparison.Describe();
                    }
                }
                else
                {
                    var task = EnsureSession();
                    yield return new WaitUntil(() => task.IsCompleted);
                    task.GetAwaiter().GetResult();
                    CheckGraph();
                    using (var visual = new VisualCommandStep(null))
                    {
                        Manager.CurrentVisualStep = visual;
                        try
                        {
                            if (step.operation == "select_nodes")
                            {
                                var before = Manager.SortNodeGUIDs(Manager.WorkingSelectedNodeGUIDs).Values.SelectMany(ids => ids).ToHashSet();
                                var selected = Manager.ReplaceWorkingNodeSelection(NodeGuids(source.Nodes));
                                receipt.RecordSelection(Manager, selected, before);
                            }
                            else if (step.operation == "color_nodes")
                            {
                                var targets = NodeGuids(source.Nodes);
                                receipt.CaptureNodeColors(Manager, targets);
                                Manager.SetMLNodesColor(targets, step.args.color);
                                host.legendManager?.SetNodeColorLabel(step.args.color, "Planned node set");
                            }
                            else if (step.operation == "color_links")
                            {
                                var targets = LinkGuids(source.Links);
                                receipt.RecordLinks(Manager, targets, source.Relationship, true, source.Direction, source.Nodes);
                                receipt.CaptureLinkColors(Manager, targets);
                                Manager.SetMLLinksColorStart(targets, step.args.color);
                                Manager.SetMLLinksColorEnd(targets, step.args.color);
                                host.legendManager?.SetEdgeColorLabel(step.args.color, source.Relationship + " links");
                            }
                            receipt.CompleteMetrics(Manager);
                            yield return visual.Play();
                        }
                        finally { Manager.CurrentVisualStep = null; }
                    }
                }
                outputs[step.output] = result;
                receipt.OutputName = step.output;
                receipt.ResultNodeIds = result.Nodes.ToArray();
                receipt.ResultLinkIds = result.Links.ToArray();
                receipt.Status = receipt.MatchedCount == 0 ? "no_matches" : "succeeded";
            }
        }
    }
}
