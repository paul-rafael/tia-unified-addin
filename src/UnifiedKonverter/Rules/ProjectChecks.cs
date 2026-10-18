using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Prh.UnifiedKonverter.Openness;

namespace Prh.UnifiedKonverter.Rules
{
    /// <summary>
    /// Prüfungen vor Data2Unified. Setzt je Variable den bereinigten Namen und die Befunde.
    /// Warnung = muss vor der Migration behoben werden, Info = bitte prüfen.
    /// </summary>
    internal static class ProjectChecks
    {
        /// <summary>Kleinster Erfassungszyklus, den WinCC Unified erlaubt.</summary>
        public const double MinUnifiedCycleMs = 250;

        private static readonly Regex CycleRegex = new Regex(@"(\d+(?:[.,]\d+)?)\s*(ms|min|s|m|h)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static void Run(ReadResult result)
        {
            foreach (HmiTagInfo tag in result.AllTags)
            {
                tag.Findings.Clear();
                tag.UnifiedName = ConverterRules.CleanTagName(tag.Name);
                CheckName(tag);
                CheckCycle(tag);
                CheckMode(tag);
                CheckDataType(tag);
            }
            CheckCollisions(result);
        }

        private static void CheckName(HmiTagInfo tag)
        {
            if (!tag.IsRenamed) return;
            string chars = new string(tag.Name.Where((c, i) => tag.UnifiedName[i] == '_' && c != '_').Distinct().ToArray());
            Add(tag, Severity.Warnung, FindingKind.Sonderzeichen, "Sonderzeichen " + Quote(Visible(chars)) + " → " + tag.UnifiedName);
        }

        /// <summary>Gleiche Namen nach der Bereinigung je HMI-Gerät, z. B. „A.B“ und „A_B“. TIA unterscheidet bei Namen keine Groß-/Kleinschreibung.</summary>
        private static void CheckCollisions(ReadResult result)
        {
            foreach (IGrouping<HmiDeviceInfo, HmiTagInfo> device in result.AllTags.GroupBy(t => t.Table.Device))
            {
                foreach (IGrouping<string, HmiTagInfo> same in device.GroupBy(t => t.UnifiedName, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                {
                    List<HmiTagInfo> tags = same.ToList();
                    foreach (HmiTagInfo tag in tags)
                    {
                        IEnumerable<string> others = tags.Where(o => o != tag).Select(o => o.Name + (o.Table == tag.Table ? "" : " (" + o.Table.Name + ")"));
                        Add(tag, Severity.Warnung, FindingKind.Kollision, "Kollision mit " + string.Join(", ", others) + " – ergibt denselben Namen " + Quote(same.Key));
                    }
                }
            }
        }

        private static void CheckCycle(HmiTagInfo tag)
        {
            double? ms = CycleMs(tag.AcquisitionCycle);
            if (ms.HasValue && ms.Value < MinUnifiedCycleMs)
            {
                Add(tag, Severity.Warnung, FindingKind.Erfassungszyklus,
                    "Erfassungszyklus " + ms.Value.ToString("0.###", CultureInfo.InvariantCulture) + " ms – Unified erlaubt mindestens 250 ms");
            }
        }

        private static void CheckMode(HmiTagInfo tag)
        {
            string mode = (tag.AcquisitionMode ?? string.Empty).ToLowerInvariant();
            if (mode.Contains("continuous") || mode.Contains("fortlaufend"))
            {
                Add(tag, Severity.Warnung, FindingKind.Erfassungsart, "Erfassungsart „Zyklisch fortlaufend“");
            }
        }

        private static void CheckDataType(HmiTagInfo tag)
        {
            string type = (tag.DataType ?? string.Empty).Trim().ToLowerInvariant();
            if (type == "uint")
                Add(tag, Severity.Info, FindingKind.Datentyp, "UInt – falls Melde-/Störwort: in Unified Word verwenden");
            else if (type == "string" || type == "wstring" || type.StartsWith("string[", StringComparison.Ordinal) || type.StartsWith("wstring[", StringComparison.Ordinal))
                Add(tag, Severity.Info, FindingKind.Datentyp, "String-Variable – nach der Migration prüfen");
            else if (type.StartsWith("array", StringComparison.Ordinal))
                Add(tag, Severity.Info, FindingKind.Datentyp, "Array-Variable – nach der Migration prüfen");
        }

        /// <summary>„200 ms“, „T200ms“, „1 s“, „1 min“ → Millisekunden; unbekanntes Format → null.</summary>
        public static double? CycleMs(string cycle)
        {
            if (string.IsNullOrEmpty(cycle)) return null;
            Match m = CycleRegex.Match(cycle);
            if (!m.Success) return null;
            double value;
            if (!double.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return null;
            switch (m.Groups[2].Value.ToLowerInvariant())
            {
                case "ms": return value;
                case "s": return value * 1000;
                case "m":
                case "min": return value * 60000;
                case "h": return value * 3600000;
                default: return null;
            }
        }

        private static void Add(HmiTagInfo tag, Severity severity, FindingKind kind, string text)
        {
            tag.Findings.Add(new Finding { Severity = severity, Kind = kind, Text = text });
        }

        private static string Quote(string s)
        {
            return "„" + s + "“";
        }

        private static string Visible(string chars)
        {
            return string.Join(" ", chars.Select(c => c == ' ' ? "Leerzeichen" : c == '\t' ? "Tab" : c.ToString()));
        }
    }
}
