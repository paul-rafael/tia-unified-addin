using System;
using System.Collections.Generic;

namespace Prh.UnifiedKonverter.EventExport
{
    /// <summary>Eine gefundene HMI-Variable mit Fundort. Das Openness-Objekt als object (Publisher-Regel ab V20).</summary>
    public sealed class TagEntry
    {
        public string Table;
        /// <summary>Ordner unterhalb von „HMI-Variablen“, oberster zuerst; leer = Systemordner.</summary>
        public List<string> Folders = new List<string>();
        public string TagName;
        internal object Tag;

        public string FolderPath { get { return string.Join("/", Folders); } }
    }

    public sealed class TableEntry
    {
        public string Name;
        public List<string> Folders = new List<string>();
        public int TagCount;
        public string FolderPath { get { return string.Join("/", Folders); } }
    }

    public sealed class ScanResult
    {
        public string ProjectName;
        public string HmiDeviceName;
        public List<TableEntry> Tables = new List<TableEntry>();
        public List<TagEntry> Tags = new List<TagEntry>();
        /// <summary>Tabellen oder Ordner, die nicht gelesen werden konnten – der Export läuft trotzdem.</summary>
        public List<ExportError> ScanErrors = new List<ExportError>();
    }

    public sealed class ExportError
    {
        public string Table;
        public string Folder;
        public string TagName;
        public string Message;
    }

    public sealed class ExportFile
    {
        public string Path;
        public string Table;
        public string Folder;
        public string TagName;
        public bool Success;
        public string Error;
    }

    public sealed class ExportResult
    {
        public ScanResult Scan;
        public List<ExportFile> Files = new List<ExportFile>();
        public string ZipPath;
        public DateTime StartedAt;
        public TimeSpan Duration;
        public bool Cancelled;

        public int Successful { get { return Files.FindAll(f => f.Success).Count; } }
        public int Failed { get { return Files.FindAll(f => !f.Success).Count; } }
    }
}
