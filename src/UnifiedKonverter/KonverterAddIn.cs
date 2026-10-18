using System;
using System.Collections.Generic;
using System.Linq;
using Prh.UnifiedKonverter.Openness;
using Prh.UnifiedKonverter.Rules;
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
        }

        private MenuStatus OnPrepareStatus(MenuSelectionProvider<IEngineeringObject> menuSelectionProvider)
        {
            // Muss schnell sein (läuft beim Öffnen des Menüs): nur Typen prüfen. Ob ein Gerät eine HMI hat, zeigt erst der Klick.
            bool fits = menuSelectionProvider.GetSelection<IEngineeringObject>().Any(o =>
                HmiObjects.IsHmiTagTable(o) || HmiObjects.IsHmiTagFolder(o) || HmiObjects.IsHmiSoftware(o) || HmiObjects.IsDevice(o));
            return fits ? MenuStatus.Enabled : MenuStatus.Disabled;
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
                    result = reader.Read(selection, () => access.IsCancellationRequested);
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
    }
}
