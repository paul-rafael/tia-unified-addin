using System;
using System.Collections.Generic;
using System.Linq;
using Prh.UnifiedKonverter.Excel;
using Prh.UnifiedKonverter.Openness;
using Prh.UnifiedKonverter.Rules;
using Siemens.Engineering;

namespace Prh.UnifiedKonverter.Transfer
{
    public sealed class SourceTextListEntry
    {
        /// <summary>Einfache Attribute (Wert, Bereich, Standard-Eintrag …) als Text.</summary>
        public Dictionary<string, string> Attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        public Dictionary<string, string> Texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class SourceTextList
    {
        public string Name;
        public Dictionary<string, string> Attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        public List<SourceTextListEntry> Entries = new List<SourceTextListEntry>();
    }

    public sealed class SourceAlarm
    {
        public string Id;
        public string Name;
        public Dictionary<string, string> Texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public string AlarmClass;
        public string TriggerTag;
        public string TriggerBit;
        /// <summary>FieldInfo im Unified-Format nach Converter-Regel (nur aus Excel-Export).</summary>
        public string FieldInfo;
        public string Parameter1;
    }

    public sealed class SourceData
    {
        public ReadResult Tags;
        public List<SourceTextList> TextLists = new List<SourceTextList>();
        public List<SourceAlarm> Alarms = new List<SourceAlarm>();
        /// <summary>Woher die Meldungen stammen bzw. warum keine gelesen wurden.</summary>
        public string AlarmSource;
        public string TextListSource;
        public bool AlarmsFromOpenness;
    }

    /// <summary>Liest Textlisten und Meldungen der alten HMI über Openness (soweit angeboten) oder aus dem TIA-Excel-Export.</summary>
    internal static class SourceReader
    {
        private static readonly string[] EntryCompositions = { "TextListEntries", "Entries", "Items" };

        public static void ReadTextLists(IEngineeringObject hmi, SourceData data)
        {
            List<IEngineeringObject> lists = new List<IEngineeringObject>();
            CollectTextLists(hmi, lists, 0);
            IEngineeringObject folder = HmiObjects.Navigate(hmi, "TextAndGraphicListFolder") as IEngineeringObject;
            if (folder != null) CollectTextLists(folder, lists, 0);

            foreach (IEngineeringObject list in lists)
            {
                SourceTextList tl = new SourceTextList { Name = HmiObjects.NameOf(list) };
                ReadSimple(list, tl.Attributes);
                string comp = EngineeringWrite.FirstComposition(list, EntryCompositions);
                if (comp != null)
                {
                    foreach (IEngineeringObject entry in HmiObjects.Composition(list, comp))
                    {
                        SourceTextListEntry e = new SourceTextListEntry();
                        ReadSimple(entry, e.Attributes);
                        e.Texts = EngineeringWrite.ReadMultilingual(entry, "Text");
                        tl.Entries.Add(e);
                    }
                }
                if (data.TextLists.All(x => x.Name != tl.Name)) data.TextLists.Add(tl);
            }
            data.TextListSource = lists.Count > 0
                ? data.TextLists.Count + " Textlisten über Openness gelesen"
                : "Die alte HMI bietet über Openness keine Textlisten an.";
        }

        private static void CollectTextLists(IEngineeringObject obj, List<IEngineeringObject> lists, int depth)
        {
            if (depth > 10) return;
            foreach (IEngineeringObject list in HmiObjects.Composition(obj, "TextLists")) lists.Add(list);
            foreach (IEngineeringObject sub in HmiObjects.Composition(obj, "Folders")) CollectTextLists(sub, lists, depth + 1);
        }

        public static void ReadAlarmsFromOpenness(IEngineeringObject hmi, SourceData data)
        {
            string comp = EngineeringWrite.FirstComposition(hmi, "DiscreteAlarms", "Alarms");
            if (comp == null)
            {
                data.AlarmSource = "Die alte HMI bietet über Openness keine Bitmeldungen an – bitte den TIA-Excel-Export der Meldungen wählen.";
                return;
            }
            foreach (IEngineeringObject alarm in HmiObjects.Composition(hmi, comp))
            {
                Dictionary<string, string> attrs = new Dictionary<string, string>(StringComparer.Ordinal);
                ReadSimple(alarm, attrs);
                SourceAlarm a = new SourceAlarm
                {
                    Name = HmiObjects.NameOf(alarm),
                    Id = Pick(attrs, "Id", "Number"),
                    AlarmClass = Pick(attrs, "AlarmClass", "Class"),
                    TriggerTag = Pick(attrs, "TriggerTag", "RaisedStateTag", "Tag"),
                    TriggerBit = Pick(attrs, "TriggerBit", "TriggerBitAddress", "RaisedStateTagBitNumber", "BitNumber"),
                };
                foreach (string textAttr in new[] { "AlarmText", "EventText", "Text" })
                {
                    a.Texts = EngineeringWrite.ReadMultilingual(alarm, textAttr);
                    if (a.Texts.Count > 0) break;
                }
                data.Alarms.Add(a);
            }
            data.AlarmsFromOpenness = true;
            data.AlarmSource = data.Alarms.Count + " Bitmeldungen über Openness gelesen";
        }

        /// <summary>
        /// TIA-Excel-Export der Bitmeldungen (erstes Blatt): Spalten wie im Converter – A ID, B Name, C Meldetext,
        /// D FieldInfo, E Klasse, F Trigger-Variable, G Trigger-Bit. FieldInfo/Parameter nach Converter-Regel.
        /// </summary>
        public static void ReadAlarmsFromExcel(string path, SourceData data)
        {
            data.Alarms.Clear();
            List<string[]> rows = XlsxReader.ReadFirstSheet(path);
            for (int i = 1; i < rows.Count; i++)
            {
                string[] r = rows[i];
                Func<int, string> cell = c => c < r.Length ? r[c] : string.Empty;
                if (cell(0).Length == 0 && cell(1).Length == 0) continue;
                FieldInfoResult fi = AlarmRules.BuildFieldInfo(cell(2), cell(3), "Zeile " + (i + 1));
                SourceAlarm a = new SourceAlarm
                {
                    Id = cell(0),
                    Name = cell(1),
                    AlarmClass = cell(4),
                    TriggerTag = cell(5),
                    TriggerBit = cell(6),
                    FieldInfo = fi.FieldInfo,
                    Parameter1 = fi.Parameter1,
                };
                a.Texts["de-DE"] = cell(2);
                data.Alarms.Add(a);
            }
            data.AlarmsFromOpenness = false;
            data.AlarmSource = data.Alarms.Count + " Bitmeldungen aus „" + System.IO.Path.GetFileName(path) + "“ (Converter-Regel HMI-Meldungen)";
        }

        private static void ReadSimple(IEngineeringObject obj, Dictionary<string, string> into)
        {
            try
            {
                foreach (EngineeringAttributeInfo info in obj.GetAttributeInfos())
                {
                    object v;
                    if (!HmiObjects.TryGetAttribute(obj, info.Name, out v) || v == null) continue;
                    if (v is IEngineeringObject) { into[info.Name] = HmiObjects.Format(v); continue; }
                    if (v is string || v is IFormattable || v is bool || v.GetType().IsEnum) into[info.Name] = HmiObjects.Format(v);
                }
            }
            catch (Exception) { }
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
    }
}
