using System;
using System.IO;
using System.Text;

namespace Prh.UnifiedKonverter.Report
{
    /// <summary>
    /// Laufprotokoll für Schritt 3 unter Dokumente\Unified-Konverter\Protokolle. Jede Zeile geht sofort an Windows
    /// (AutoFlush) – stürzt TIA ab, zeigt die letzte Zeile, bei welchem Schritt bzw. Objekt.
    /// Fehler beim Protokollieren werden ignoriert: das Protokoll darf die Übertragung nie stören.
    /// </summary>
    internal static class Protocol
    {
        private static readonly object Gate = new object();
        private static StreamWriter _writer;
        private static string _path;

        public static string FilePath { get { return _path; } }

        public static void Start(string name)
        {
            lock (Gate)
            {
                Close();
                try
                {
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Unified-Konverter", "Protokolle");
                    Directory.CreateDirectory(dir);
                    _path = Path.Combine(dir, name + "_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".log");
                    _writer = new StreamWriter(new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(true));
                    _writer.AutoFlush = true;
                }
                catch (Exception)
                {
                    Close();
                }
            }
            Line("Unified-Konverter " + typeof(Protocol).Assembly.GetName().Version + " · " + name);
        }

        public static void Line(string text)
        {
            lock (Gate)
            {
                if (_writer == null) return;
                try { _writer.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + "  " + text); }
                catch (Exception) { }
            }
        }

        public static void Close()
        {
            lock (Gate)
            {
                try { if (_writer != null) _writer.Dispose(); }
                catch (Exception) { }
                _writer = null;
            }
        }
    }
}
