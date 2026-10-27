using System.Collections.Generic;
using System.Linq;
using Prh.UnifiedKonverter.Excel;
using Prh.UnifiedKonverter.Openness;

namespace Prh.UnifiedKonverter.Report
{
    /// <summary>
    /// Schritt 1: HMI-Variablen mit den bereinigten Namen als Excel-Liste im Aufbau des TIA-Exports
    /// (Blatt „Hmi Tags“, Spalten wie in TIA). Ersetzt das Umbenennen im Projekt: Comfort/Advanced-Variablen
    /// lassen sich per Openness nicht umbenennen („set_Name“ wird nicht unterstützt).
    /// Variablen mit Kollision behalten ihren Namen – sonst stünden doppelte Namen in der Liste.
    /// </summary>
    internal static class TagListExport
    {
        /// <summary>Gleicher Dateiname wie beim Web-Converter (Button „HMI TagList Konverter“).</summary>
        public const string FileName = "HMITagList_MIGUNI.xlsx";

        private static readonly string[] Header = { "Name", "Path", "Connection", "PLC tag", "DataType", "Length" };

        /// <summary>Variablen, die in der Liste einen neuen Namen bekommen.</summary>
        public static List<HmiTagInfo> Renamed(IEnumerable<HmiTagInfo> tags)
        {
            return tags.Where(t => t.IsRenamed && !IsCollision(t)).ToList();
        }

        public static void Write(string path, IEnumerable<HmiTagInfo> tags)
        {
            List<string[]> rows = new List<string[]> { Header };
            foreach (HmiTagInfo t in tags)
            {
                string name = t.IsRenamed && !IsCollision(t) ? t.UnifiedName : t.Name;
                string tablePath = string.IsNullOrEmpty(t.Table.Folder) ? t.Table.Name : t.Table.Folder + "\\" + t.Table.Name;
                rows.Add(new[] { name, tablePath, t.Connection, t.PlcTag, t.DataType, t.Length });
            }
            XlsxWriter.Write(path, "Hmi Tags", rows);
        }

        private static bool IsCollision(HmiTagInfo t)
        {
            return t.Findings.Any(f => f.Kind == FindingKind.Kollision);
        }
    }
}
