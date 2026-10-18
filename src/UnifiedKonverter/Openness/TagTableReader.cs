using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Siemens.Engineering;

namespace Prh.UnifiedKonverter.Openness
{
    /// <summary>
    /// Sammelt die HMI-Variablentabellen einer Auswahl (HMI-Gerät, Variablenordner oder einzelne Tabellen)
    /// und liest die für die Migration relevanten Eigenschaften. Ändert nichts am Projekt.
    /// </summary>
    internal sealed class TagTableReader
    {
        // Mögliche Attributnamen je Eigenschaft (Comfort/Advanced und Unified heißen teils verschieden).
        private static readonly string[] DataTypeNames = { "DataType", "DataTypeName" };
        private static readonly string[] LengthNames = { "Length" };
        private static readonly string[] ConnectionNames = { "Connection" };
        private static readonly string[] PlcTagNames = { "PlcTag", "ControllerTag", "PlcName" };
        private static readonly string[] CycleNames = { "AcquisitionCycle" };
        private static readonly string[] ModeNames = { "AcquisitionMode" };

        private readonly Dictionary<Type, HashSet<string>> _attributesByType = new Dictionary<Type, HashSet<string>>();
        private readonly Dictionary<object, HmiDeviceInfo> _devices = new Dictionary<object, HmiDeviceInfo>();
        private readonly ReadResult _result = new ReadResult();

        /// <summary>Für das spätere Umbenennen: Openness-Objekt je gelesener Variable (als object – Publisher-Regel ab V20).</summary>
        public readonly Dictionary<HmiTagInfo, object> Sources = new Dictionary<HmiTagInfo, object>();

        public ReadResult Read(IEnumerable<IEngineeringObject> selection, Func<bool> cancelled)
        {
            Stopwatch watch = Stopwatch.StartNew();
            List<IEngineeringObject> tables = new List<IEngineeringObject>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (IEngineeringObject obj in selection)
            {
                Collect(obj, tables, seen, 0);
            }

            foreach (IEngineeringObject table in tables)
            {
                if (cancelled()) break;
                _result.Tables.Add(ReadTable(table));
            }
            _result.Duration = watch.Elapsed;
            return _result;
        }

        private static void Collect(IEngineeringObject obj, List<IEngineeringObject> tables, HashSet<string> seen, int depth)
        {
            if (obj == null || depth > 40) return;

            if (HmiObjects.IsHmiTagTable(obj))
            {
                HmiKind kind;
                IEngineeringObject device = HmiObjects.FindHmiSoftware(obj, out kind);
                string key = (device == null ? "?" : HmiObjects.NameOf(device)) + "|" + HmiObjects.FolderPath(obj) + "|" + HmiObjects.NameOf(obj);
                if (seen.Add(key)) tables.Add(obj);
                return;
            }

            if (HmiObjects.IsDevice(obj))
            {
                IEngineeringObject software = HmiObjects.SoftwareOf(obj);
                if (software != null) Collect(software, tables, seen, depth + 1);
                foreach (IEngineeringObject item in HmiObjects.Composition(obj, "DeviceItems"))
                {
                    Collect(item, tables, seen, depth + 1);
                }
                return;
            }

            if (obj.GetType().Name == "HmiTarget")
            {
                IEngineeringObject folder = HmiObjects.Navigate(obj, "TagFolder") as IEngineeringObject;
                if (folder != null) Collect(folder, tables, seen, depth + 1);
            }

            if (HmiObjects.IsHmiSoftware(obj) || HmiObjects.IsHmiTagFolder(obj))
            {
                foreach (string composition in new[] { "TagTables", "Folders", "Groups", "TagTableGroups" })
                {
                    foreach (IEngineeringObject child in HmiObjects.Composition(obj, composition))
                    {
                        Collect(child, tables, seen, depth + 1);
                    }
                }
            }
        }

        private HmiTableInfo ReadTable(IEngineeringObject table)
        {
            HmiTableInfo info = new HmiTableInfo
            {
                Name = HmiObjects.NameOf(table),
                Folder = HmiObjects.FolderPath(table),
                Device = DeviceOf(table),
            };

            foreach (IEngineeringObject tag in HmiObjects.Composition(table, "Tags"))
            {
                try
                {
                    HmiTagInfo t = ReadTag(tag, info);
                    info.Tags.Add(t);
                    Sources[t] = tag;
                }
                catch (Exception ex)
                {
                    info.Problems.Add("Variable nicht lesbar: " + ex.Message);
                }
            }
            return info;
        }

        private HmiDeviceInfo DeviceOf(IEngineeringObject table)
        {
            HmiKind kind;
            IEngineeringObject software = HmiObjects.FindHmiSoftware(table, out kind);
            object key = software == null ? (object)"?" : HmiObjects.NameOf(software);
            HmiDeviceInfo device;
            if (_devices.TryGetValue(key, out device)) return device;

            device = new HmiDeviceInfo { Name = software == null ? "?" : HmiObjects.NameOf(software), Kind = kind };
            if (software != null)
            {
                try { foreach (EngineeringCompositionInfo c in software.GetCompositionInfos()) device.Compositions.Add(c.Name); }
                catch (Exception) { }
                try { foreach (EngineeringAttributeInfo a in software.GetAttributeInfos()) device.Attributes.Add(a.Name + " (" + a.AccessMode + ")"); }
                catch (Exception) { }
            }
            _devices[key] = device;
            _result.Devices.Add(device);
            return device;
        }

        private HmiTagInfo ReadTag(IEngineeringObject tag, HmiTableInfo table)
        {
            HashSet<string> available = AvailableAttributes(tag, table.Device);
            return new HmiTagInfo
            {
                Table = table,
                Name = HmiObjects.NameOf(tag),
                DataType = Get(tag, available, DataTypeNames),
                Length = Get(tag, available, LengthNames),
                Connection = Get(tag, available, ConnectionNames),
                PlcTag = Get(tag, available, PlcTagNames),
                AcquisitionCycle = Get(tag, available, CycleNames),
                AcquisitionMode = Get(tag, available, ModeNames),
            };
        }

        private static string Get(IEngineeringObject tag, HashSet<string> available, string[] candidates)
        {
            foreach (string name in candidates)
            {
                if (!available.Contains(name)) continue;
                object value;
                if (HmiObjects.TryGetAttribute(tag, name, out value)) return HmiObjects.Format(value);
            }
            return null;
        }

        /// <summary>Attributliste einmal je Variablentyp abfragen; beim ersten Mal alle Werte für die Diagnose mitschreiben.</summary>
        private HashSet<string> AvailableAttributes(IEngineeringObject tag, HmiDeviceInfo device)
        {
            Type type = tag.GetType();
            HashSet<string> names;
            if (_attributesByType.TryGetValue(type, out names)) return names;

            names = new HashSet<string>(StringComparer.Ordinal);
            foreach (EngineeringAttributeInfo info in tag.GetAttributeInfos())
            {
                names.Add(info.Name);
                AttributeDump dump = new AttributeDump
                {
                    DeviceKind = device.KindText,
                    TypeName = type.FullName,
                    Name = info.Name,
                    Access = info.AccessMode.ToString(),
                };
                try { dump.Value = HmiObjects.Format(tag.GetAttribute(info.Name)); }
                catch (Exception ex) { dump.Error = ex.GetType().Name + ": " + ex.Message; }
                _result.Attributes.Add(dump);
            }
            _attributesByType[type] = names;
            return names;
        }
    }
}
