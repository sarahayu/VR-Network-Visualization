using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Whisper.Samples
{
    /// <summary>
    /// Manages the persistent legend panel that tracks what colors, shapes,
    /// and edge colors mean in the current visualization session.
    /// All entries start as "Not used" and are updated by commands.
    /// </summary>
    public class LegendManager : MonoBehaviour
    {
        [Header("UI")]
        public TMP_Text legendText;

        // ---- Node Color ----
        // 6 fixed palette slots. sel colors match server ALLOWED_COLORS exactly so legend
        // slots are found and updated (not duplicated) when a command uses one of these colors.
        private static readonly (string reg, string sel)[] NodePalette =
        {
            ("#FF9999", "#FF0000"),   // red
            ("#FFDB99", "#FFA500"),   // orange
            ("#99FF99", "#00FF00"),   // green
            ("#9999FF", "#0000FF"),   // blue
            ("#CC99CC", "#800080"),   // purple
            ("#FFC3E1", "#FF69B4"),   // pink
        };

        private struct NodeColorEntry { public string reg; public string sel; public string label; public bool isGradient; public string[] gradientColors; }
        private NodeColorEntry[] _nodeColors = new NodeColorEntry[6];
        private int _nextColorSlot = 0;

        // ---- Node Shape ----
        // Order matches VR shape indices: 0=sphere, 1=cube, 2=triangle.
        // Default hides sphere (empty label) so only Cube + Triangle show until a command assigns it.
        private static readonly string[] ShapeSymbols = { "●", "■", "▲" };
        private static readonly string[] ShapeDefaults = { "", "Not used", "Not used" };
        private string[] _shapeLabels = new string[3];

        // ---- Edge Color ----
        // Stores (hex, label) pairs in order they were added.
        private List<(string hex, string label)> _edgeColors = new List<(string, string)>();

        private void Awake()
        {
            ResetAll();
        }

        // ------------------------------------------------------------------ //
        //  Public API
        // ------------------------------------------------------------------ //

        public void ResetAll()
        {
            for (int i = 0; i < 6; i++)
                _nodeColors[i] = new NodeColorEntry { reg = NodePalette[i].reg, sel = NodePalette[i].sel, label = "Not used" };
            _nextColorSlot = 0;

            for (int i = 0; i < 3; i++) _shapeLabels[i] = ShapeDefaults[i];

            _edgeColors.Clear();
            Rebuild();
        }

        /// <summary>
        /// Call after a colorNode action. Finds a matching slot or claims the next free one.
        /// </summary>
        public void SetNodeColorLabel(string hexColor, string label)
        {
            Debug.Log($"[LegendManager] SetNodeColorLabel({hexColor}, {label}) — legendText={(legendText != null ? "assigned" : "NULL")}");
            hexColor = hexColor.ToUpper();

            // Update existing slot that already uses this hex
            for (int i = 0; i < _nodeColors.Length; i++)
            {
                if (string.Equals(_nodeColors[i].sel, hexColor, System.StringComparison.OrdinalIgnoreCase))
                {
                    _nodeColors[i].label = label;
                    _nodeColors[i].isGradient = false;
                    Rebuild();
                    return;
                }
            }

            // Assign to next free slot
            if (_nextColorSlot < _nodeColors.Length)
            {
                int slot = _nextColorSlot++;
                _nodeColors[slot].sel = hexColor;
                _nodeColors[slot].reg = Lighten(hexColor);
                _nodeColors[slot].label = label;
                _nodeColors[slot].isGradient = false;
                Rebuild();
            }
        }

        /// <summary>
        /// Call after colorByAttribute. Replaces all node color slots with the mapping.
        /// </summary>
        public void SetNodeColorMapping(IEnumerable<(string label, string hex)> mapping)
        {
            for (int i = 0; i < _nodeColors.Length; i++)
                _nodeColors[i] = new NodeColorEntry { reg = NodePalette[i].reg, sel = NodePalette[i].sel, label = "Not used" };
            _nextColorSlot = 0;

            foreach (var (label, hex) in mapping)
            {
                if (_nextColorSlot >= _nodeColors.Length) break;
                int slot = _nextColorSlot++;
                _nodeColors[slot].sel = hex;
                _nodeColors[slot].reg = Lighten(hex);
                _nodeColors[slot].label = label;
                _nodeColors[slot].isGradient = false;
            }
            Rebuild();
        }

        /// <summary>
        /// Call after colorByGPA / colorByValue. Shows a gradient strip in slot 0.
        /// </summary>
        public void SetNodeGradient(string attributeLabel, string[] gradientHexColors)
        {
            // Reset node slots, put gradient in slot 0
            for (int i = 0; i < _nodeColors.Length; i++)
                _nodeColors[i] = new NodeColorEntry { reg = NodePalette[i].reg, sel = NodePalette[i].sel, label = "Not used" };
            _nextColorSlot = 1;

            _nodeColors[0] = new NodeColorEntry
            {
                isGradient = true,
                gradientColors = gradientHexColors,
                label = attributeLabel + " (low → high)"
            };
            Rebuild();
        }

        /// <summary>
        /// Call after shapeByAttribute. Assigns labels to the three shape slots.
        /// </summary>
        public void SetShapeMapping(IEnumerable<string> labels)
        {
            for (int i = 0; i < 3; i++) _shapeLabels[i] = ShapeDefaults[i];
            int i2 = 0;
            foreach (var label in labels)
            {
                if (i2 >= 3) break;
                _shapeLabels[i2++] = label;
            }
            Rebuild();
        }

        /// <summary>
        /// Call after colorLink. Adds/updates an edge color entry. Gray (#808080) is skipped (it means "reset").
        /// </summary>
        public void SetEdgeColorLabel(string hexColor, string label)
        {
            if (string.Equals(hexColor, "#808080", System.StringComparison.OrdinalIgnoreCase)) return;

            hexColor = hexColor.ToUpper();
            for (int i = 0; i < _edgeColors.Count; i++)
            {
                if (string.Equals(_edgeColors[i].hex, hexColor, System.StringComparison.OrdinalIgnoreCase))
                {
                    _edgeColors[i] = (hexColor, label);
                    Rebuild();
                    return;
                }
            }
            _edgeColors.Add((hexColor, label));
            Rebuild();
        }

        // ------------------------------------------------------------------ //
        //  Internal
        // ------------------------------------------------------------------ //

        private void Rebuild()
        {
            if (legendText == null)
            {
                Debug.LogWarning("[LegendManager] legendText is not assigned — legend will not display.");
                return;
            }

            var sb = new StringBuilder();

            // ---- Node Color ---- (always show all slots, including "Not used")
            sb.AppendLine("<b>Node Color (Regular / Selected)</b>");
            sb.AppendLine();
            foreach (var e in _nodeColors)
            {
                if (e.isGradient && e.gradientColors != null)
                {
                    foreach (var c in e.gradientColors) sb.Append($"<color={c}>■</color>");
                    sb.AppendLine($"   {e.label}");
                }
                else
                {
                    sb.AppendLine($"<color={e.reg}>■</color><color={e.sel}>■</color>   {e.label}");
                }
            }

            // ---- Node Shape ---- (skip slots with empty label)
            sb.AppendLine();
            sb.AppendLine("<b>Node Shape</b>");
            sb.AppendLine();
            var shapeEntries = ShapeSymbols
                .Select((sym, i) => (sym, label: _shapeLabels[i]))
                .Where(x => !string.IsNullOrEmpty(x.label))
                .ToArray();
            if (shapeEntries.Length > 0)
                sb.AppendLine(string.Join("    ", shapeEntries.Select(x => $"{x.sym} {x.label}")));

            // ---- Edge Color ---- (show used entries; if none, show 2 placeholder rows)
            sb.AppendLine();
            sb.AppendLine("<b>Edge Color</b>");
            sb.AppendLine();
            if (_edgeColors.Count > 0)
            {
                foreach (var (hex, label) in _edgeColors)
                    sb.AppendLine($"<color={hex}>A → B</color>  {label}");
            }
            else
            {
                sb.AppendLine("<color=#808080>A → B</color>  Not used");
                sb.AppendLine("<color=#808080>A → B</color>  Not used");
            }

            legendText.text = sb.ToString().TrimEnd();
            legendText.ForceMeshUpdate();
        }

        /// <summary>
        /// Converts a raw Cypher attribute value into a human-readable legend label.
        /// e.g. PrettifyLabel("smoker", "true") → "Smoker"
        ///      PrettifyLabel("smoker", "false") → "Non-smoker"
        ///      PrettifyLabel("grade", "9") → "Grade: 9"
        ///      PrettifyLabel("sex", "'F'") → "Sex: F"
        /// </summary>
        public static string PrettifyLabel(string attrName, string cypherValue)
        {
            string val = cypherValue.Trim('\'', ' ');
            string attr = attrName.Length == 0 ? "" :
                char.ToUpper(attrName[0]) + attrName.Substring(1).ToLower();

            if (val.ToLower() == "true")  return attr;
            if (val.ToLower() == "false") return "Non-" + attrName.ToLower();
            return $"{attr}: {val}";
        }

        /// <summary>
        /// Converts a selectNode action parameter into a short legend label.
        /// Handles WHERE-clause patterns ("n.smoker = true" → "Smoker"),
        /// known keywords ("top3friendship" → "Top 3 Friendship"), and plain names.
        /// </summary>
        public static string ToShortLabel(string actionParam)
        {
            // Handle WHERE-clause patterns: "n.attribute = value" or "n.attribute = 'value'"
            var m = System.Text.RegularExpressions.Regex.Match(
                actionParam, @"n\.(\w+)\s*=\s*(.+)");
            if (m.Success)
                return PrettifyLabel(m.Groups[1].Value, m.Groups[2].Value.Trim());

            return actionParam.ToLower() switch
            {
                "smoker"         => "Smokers",
                "top3friendship" => "Top 3 Friendship",
                "top5friendship" => "Top 5 Friendship",
                "topaggression"  => "Top Aggression",
                "all"            => "",
                _ => actionParam.Length > 0
                    ? char.ToUpper(actionParam[0]) + actionParam.Substring(1)
                    : ""
            };
        }

        private static string Lighten(string hex)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out Color c)) return hex;
            float t = 0.6f;
            Color light = new Color(c.r + (1 - c.r) * t, c.g + (1 - c.g) * t, c.b + (1 - c.b) * t);
            return $"#{Mathf.RoundToInt(light.r * 255):X2}{Mathf.RoundToInt(light.g * 255):X2}{Mathf.RoundToInt(light.b * 255):X2}";
        }
    }
}
