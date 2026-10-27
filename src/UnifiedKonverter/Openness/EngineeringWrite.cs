using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Prh.UnifiedKonverter.Report;
using Siemens.Engineering;

namespace Prh.UnifiedKonverter.Openness
{
    /// <summary>
    /// Schreibzugriffe ohne feste Typ-Abhängigkeit: Kompositionen über Reflection (Find/Create),
    /// Attribute mit automatischer Typumwandlung (Text → Enum/Zahl/Bool), mehrsprachige Texte.
    /// So baut derselbe Code für V20 und V21 und meldet fehlende Attribute statt abzustürzen.
    /// </summary>
    internal static class EngineeringWrite
    {
        public static object CompositionObject(IEngineeringObject parent, string composition)
        {
            try { return parent.GetComposition(composition); }
            catch (Exception) { return null; }
        }

        /// <summary>composition.Find(name) – null, wenn nicht vorhanden oder nicht unterstützt.</summary>
        public static IEngineeringObject Find(IEngineeringObject parent, string composition, string name)
        {
            object comp = CompositionObject(parent, composition);
            if (comp == null) return null;
            MethodInfo find = comp.GetType().GetMethod("Find", new[] { typeof(string) });
            if (find != null)
            {
                try { return find.Invoke(comp, new object[] { name }) as IEngineeringObject; }
                catch (Exception) { }
            }
            return HmiObjects.Composition(parent, composition).FirstOrDefault(o => string.Equals(HmiObjects.NameOf(o), name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>composition.Create(name). Wirft mit verständlicher Meldung, wenn es kein Create(string) gibt.</summary>
        public static IEngineeringObject Create(IEngineeringObject parent, string composition, string name)
        {
            object comp = CompositionObject(parent, composition);
            if (comp == null) throw new InvalidOperationException("Komposition „" + composition + "“ gibt es am Ziel nicht.");
            MethodInfo create = comp.GetType().GetMethod("Create", new[] { typeof(string) });
            if (create == null) throw new InvalidOperationException("„" + composition + "“ bietet kein Create(Name).");
            Protocol.Line("   anlegen in " + composition + ": " + name);
            try
            {
                return (IEngineeringObject)create.Invoke(comp, new object[] { name });
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException ?? ex;
            }
        }

        /// <summary>Erster Kompositionsname aus der Liste, den das Objekt anbietet.</summary>
        public static string FirstComposition(IEngineeringObject obj, params string[] candidates)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            try { foreach (EngineeringCompositionInfo c in obj.GetCompositionInfos()) names.Add(c.Name); }
            catch (Exception) { return null; }
            return candidates.FirstOrDefault(names.Contains);
        }

        public static HashSet<string> WritableAttributes(IEngineeringObject obj)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                foreach (EngineeringAttributeInfo info in obj.GetAttributeInfos())
                {
                    if (info.AccessMode.ToString().IndexOf("Write", StringComparison.OrdinalIgnoreCase) >= 0) names.Add(info.Name);
                }
            }
            catch (Exception) { }
            return names;
        }

        /// <summary>
        /// Setzt das erste vorhandene Attribut aus <paramref name="candidates"/>. Liefert null bei Erfolg,
        /// sonst eine kurze Begründung (für die Ergebnisliste).
        /// </summary>
        public static string TrySet(IEngineeringObject obj, HashSet<string> writable, string value, params string[] candidates)
        {
            if (string.IsNullOrEmpty(value)) return null;
            string name = candidates.FirstOrDefault(writable.Contains);
            if (name == null) return candidates[0] + ": am Ziel nicht schreibbar";
            Protocol.Line("   " + name + " = " + value);
            try
            {
                obj.SetAttribute(name, Convert(obj, name, value));
                return null;
            }
            catch (Exception ex)
            {
                return name + " = „" + value + "“: " + Unwrap(ex).Message;
            }
        }

