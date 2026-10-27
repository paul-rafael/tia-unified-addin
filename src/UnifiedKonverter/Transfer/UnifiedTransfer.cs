using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Prh.UnifiedKonverter.Openness;
using Prh.UnifiedKonverter.Report;
using Prh.UnifiedKonverter.Rules;
using Siemens.Engineering;

namespace Prh.UnifiedKonverter.Transfer
{
    public enum TransferKind { Variable, Textliste, Meldung }

    public enum TransferState { Neu, Vorhanden, Angelegt, Aktualisiert, Übersprungen, Fehler }

    public sealed class TransferItem
    {
        public TransferKind Kind;
        public string SourceName;
        public string TargetName;
        /// <summary>Variablentabelle bzw. leer.</summary>
        public string Container;
        public TransferState State;
        public List<string> Notes = new List<string>();
        internal object Payload;

        public string StateText
        {
            get
            {
                switch (State)
                {
                    case TransferState.Neu: return "wird angelegt";
                    case TransferState.Vorhanden: return "vorhanden";
                    case TransferState.Angelegt: return "angelegt";
                    case TransferState.Aktualisiert: return "aktualisiert";
                    case TransferState.Übersprungen: return "übersprungen";
                    default: return "Fehler";
                }
            }
        }
    }

    public sealed class TransferOptions
    {
        public bool Tags = true;
        public bool TextLists = true;
        public bool Alarms = true;
        /// <summary>false: Objekte, die es am Ziel schon gibt (z. B. von Data2Unified), bleiben unverändert.</summary>
        public bool UpdateExisting;
    }

    /// <summary>
    /// Schritt 3: überträgt Variablen, Textlisten und Bitmeldungen der alten HMI in die gewählte Unified-HMI.
    /// Namen nach den Converter-Regeln; Verweise auf umbenannte Variablen (Trigger, PLC) werden mitgeführt.
    /// </summary>
    internal sealed class UnifiedTransfer
    {
        private readonly SourceData _source;
        // Als object gespeichert: Der Siemens-Publisher (ab V20) verbietet Felder mit TIA-Objekttypen.
        private readonly object _targetRef;

        public UnifiedTransfer(SourceData source, UnifiedTarget target)
        {
            _source = source;
            _targetRef = target.Software;
        }

        private IEngineeringObject Target()
        {
            return (IEngineeringObject)_targetRef;
        }

        // ── Plan (nur lesen) ────────────────────────────────────────────────────

        public List<TransferItem> Plan(TransferOptions options)
        {
            Protocol.Line("Vorschau für Ziel lesen");
            List<TransferItem> items = new List<TransferItem>();
            if (options.Tags && _source.Tags != null)
            {
                HashSet<string> existing = Names(Target(), "Tags");
                foreach (HmiTagInfo tag in _source.Tags.AllTags)
                {
                    string name = tag.UnifiedName ?? ConverterRules.CleanTagName(tag.Name);
                    TransferItem item = new TransferItem
                    {
                        Kind = TransferKind.Variable, SourceName = tag.Name, TargetName = name, Container = tag.Table.Name,
                        State = existing.Contains(name) ? TransferState.Vorhanden : TransferState.Neu, Payload = tag,
                    };
                    if (tag.Findings.Any(f => f.Kind == FindingKind.Kollision))
                    {
                        item.State = TransferState.Übersprungen;
                        item.Notes.Add("Kollision – erst in Schritt 1 eindeutig benennen");
                    }
                    items.Add(item);
                }
            }
            if (options.TextLists)
            {
                HashSet<string> existing = Names(Target(), "TextLists");
                foreach (SourceTextList tl in _source.TextLists)
                {
                    string name = ConverterRules.CleanTextListName(tl.Name);
                    items.Add(new TransferItem
                    {
                        Kind = TransferKind.Textliste, SourceName = tl.Name, TargetName = name, Container = tl.Entries.Count + " Einträge",
                        State = existing.Contains(name) ? TransferState.Vorhanden : TransferState.Neu, Payload = tl,
                    });
                }
            }
            if (options.Alarms)
            {
                HashSet<string> existing = Names(Target(), "DiscreteAlarms");
                foreach (SourceAlarm alarm in _source.Alarms)
                {
                    items.Add(new TransferItem
                    {
                        Kind = TransferKind.Meldung, SourceName = alarm.Name, TargetName = alarm.Name, Container = alarm.AlarmClass,
                        State = existing.Contains(alarm.Name) ? TransferState.Vorhanden : TransferState.Neu, Payload = alarm,
                    });
                }
            }
            Protocol.Line("Vorschau: " + items.Count + " Objekte");
            return items;
        }

