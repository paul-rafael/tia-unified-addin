using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Siemens.Engineering;

namespace Prh.UnifiedKonverter.EventExport
{
    /// <summary>
    /// Exportiert eine HMI-Variable als SimaticML-XML über Tag.Export(FileInfo, ExportOptions.WithDefaults).
    /// Aufruf über Reflection, damit derselbe Code für V20 und V21 baut; fehlt Export, gibt es eine klare Meldung.
    /// Muss im Thread des Menü-Aufrufs laufen (Openness).
    /// </summary>
    internal static class TagXmlExporter
    {
        public const string ExportOption = "WithDefaults";

        public static void Export(object tag, string filePath)
        {
            IEngineeringObject obj = (IEngineeringObject)tag;
            MethodInfo export = obj.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(m =>
            {
                ParameterInfo[] p = m.GetParameters();
                return m.Name == "Export" && p.Length == 2 && p[0].ParameterType == typeof(FileInfo) && p[1].ParameterType.IsEnum;
            });
            if (export == null) throw new NotSupportedException("„" + obj.GetType().Name + "“ bietet kein Export(FileInfo, ExportOptions) an.");
            object options = Enum.Parse(export.GetParameters()[1].ParameterType, ExportOption);
            try
            {
                export.Invoke(obj, new object[] { new FileInfo(filePath), options });
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException ?? ex;
            }
        }
    }

    /// <summary>
    /// Eindeutige Dateinamen nach dem Schema „Tabelle__Ordnerpfad__Variable.xml“ (ohne Ordner: „Tabelle__Variable.xml“).
    /// Ungültige Zeichen werden „_“, lange Namen gekürzt (mit Prüfsumme), Kollisionen bekommen „~2“, „~3“ …
    /// Groß/Klein zählt nicht (Windows-Dateisystem).
    /// </summary>
    internal sealed class FileNamer
    {
        private const int MaxStem = 150;
        private static readonly HashSet<char> Invalid = new HashSet<char>(Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }));
        private static readonly string[] Reserved = { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
        private readonly HashSet<string> _used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public string Next(TagEntry tag)
        {
            List<string> parts = new List<string> { Clean(tag.Table) };
            if (tag.Folders.Count > 0) parts.Add(string.Join("_", tag.Folders.Select(Clean)));
            parts.Add(Clean(tag.TagName));
            string stem = string.Join("__", parts);
            if (stem.Length > MaxStem) stem = stem.Substring(0, MaxStem - 9) + "~" + Hash(stem);

            string name = stem + ".xml";
            for (int n = 2; !_used.Add(name); n++) name = stem + "~" + n + ".xml";
            return name;
        }

        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "_";
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char c in s) sb.Append(c < 0x20 || Invalid.Contains(c) ? '_' : c);
            string r = sb.ToString().TrimEnd('.', ' ');
            if (r.Length == 0) r = "_";
            if (Reserved.Contains(r.ToUpperInvariant())) r = "_" + r;
            return r;
        }

        private static string Hash(string s)
        {
            using (SHA1 sha = SHA1.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
                return string.Concat(h.Take(4).Select(b => b.ToString("x2")));
            }
        }
    }
}