        private static object Convert(IEngineeringObject obj, string attribute, string value)
        {
            PropertyInfo p = obj.GetType().GetProperty(attribute);
            Type type = p == null ? typeof(string) : p.PropertyType;
            Type underlying = Nullable.GetUnderlyingType(type) ?? type;
            if (underlying == typeof(string) || underlying == typeof(object)) return value;
            if (underlying.IsEnum)
            {
                string match = Enum.GetNames(underlying).FirstOrDefault(n => string.Equals(n, value.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
                if (match == null) throw new ArgumentException("kein gültiger Wert, erlaubt: " + string.Join(", ", Enum.GetNames(underlying)));
                return Enum.Parse(underlying, match);
            }
            if (underlying == typeof(bool)) return bool.Parse(value);
            if (underlying == typeof(int)) return int.Parse(value, CultureInfo.InvariantCulture);
            if (underlying == typeof(uint)) return uint.Parse(value, CultureInfo.InvariantCulture);
            if (underlying == typeof(short)) return short.Parse(value, CultureInfo.InvariantCulture);
            if (underlying == typeof(ushort)) return ushort.Parse(value, CultureInfo.InvariantCulture);
            if (underlying == typeof(long)) return long.Parse(value, CultureInfo.InvariantCulture);
            if (underlying == typeof(double)) return double.Parse(value, CultureInfo.InvariantCulture);
            if (underlying == typeof(float)) return float.Parse(value, CultureInfo.InvariantCulture);
            return value;
        }

        public static Exception Unwrap(Exception ex)
        {
            while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
            return ex;
        }

        // ── Mehrsprachige Texte (MultilingualText → Items → Language.Culture / Text) ──

        /// <summary>Alle Sprachen eines mehrsprachigen Texts, Schlüssel = Kulturname (z. B. de-DE).</summary>
        public static Dictionary<string, string> ReadMultilingual(IEngineeringObject owner, string attribute)
        {
            Dictionary<string, string> texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            object value;
            if (!HmiObjects.TryGetAttribute(owner, attribute, out value)) return texts;
            IEngineeringObject ml = value as IEngineeringObject;
            if (ml == null)
            {
                if (value != null) texts[""] = value.ToString();
                return texts;
            }
            foreach (IEngineeringObject item in HmiObjects.Composition(ml, "Items"))
            {
                string culture = CultureOf(item);
                object text;
                if (HmiObjects.TryGetAttribute(item, "Text", out text) && text != null) texts[culture] = text.ToString();
            }
            return texts;
        }

        /// <summary>Schreibt die Texte in die gleichnamigen Sprachen des Ziels; null bei Erfolg, sonst Begründung.</summary>
        public static string WriteMultilingual(IEngineeringObject owner, string attribute, Dictionary<string, string> texts)
        {
            if (texts == null || texts.Count == 0) return null;
            object value;
            if (!HmiObjects.TryGetAttribute(owner, attribute, out value)) return attribute + ": am Ziel nicht vorhanden";
            IEngineeringObject ml = value as IEngineeringObject;
            if (ml == null)
            {
                try { owner.SetAttribute(attribute, texts.Values.First()); return null; }
                catch (Exception ex) { return attribute + ": " + Unwrap(ex).Message; }
            }

            List<IEngineeringObject> items = HmiObjects.Composition(ml, "Items").ToList();
            if (items.Count == 0) return attribute + ": keine Projektsprache am Ziel";
            int written = 0;
            List<string> errors = new List<string>();
            foreach (IEngineeringObject item in items)
            {
                string culture = CultureOf(item);
                string text;
                if (!texts.TryGetValue(culture, out text) && !(items.Count == 1 && texts.TryGetValue("", out text))) continue;
                try { item.SetAttribute("Text", text); written++; }
                catch (Exception ex) { errors.Add(culture + ": " + Unwrap(ex).Message); }
            }
            if (written == 0 && errors.Count == 0)
            {
                // Keine gemeinsame Sprache: ersten Text in die erste Zielsprache schreiben.
                try { items[0].SetAttribute("Text", texts.Values.First()); written++; }
                catch (Exception ex) { errors.Add(Unwrap(ex).Message); }
            }
            return errors.Count == 0 ? null : attribute + ": " + string.Join("; ", errors);
        }

        private static string CultureOf(IEngineeringObject item)
        {
            object language;
            if (!HmiObjects.TryGetAttribute(item, "Language", out language) || language == null) return "";
            IEngineeringObject lang = language as IEngineeringObject;
            if (lang != null)
            {
                object culture;
                if (HmiObjects.TryGetAttribute(lang, "Culture", out culture) && culture != null)
                {
                    CultureInfo ci = culture as CultureInfo;
                    return ci != null ? ci.Name : culture.ToString();
                }
            }
            CultureInfo direct = language as CultureInfo;
            return direct != null ? direct.Name : language.ToString();
        }
    }
}
