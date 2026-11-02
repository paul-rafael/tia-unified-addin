using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using Siemens.Engineering;

namespace Prh.UnifiedKonverter.EventExport
{
    /// <summary>
    /// HMI Event Exporter: alle Variablen eines Comfort/Advanced-HMI-Geräts einzeln als SimaticML-XML exportieren,
    /// Manifest und Bericht erzeugen, alles in ein ZIP packen. Unabhängig von der Oberfläche; läuft im Thread des
    /// Menü-Aufrufs. Ein Fehler bei einer Variable bricht den Export nicht ab.
    /// </summary>
    internal static class ExportEngine
    {
        public static ScanResult Scan(object hmiTarget)
        {
            ScanResult scan = new ScanResult();
            IEngineeringObject hmi = (IEngineeringObject)hmiTarget;
            HmiDeviceScanner.Describe(hmi, scan);
            TagTableScanner.Scan(hmi, scan);
            return scan;
        }

        /// <param name="progress">(fertig, gesamt, Variable) – z. B. für den Text des TIA-Fortschrittsdialogs.</param>
        public static ExportResult Run(ScanResult scan, string zipPath, Func<bool> cancelled, Action<int, int, string> progress)
        {
            ExportResult result = new ExportResult { Scan = scan, ZipPath = zipPath, StartedAt = DateTime.Now };
            Stopwatch watch = Stopwatch.StartNew();
            string work = Path.Combine(Path.GetTempPath(), "HMI_Event_Export_" + Guid.NewGuid().ToString("N"));
            string tagsDir = Path.Combine(work, "Tags");
            Directory.CreateDirectory(tagsDir);
            try
            {
                FileNamer namer = new FileNamer();
                for (int i = 0; i < scan.Tags.Count; i++)
                {
                    if (cancelled()) { result.Cancelled = true; break; }
                    TagEntry tag = scan.Tags[i];
                    if (progress != null) progress(i, scan.Tags.Count, tag.TagName);
                    string fileName = namer.Next(tag);
                    ExportFile file = new ExportFile { Path = "Tags/" + fileName, Table = tag.Table, Folder = tag.FolderPath, TagName = tag.TagName };
                    try
                    {
                        TagXmlExporter.Export(tag.Tag, Path.Combine(tagsDir, fileName));
                        file.Success = true;
                    }
                    catch (Exception ex)
                    {
                        file.Error = ex.GetType().Name + ": " + ex.Message;
                    }
                    result.Files.Add(file);
                }
                result.Duration = watch.Elapsed;
                WriteZip(result, tagsDir);
            }
            finally
            {
                try { Directory.Delete(work, true); }
                catch (Exception) { }
            }
            return result;
        }

        private static void WriteZip(ExportResult result, string tagsDir)
        {
            // Erst prüfen, dann Manifest schreiben – sonst stimmen die Zähler nicht.
            foreach (ExportFile f in result.Files)
            {
                if (f.Success && !File.Exists(Source(tagsDir, f)))
                {
                    f.Success = false;
                    f.Error = "Export meldete Erfolg, aber die Datei fehlt.";
                }
            }

            string dir = Path.GetDirectoryName(result.ZipPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(result.ZipPath)) File.Delete(result.ZipPath);
            using (FileStream fs = new FileStream(result.ZipPath, FileMode.CreateNew, FileAccess.Write))
            using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                AddText(zip, "manifest.json", ManifestWriter.Manifest(result));
                foreach (ExportFile f in result.Files)
                {
                    if (!f.Success) continue;
                    ZipArchiveEntry entry = zip.CreateEntry(f.Path, CompressionLevel.Optimal);
                    using (Stream target = entry.Open())
                    using (FileStream source = File.OpenRead(Source(tagsDir, f)))
                    {
                        source.CopyTo(target);
                    }
                }
                AddText(zip, "Report/export-report.json", ManifestWriter.Report(result));
            }
        }

        private static string Source(string tagsDir, ExportFile f)
        {
            return Path.Combine(tagsDir, f.Path.Substring("Tags/".Length));
        }

        private static void AddText(ZipArchive zip, string name, string content)
        {
            ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (StreamWriter w = new StreamWriter(entry.Open(), new UTF8Encoding(false))) w.Write(content);
        }
    }
}
