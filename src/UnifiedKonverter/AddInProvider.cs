using System.Collections.Generic;
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
    /// Stellt das Add-In im Kontextmenü des Projektbaums bereit.
    /// Der Konstruktor muss schlank bleiben: TIA Portal lädt Add-Ins mit kurzem Zeitlimit.
    /// </summary>
    public sealed class AddInProvider : ProjectTreeAddInProvider
    {
        private readonly TiaPortal _tiaPortal;

        public AddInProvider(TiaPortal tiaPortal)
        {
            _tiaPortal = tiaPortal;
        }

        protected override IEnumerable<ContextMenuAddIn> GetContextMenuAddIns()
        {
            yield return new KonverterAddIn(_tiaPortal);
        }
    }
}
