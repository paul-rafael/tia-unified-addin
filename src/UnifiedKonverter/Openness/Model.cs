using System;
using System.Collections.Generic;
using System.Linq;

namespace Prh.UnifiedKonverter.Openness
{
    public sealed class HmiDeviceInfo
    {
        public string Name;
        public HmiKind Kind;
        /// <summary>Kompositionen und Attribute des HMI-Geräts – zeigt, was Openness dort anbietet (z. B. TextLists, DiscreteAlarms).</summary>
        public List<string> Compositions = new List<string>();
        public List<string> Attributes = new List<string>();

        public string KindText
        {
            get
            {
                switch (Kind)
                {
                    case HmiKind.Classic: return "Comfort/Advanced";
                    case HmiKind.Unified: return "Unified";
                    default: return "Gerätetyp unbekannt";
                }
            }
        }
    }

    public sealed class HmiTableInfo
    {
        public string Name;
        /// <summary>Ordnerpfad unterhalb des HMI-Geräts, ohne den Tabellennamen.</summary>
        public string Folder;
        public HmiDeviceInfo Device;
        public List<HmiTagInfo> Tags = new List<HmiTagInfo>();
        public List<string> Problems = new List<string>();

        public int TagsWithFindings { get { return Tags.Count(t => t.Findings.Count > 0); } }
        public int Warnings { get { return Tags.Count(t => t.HasWarning); } }
    }

    public sealed class HmiTagInfo
    {
        public HmiTableInfo Table;
        public string Name;
        public string DataType;
        public string Length;
        public string Connection;
        public string PlcTag;
        public string AcquisitionCycle;
        public string AcquisitionMode;

        /// <summary>Name nach der Bereinigungsregel des Converters.</summary>
        public string UnifiedName;
        public List<Finding> Findings = new List<Finding>();

        public bool IsRenamed { get { return UnifiedName != null && !string.Equals(UnifiedName, Name, StringComparison.Ordinal); } }
        public bool HasWarning { get { return Findings.Any(f => f.Severity == Severity.Warnung); } }
    }

    public enum Severity
    {
        Info,
        Warnung,
    }

    public enum FindingKind
    {
        Sonderzeichen,
        Kollision,
        Erfassungszyklus,
        Erfassungsart,
        Datentyp,
    }

    public sealed class Finding
    {
        public Severity Severity;
        public FindingKind Kind;
        public string Text;
    }

    /// <summary>Ein Openness-Attribut eines Variablentyps (Diagnose: was ist lesbar, was schreibbar).</summary>
    public sealed class AttributeDump
    {
        public string DeviceKind;
        public string TypeName;
        public string Name;
        public string Access;
        public string Value;
        public string Error;
    }

    public sealed class ReadResult
    {
        public List<HmiTableInfo> Tables = new List<HmiTableInfo>();
        public List<HmiDeviceInfo> Devices = new List<HmiDeviceInfo>();
        public List<AttributeDump> Attributes = new List<AttributeDump>();
        public TimeSpan Duration;

        public IEnumerable<HmiTagInfo> AllTags { get { return Tables.SelectMany(t => t.Tags); } }
    }
}
