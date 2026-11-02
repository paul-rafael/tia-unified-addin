using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;

namespace Prh.UnifiedKonverter.Excel
{
    /// <summary>
    /// Schreibt eine einfache .xlsx-Datei mit einem oder mehreren Tabellenblättern: erste Zeile fett als Überschrift,
    /// Texte als Inline-Strings, Zahlen als Zahlen, Spaltenbreiten nach Inhalt. Ohne externe Bibliothek.
    /// </summary>
    internal sealed class XlsxSheet
    {
        public string Name;
        /// <summary>Zellen: string = Text, double/int = Zahl, null = leer. Zeile 1 wird fett.</summary>
        public List<object[]> Rows = new List<object[]>();
    }

    internal static class XlsxWriter
    {
        public static void Write(string path, string sheetName, IList<string[]> rows)
        {
            XlsxSheet sheet = new XlsxSheet { Name = sheetName };
            foreach (string[] r in rows) sheet.Rows.Add(r.Cast<object>().ToArray());
            Write(path, new[] { sheet });
        }

        public static void Write(string path, IList<XlsxSheet> sheets)
        {
            if (File.Exists(path)) File.Delete(path);
            using (FileStream fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                Add(zip, "[Content_Types].xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                    "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                    "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                    "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                    string.Concat(sheets.Select((x, i) => "<Override PartName=\"/xl/worksheets/sheet" + (i + 1) + ".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>")) +
                    "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                    "</Types>");
                Add(zip, "_rels/.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                    "</Relationships>");
                Add(zip, "xl/workbook.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                    "<sheets>" + string.Concat(sheets.Select((x, i) => "<sheet name=\"" + Escape(x.Name) + "\" sheetId=\"" + (i + 1) + "\" r:id=\"rId" + (i + 1) + "\"/>")) + "</sheets></workbook>");
                Add(zip, "xl/_rels/workbook.xml.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    string.Concat(sheets.Select((x, i) => "<Relationship Id=\"rId" + (i + 1) + "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet" + (i + 1) + ".xml\"/>")) +
                    "<Relationship Id=\"rId" + (sheets.Count + 1) + "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                    "</Relationships>");
                Add(zip, "xl/styles.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                    "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
                    "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>" +
                    "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
                    "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                    "<cellXfs count=\"2\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
                    "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/></cellXfs>" +
                    "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
                    "</styleSheet>");
                for (int i = 0; i < sheets.Count; i++) Add(zip, "xl/worksheets/sheet" + (i + 1) + ".xml", Sheet(sheets[i].Rows));
            }
        }

        private static string Sheet(IList<object[]> rows)
        {
            int columns = rows.Count == 0 ? 0 : rows.Max(r => r.Length);
            StringBuilder sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            if (columns > 0)
            {
                sb.Append("<cols>");
                for (int c = 0; c < columns; c++)
                {
                    int chars = rows.Max(r => c < r.Length && r[c] != null ? Text(r[c]).Length : 0);
                    double width = Math.Min(60, Math.Max(10, chars + 2));
                    sb.Append("<col min=\"" + (c + 1) + "\" max=\"" + (c + 1) + "\" width=\"" + width.ToString(CultureInfo.InvariantCulture) + "\" customWidth=\"1\"/>");
                }
                sb.Append("</cols>");
            }
            sb.Append("<sheetData>");
            for (int r = 0; r < rows.Count; r++)
            {
                sb.Append("<row r=\"" + (r + 1) + "\">");
                for (int c = 0; c < rows[r].Length; c++)
                {
                    object value = rows[r][c];
                    string reference = ColumnName(c) + (r + 1);
                    string style = r == 0 ? " s=\"1\"" : "";
                    if (value is double || value is int || value is long)
                    {
                        sb.Append("<c r=\"" + reference + "\"" + style + "><v>" + Text(value) + "</v></c>");
                        continue;
                    }
                    string text = value as string;
                    if (string.IsNullOrEmpty(text)) continue;
                    sb.Append("<c r=\"" + reference + "\" t=\"inlineStr\"" + style + "><is><t xml:space=\"preserve\">");
                    sb.Append(Escape(text));
                    sb.Append("</t></is></c>");
                }
                sb.Append("</row>");
            }
            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        private static string Text(object value)
        {
            IFormattable f = value as IFormattable;
            return f != null ? f.ToString(null, CultureInfo.InvariantCulture) : value as string ?? string.Empty;
        }

        public static string ColumnName(int index)
        {
            string name = "";
            for (int n = index + 1; n > 0; n = (n - 1) / 26) name = (char)('A' + (n - 1) % 26) + name;
            return name;
        }

        private static readonly System.Text.RegularExpressions.Regex ExcelEscape =
            new System.Text.RegularExpressions.Regex("_x[0-9A-Fa-f]{4}_");

        private static string Escape(string text)
        {
            // Wie Excel: vorhandene „_xHHHH_“ schützen, CR und andere Steuerzeichen als „_xHHHH_“ kodieren –
            // ein rohes CR würde beim Einlesen (XML-Zeilenende-Normalisierung) verloren gehen.
            string t = ExcelEscape.Replace(text, m => "_x005F" + m.Value);
            StringBuilder clean = new StringBuilder(t.Length);
            foreach (char ch in t)
            {
                if (ch < 0x20 && ch != '\t' && ch != '\n') clean.Append("_x" + ((int)ch).ToString("X4") + "_");
                else clean.Append(ch);
            }
            return SecurityElement.Escape(clean.ToString());
        }

        private static void Add(ZipArchive zip, string name, string content)
        {
            ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (StreamWriter w = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
            {
                w.Write(content);
            }
        }
    }
}
