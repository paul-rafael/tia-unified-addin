using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Prh.UnifiedKonverter.Openness;

namespace Prh.UnifiedKonverter.Report
{
    /// <summary>
    /// Schreibt den Bericht des Machbarkeitstests nach Dokumente\Unified-Konverter\Berichte\&lt;Zeitstempel&gt;:
    /// Variablen.csv (alle Attribute je Variable), Attribute.csv (Attribut, Zugriffsart, Beispielwert), Struktur.txt.
    /// CSV mit Semikolon und UTF-8-BOM, damit deutsches Excel sie direkt öffnet.
    /// </summary>
    internal static class ReportWriter
    {
        public static string Write(IList<TagTableSnapshot> snapshots)
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Unified-Konverter", "Berichte", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            Directory.CreateDirectory(dir);

            WriteTags(Path.Combine(dir, "Variablen.csv"), snapshots);
            WriteAttributes(Path.Combine(dir, "Attribute.csv"), snapshots);
            WriteStructure(Path.Combine(dir, "Struktur.txt"), snapshots);
            return dir;
        }

        private static void WriteTags(string path, IList<TagTableSnapshot> snapshots)
        {
            List<string> attrNames = snapshots.SelectMany(s => s.Tags).SelectMany(t => t.Attributes)
                .Select(a => a.Name).Distinct().ToList();

            StringBuilder sb = new StringBuilder();
            AppendRow(sb, new[] { "Gerät", "Art", "Tabelle", "Variable" }.Concat(attrNames));
            foreach (TagTableSnapshot s in snapshots)
            {
                foreach (TagRow t in s.Tags)
                {
                    IEnumerable<string> values = attrNames.Select(n =>
                    {
                        AttributeValue a = t.Attributes.FirstOrDefault(x => x.Name == n);
                        if (a == null) return string.Empty;
                        return a.Error != null ? "#FEHLER " + a.Error : a.Value;
                    });
                    AppendRow(sb, new[] { s.DeviceName, s.Kind.ToString(), s.TableName, t.Name }.Concat(values));
                }
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        private static void WriteAttributes(string path, IList<TagTableSnapshot> snapshots)
        {
            StringBuilder sb = new StringBuilder();
            AppendRow(sb, new[] { "Art", "Variablentyp", "Attribut", "Zugriff", "Beispielwert", "Lesefehler" });
            foreach (IGrouping<string, TagRow> group in snapshots
                .SelectMany(s => s.Tags.Select(t => new { s.Kind, Tag = t }))
                .GroupBy(x => x.Kind + "|" + x.Tag.TypeName, x => x.Tag))
            {
                string[] key = group.Key.Split('|');
                Dictionary<string, AttributeValue> seen = new Dictionary<string, AttributeValue>();
                foreach (TagRow t in group)
                {
                    foreach (AttributeValue a in t.Attributes)
                    {
                        AttributeValue existing;
                        if (!seen.TryGetValue(a.Name, out existing) || (string.IsNullOrEmpty(existing.Value) && !string.IsNullOrEmpty(a.Value)))
                        {
                            seen[a.Name] = a;
                        }
                    }
                }
                foreach (AttributeValue a in seen.Values.OrderBy(x => x.Name))
                {
                    AppendRow(sb, new[] { key[0], key[1], a.Name, a.Access, a.Value, a.Error });
                }
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        private static void WriteStructure(string path, IList<TagTableSnapshot> snapshots)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Unified-Konverter 0.1 – Machbarkeitstest (nur lesen)");
            sb.AppendLine("Erstellt: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"));
            sb.AppendLine("Add-In-Prozess: " + (Environment.Is64BitProcess ? "64 Bit" : "32 Bit") + ", .NET " + Environment.Version);
            foreach (TagTableSnapshot s in snapshots)
            {
                sb.AppendLine();
                sb.AppendLine("Tabelle:  " + s.TableName + "  (" + s.TableTypeName + ")");
                sb.AppendLine("Gerät:    " + (s.DeviceName ?? "nicht gefunden") + "  (" + s.Kind + ")");
                sb.AppendLine("Variablen: " + s.Tags.Count);
                sb.AppendLine("Kompositionen des HMI-Geräts: " + string.Join(", ", s.DeviceCompositions));
                sb.AppendLine("Attribute des HMI-Geräts: " + string.Join(", ", s.DeviceAttributes));
                foreach (string p in s.Problems) sb.AppendLine("Problem: " + p);
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        private static void AppendRow(StringBuilder sb, IEnumerable<string> cells)
        {
            sb.AppendLine(string.Join(";", cells.Select(Escape)));
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            bool quote = value.IndexOfAny(new[] { ';', '"', '\r', '\n' }) >= 0;
            return quote ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }
    }
}
