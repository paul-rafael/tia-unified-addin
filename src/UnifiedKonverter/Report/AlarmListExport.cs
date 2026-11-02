using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Prh.UnifiedKonverter.Excel;
using Prh.UnifiedKonverter.Rules;

namespace Prh.UnifiedKonverter.Report
{
    /// <summary>
    /// HMI-Meldungen wie im Web-Converter (converters/alarms.ts, VBA Modul8.ConvertAlarmList_Unified):
    /// erstes Blatt eines TIA-Meldungs-Exports → neue Arbeitsmappe „DiscreteAlarms“ im Unified-Spaltenaufbau (44 Spalten).
    /// Comfort/Advanced liefert Meldungen nicht über Openness, deshalb ist der TIA-Excel-Export die Quelle.
    /// </summary>
    internal static class AlarmListExport
    {
        public const string FileName = "Unified_HMI-Alarms.xlsx";
        public const string SheetName = "DiscreteAlarms";

        public static readonly string[] Header =
        {
            "ID", "Name", "Alarm text [de-DE], Alarm text 1", "FieldInfo [Alarm text 1]",
            "Class", "Trigger tag", "Trigger bit", "Trigger mode",
            "Acknowledgement tag", "Acknowledgement bit",
            "PLC acknowledgement tag", "PLC acknowledgement bit",
            "Priority", "Info text [de-DE], Info text",
            "Additional text 1 [de-DE], Alarm text 2", "FieldInfo [Alarm text 2]",
            "Additional text 2 [de-DE], Alarm text 3", "FieldInfo [Alarm text 3]",
            "Additional text 3 [de-DE], Alarm text 4", "FieldInfo [Alarm text 4]",
            "Additional text 4 [de-DE], Alarm text 5", "FieldInfo [Alarm text 5]",
            "Additional text 5 [de-DE], Alarm text 6", "FieldInfo [Alarm text 6]",
            "Additional text 6 [de-DE], Alarm text 7", "FieldInfo [Alarm text 7]",
            "Additional text 7 [de-DE], Alarm text 8", "FieldInfo [Alarm text 8]",
            "Additional text 8 [de-DE], Alarm text 9", "FieldInfo [Alarm text 9]",
            "Additional text 9 [de-DE], Alarm text 10", "FieldInfo [Alarm text 10]",
            "Alarm parameter 1", "Alarm parameter 2", "Alarm parameter 3",
            "Alarm parameter 4", "Alarm parameter 5", "Alarm parameter 6",
            "Alarm parameter 7", "Alarm parameter 8", "Alarm parameter 9",
            "Alarm parameter 10", "Area", "Origin",
        };

        private const string NoValue = "<No value>";
        private static readonly Regex NumberRe = new Regex(@"^[+-]?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?$");

        public sealed class Result
        {
            public int Alarms;
            public int WithParameters;
        }

        public static Result Convert(string sourcePath, string targetPath)
        {
            ExportKind.Assert(sourcePath, ExportKind.Kind.Alarms);
            List<string[]> rows = XlsxReader.ReadFirstSheet(sourcePath);
            // Letzte Zeile = letzte nicht-leere Zelle in Spalte A (auch leere Zwischenzeilen werden übernommen).
            int lastRow = 0;
            for (int i = rows.Count - 1; i >= 1; i--)
            {
                if (Cell(rows[i], 0).Length > 0) { lastRow = i; break; }
            }

            XlsxSheet sheet = new XlsxSheet { Name = SheetName };
            sheet.Rows.Add(Header.Cast<object>().ToArray());
            Result result = new Result();
            for (int i = 1; i <= lastRow; i++)
            {
                string[] src = rows[i];
                object[] row = new object[Header.Length];
                for (int c = 0; c < 7; c++) row[c] = Value(Cell(src, c));

                row[7] = AlarmRules.TriggerMode;
                row[8] = NoValue;
                row[9] = 0;
                row[10] = NoValue;
                row[11] = 0;
                row[12] = 0;
                // Wie im getesteten Original: Spalten 14, 16 … 32 (1-basiert) „<No value>“, die Zusatztexte dazwischen leer.
                for (int c = 14; c <= 33; c += 2) row[c - 1] = NoValue;
                for (int c = 34; c <= 42; c++) row[c - 1] = NoValue;
                row[42] = "HMI_RT_2::Alarming";

                FieldInfoResult fi = AlarmRules.BuildFieldInfo(Cell(src, 2), Cell(src, 3), "Zeile " + (i + 1));
                if (!string.IsNullOrEmpty(fi.Parameter1))
                {
                    row[32] = Value(fi.Parameter1);
                    result.WithParameters++;
                }
                row[3] = Value(fi.FieldInfo);
                sheet.Rows.Add(row);
                result.Alarms++;
            }

            XlsxWriter.Write(targetPath, new[] { sheet });
            return result;
        }

        private static string Cell(string[] row, int c)
        {
            return c < row.Length ? row[c] ?? string.Empty : string.Empty;
        }

        /// <summary>Wie Excel bei „cell.Value = Text“: Zahlentext wird Zahl, leer bleibt leer.</summary>
        private static object Value(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            double d;
            if (NumberRe.IsMatch(text) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            return text;
        }
    }

    /// <summary>Art eines TIA-Exports nach Blattnamen und Spaltenköpfen (wie converters/common.ts detectExportKind).</summary>
    internal static class ExportKind
    {
        public enum Kind { Unbekannt, Textlisten, Alarms, Variablen }

        public static Kind Detect(string path)
        {
            List<string> sheets = XlsxReader.SheetNames(path);
            if (sheets.Contains("TextList") && sheets.Contains("TextListEntry")) return Kind.Textlisten;
            List<string[]> rows = XlsxReader.ReadFirstSheet(path);
            string[] headers = rows.Count == 0 ? new string[0] : rows[0].Take(60).Select(h => (h ?? "").Trim().ToLowerInvariant()).ToArray();
            if (headers.Any(h => h.StartsWith("alarm text", StringComparison.Ordinal))) return Kind.Alarms;
            if (headers.Contains("plc tag") || headers.Contains("datatype") || headers.Contains("data type")) return Kind.Variablen;
            return Kind.Unbekannt;
        }

        /// <summary>Bricht mit verständlicher Meldung ab, wenn die Datei erkennbar ein anderer Export ist.</summary>
        public static void Assert(string path, Kind expected)
        {
            Kind kind = Detect(path);
            if (kind == Kind.Unbekannt || kind == expected) return;
            string what = kind == Kind.Textlisten ? "ein Textlisten-Export" : kind == Kind.Alarms ? "ein Meldungs-Export" : "ein Variablen-Export";
            throw new InvalidDataException("„" + Path.GetFileName(path) + "“ ist " + what + ", hier wird der TIA-Excel-Export der HMI-Meldungen erwartet.");
        }
    }
}
