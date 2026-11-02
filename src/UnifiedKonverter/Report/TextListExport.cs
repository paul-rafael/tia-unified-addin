using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Prh.UnifiedKonverter.Excel;
using Prh.UnifiedKonverter.Rules;
using Prh.UnifiedKonverter.Transfer;

namespace Prh.UnifiedKonverter.Report
{
    /// <summary>
    /// Schritt 1: Textlisten der alten HMI (über Openness gelesen) als Excel im Aufbau des TIA-Textlisten-Exports
    /// (Blätter „TextList“ und „TextListEntry“). Namen nach der Converter-Regel „Textlisten“
    /// (TextList!A und TextListEntry!B: „#“, „/“, „.“ → „_“, „[“ → „(“, „]“ → „)“).
    /// Leere Sprachen werden wie beim klassischen Converter gefüllt: Text einer anderen Sprache, sonst „—“.
    /// </summary>
    internal static class TextListExport
    {
        public const string FileName = "TextLists_MIGUNI.xlsx";
        private const string Dash = "—";

        // Mögliche Openness-Attributnamen (Comfort/Advanced), Reihenfolge = Vorrang
        private static readonly string[] RangeNames = { "ListRange", "TextListRange", "Range", "Type" };
        private static readonly string[] DefaultNames = { "DefaultEntry", "IsDefault", "Default", "IsDefaultEntry" };
        private static readonly string[] ValueNames = { "Value", "RangeValue", "Range", "From" };

        private static readonly Regex Paragraph = new Regex(@"</p>\s*<p>", RegexOptions.IgnoreCase);
        private static readonly Regex Wrapper = new Regex(@"</?(body|p)\s*>", RegexOptions.IgnoreCase);

        public sealed class Result
        {
            public int Lists;
            public int Entries;
            public int RenamedLists;
            public int FilledTexts;
        }

        public static Result Write(string path, IList<SourceTextList> lists)
        {
            Result result = new Result();
            List<string> cultures = lists.SelectMany(l => l.Comments.Keys.Concat(l.Entries.SelectMany(e => e.Texts.Keys)))
                .Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c.Equals("de-DE", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
            if (cultures.Count == 0) cultures.Add("de-DE");

            XlsxSheet listSheet = new XlsxSheet { Name = "TextList" };
            listSheet.Rows.Add(new object[] { "Name", "ListRange" }.Concat(cultures.Select(c => "Comment [" + c + "]")).ToArray());
            XlsxSheet entrySheet = new XlsxSheet { Name = "TextListEntry" };
            // „FieldInfos“ wie im aktuellen TIA-Export hinter den Sprachen; Openness liefert die Feldangaben nicht, die Spalte bleibt leer.
            entrySheet.Rows.Add(new object[] { "Name", "Parent", "Default entry", "Value" }.Concat(cultures.Select(c => "Text [" + c + "]")).Concat(new[] { "FieldInfos" }).ToArray());

            foreach (SourceTextList list in lists)
            {
                string name = ConverterRules.CleanTextListName(list.Name);
                if (name != list.Name) result.RenamedLists++;
                result.Lists++;
                listSheet.Rows.Add(new object[] { name, Pick(list.Attributes, RangeNames) }
                    .Concat(cultures.Select(c => (object)Lookup(list.Comments, c, cultures.Count))).ToArray());

                int index = 0;
                foreach (SourceTextListEntry entry in list.Entries)
                {
                    index++;
                    result.Entries++;
                    Dictionary<string, string> texts = entry.Texts.ToDictionary(kv => kv.Key, kv => CleanText(kv.Value), StringComparer.OrdinalIgnoreCase);
                    string fallback = cultures.Select(c => Lookup(texts, c, cultures.Count)).FirstOrDefault(t => !string.IsNullOrEmpty(t));
                    List<object> row = new List<object>
                    {
                        Pick(entry.Attributes, "Name") ?? "Entry_" + index,
                        name,
                        Bool(Pick(entry.Attributes, DefaultNames)),
                        Pick(entry.Attributes, ValueNames),
                    };
                    foreach (string c in cultures)
                    {
                        string text = Lookup(texts, c, cultures.Count);
                        if (string.IsNullOrEmpty(text))
                        {
                            text = string.IsNullOrEmpty(fallback) ? Dash : fallback;
                            result.FilledTexts++;
                        }
                        row.Add(text);
                    }
                    entrySheet.Rows.Add(row.ToArray());
                }
            }

            XlsxWriter.Write(path, new[] { listSheet, entrySheet });
            return result;
        }

        /// <summary>Openness liefert formatierte Texte als „&lt;body&gt;&lt;p&gt;…&lt;/p&gt;&lt;/body&gt;“ – nur den Text behalten, Feld-Verweise bleiben.</summary>
        public static string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string t = Paragraph.Replace(text, "\n");
            t = Wrapper.Replace(t, "");
            return t.Trim();
        }

        private static string Lookup(Dictionary<string, string> texts, string culture, int cultureCount)
        {
            string text;
            if (texts.TryGetValue(culture, out text)) return text;
            // Text ohne Sprachangabe (einsprachiges Projekt)
            return cultureCount == 1 && texts.TryGetValue("", out text) ? text : null;
        }

        private static string Pick(Dictionary<string, string> attrs, params string[] names)
        {
            foreach (string n in names)
            {
                string v;
                if (attrs.TryGetValue(n, out v) && !string.IsNullOrEmpty(v)) return v;
            }
            return null;
        }

        private static string Bool(string value)
        {
            if (string.IsNullOrEmpty(value)) return "False";
            return value.Equals("true", StringComparison.OrdinalIgnoreCase) ? "True" : value.Equals("false", StringComparison.OrdinalIgnoreCase) ? "False" : value;
        }
    }
}
