using System.Collections.Generic;
using Prh.UnifiedKonverter.Openness;
using Siemens.Engineering;

namespace Prh.UnifiedKonverter.EventExport
{
    /// <summary>
    /// Ermittelt aus der Auswahl im Projektbaum das HMI-Gerät. Verlässt sich nicht darauf, dass die Auswahl schon ein
    /// HmiTarget ist: Gerät, Geräteelement, HMI-Software, Variablenordner oder -tabelle werden nach oben bzw. unten aufgelöst.
    /// </summary>
    internal static class HmiDeviceScanner
    {
        public enum Outcome { Ok, KeineHmi, Unified, Mehrere }

        /// <summary>Genau ein Comfort/Advanced-HMI-Gerät (HmiTarget) – sonst der Grund in <paramref name="outcome"/>.</summary>
        public static IEngineeringObject FindTarget(IEnumerable<IEngineeringObject> selection, out Outcome outcome, out List<string> names)
        {
            names = new List<string>();
            IEngineeringObject found = null;
            bool unified = false;
            foreach (IEngineeringObject hmi in HmiObjects.FindAllHmiIn(selection))
            {
                if (hmi.GetType().Name != "HmiTarget") { unified = true; continue; }
                names.Add(HmiObjects.NameOf(hmi));
                if (found == null) found = hmi;
            }
            outcome = names.Count > 1 ? Outcome.Mehrere : found != null ? Outcome.Ok : unified ? Outcome.Unified : Outcome.KeineHmi;
            return outcome == Outcome.Ok ? found : null;
        }

        /// <summary>Name des Geräts (z. B. „HMI_1“) und der HMI-Software (z. B. „HMI_RT_1“) sowie des Projekts.</summary>
        public static void Describe(IEngineeringObject hmiTarget, ScanResult into)
        {
            into.HmiDeviceName = HmiObjects.NameOf(hmiTarget);
            IEngineeringObject project = ProjectScan.FindProject(hmiTarget);
            into.ProjectName = project == null ? "" : HmiObjects.NameOf(project);
        }
    }
}
