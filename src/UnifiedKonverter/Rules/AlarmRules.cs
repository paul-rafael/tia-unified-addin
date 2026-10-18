using System;
using System.Collections.Generic;
using System.Text;

namespace Prh.UnifiedKonverter.Rules
{
    public sealed class FieldInfoResult
    {
        /// <summary>Neue FieldInfo im Unified-Format (endet wie im Original mit CR LF).</summary>
        public string FieldInfo;
        /// <summary>Variable der letzten Referenz mit Variable („Alarm parameter 1“), sonst leer.</summary>
        public string Parameter1;
    }

    /// <summary>
    /// HMI-Meldungen – exakt nach dem Converter (VBA Modul8.ConvertAlarmList_Unified mit ExtractRefs,
    /// ExtractTagByRef, ExtractLengthByRef; converters/alarms.ts). InStr = binärer Vergleich, Mid mit
    /// negativer Länge = Laufzeitfehler 5 (hier: FormatException mit Hinweis).
    /// </summary>
    internal static class AlarmRules
    {
        public const string TriggerMode = "On rising edge";

        /// <summary>ExtractRefs: jedes ref="…" im Meldetext, in Reihenfolge.</summary>
        public static List<string> ExtractRefs(string text, string context)
        {
            List<string> refs = new List<string>();
            int pos = 1;
            while (InStr(pos, text, "ref=\"") > 0)
            {
                pos = InStr(pos, text, "ref=\"") + 5;
                refs.Add(Mid(text, pos, InStr(pos, text, "\"") - pos, context));
                pos = pos + 1;
            }
            return refs;
        }

        /// <summary>ExtractTagByRef: „Tag = …;“ ab der ersten Fundstelle von „id = &lt;refID&gt;“ (Teilstring, wie im Original).</summary>
        public static string ExtractTagByRef(string fieldInfo, string refId, string context)
        {
            int start = InStr(1, fieldInfo, "id = " + refId);
            if (start > 0)
            {
                start = InStr(start, fieldInfo, "Tag = ");
                if (start > 0)
                {
                    start += 6;
                    int end = InStr(start, fieldInfo, ";");
                    return VbaTrim(Mid(fieldInfo, start, end - start, context));
                }
            }
            return string.Empty;
        }

        /// <summary>ExtractLengthByRef: „Length = …;“ ab „id = &lt;refID&gt;“, Rückfall „3“.</summary>
        public static string ExtractLengthByRef(string fieldInfo, string refId, string context)
        {
            int start = InStr(1, fieldInfo, "id = " + refId);
            if (start > 0)
            {
                start = InStr(start, fieldInfo, "Length = ");
                if (start > 0)
                {
                    start += "Length = ".Length;
                    int end = InStr(start, fieldInfo, ";");
                    return VbaTrim(Mid(fieldInfo, start, end - start, context));
                }
            }
            return "3";
        }

        public static FieldInfoResult BuildFieldInfo(string alarmText, string fieldInfoOld, string context)
        {
            StringBuilder sb = new StringBuilder();
            string parameter1 = string.Empty;
            foreach (string refId in ExtractRefs(alarmText ?? string.Empty, context))
            {
                string tag = ExtractTagByRef(fieldInfoOld ?? string.Empty, refId, context);
                if (tag != string.Empty)
                {
                    string length = ExtractLengthByRef(fieldInfoOld ?? string.Empty, refId, context);
                    sb.Append("<ref id = ").Append(refId).Append("; type = AlarmParameterWithOrWithoutCommonTextList; ")
                      .Append("Parameter = Parameter 1; ")
                      .Append("Tag = ").Append(tag).Append("; DisplayType = Decimal; Length = ").Append(length).Append("; Precision = 0; ")
                      .Append("Alignment = Right; ZeroPadding = False;>\r\n");
                    parameter1 = tag;
                }
            }
            // VBA-Trim entfernt nur Leerzeichen – das abschließende CR LF bleibt (wie im Original).
            return new FieldInfoResult { FieldInfo = VbaTrim(sb.ToString()), Parameter1 = parameter1 };
        }

        /// <summary>InStr(start, haystack, needle): 1-basiert, binär, 0 = nicht gefunden.</summary>
        private static int InStr(int start, string hay, string needle)
        {
            if (start > hay.Length + 1) return 0;
            return hay.IndexOf(needle, start - 1, StringComparison.Ordinal) + 1;
        }

        private static string Mid(string s, int start, int length, string context)
        {
            if (length < 0)
            {
                throw new FormatException("Ungültiger Aufbau" + (string.IsNullOrEmpty(context) ? "" : " (" + context + ")")
                    + ": erwartetes Trennzeichen fehlt. Das Original-Makro bricht hier mit Laufzeitfehler 5 ab.");
            }
            int from = Math.Min(start - 1, s.Length);
            return s.Substring(from, Math.Min(length, s.Length - from));
        }

        private static string VbaTrim(string s)
        {
            return s.Trim(' ');
        }
    }
}