        private static HashSet<string> Names(IEngineeringObject parent, string composition)
        {
            return new HashSet<string>(HmiObjects.Composition(parent, composition).Select(HmiObjects.NameOf), StringComparer.OrdinalIgnoreCase);
        }

        // ── Ausführen ───────────────────────────────────────────────────────────

        public void Execute(List<TransferItem> items, TransferOptions options, Func<bool> cancelled)
        {
            Dictionary<string, string> renamed = _source.Tags == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : _source.Tags.AllTags.Where(t => t.IsRenamed).GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().UnifiedName, StringComparer.OrdinalIgnoreCase);

            foreach (TransferItem item in items)
            {
                if (cancelled()) break;
                if (item.State == TransferState.Übersprungen) continue;
                if (item.State == TransferState.Vorhanden && !options.UpdateExisting)
                {
                    item.State = TransferState.Übersprungen;
                    item.Notes.Add("am Ziel schon vorhanden");
                    continue;
                }
                bool existed = item.State == TransferState.Vorhanden;
                Protocol.Line(item.Kind + " " + item.SourceName + " → " + item.TargetName + (existed ? " (aktualisieren)" : ""));
                try
                {
                    switch (item.Kind)
                    {
                        case TransferKind.Variable: WriteTag(item, (HmiTagInfo)item.Payload); break;
                        case TransferKind.Textliste: WriteTextList(item, (SourceTextList)item.Payload); break;
                        case TransferKind.Meldung: WriteAlarm(item, (SourceAlarm)item.Payload, renamed); break;
                    }
                    item.State = existed ? TransferState.Aktualisiert : TransferState.Angelegt;
                }
                catch (Exception ex)
                {
                    item.State = TransferState.Fehler;
                    item.Notes.Insert(0, EngineeringWrite.Unwrap(ex).Message);
                    Protocol.Line("   Fehler: " + EngineeringWrite.Unwrap(ex).Message);
                }
            }
        }

        private void WriteTag(TransferItem item, HmiTagInfo tag)
        {
            IEngineeringObject table = EngineeringWrite.Find(Target(), "TagTables", tag.Table.Name)
                ?? EngineeringWrite.Create(Target(), "TagTables", tag.Table.Name);
            IEngineeringObject t = EngineeringWrite.Find(Target(), "Tags", item.TargetName)
                ?? EngineeringWrite.Create(table, "Tags", item.TargetName);
            HashSet<string> w = EngineeringWrite.WritableAttributes(t);

            Note(item, EngineeringWrite.TrySet(t, w, tag.Connection, "Connection"));
            Note(item, EngineeringWrite.TrySet(t, w, tag.PlcTag, "PlcTag"));
            Note(item, EngineeringWrite.TrySet(t, w, tag.DataType, "DataType"));
            Note(item, EngineeringWrite.TrySet(t, w, tag.Length, "Length"));
            Note(item, EngineeringWrite.TrySet(t, w, UnifiedCycle(tag.AcquisitionCycle), "AcquisitionCycle"));
            Note(item, EngineeringWrite.TrySet(t, w, UnifiedMode(tag.AcquisitionMode), "AcquisitionMode"));
        }

        private void WriteTextList(TransferItem item, SourceTextList tl)
        {
            IEngineeringObject list = EngineeringWrite.Find(Target(), "TextLists", item.TargetName)
                ?? EngineeringWrite.Create(Target(), "TextLists", item.TargetName);
            HashSet<string> lw = EngineeringWrite.WritableAttributes(list);
            foreach (KeyValuePair<string, string> a in tl.Attributes)
            {
                if (a.Key != "Name" && lw.Contains(a.Key)) Note(item, EngineeringWrite.TrySet(list, lw, a.Value, a.Key));
            }

            string comp = EngineeringWrite.FirstComposition(list, "Entries", "TextListEntries", "Items");
            if (comp == null) { item.Notes.Add("Einträge: Ziel bietet keine Eintragsliste an"); return; }
            int index = 0;
            foreach (SourceTextListEntry e in tl.Entries)
            {
                index++;
                string entryName;
                if (!e.Attributes.TryGetValue("Name", out entryName) || string.IsNullOrEmpty(entryName)) entryName = "Entry_" + index;
                IEngineeringObject entry;
                try
                {
                    entry = EngineeringWrite.Find(list, comp, entryName) ?? EngineeringWrite.Create(list, comp, entryName);
                }
                catch (Exception ex)
                {
                    item.Notes.Add("Eintrag " + index + ": " + EngineeringWrite.Unwrap(ex).Message);
                    continue;
                }
                HashSet<string> ew = EngineeringWrite.WritableAttributes(entry);
                foreach (KeyValuePair<string, string> a in e.Attributes)
                {
                    if (a.Key != "Name" && ew.Contains(a.Key)) Note(item, EngineeringWrite.TrySet(entry, ew, a.Value, a.Key));
                }
                Note(item, EngineeringWrite.WriteMultilingual(entry, "Text", e.Texts));
            }
        }

