using System;
using System.Collections.Generic;
using Prh.UnifiedKonverter.Openness;
using Siemens.Engineering;

namespace Prh.UnifiedKonverter.EventExport
{
    /// <summary>
    /// Durchsucht das Variablensystem eines Comfort/Advanced-HMI-Geräts (HmiTarget.TagFolder):
    /// Tabellen im Systemordner und in beliebig tief verschachtelten Benutzerordnern, auch leere Tabellen.
    /// Nur lesend. Ein nicht lesbarer Ordner oder eine nicht lesbare Tabelle wird gemeldet, der Rest weiter durchsucht.
    /// </summary>
    internal static class TagTableScanner
    {
        private const int MaxDepth = 64;

        public static void Scan(IEngineeringObject hmiTarget, ScanResult result)
        {
            IEngineeringObject root = HmiObjects.Navigate(hmiTarget, "TagFolder") as IEngineeringObject;
            if (root == null) throw new InvalidOperationException("Das HMI-Gerät bietet keinen Ordner „HMI-Variablen“ (TagFolder) an.");
            ScanFolder(root, new List<string>(), result, 0);
        }

        private static void ScanFolder(IEngineeringObject folder, List<string> path, ScanResult result, int depth)
        {
            if (depth > MaxDepth) return;
            foreach (IEngineeringObject table in HmiObjects.Composition(folder, "TagTables"))
            {
                ScanTable(table, path, result);
            }
            foreach (IEngineeringObject sub in HmiObjects.Composition(folder, "Folders"))
            {
                List<string> subPath = new List<string>(path) { HmiObjects.NameOf(sub) };
                try
                {
                    ScanFolder(sub, subPath, result, depth + 1);
                }
                catch (Exception ex)
                {
                    result.ScanErrors.Add(new ExportError { Folder = string.Join("/", subPath), Message = "Ordner nicht lesbar: " + ex.Message });
                }
            }
        }

        private static void ScanTable(IEngineeringObject table, List<string> path, ScanResult result)
        {
            TableEntry entry = new TableEntry { Name = HmiObjects.NameOf(table), Folders = new List<string>(path) };
            result.Tables.Add(entry);
            try
            {
                foreach (IEngineeringObject tag in HmiObjects.Composition(table, "Tags"))
                {
                    result.Tags.Add(new TagEntry { Table = entry.Name, Folders = entry.Folders, TagName = HmiObjects.NameOf(tag), Tag = tag });
                    entry.TagCount++;
                }
            }
            catch (Exception ex)
            {
                result.ScanErrors.Add(new ExportError { Table = entry.Name, Folder = entry.FolderPath, Message = "Tabelle nicht lesbar: " + ex.Message });
            }
        }
    }
}
