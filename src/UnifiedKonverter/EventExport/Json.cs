using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Prh.UnifiedKonverter.EventExport
{
    /// <summary>
    /// Kleiner JSON-Schreiber für Manifest und Bericht (keine externe Bibliothek im Add-In).
    /// Werte: string, bool, int/long/double, DateTime (ISO 8601), IDictionary&lt;string, object&gt;, IEnumerable, null.
    /// </summary>
    internal static class Json
    {
        public static string Write(object value)
        {
            StringBuilder sb = new StringBuilder();
            WriteValue(sb, value, 0);
            sb.Append('\n');
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object value, int indent)
        {
            if (value == null) { sb.Append("null"); return; }
            string s = value as string;
            if (s != null) { WriteString(sb, s); return; }
            if (value is bool) { sb.Append((bool)value ? "true" : "false"); return; }
            if (value is DateTime) { WriteString(sb, ((DateTime)value).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)); return; }
            if (value is int || value is long) { sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture)); return; }
            if (value is double) { sb.Append(((double)value).ToString("0.###", CultureInfo.InvariantCulture)); return; }

            IDictionary<string, object> obj = value as IDictionary<string, object>;
            if (obj != null)
            {
                if (obj.Count == 0) { sb.Append("{}"); return; }
                sb.Append("{\n");
                int i = 0;
                foreach (KeyValuePair<string, object> kv in obj)
                {
                    sb.Append(' ', (indent + 1) * 2);
                    WriteString(sb, kv.Key);
                    sb.Append(": ");
                    WriteValue(sb, kv.Value, indent + 1);
                    sb.Append(++i < obj.Count ? ",\n" : "\n");
                }
                sb.Append(' ', indent * 2).Append('}');
                return;
            }

            System.Collections.IEnumerable list = value as System.Collections.IEnumerable;
            if (list != null)
            {
                List<object> items = list.Cast<object>().ToList();
                if (items.Count == 0) { sb.Append("[]"); return; }
                sb.Append("[\n");
                for (int i = 0; i < items.Count; i++)
                {
                    sb.Append(' ', (indent + 1) * 2);
                    WriteValue(sb, items[i], indent + 1);
                    sb.Append(i + 1 < items.Count ? ",\n" : "\n");
                }
                sb.Append(' ', indent * 2).Append(']');
                return;
            }
            WriteString(sb, value.ToString());
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
