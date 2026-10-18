using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace Prh.UnifiedKonverter.Excel
{
    /// <summary>
    /// Liest das erste Blatt einer .xlsx-Datei als Text-Raster (Shared Strings, Inline-Strings, Zahlen, Wahrheitswerte).
    /// Reicht für TIA-Exporte; Formeln liefern ihren gespeicherten Wert.
    /// </summary>
    internal static class XlsxReader
    {
        private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";

        /// <summary>Zeilen ab Zeile 1; Zelle [r][c] mit c ab 0. Leere Zellen = "".</summary>
        public static List<string[]> ReadFirstSheet(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                List<string> shared = ReadSharedStrings(zip);
                string sheetPath = FirstSheetPath(zip);
                XDocument sheet = Load(zip, sheetPath);
                if (sheet == null) throw new InvalidDataException("Erstes Tabellenblatt nicht gefunden.");

                SortedDictionary<int, Dictionary<int, string>> grid = new SortedDictionary<int, Dictionary<int, string>>();
                int maxCol = 0;
                foreach (XElement row in sheet.Descendants(Main + "row"))
                {
                    foreach (XElement c in row.Elements(Main + "c"))
                    {
                        string reference = (string)c.Attribute("r");
                        if (reference == null) continue;
                        int col, r;
                        ParseRef(reference, out col, out r);
                        string value = CellText(c, shared);
                        if (value.Length == 0) continue;
                        Dictionary<int, string> cells;
                        if (!grid.TryGetValue(r, out cells)) grid[r] = cells = new Dictionary<int, string>();
                        cells[col] = value;
                        maxCol = Math.Max(maxCol, col);
                    }
                }

                List<string[]> rows = new List<string[]>();
                int lastRow = grid.Count == 0 ? 0 : grid.Keys.Max();
                for (int r = 1; r <= lastRow; r++)
                {
                    string[] line = new string[maxCol];
                    for (int c = 0; c < maxCol; c++) line[c] = string.Empty;
                    Dictionary<int, string> cells;
                    if (grid.TryGetValue(r, out cells))
                    {
                        foreach (KeyValuePair<int, string> kv in cells) line[kv.Key - 1] = kv.Value;
                    }
                    rows.Add(line);
                }
                return rows;
            }
        }

        private static string CellText(XElement c, List<string> shared)
        {
            string type = (string)c.Attribute("t");
            if (type == "inlineStr")
            {
                XElement isElement = c.Element(Main + "is");
                return isElement == null ? string.Empty : string.Concat(isElement.Descendants(Main + "t").Select(t => t.Value));
            }
            string v = (string)c.Element(Main + "v") ?? string.Empty;
            if (type == "s")
            {
                int index;
                return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out index) && index < shared.Count ? shared[index] : string.Empty;
            }
            if (type == "b") return v == "1" ? "TRUE" : "FALSE";
            if (type == "str" || type == "e") return v;
            double d;
            if (v.Length > 0 && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
            {
                return d.ToString("R", CultureInfo.InvariantCulture);
            }
            return v;
        }

        private static List<string> ReadSharedStrings(ZipArchive zip)
        {
            XDocument doc = Load(zip, "xl/sharedStrings.xml");
            if (doc == null) return new List<string>();
            return doc.Root.Elements(Main + "si")
                .Select(si => string.Concat(si.Descendants(Main + "t").Where(t => t.Parent.Name != Main + "rPh").Select(t => t.Value)))
                .ToList();
        }

        private static string FirstSheetPath(ZipArchive zip)
        {
            XDocument wb = Load(zip, "xl/workbook.xml");
            XDocument rels = Load(zip, "xl/_rels/workbook.xml.rels");
            XElement first = wb == null ? null : wb.Descendants(Main + "sheet").FirstOrDefault();
            if (first != null && rels != null)
            {
                string id = (string)first.Attribute(Rel + "id");
                XElement rel = rels.Root.Elements(PkgRel + "Relationship").FirstOrDefault(x => (string)x.Attribute("Id") == id);
                if (rel != null)
                {
                    string target = ((string)rel.Attribute("Target")).TrimStart('/');
                    return target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase) ? target : "xl/" + target;
                }
            }
            return "xl/worksheets/sheet1.xml";
        }

        private static XDocument Load(ZipArchive zip, string path)
        {
            ZipArchiveEntry entry = zip.Entries.FirstOrDefault(e => string.Equals(e.FullName, path, StringComparison.OrdinalIgnoreCase));
            if (entry == null) return null;
            using (Stream s = entry.Open()) return XDocument.Load(s);
        }

        private static void ParseRef(string reference, out int col, out int row)
        {
            col = 0;
            int i = 0;
            while (i < reference.Length && char.IsLetter(reference[i]))
            {
                col = col * 26 + (char.ToUpperInvariant(reference[i]) - 'A' + 1);
                i++;
            }
            row = int.Parse(reference.Substring(i), CultureInfo.InvariantCulture);
        }
    }
}
