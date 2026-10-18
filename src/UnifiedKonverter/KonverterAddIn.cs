using System;
using System.Collections.Generic;
using System.Linq;
using Prh.UnifiedKonverter.Openness;
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
    /// Schritt 1 „Für Unified vorbereiten“: prüfen, Vorschau, Umbenennen erst nach Bestätigung.
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

            try
            {
                using (ExclusiveAccess access = _tiaPortal.ExclusiveAccess())
                {
                    access.Text = "Unified-Konverter: HMI-Variablen werden gelesen …";
                    result = reader.Read(selection, CancelCheck(access));
                }
                ProjectChecks.Run(result);
            }
            catch (Exception ex)
            {
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
                using (PrepareForm form = new PrepareForm(result, tags => TagRenamer.Apply(_tiaPortal, tags, reader.Sources)))
                {
                    form.ShowDialog();
                }
            });
        }

        private void OnTransfer(MenuSelectionProvider<IEngineeringObject> menuSelectionProvider)
        {
            List<IEngineeringObject> selection = menuSelectionProvider.GetSelection<IEngineeringObject>().ToList();
            SourceData source = new SourceData();
            List<UnifiedTarget> targets;
            IEngineeringObject project;

            IEngineeringObject hmi = HmiObjects.FindHmiIn(selection);
            if (hmi == null || hmi.GetType().Name != "HmiTarget")
            {
                UiThread.Run(() => MessageForm.ShowInfo("Bitte die alte HMI wählen",
                    "„Zu Unified übertragen“ startet an der alten HMI (Comfort/Advanced): mit der rechten Maustaste auf das Gerät klicken.\n\n" +
                    "Die Ziel-Unified-HMI wählst du danach im Fenster aus."));
                return;
            }

            try
            {
                using (ExclusiveAccess access = _tiaPortal.ExclusiveAccess())
                {
                    access.Text = "Unified-Konverter: alte HMI wird gelesen …";
                    source.Tags = new TagTableReader().Read(new[] { hmi }, CancelCheck(access));
                    ProjectChecks.Run(source.Tags);
                    SourceReader.ReadTextLists(hmi, source);
                    SourceReader.ReadAlarmsFromOpenness(hmi, source);
                    project = ProjectScan.FindProject(hmi);
                    targets = ProjectScan.FindUnifiedTargets(project)
                        .GroupBy(t => t.ToString()).Select(g => g.First()).ToList();
                }
            }
            catch (Exception ex)
            {
                UiThread.Run(() => MessageForm.ShowError("Lesen fehlgeschlagen", ex.ToString()));
                return;
            }

            string sourceName = HmiObjects.NameOf(hmi);
            object projectRef = project;
            UiThread.Run(() =>
            {
                using (TransferForm form = new TransferForm(sourceName, source, targets, (description, body) => RunWriting(projectRef, description, body)))
                {
                    form.ShowDialog();
                }
            });
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
                    body(CancelCheck(access));
                    return;
                }
                using (Transaction transaction = access.Transaction(transactionSupport, description))
                {
                    body(CancelCheck(access));
                    transaction.CommitOnDispose();
                }
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
