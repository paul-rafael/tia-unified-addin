using System;
using System.Collections.Generic;
using System.Linq;
using Prh.UnifiedKonverter.EventExport;
using Prh.UnifiedKonverter.Openness;
using Prh.UnifiedKonverter.Report;
using Prh.UnifiedKonverter.Ui;
using Siemens.Engineering;
using Siemens.Engineering.AddIn;
using Siemens.Engineering.AddIn.Menu;
#if TIA_V21
using Siemens.Engineering.AddIn.Base;
using Siemens.Engineering.AddIn.Base.Menu;
#endif

namespace Prh.UnifiedKonverter
{
    /// <summary>
    /// Kontextmenü „HMI Event Exporter“ am HMI-Gerät: alle HMI-Variablen eines Comfort/Advanced-Geräts einzeln als
    /// SimaticML-XML (Tag.Export, inkl. Ereignisse) in ein ZIP für den HMI Event Converter. Ändert nichts am Projekt.
    /// </summary>
    public sealed class EventExporterAddIn : ContextMenuAddIn
    {
        private readonly TiaPortal _tiaPortal;

        public EventExporterAddIn(TiaPortal tiaPortal) : base("HMI Event Exporter")
        {
            _tiaPortal = tiaPortal;
        }

        protected override void BuildContextMenuItems(ContextMenuAddInRoot addInRootSubmenu)
        {
            addInRootSubmenu.Items.AddActionItem<IEngineeringObject>("Variablen inklusive Events exportieren …", OnExport, OnStatus);
        }

        private MenuStatus OnStatus(MenuSelectionProvider<IEngineeringObject> menuSelectionProvider)
        {
            // Schnell: nur Typen prüfen. Ob wirklich ein Comfort/Advanced-Gerät dahintersteht, prüft erst der Klick.
            foreach (IEngineeringObject o in menuSelectionProvider.GetSelection<IEngineeringObject>())
            {
                if (HmiObjects.IsHmiSoftware(o) || HmiObjects.IsDevice(o) || HmiObjects.IsHmiTagFolder(o) || HmiObjects.IsHmiTagTable(o)) return MenuStatus.Enabled;
            }
            return MenuStatus.Disabled;
        }

        private void OnExport(MenuSelectionProvider<IEngineeringObject> menuSelectionProvider)
        {
            List<IEngineeringObject> selection = menuSelectionProvider.GetSelection<IEngineeringObject>().ToList();
            HmiDeviceScanner.Outcome outcome;
            List<string> names;
            IEngineeringObject hmi = HmiDeviceScanner.FindTarget(selection, out outcome, out names);
            if (hmi == null)
            {
                string text = outcome == HmiDeviceScanner.Outcome.Mehrere
                    ? "Es sind mehrere HMI-Geräte markiert (" + string.Join(", ", names) + "). Bitte genau ein Gerät wählen – je Gerät entsteht ein eigenes Archiv."
                    : outcome == HmiDeviceScanner.Outcome.Unified
                        ? "Das gewählte Gerät ist eine WinCC-Unified-HMI. Der HMI Event Exporter ist für WinCC Comfort/Advanced (Variablen-Ereignisse aus WinCC Advanced)."
                        : "In der Auswahl wurde kein HMI-Gerät gefunden. Bitte mit der rechten Maustaste auf das Comfort/Advanced-HMI-Gerät klicken.";
                UiThread.Run(() => MessageForm.ShowInfo("Bitte ein HMI-Gerät wählen", text));
                return;
            }

            ScanResult scan;
            Protocol.Start("EventExport");
            try
            {
                using (ExclusiveAccess access = _tiaPortal.ExclusiveAccess())
                {
                    access.Text = "HMI Event Exporter: Variablentabellen werden gesucht …";
                    scan = ExportEngine.Scan(hmi);
                }
                Protocol.Line("Gerät: " + scan.HmiDeviceName + ", Projekt: " + scan.ProjectName + ", Tabellen: " + scan.Tables.Count + ", Variablen: " + scan.Tags.Count);
            }
            catch (Exception ex)
            {
                Protocol.Line("FEHLER beim Suchen: " + ex);
                Protocol.Close();
                UiThread.Run(() => MessageForm.ShowError("Variablen nicht lesbar", ex.ToString()));
                return;
            }

            UiThread.Run(() =>
            {
                using (EventExportForm form = new EventExportForm(scan, zipPath => RunExport(scan, zipPath)))
                {
                    form.ShowDialog();
                }
            });
            Protocol.Close();
        }

        private ExportResult RunExport(ScanResult scan, string zipPath)
        {
            Protocol.Line("Export nach " + zipPath);
            using (ExclusiveAccess access = _tiaPortal.ExclusiveAccess())
            {
                access.Text = "HMI Event Exporter: Export startet …";
                ExportResult result = ExportEngine.Run(scan, zipPath, CancelCheck(access), ProgressText(access));
                Protocol.Line("Fertig: " + result.Successful + " erfolgreich, " + result.Failed + " fehlgeschlagen, " + result.Duration.TotalSeconds.ToString("0.0") + " s"
                    + (result.Cancelled ? " (abgebrochen)" : ""));
                foreach (ExportFile f in result.Files.Where(f => !f.Success).Take(50)) Protocol.Line("   " + f.Table + " › " + f.TagName + ": " + f.Error);
                return result;
            }
        }

        // Keine eingefangenen TIA-Typen in Lambdas (Publisher-Regel ab V20) – deshalb object.
        private static Func<bool> CancelCheck(object access)
        {
            return () => ((ExclusiveAccess)access).IsCancellationRequested;
        }

        private static Action<int, int, string> ProgressText(object access)
        {
            return (done, total, tag) =>
            {
                if (done % 10 != 0 && done + 1 != total) return;
                try { ((ExclusiveAccess)access).Text = "HMI Event Exporter: " + (done + 1).ToString("N0") + " / " + total.ToString("N0") + " – " + tag; }
                catch (Exception) { }
            };
        }
    }
}
