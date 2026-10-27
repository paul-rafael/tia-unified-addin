using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;

namespace Prh.UnifiedKonverter.Excel
{
    /// <summary>
    /// Schreibt eine einfache .xlsx-Datei mit einem Tabellenblatt: erste Zeile fett als Überschrift,
    /// alle Zellen als Text (Inline-Strings), Spaltenbreiten nach Inhalt. Ohne externe Bibliothek.
    /// </summary>
    internal static class XlsxWriter
    {
        public static void Write(string path, string sheetName, IList<string[]> rows)
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
                    "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
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
                    "<sheets><sheet name=\"" + Escape(sheetName) + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
                Add(zip, "xl/_rels/workbook.xml.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                    "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
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
                Add(zip, "xl/worksheets/sheet1.xml", Sheet(rows));
            }
        }

        private static string Sheet(IList<string[]> rows)
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
                    int chars = rows.Max(r => c < r.Length && r[c] != null ? r[c].Length : 0);
                    double width = Math.Min(60, Math.Max(10, chars + 2));
                    sb.Append("<col min=\"" + (c + 1) + "\" max=\"" + (c + 1) + "\" width=\"" + width.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\" customWidth=\"1\"/>");
                }
                sb.Append("</cols>");
            }
            sb.Append("<sheetData>");
            for (int r = 0; r < rows.Count; r++)
            {
                sb.Append("<row r=\"" + (r + 1) + "\">");
                for (int c = 0; c < rows[r].Length; c++)
                {
                    string value = rows[r][c];
                    if (string.IsNullOrEmpty(value)) continue;
                    sb.Append("<c r=\"" + ColumnName(c) + (r + 1) + "\" t=\"inlineStr\"" + (r == 0 ? " s=\"1\"" : "") + "><is><t xml:space=\"preserve\">");
                    sb.Append(Escape(value));
                    sb.Append("</t></is></c>");
                }
                sb.Append("</row>");
            }
            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        public static string ColumnName(int index)
        {
            string name = "";
            for (int n = index + 1; n > 0; n = (n - 1) / 26) name = (char)('A' + (n - 1) % 26) + name;
            return name;
        }

        private static string Escape(string text)
        {
            // Steuerzeichen außer Tab/Zeilenumbruch sind in XML 1.0 nicht erlaubt.
            StringBuilder clean = new StringBuilder(text.Length);
            foreach (char ch in text) clean.Append(ch < 0x20 && ch != '\t' && ch != '\n' && ch != '\r' ? ' ' : ch);
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
