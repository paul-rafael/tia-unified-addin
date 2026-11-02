using System.Collections.Generic;
using System.Linq;

namespace Prh.UnifiedKonverter.EventExport
{
    /// <summary>manifest.json (Format „hmi-event-export“, Version 1) und Report/export-report.json.</summary>
    internal static class ManifestWriter
    {
        public const string Format = "hmi-event-export";
        public const int FormatVersion = 1;

        public static string TiaPortalVersion
        {
            get
            {
#if TIA_V21
                return "V21";
#else
                return "V20";
#endif
            }
        }

        public static string Manifest(ExportResult r)
        {
            return Json.Write(new Dictionary<string, object>
            {
                { "format", Format },
                { "formatVersion", FormatVersion },
                { "tiaPortalVersion", TiaPortalVersion },
                { "exporter", "Unified-Konverter " + typeof(ManifestWriter).Assembly.GetName().Version },
                { "exportOptions", TagXmlExporter.ExportOption },
                { "projectName", r.Scan.ProjectName },
                { "hmiDeviceName", r.Scan.HmiDeviceName },
                { "exportedAt", r.StartedAt },
                { "complete", !r.Cancelled },
                { "tableCount", r.Scan.Tables.Count },
                { "tagCount", r.Scan.Tags.Count },
                { "successfulExports", r.Successful },
                { "failedExports", r.Failed },
                { "files", r.Files.Select(f => (object)FileEntry(f)).ToList() },
            });
        }

        private static Dictionary<string, object> FileEntry(ExportFile f)
        {
            Dictionary<string, object> d = new Dictionary<string, object>
            {
                { "path", f.Success ? f.Path : null },
                { "table", f.Table },
                { "folder", f.Folder },
                { "tagName", f.TagName },
                { "status", f.Success ? "success" : "failed" },
            };
            if (!f.Success) d.Add("error", f.Error);
            return d;
        }

        public static string Report(ExportResult r)
        {
            return Json.Write(new Dictionary<string, object>
            {
                { "hmiDeviceName", r.Scan.HmiDeviceName },
                { "projectName", r.Scan.ProjectName },
                { "exportedAt", r.StartedAt },
                { "durationSeconds", r.Duration.TotalSeconds },
                { "cancelled", r.Cancelled },
                { "tablesFound", r.Scan.Tables.Count },
                { "tagsFound", r.Scan.Tags.Count },
                { "successfulExports", r.Successful },
                { "failedExports", r.Failed },
                { "tables", r.Scan.Tables.Select(t => (object)new Dictionary<string, object>
                    {
                        { "table", t.Name }, { "folder", t.FolderPath }, { "tagCount", t.TagCount },
                    }).ToList() },
                { "errors", r.Scan.ScanErrors.Select(e => (object)Error(e.Table, e.Folder, e.TagName, e.Message))
                    .Concat(r.Files.Where(f => !f.Success).Select(f => (object)Error(f.Table, f.Folder, f.TagName, f.Error))).ToList() },
            });
        }

        private static Dictionary<string, object> Error(string table, string folder, string tag, string message)
        {
            return new Dictionary<string, object> { { "table", table }, { "folder", folder }, { "tagName", tag }, { "message", message } };
        }
    }
}