        private void WriteAlarm(TransferItem item, SourceAlarm alarm, Dictionary<string, string> renamed)
        {
            IEngineeringObject a = EngineeringWrite.Find(Target(), "DiscreteAlarms", item.TargetName)
                ?? EngineeringWrite.Create(Target(), "DiscreteAlarms", item.TargetName);
            HashSet<string> w = EngineeringWrite.WritableAttributes(a);

            Note(item, EngineeringWrite.TrySet(a, w, alarm.Id, "Id"));
            Note(item, EngineeringWrite.TrySet(a, w, alarm.AlarmClass, "AlarmClass"));
            Note(item, EngineeringWrite.TrySet(a, w, Renamed(alarm.TriggerTag, renamed), "RaisedStateTag", "TriggerTag"));
            Note(item, EngineeringWrite.TrySet(a, w, alarm.TriggerBit, "TriggerBitAddress", "RaisedStateTagBitNumber", "TriggerBit"));
            Note(item, EngineeringWrite.TrySet(a, w, AlarmRules.TriggerMode.Replace(" ", ""), "TriggerMode"));
            Note(item, EngineeringWrite.WriteMultilingual(a, "EventText", alarm.Texts));
            if (!string.IsNullOrEmpty(alarm.Parameter1))
            {
                string p = Note(item, EngineeringWrite.TrySet(a, w, Renamed(alarm.Parameter1, renamed), "AlarmParameter1", "Parameter1"));
                if (p != null) item.Notes.Add("Meldetext-Variable „" + alarm.Parameter1 + "“ bitte von Hand als Parameter 1 setzen");
            }
        }

        private static string Renamed(string tagName, Dictionary<string, string> renamed)
        {
            string newName;
            return tagName != null && renamed.TryGetValue(tagName, out newName) ? newName : tagName;
        }

        private static string Note(TransferItem item, string note)
        {
            if (note != null) item.Notes.Add(note);
            return note;
        }

        // ── Werte-Abbildung Classic → Unified ───────────────────────────────────

        private static readonly int[] UnifiedCyclesMs = { 250, 500, 1000, 2000, 5000, 10000, 60000, 300000, 600000, 3600000 };

        /// <summary>„200 ms“ → „T250ms“, „1 s“ → „T1s“: nächster Unified-Zyklus, mindestens 250 ms.</summary>
        public static string UnifiedCycle(string classic)
        {
            double? ms = ProjectChecks.CycleMs(classic);
            if (!ms.HasValue) return null;
            int target = UnifiedCyclesMs.FirstOrDefault(c => c >= ms.Value);
            if (target == 0) target = UnifiedCyclesMs[UnifiedCyclesMs.Length - 1];
            if (target < 1000) return "T" + target + "ms";
            if (target < 60000) return "T" + (target / 1000) + "s";
            if (target < 3600000) return "T" + (target / 60000) + "min";
            return "T" + (target / 3600000) + "h";
        }

        /// <summary>Zyklisch fortlaufend gibt es in Unified nicht → zyklisch bei Verwendung.</summary>
        public static string UnifiedMode(string classic)
        {
            if (string.IsNullOrEmpty(classic)) return null;
            string m = classic.ToLowerInvariant();
            if (m.Contains("continuous") || m.Contains("fortlaufend") || m.Contains("onuse") || m.Contains("verwendung")) return "CyclicInOperation";
            if (m.Contains("demand") || m.Contains("anforderung")) return "OnDemand";
            return classic;
        }

        public static string Describe(TransferItem item)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} {1}", item.Kind, item.TargetName);
        }
    }
}
