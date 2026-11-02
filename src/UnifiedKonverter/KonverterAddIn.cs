using System;
using System.Collections.Generic;
using System.Linq;
using Prh.UnifiedKonverter.Openness;
using Prh.UnifiedKonverter.Report;
using Prh.UnifiedKonverter.Rules;
using Prh.UnifiedKonverter.Transfer;
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
    /// Kontextmenü „Unified-Konverter“ an der alten HMI (Gerät), am Ordner „HMI-Variablen“ oder an Variablentabellen.
    /// Schritt 1 „Für Unified vorbereiten“: prüfen, Vorschau, Export der bereinigten Namen als Excel-Liste.
    /// Schritt 3 „Zu Unified übertragen“: Variablen, Textlisten, Bitmeldungen in eine gewählte Unified-HMI.
    /// </summary>
    public sealed class KonverterAddIn : ContextMenuAddIn
    {
        private readonly TiaPortal _tiaPortal;

        public KonverterAddIn(TiaPortal tiaPortal) : base("Unified-Konverter")
        {
            _tiaPortal = tiaPortal;
        }

        protected override void BuildContextMenuItems(ContextMenuAddInRoot addInRootSubmenu)
        {
            addInRootSubmenu.Items.AddActionItem<IEngineeringObject>("Für Unified vorbereiten …", OnPrepare, OnPrepareStatus);
            addInRootSubmenu.Items.AddActionItem<IEngineeringObject>("Zu Unified übertragen …", OnTransfer, OnPrepareStatus);
        }

        private MenuStatus OnPrepareStatus(MenuSelectionProvider<IEngineeringObject> menuSelectionProvider)
        {
            // Muss schnell sein (läuft beim Öffnen des Menüs): nur Typen prüfen. Ob ein Gerät eine HMI hat, zeigt erst der Klick.
            // Schleife statt Lambda: zwischengespeicherte Lambdas mit TIA-Typen lehnt der Siemens-Publisher ab.
            foreach (IEngineeringObject o in menuSelectionProvider.GetSelection<IEngineeringObject>())
            {
                if (HmiObjects.IsHmiTagTable(o) || HmiObjects.IsHmiTagFolder(o) || HmiObjects.IsHmiSoftware(o) || HmiObjects.IsDevice(o)) return MenuStatus.Enabled;
            }
            return MenuStatus.Disabled;
        }

        private void OnPrepare(MenuSelectionProvider<IEngineeringObject> menuSelectionProvider)
        {
            List<IEngineeringObject> selection = menuSelectionProvider.GetSelection<IEngineeringObject>().ToList();
            TagTableReader reader = new TagTableReader();
            ReadResult result;
            SourceData extra = new SourceData();

            Protocol.Start("Vorbereitung");
            try
            {
                using (ExclusiveAccess access = _tiaPortal.ExclusiveAccess())
                {
                    access.Text = "Unified-Konverter: HMI-Variablen werden gelesen …";
                    result = reader.Read(selection, CancelCheck(access));
                    LogTables(result);
                    access.Text = "Unified-Konverter: Textlisten und Meldungen werden gelesen …";
                    foreach (IEngineeringObject hmi in HmiObjects.FindAllHmiIn(selection))
                    {
                        if (hmi.GetType().Name != "HmiTarget") continue;
                        Protocol.Line("Textlisten/Meldungen: " + HmiObjects.NameOf(hmi));
                        SourceReader.ReadTextLists(hmi, extra);
                        SourceReader.ReadAlarmsFromOpenness(hmi, extra);
                    }
                    LogTextLists(extra);
                }
                ProjectChecks.Run(result);
            }
            catch (Exception ex)
            {
                Protocol.Line("FEHLER beim Lesen: " + ex);
                UiThread.Run(() => MessageForm.ShowError("Lesen fehlgeschlagen", ex.ToString()));
                return;
            }

            if (result.Tables.Count == 0)
            {
                UiThread.Run(() => MessageForm.ShowInfo("Keine HMI-Variablen gefunden",
                    "In der Auswahl wurde keine HMI-Variablentabelle gefunden.\n\nBitte mit der rechten Maustaste auf das alte HMI-Gerät (Comfort/Advanced), " +
                    "den Ordner „HMI-Variablen“ oder eine Variablentabelle klicken."));
                return;
            }

            UiThread.Run(() =>
            {
                using (PrepareForm form = new PrepareForm(result, extra))
                {
                    form.ShowDialog();
                }
            });
            Protocol.Close();
        }

        private static void LogTables(ReadResult result)
        {
            Protocol.Line("Variablentabellen: " + result.Tables.Count + ", Variablen: " + result.AllTags.Count());
            foreach (HmiTableInfo t in result.Tables)
            {
                Protocol.Line("   " + t.Device.Name + " · " + (string.IsNullOrEmpty(t.Folder) ? "" : t.Folder + " › ") + t.Name + " (" + t.Tags.Count + ")");
            }
        }

        /// <summary>Welche Attribute Openness an Textlisten und Einträgen liefert – für die Spaltenzuordnung im Excel-Export.</summary>
        private static void LogTextLists(SourceData data)
        {
            Protocol.Line("Textlisten: " + data.TextLists.Count + " (" + data.TextListSource + ")");
            foreach (SourceTextList tl in data.TextLists)
            {
                Protocol.Line("   " + tl.Name + ": " + tl.Entries.Count + " Einträge; " + string.Join("; ", tl.Attributes.Select(a => a.Key + "=" + a.Value)));
            }
            SourceTextList first = data.TextLists.FirstOrDefault(l => l.Entries.Count > 0);
            if (first != null)
            {
                SourceTextListEntry e = first.Entries[0];
                Protocol.Line("   Beispiel-Eintrag: " + string.Join("; ", e.Attributes.Select(a => a.Key + "=" + a.Value))
                    + " | Texte: " + string.Join("; ", e.Texts.Select(a => a.Key + "=" + a.Value)));
            }
            Protocol.Line("Meldungen: " + data.AlarmSource);
        }

        private void OnTransfer(MenuSelectionProvider<IEngineeringObject> menuSelectionProvider)
        {
            List<IEngineeringObject> selection = menuSelectionProvider.GetSelection<IEngineeringObject>().ToList();
            SourceData source = new SourceData();
            List<UnifiedTarget> targets;
            IEngineeringObject project;

            // Alle alten HMIs der Auswahl (mehrere Geräte mit Strg+Klick markieren) – Schleife statt Lambda (Publisher-Regel).
            List<IEngineeringObject> sources = new List<IEngineeringObject>();
            foreach (IEngineeringObject h in HmiObjects.FindAllHmiIn(selection))
            {
                if (h.GetType().Name == "HmiTarget") sources.Add(h);
            }
            if (sources.Count == 0)
            {
                UiThread.Run(() => MessageForm.ShowInfo("Bitte die alte HMI wählen",
                    "„Zu Unified übertragen“ startet an der alten HMI (Comfort/Advanced): mit der rechten Maustaste auf das Gerät klicken.\n\n" +
                    "Die Ziel-Unified-HMI wählst du danach im Fenster aus."));
                return;
            }

            List<string> sourceNames = new List<string>();
            foreach (IEngineeringObject h in sources) sourceNames.Add(HmiObjects.NameOf(h));
            string sourceName = string.Join(", ", sourceNames);
            Protocol.Start("Uebertragung");
            Protocol.Line("Quelle: " + sourceName);
            try
            {
                using (ExclusiveAccess access = _tiaPortal.ExclusiveAccess())
                {
                    access.Text = "Unified-Konverter: alte HMI wird gelesen …";
                    Protocol.Line("Lesen: Variablen");
                    source.Tags = new TagTableReader().Read(sources, CancelCheck(access));
                    ProjectChecks.Run(source.Tags);
                    LogTables(source.Tags);
                    foreach (IEngineeringObject h in sources)
                    {
                        Protocol.Line("Lesen: Textlisten und Meldungen von " + HmiObjects.NameOf(h));
                        SourceReader.ReadTextLists(h, source);
                        SourceReader.ReadAlarmsFromOpenness(h, source);
                    }
                    Protocol.Line("Lesen: Unified-Ziele im Projekt");
                    project = ProjectScan.FindProject(sources[0]);
                    targets = ProjectScan.FindUnifiedTargets(project)
                        .GroupBy(t => t.ToString()).Select(g => g.First()).ToList();
                }
                Protocol.Line("Gelesen: " + source.Tags.AllTags.Count() + " Variablen, " + source.TextLists.Count + " Textlisten, "
                    + source.Alarms.Count + " Meldungen, " + targets.Count + " Unified-Ziele");
            }
            catch (Exception ex)
            {
                Protocol.Line("FEHLER beim Lesen: " + ex);
                UiThread.Run(() => MessageForm.ShowError("Lesen fehlgeschlagen", ex.ToString()));
                return;
            }

            object projectRef = project;
            UiThread.Run(() =>
            {
                using (TransferForm form = new TransferForm(sourceName, source, targets, (description, body) => RunWriting(projectRef, description, body)))
                {
                    form.ShowDialog();
                }
            });
            Protocol.Line("Fenster geschlossen");
            Protocol.Close();
        }

        /// <summary>Schreibt unter ExclusiveAccess und in einer Transaktion – in TIA mit einem „Rückgängig“ zurücknehmbar.</summary>
        private void RunWriting(object project, string description, Action<Func<bool>> body)
        {
            using (ExclusiveAccess access = _tiaPortal.ExclusiveAccess())
            {
                access.Text = "Unified-Konverter: " + description + " …";
                ITransactionSupport transactionSupport = project as ITransactionSupport;
                if (transactionSupport == null)
                {
                    Protocol.Line("Schreiben ohne Transaktion: " + description);
                    body(CancelCheck(access));
                    return;
                }
                Protocol.Line("Transaktion starten: " + description);
                using (Transaction transaction = access.Transaction(transactionSupport, description))
                {
                    body(CancelCheck(access));
                    transaction.CommitOnDispose();
                    Protocol.Line("Transaktion abschließen");
                }
                Protocol.Line("Transaktion abgeschlossen");
            }
        }

        /// <summary>
        /// Abbruch-Abfrage ohne eingefangene TIA-Typen: Lambdas speichern eingefangene Variablen als Felder,
        /// und Felder mit TIA-Objekttypen lehnt der Siemens-Publisher (ab V20) ab.
        /// </summary>
        private static Func<bool> CancelCheck(object access)
        {
            return () => ((ExclusiveAccess)access).IsCancellationRequested;
        }
    }
}
