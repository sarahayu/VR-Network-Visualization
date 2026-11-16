/*
    CommandExecutionReport
    
    Describe Executions
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using VidiGraph;

namespace Whisper.Samples
{
    [Serializable]
    public sealed class CommandExecutionReport
    {
        public string CommandId = Guid.NewGuid().ToString("N");
        public List<CommandStepResult> Steps = new();
        public bool HasFailure => Steps.Any(s => s.Status == "failed" || s.Status == "skipped" || s.Status == "cancelled");

        // Adapter for the existing server contract; IDs are stable for this execution.
        public static CommandExecutionReport FromLegacy(string[][] actions, string[] queries)
        {
            var report = new CommandExecutionReport();
            for (int i = 0; i < (actions?.Length ?? 0); i++)
                report.Steps.Add(new CommandStepResult
                {
                    StepId = $"step-{i + 1}",
                    Operation = actions[i]?.FirstOrDefault() ?? "invalid",
                    Parameter = actions[i]?.ElementAtOrDefault(1),
                    Query = queries?.ElementAtOrDefault(i),
                    DependsOn = i == 0 ? Array.Empty<string>() : new[] { $"step-{i}" }
                });
            return report;
        }

        // Finalize interrupted steps: running becomes failed and unstarted steps become skipped.
        public void Finish()
        {
            foreach (var step in Steps)
            {
                if (step.Status == "running")
                {
                    step.Status = "failed";
                    step.Error = step.Error ?? "Execution stopped before this step completed; changes may be partial.";
                }
                else if (step.Status == "pending") step.Status = "skipped";
            }
        }

        public string Summary()
        {
            var lines = new List<string>();
            foreach (var step in Steps)
            {
                // A link query is preparatory; its following color action carries the same metrics.
                if (step.Operation == "selectLink" && step.Status != "failed" &&
                    Steps.Any(next => next.Operation == "colorLink" && (next.Status == "succeeded" || next.Status == "no_matches") &&
                        next.AffectedIds.SequenceEqual(step.AffectedIds))) continue;
                string line = step.Summary();
                if (!string.IsNullOrEmpty(line)) lines.Add(line);
            }
            if (Steps.Any(s => s.Status == "skipped")) lines.Add("Remaining steps were skipped.");
            return string.Join("\n", lines);
        }
    }

    [Serializable]
    public sealed class CommandStepResult
    {
        public string StepId, Operation, Parameter, Query;
        public string Details, OutputName;
        public int[] ResultNodeIds, ResultLinkIds;
        public NumericComparison Comparison;
        public string SummaryText => Summary();
        public string[] DependsOn;
        public string Status = "pending";
        public string Scope, LinkType, Direction, Error;
        public string[] AffectedIds = Array.Empty<string>();
        public int? MatchedCount, ChangedCount, ScopeTotal, NeighborCount, IncidentTotal;
        public int DeselectedCount;
        private readonly Dictionary<string, Color> nodeColors = new();
        private readonly Dictionary<string, (Color start, Color end)> linkColors = new();

        // Record the resulting selection, newly selected nodes, and deselections relative to the previous set.
        public void RecordSelection(NetworkManager manager, IEnumerable<string> ids, HashSet<int> before)
        {
            AffectedIds = ids.Distinct().OrderBy(id => id).ToArray();
            MatchedCount = AffectedIds.Length;
            Scope = "working graph";
            ScopeTotal = manager.WorkingSubgraphAllNodeGUIDs.Count;
            var after = manager.SortNodeGUIDs(AffectedIds).Values.SelectMany(x => x).ToHashSet();
            ChangedCount = after.Except(before).Count();
            DeselectedCount = before.Except(after).Count();
        }

        // Snapshot target colors before assignment so repeated coloring can report zero actual changes.
        public void CaptureNodeColors(NetworkManager manager, IEnumerable<string> ids)
        {
            AffectedIds = AffectedIds.Concat(ids).Distinct().OrderBy(id => id).ToArray();
            Scope = manager.HasWorkingSession ? "working graph" : "dataset";
            ScopeTotal = manager.HasWorkingSession ? manager.WorkingSubgraphAllNodeGUIDs.Count : manager.NetworkGlobal.Nodes.Count;
            MatchedCount = AffectedIds.Length;
            foreach (string id in AffectedIds)
            {
                if (nodeColors.ContainsKey(id)) continue;
                if (!manager.TryGetCommandNodeColor(id, out var color))
                    throw new InvalidOperationException("A target node is no longer available.");
                nodeColors[id] = color;
            }
        }

        // Snapshot both endpoint colors before assignment; targets and changes are separate metrics.
        public void CaptureLinkColors(NetworkManager manager, IEnumerable<string> ids)
        {
            foreach (string id in ids.Distinct())
            {
                if (!manager.TryGetCommandLinkColors(id, out var start, out var end))
                    throw new InvalidOperationException("A target link is no longer available.");
                linkColors[id] = (start, end);
            }
        }

        // Record matched links and, where applicable, scoped incident-link and neighbor denominators.
        public void RecordLinks(NetworkManager manager, IEnumerable<string> ids, string type, bool selectedScope, string direction, IEnumerable<int> savedNodeIds = null)
        {
            AffectedIds = ids.Distinct().OrderBy(id => id).ToArray();
            MatchedCount = AffectedIds.Length;
            Scope = manager.HasWorkingSession ? "working graph" : "dataset";
            LinkType = type == "all" ? "links" : type + " links";
            Direction = direction;
            ScopeTotal = manager.HasWorkingSession ? manager.WorkingSubgraphAllLinkGUIDs.Count : manager.NetworkGlobal.Links.Count;
            var selected = savedNodeIds != null ? new HashSet<int>(savedNodeIds)
                : manager.SortNodeGUIDs(manager.WorkingSelectedNodeGUIDs).Values.SelectMany(x => x).ToHashSet();
            if (!selectedScope || selected.Count != 1) return;
            int node = selected.Single();
            var neighbors = new HashSet<int>();
            foreach (string guid in AffectedIds)
            {
                var link = manager.NetworkGlobal.Links[manager.LinkGUIDToID[guid].Item2];
                if (link.SourceNodeID != node && link.TargetNodeID != node) return;
                int other = link.SourceNodeID == node ? link.TargetNodeID : link.SourceNodeID;
                if (other != node) neighbors.Add(other);
            }
            NeighborCount = neighbors.Count;
            // Denominator is all relationship types in the SAME graph and direction.
            var scopeIds = manager.HasWorkingSession
                ? manager.GetCurWorkingSubgraphLinkIDs() : manager.NetworkGlobal.Links.Keys;
            IncidentTotal = scopeIds.Distinct().Count(id =>
            {
                var link = manager.NetworkGlobal.Links[id];
                return LinkQueryScope.Matches(link.SourceNodeID, link.TargetNodeID, selected, direction);
            });
        }

        // Compare captured appearance with live values after assignment to count actual changes.
        public void CompleteMetrics(NetworkManager manager)
        {
            if (Operation.StartsWith("color", StringComparison.Ordinal) && Operation != "colorLink")
                ChangedCount = nodeColors.Count(pair => !manager.TryGetCommandNodeColor(pair.Key, out var color)
                    ? throw new InvalidOperationException("A target node disappeared.") : color != pair.Value);
            if (Operation == "colorLink")
                ChangedCount = linkColors.Count(pair => !manager.TryGetCommandLinkColors(pair.Key, out var start, out var end)
                    ? throw new InvalidOperationException("A target link disappeared.") : start != pair.Value.start || end != pair.Value.end);
        }

        private static string Percent(int count, int? total) => total > 0
            ? (100.0 * count / total.Value).ToString("0.#", CultureInfo.InvariantCulture) + "%" : null;

        public string Summary()
        {
            if (Status == "failed") return $"{StepId} ({Operation}) failed. {Error ?? "Changes may be partial."}";
            if (Status == "cancelled") return $"{StepId}: {Details ?? "Cancelled; completed changes retained."}";
            
            if (Status == "skipped") return $"{StepId} was skipped.";
            if (Status == "pending") return null;
            
            if (!string.IsNullOrEmpty(Details)) return Details;
            if (!MatchedCount.HasValue) return $"Completed {Operation}.";
            
            int count = MatchedCount.Value;
            string entity = Operation.Contains("Link") ? LinkType ?? "links" : "nodes";
            
            if (count == 0 && Operation == "selectNode")
                return $"No matching nodes in the {Scope}; selection cleared ({DeselectedCount} deselected).";
            
            if (count == 0) return $"No matching {entity} in the {Scope}; nothing changed.";
            
            if (Operation == "selectNode")
                return $"Selected {count} of {ScopeTotal} nodes in the {Scope} ({Percent(count, ScopeTotal)}).";
            
            string text = Operation.StartsWith("color", StringComparison.Ordinal)
                ? ChangedCount == 0 ? $"All {count} matching {entity} already had the requested color."
                    : $"Colored {ChangedCount} of {count} matching {entity}."
                : $"Found {count} matching {entity}.";
            
            if (!Operation.Contains("Link") && ScopeTotal > 0)
                text += $" Matching nodes represent {Percent(count, ScopeTotal)} of the {Scope}.";
            
            if (NeighborCount.HasValue)
            {
                string direction = Direction == "both" ? "incident" : Direction;
                text += $" {NeighborCount} distinct neighbors; {count} of {IncidentTotal} {direction} links of the selected node in the {Scope}";
                string percent = Percent(count, IncidentTotal);
                text += (percent == null ? "." : $" ({percent}).");
            }
            return text;
        }
    }

    public static class LinkQueryScope
    {
        private static readonly Regex Selected = new(@"\bn\.selected\s*=\s*true\b", RegexOptions.IgnoreCase);
        public static bool IsSelected(string query, string parameter) => Selected.IsMatch(query ?? "") ||
            (parameter ?? "").EndsWith(":selected", StringComparison.OrdinalIgnoreCase);
        public static string RemoveSelectedPredicate(string query) => Selected.Replace(query ?? "", "true");
        // An explicit query or node scope wins even when empty; never broaden empty matches to all links.
        public static HashSet<string> ResolveTargets(bool hasQuery, IEnumerable<string> queried,
            IEnumerable<string> nodeLinks, IEnumerable<string> allLinks, bool hasNodeScope) =>
            new(hasQuery ? queried : hasNodeScope ? nodeLinks : allLinks);

        public static string Direction(string query)
        {
            const string node = @"\(\s*n\b[^)]*\)";
            if (Regex.IsMatch(query ?? "", node + @"\s*<-\[", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(query ?? "", @"\[[^\]]*\]\s*->\s*" + node, RegexOptions.IgnoreCase)) return "incoming";
            if (Regex.IsMatch(query ?? "", node + @"\s*-\[[^\]]*\]\s*->", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(query ?? "", @"<-\[[^\]]*\]\s*-\s*" + node, RegexOptions.IgnoreCase)) return "outgoing";
            return "both";
        }
        public static bool Matches(int source, int target, HashSet<int> nodes, string direction) =>
            (direction != "incoming" && nodes.Contains(source)) || (direction != "outgoing" && nodes.Contains(target));
    }

    public static class CommandExecutionRunner
    {
        // Flatten nested iterators so exceptions from a visual step are recorded too.
        // Flatten nested coroutines so failures inside visual playback also stop execution and finalize receipts.
        public static IEnumerator Run(IEnumerator routine, CommandExecutionReport report)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            try
            {
                while (stack.Count > 0)
                {
                    object current = null;
                    bool moved = false;
                    Exception failure = null;
                    try
                    {
                        moved = stack.Peek().MoveNext();
                        if (moved) current = stack.Peek().Current;
                    }
                    catch (Exception exception) { failure = exception; }
                    if (failure != null)
                    {
                        var step = report.Steps.FirstOrDefault(s => s.Status == "running");
                        if (step != null) step.Error = failure.Message + " Changes may be partial.";
                        Debug.LogException(failure);
                        yield break;
                    }
                    if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (current is IEnumerator nested) stack.Push(nested);
                    else yield return current;
                }
            }
            finally
            {
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                report.Finish();
            }
        }
    }
}
