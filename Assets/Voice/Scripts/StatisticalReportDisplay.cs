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

        /// <summary>
        /// Executes the deserialized `stats` portion of ClassificationResponse.
        /// Each item supplies a display label and a Cypher query that returns one numeric value.
        /// </summary>
        public async void DisplayReports(StatisticalQuery[] statisticalQueries)
        {
            int currentVersion = ++reportVersion;

            if (outputText == null)
            {
                Debug.LogError("StatisticalReportDisplay requires an Output Text reference.", this);
                return;
            }

            if (networkManager == null || databaseStorage == null)
            {
                ShowMessage("Statistical report unavailable: data references are not assigned.");
                Debug.LogError("StatisticalReportDisplay requires Network Manager and Database Storage references.", this);
                return;
            }

            if (statisticalQueries == null || statisticalQueries.Length == 0)
            {
                if (clearPreviousReport)
                    outputText.text = string.Empty;
                return;
            }

            var report = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(reportHeading))
                report.AppendLine($"<b>{reportHeading}</b>");

            outputText.text = report + "Calculating…";

            foreach (StatisticalQuery statistic in statisticalQueries)
            {
                if (statistic == null || string.IsNullOrWhiteSpace(statistic.query))
                    continue;

                string label = GetDisplayLabel(statistic);

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

            string reportText = report.ToString().TrimEnd();
            if (clearPreviousReport)
                outputText.text = reportText;
            else if (!string.IsNullOrEmpty(reportText))
                outputText.text += (string.IsNullOrEmpty(outputText.text) ? "" : "\n") + reportText;
        }

        private void ShowMessage(string message)
        {
            if (outputText == null)
                return;

            if (clearPreviousReport)
                outputText.text = message;
            else
                outputText.text += (string.IsNullOrEmpty(outputText.text) ? "" : "\n") + message;
        }

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
