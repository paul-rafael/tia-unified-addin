using System;
using System.Collections.Generic;
using System.Linq;
using Prh.UnifiedKonverter.Openness;
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
    /// Kontextmenü „Unified-Konverter“. Version 0.1 enthält nur den Machbarkeitstest:
    /// Variablen einer HMI-Variablentabelle (Comfort/Advanced oder Unified) auflisten – ausschließlich lesend.
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
            addInRootSubmenu.Items.AddActionItem<IEngineeringObject>("Variablen auflisten", OnListTags, OnListTagsStatus);
        }

        private MenuStatus OnListTagsStatus(MenuSelectionProvider<IEngineeringObject> menuSelectionProvider)
        {
            // Sichtbar, aber gesperrt, wenn keine HMI-Variablentabelle markiert ist – so bleibt der Eintrag auffindbar.
            return menuSelectionProvider.GetSelection<IEngineeringObject>().Any(HmiObjects.IsHmiTagTable)
                ? MenuStatus.Enabled
                : MenuStatus.Disabled;
        }

        private void OnListTags(MenuSelectionProvider<IEngineeringObject> menuSelectionProvider)
        {
            List<IEngineeringObject> tables = menuSelectionProvider.GetSelection<IEngineeringObject>().Where(HmiObjects.IsHmiTagTable).ToList();
            List<TagTableSnapshot> snapshots = new List<TagTableSnapshot>();

            try
            {
                // Mit dem Recht TIA.ReadOnly verweigert TIA das Setzen von ExclusiveAccess.Text
                // („not permitted in a read-only context“) – daher nur Sperre und Abbruch, kein Fortschrittstext.
                using (ExclusiveAccess access = _tiaPortal.ExclusiveAccess())
                {
                    foreach (IEngineeringObject table in tables)
                    {
                        if (access.IsCancellationRequested) break;
                        snapshots.Add(TagTableReader.Read(table));
                    }
                }
            }
            catch (Exception ex)
            {
                UiThread.Run(() => MessageForm.ShowError("Lesen fehlgeschlagen", ex.ToString()));
                return;
            }

            if (snapshots.Count == 0) return;
            UiThread.Run(() =>
            {
                using (TagListForm form = new TagListForm(snapshots))
                {
                    form.ShowDialog();
                }
            });
        }
    }
}
