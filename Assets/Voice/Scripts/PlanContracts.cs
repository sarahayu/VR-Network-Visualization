/*  
    PlanContracts.cs
        - Defines the graph snapshots, format executable plans, validation rules,
          and deterministic analysis helpers that is shared by executors
        - Jev may compose these new sequecies of supported Operators
        - Supported plans are ran internally meaning, user does not need to know
        - Ussuported Actions create a plan that is proposed before executions
*/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Whisper.Samples
{
    /// <summary>
    /// Graph/session snapshot captured for a command. IDs are application IDs,
    /// not Neo4j internal IDs. Scope and selection remain fixed during a plan.
    /// The server sends scope counts to the model while retaining full IDs.
    /// </summary>
    [Serializable]
    public sealed class InteractionContext
    {
        public string graph_id;
        public string scope_label;
        public int working_graph_id;
        public int[] node_ids, link_ids, selected_node_ids;
        public string[] attributes, numeric_attributes, relationship_types, direct_operations;
        public int max_steps = 8;
    }

    /// <summary>
    /// Executable plan returned by Jev, with a server-assigned plan ID.
    /// Summary and step descriptions are internal metadata, not computed results
    /// or instructions to display a preview. Alternative proposals use the
    /// classification response's clarify/clarification_id fields instead.
    /// </summary>
    [Serializable]
    public sealed class AnalysisPlan
    {
        public string plan_id, summary;
        public PlannedStep[] steps;
        // Capture unknown JSON fields so validation can reject them.
        [JsonExtensionData] public Dictionary<string, JToken> extra;
    }

    /// <summary>
    /// One supported operation. Input names scope, selection, or a prior output;
    /// depends_on must include that output's producing step. Output names are
    /// unique, and dependencies may reference only earlier steps.
    /// </summary>
    [Serializable]
    public sealed class PlannedStep
    {
        public string id, operation, description, output;
        public string[] depends_on;
        public PlanArguments args;
        [JsonExtensionData] public Dictionary<string, JToken> extra;
    }

    /// <summary>
    /// Argument fields shared across operations; only fields applicable to the
    /// chosen operation may be populated. The C# comparison field maps to the
    /// JSON key "operator". Exact argument contracts also live in Python's
    /// OPERATIONS registry and must stay aligned with the Unity executor.
    /// </summary>
    [Serializable]
    public sealed class PlanArguments
    {
        public string input, relationship, direction, attribute, color;
        [JsonProperty("operator")] public string comparison;
        public int? limit;
        public double? value;
        [JsonExtensionData] public Dictionary<string, JToken> extra;
    }

    /// <summary>
    /// Validate the entire plan before execution can mutate the graph.
    /// New combinations are allowed, but unknown operations, extra fields,
    /// invalid arguments, and incompatible saved-set references are rejected.
    /// </summary>
    public static class PlanValidation
    {
        public static readonly string[] Operations = { "rank_nodes", "filter_numeric", "select_nodes", "find_links", "color_nodes", "color_links", "compare_numeric" };
        /// <summary>
        /// Enforce the caller's step limit, capped at 12, and the snapshot schema.
        /// All operations take nodes except color_links, which takes links.
        /// Statistics outputs cannot be used as inputs to another operation.
        /// Throws ArgumentException if the plan is invalid; does not execute it.
        /// </summary>
        public static void Validate(AnalysisPlan plan, InteractionContext context, int maxSteps)
        {   
            // If Context is missing
            if (context == null || context.node_ids == null || context.link_ids == null || context.selected_node_ids == null)
                throw new ArgumentException("An interaction snapshot is required.");
            // If there is no plan
            if (plan == null || string.IsNullOrWhiteSpace(plan.plan_id) || string.IsNullOrWhiteSpace(plan.summary) ||
                plan.summary.Length > 1200 || plan.steps == null || plan.steps.Length < 1 || plan.steps.Length > Math.Min(12, maxSteps) || plan.extra?.Count > 0)
                throw new ArgumentException("Invalid plan or step limit exceeded.");

            var outputs = new Dictionary<string, (string kind, string producer)> { ["scope"] = ("nodes", null), ["selection"] = ("nodes", null) };
            var ids = new HashSet<string>();
            
            // For every planned step, make sure it is able to be executed
            foreach (var step in plan.steps)
            {
                if (step == null || !Identifier(step.id) || !Identifier(step.output) || ids.Contains(step.id) || outputs.ContainsKey(step.output) ||
                    !Operations.Contains(step.operation) || string.IsNullOrWhiteSpace(step.description) || step.description.Length > 300 ||
                    step.extra?.Count > 0 || step.args == null || step.args.extra?.Count > 0 || step.depends_on == null ||
                    step.depends_on.Distinct().Count() != step.depends_on.Length || step.depends_on.Any(id => !ids.Contains(id)))
                    
                    throw new ArgumentException("Invalid operation, identifier, or dependency.");
                
                var a = step.args;
                if (a.input == null || !outputs.TryGetValue(a.input, out var source) ||
                    source.kind != (step.operation == "color_links" ? "links" : "nodes") ||
                    (source.producer != null && !step.depends_on.Contains(source.producer)))
                    throw new ArgumentException("Input must reference an earlier output of the correct type and depend on its producer.");
                
                bool links = step.operation == "rank_nodes" || step.operation == "find_links";
                bool numeric = step.operation == "filter_numeric" || step.operation == "compare_numeric";
                bool color = step.operation == "color_nodes" || step.operation == "color_links";
                
                if (links && (a.relationship == null || !(context.relationship_types ?? Array.Empty<string>()).Append("all").Contains(a.relationship) ||
                    !new[] { "incoming", "outgoing", "both" }.Contains(a.direction))) throw new ArgumentException("Unknown relationship or direction.");
                
                if (!links && (a.relationship != null || a.direction != null)) 
                    throw new ArgumentException("Unexpected link arguments.");
                
                if (numeric && (a.attribute == null || !(context.numeric_attributes ?? Array.Empty<string>()).Contains(a.attribute))) 
                    throw new ArgumentException("Unknown numeric attribute.");
                
                if (!numeric && a.attribute != null) 
                    throw new ArgumentException("Unexpected attribute.");
                
                if (color && (a.color == null || !Regex.IsMatch(a.color, "^#[0-9a-fA-F]{6}$"))) 
                    throw new ArgumentException("Invalid color.");
                
                if (!color && a.color != null) 
                    throw new ArgumentException("Unexpected color.");
                
                if (step.operation == "rank_nodes" ? !a.limit.HasValue || a.limit < 1 || a.limit > 1000 : a.limit.HasValue)
                    throw new ArgumentException("Invalid rank limit.");
                
                if (step.operation == "filter_numeric")
                {
                    if (!new[] { "gt", "gte", "lt", "lte", "eq" }.Contains(a.comparison) || !a.value.HasValue ||
                        double.IsNaN(a.value.Value) || double.IsInfinity(a.value.Value)) throw new ArgumentException("Invalid numeric filter.");
                }
                else if (a.comparison != null || a.value.HasValue) 
                    throw new ArgumentException("Unexpected filter arguments.");
                
                outputs[step.output] = (step.operation == "find_links" || step.operation == "color_links" ? "links" :
                    step.operation == "compare_numeric" ? "statistics" : "nodes", step.id);
                
                ids.Add(step.id);
            }
        }
        private static bool Identifier(string value) => value != null && Regex.IsMatch(value, "^[A-Za-z][A-Za-z0-9_]{0,63}$");
    }

    /// <summary>
    /// Descriptive statistics for distinct nodes. Missing and non-finite values
    /// count as missing; a group with no valid values has a null mean, not zero.
    /// </summary>
    [Serializable]
    public sealed class NumericGroup
    {
        public int total, valid, missing;
        public double? mean;
        public static NumericGroup Calculate(IEnumerable<int> nodeIds, IDictionary<int, double> values)
        {
            var ids = nodeIds.Distinct().ToArray();
            var numbers = ids.Where(values.ContainsKey).Select(id => values[id]).Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToArray();
            return new NumericGroup { total = ids.Length, valid = numbers.Length, missing = ids.Length - numbers.Length,
                mean = numbers.Length == 0 ? (double?)null : numbers.Average() };
        }
    }

    /// <summary>
    /// Compare a saved node set with its complement in the original scope.
    /// Difference is group mean minus remainder mean, or null without both means.
    /// These results do not establish statistical significance or causation.
    /// </summary>
    [Serializable]
    public sealed class NumericComparison
    {
        public string attribute, scope;
        public NumericGroup selected, remaining;
        public double? difference;
        public static NumericComparison Calculate(string attribute, string scope, IEnumerable<int> group, IEnumerable<int> universe, IDictionary<int, double> values)
        {
            var all = new HashSet<int>(universe);
            var target = new HashSet<int>(group);
            target.IntersectWith(all);
            var a = NumericGroup.Calculate(target, values);
            var b = NumericGroup.Calculate(all.Except(target), values);
            return new NumericComparison { attribute = attribute, scope = scope, selected = a, remaining = b, difference = a.mean - b.mean };
        }
        public string Describe()
        {
            string Number(double? value) => value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "no data";
            return $"{attribute} in the saved group: mean {Number(selected.mean)} ({selected.valid}/{selected.total} valid; {selected.missing} missing). " +
                $"Remaining nodes: mean {Number(remaining.mean)} ({remaining.valid}/{remaining.total} valid; {remaining.missing} missing). " +
                $"Mean difference (group minus remainder): {Number(difference)}. Scope: {scope}. Missing/non-finite values excluded; descriptive comparison only, no significance test.";
        }
    }

    // REMINDER: We do not care about deterministic results but rather "Correct" output
    //           So might need to rework this later!

    /// <summary>Deterministic ranking and numeric filtering used by execution.</summary>
    public static class PlanMath
    {
        /// <summary>
        /// Rank by incident relationship count in the requested direction.
        /// Deduplicate relationship IDs, retain zero-degree candidates, and
        /// break ties by ascending node ID. In both directions, count a self-loop
        /// once; parallel relationships with distinct IDs remain separate.
        /// The caller supplies relationships from the snapshot scope.
        /// </summary>
        public static int[] Rank(IEnumerable<int> candidates, IEnumerable<(int id, int source, int target)> links, string direction, int limit)
        {
            var counts = candidates.Distinct().ToDictionary(id => id, _ => 0);
            foreach (var link in links.GroupBy(l => l.id).Select(g => g.First()))
            {
                if (direction != "incoming" && counts.ContainsKey(link.source)) counts[link.source]++;
                if (direction != "outgoing" && counts.ContainsKey(link.target) && (direction == "incoming" || link.target != link.source)) counts[link.target]++;
            }
            return counts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).Take(limit).Select(pair => pair.Key).ToArray();
        }
        public static bool Compare(double value, string operation, double threshold) => operation switch
        {
            "gt" => value > threshold, "gte" => value >= threshold, "lt" => value < threshold,
            "lte" => value <= threshold, "eq" => value == threshold, _ => throw new ArgumentException("Unknown comparison.")
        };
    }
}
