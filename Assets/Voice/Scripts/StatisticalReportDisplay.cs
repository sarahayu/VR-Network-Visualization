using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using VidiGraph;
using Neo4j.Driver;

namespace Whisper.Samples
{
    /// <summary>
    /// Executes statistical Cypher queries returned in the classification JSON
    /// and writes their results to a legacy Unity UI Text component.
    /// </summary>
    public class StatisticalReportDisplay : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private DatabaseStorage databaseStorage;

        [Header("Output")]
        [SerializeField] private Text outputText;
        [SerializeField] private bool clearPreviousReport = true;
        [SerializeField] private string reportHeading = "Statistical report";
        [SerializeField, Min(1)] private int queryTimeoutSeconds = 10;

        private int reportVersion;
        private string activeCommandId;

        /// <summary>
        /// Executes the deserialized `stats` portion of ClassificationResponse.
        /// Each item supplies a display label and a Cypher query that returns one numeric value.
        /// </summary>
        public void BeginCommand()
        {
            activeCommandId = null;
            ++reportVersion; // Invalidate an earlier asynchronous statistical report.
            if (outputText != null && clearPreviousReport) outputText.text = string.Empty;
        }

        public void DisplayReports(StatisticalQuery[] statisticalQueries)
            => DisplayCommandReport(null, statisticalQueries);

        public async void DisplayCommandReport(CommandExecutionReport execution, StatisticalQuery[] statisticalQueries)
        {
            int currentVersion = ++reportVersion;
            activeCommandId = execution?.CommandId;

            if (outputText == null)
            {
                Debug.LogError("StatisticalReportDisplay requires an Output Text reference.", this);
                return;
            }

            string previousText = clearPreviousReport ? string.Empty : outputText.text;
            var report = new StringBuilder();
            string summary = execution?.Summary();
            if (!string.IsNullOrWhiteSpace(summary))
                report.AppendLine("<b>Command result</b>").AppendLine(EscapeText(summary));
            if (statisticalQueries == null || statisticalQueries.Length == 0)
            {
                outputText.text = JoinReports(previousText, report.ToString().TrimEnd());
                return;
            }
            if (!string.IsNullOrWhiteSpace(reportHeading))
                report.AppendLine($"<b>{EscapeText(reportHeading)}</b>");
            if (networkManager == null || databaseStorage == null)
            {
                report.AppendLine("Statistical report unavailable: data references are not assigned.");
                outputText.text = JoinReports(previousText, report.ToString().TrimEnd());
                return;
            }
            outputText.text = JoinReports(previousText, report + "Calculating…");

            foreach (StatisticalQuery statistic in statisticalQueries)
            {
                if (currentVersion != reportVersion || outputText == null) return;
                if (statistic == null || string.IsNullOrWhiteSpace(statistic.query))
                    continue;

                string label = EscapeText(GetDisplayLabel(statistic));

                try
                {
                    double result = await databaseStorage.GetValueFromStoreAsync(
                        networkManager.NetworkGlobal,
                        statistic.query,
                        queryTimeoutSeconds
                    );

                    if (currentVersion != reportVersion || outputText == null)
                        return;

                    report.Append(label)
                        .Append(": ")
                        .AppendLine(double.IsNaN(result)
                            ? "no data"
                            : result.ToString("0.###", CultureInfo.InvariantCulture));
                }
                catch (TransientException exception)
                {
                    Debug.LogError($"Statistical query timed out or was interrupted: {statistic.query}\n{exception}", this);
                    report.Append(label)
                        .AppendLine(": query timed out");
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Statistical query failed: {statistic.query}\n{exception}", this);
                    report.Append(label)
                        .AppendLine(": unable to calculate");
                }
            }

            if (currentVersion != reportVersion || outputText == null)
                return;

            outputText.text = JoinReports(previousText, report.ToString().TrimEnd());
        }

        public void ShowPlanProgress(string commandId, string plan, int step, int total, string description)
        {
            ++reportVersion;
            activeCommandId = commandId;
            if (outputText != null)
                outputText.text = $"<b>Plan</b>\n{EscapeText(plan)}\nStep {step}/{total}: {EscapeText(description)}\nSay or type 'cancel' to stop after the current step.";
        }

        public void AppendExplanation(string commandId, string explanation)
        {
            if (outputText == null || commandId != activeCommandId || string.IsNullOrWhiteSpace(explanation)) return;
            outputText.text += "\n\n<b>Jev summary</b>\n" + EscapeText(explanation);
        }

        private static string JoinReports(string previous, string current) =>
            string.IsNullOrEmpty(previous) ? current : string.IsNullOrEmpty(current) ? previous : previous + "\n" + current;

        private static string EscapeText(string value) => value.Replace("<", "‹").Replace(">", "›");

        private static string GetDisplayLabel(StatisticalQuery statistic)
        {
            string suppliedLabel = statistic.label?.Trim();
            if (!IsGenericLabel(suppliedLabel))
                return suppliedLabel;

            string query = statistic.query ?? string.Empty;

            Match category = Regex.Match(
                query,
                @"\bn\.(?<attribute>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?:'(?<quoted>[^']+)'|""(?<doubleQuoted>[^""]+)""|(?<plain>[-+]?\d+(?:\.\d+)?|true|false))",
                RegexOptions.IgnoreCase
            );
            if (category.Success)
            {
                string value = category.Groups["quoted"].Success
                    ? category.Groups["quoted"].Value
                    : category.Groups["doubleQuoted"].Success
                        ? category.Groups["doubleQuoted"].Value
                        : category.Groups["plain"].Value;
                return $"{ToDisplayName(category.Groups["attribute"].Value)} {value}";
            }

            Match aggregate = Regex.Match(
                query,
                @"\b(?<function>avg|min|max|sum)\s*\(\s*(?:toFloat\s*\(\s*)?(?:[nr]\.)?(?<attribute>[A-Za-z_][A-Za-z0-9_]*)?",
                RegexOptions.IgnoreCase
            );
            if (aggregate.Success)
            {
                string prefix = aggregate.Groups["function"].Value.ToLowerInvariant() switch
                {
                    "avg" => "Average",
                    "min" => "Minimum",
                    "max" => "Maximum",
                    _ => "Total"
                };
                string attribute = ToDisplayName(aggregate.Groups["attribute"].Value);
                return string.IsNullOrEmpty(attribute) ? prefix : $"{prefix} {attribute}";
            }

            return "Total Affected";
        }

        private static bool IsGenericLabel(string label)
        {
            return string.IsNullOrWhiteSpace(label)
                || label.Equals("Statistical report", StringComparison.OrdinalIgnoreCase)
                || Regex.IsMatch(label, @"^Result(?:\s*\d+)?$", RegexOptions.IgnoreCase)
                || label.Equals("Statistic", StringComparison.OrdinalIgnoreCase)
                || label.Equals("Value", StringComparison.OrdinalIgnoreCase);
        }

        private static string ToDisplayName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string spaced = Regex.Replace(value.Trim(), @"[_-]+", " ");
            spaced = Regex.Replace(spaced, @"(?<=[a-z0-9])(?=[A-Z])", " ");
            if (spaced.Equals("gpa", StringComparison.OrdinalIgnoreCase))
                return "GPA";

            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced.ToLowerInvariant());
        }

    }
}
