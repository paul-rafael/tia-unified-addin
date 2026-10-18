using System.Text;

namespace Prh.UnifiedKonverter.Rules
{
    /// <summary>
    /// Bereinigungsregeln exakt nach dem WinCC Unified List Converter (maßgeblich: getestetes Excel-VBA).
    /// Quelle: wincc-unified-list-converter, docs/ANALYSE_UND_PLAN.md Abschnitt 2, src/converters/*.ts, src/vba/vba.ts.
    /// </summary>
    internal static class ConverterRules
    {
        /// <summary>
        /// HMI-Variablen (VBA Modul1.SonderzeichenMitKnopf, converters/tags.ts → replaceNonAlnum):
        /// jede UTF-16-Codeeinheit, die nicht [A-Za-z0-9] ist (Like, binärer Vergleich), wird zu „_“.
        /// Umlaute, ß, Leerzeichen, „-“, „.“, „[“, „]“ usw. → „_“. Die Länge bleibt gleich.
        /// </summary>
        public static string CleanTagName(string name)
        {
            if (name == null) return null;
            StringBuilder sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                bool keep = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                sb.Append(keep ? c : '_');
            }
            return sb.ToString();
        }

        /// <summary>
        /// Textlisten · Aktuell (VBA Modul3.ReplaceSpecialCharsInWorkbook): Ersetzungen in dieser Reihenfolge
        /// „#“→„_“, „/“→„_“, „.“→„_“, „[“→„(“, „]“→„)“. Für Schritt 1 (Textlisten) vorbereitet, noch nicht angebunden.
        /// </summary>
        public static string CleanTextListName(string name)
        {
            if (name == null) return null;
            return name.Replace("#", "_").Replace("/", "_").Replace(".", "_").Replace("[", "(").Replace("]", ")");
        }
    }
}
