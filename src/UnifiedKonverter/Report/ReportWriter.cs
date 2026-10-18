using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Prh.UnifiedKonverter.Openness;

namespace Prh.UnifiedKonverter.Report
{
    /// <summary>
    /// Schreibt den Bericht nach Dokumente\Unified-Konverter\Berichte\&lt;Zeitstempel&gt;:
    /// Pruefung.csv (alle Variablen mit Unified-Name und Befunden), Attribute.csv (Openness-Attribute je Variablentyp
    /// mit Zugriffsart), Struktur.txt (Geräte, Tabellen, Kompositionen). CSV mit Semikolon und UTF-8-BOM für deutsches Excel.
    /// </summary>
    internal static class ReportWriter
    {
        public static string Write(ReadResult result)
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Unified-Konverter", "Berichte", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            Directory.CreateDirectory(dir);

            WriteCheck(Path.Combine(dir, "Pruefung.csv"), result);
            WriteAttributes(Path.Combine(dir, "Attribute.csv"), result);
            WriteStructure(Path.Combine(dir, "Struktur.txt"), result);
            return dir;
        }

        private static void WriteCheck(string path, ReadResult result)
        {
            StringBuilder sb = new StringBuilder();
            AppendRow(sb, new[] { "Gerät", "Art", "Ordner", "Tabelle", "Name", "Name in Unified", "Datentyp", "Länge", "Verbindung",
                "PLC-Variable", "Erfassungszyklus", "Erfassungsart", "Stufe", "Hinweise" });
            foreach (HmiTagInfo t in result.AllTags)
            {
                string level = t.HasWarning ? "Warnung" : t.Findings.Count > 0 ? "Info" : "";
                AppendRow(sb, new[] { t.Table.Device.Name, t.Table.Device.KindText, t.Table.Folder, t.Table.Name, t.Name, t.UnifiedName,
                    t.DataType, t.Length, t.Connection, t.PlcTag, t.AcquisitionCycle, t.AcquisitionMode, level,
                    string.Join(" · ", t.Findings.Select(f => f.Text)) });
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        private static void WriteAttributes(string path, ReadResult result)
        {
            StringBuilder sb = new StringBuilder();
            AppendRow(sb, new[] { "Art", "Variablentyp", "Attribut", "Zugriff", "Beispielwert", "Lesefehler" });
            foreach (AttributeDump a in result.Attributes.OrderBy(x => x.TypeName).ThenBy(x => x.Name))
            {
                AppendRow(sb, new[] { a.DeviceKind, a.TypeName, a.Name, a.Access, a.Value, a.Error });
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        private static void WriteStructure(string path, ReadResult result)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Unified-Konverter – Schritt 1 „Für Unified vorbereiten“");
            sb.AppendLine("Erstellt: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"));
            sb.AppendLine("Add-In-Prozess: " + (Environment.Is64BitProcess ? "64 Bit" : "32 Bit") + ", .NET " + Environment.Version);
            sb.AppendLine("Lesedauer: " + result.Duration.TotalSeconds.ToString("0.0") + " s");
            foreach (HmiDeviceInfo d in result.Devices)
            {
                sb.AppendLine();
                sb.AppendLine("HMI-Gerät: " + d.Name + " (" + d.KindText + ")");
                sb.AppendLine("  Kompositionen: " + string.Join(", ", d.Compositions));
                sb.AppendLine("  Attribute: " + string.Join(", ", d.Attributes));
                foreach (HmiTableInfo t in result.Tables.Where(x => x.Device == d))
                {
                    sb.AppendLine("  Tabelle: " + (string.IsNullOrEmpty(t.Folder) ? "" : t.Folder + " › ") + t.Name + " – " + t.Tags.Count + " Variablen");
                    foreach (string p in t.Problems) sb.AppendLine("    Problem: " + p);
                }
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
