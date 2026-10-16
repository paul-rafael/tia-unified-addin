using System;
using System.Threading;
using System.Windows.Forms;

namespace Prh.UnifiedKonverter.Ui
{
    /// <summary>
    /// Fenster laufen in einem eigenen STA-Thread, weil nicht garantiert ist,
    /// in welchem Thread TIA Portal den Menü-Aufruf des Add-Ins ausführt.
    /// </summary>
    internal static class UiThread
    {
        public static void Run(Action action)
        {
            Exception error = null;
            Thread thread = new Thread(() =>
            {
                try
                {
                    Application.EnableVisualStyles();
                    action();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            thread.Join();
            if (error != null) throw new InvalidOperationException("Fehler im Fenster des Add-Ins.", error);
        }
    }
}
