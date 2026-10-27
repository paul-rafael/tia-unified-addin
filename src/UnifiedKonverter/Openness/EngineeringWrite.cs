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
            IEngineeringObject created;
            if (TryCreate(parent, composition, out created, name)) return created;

            // Create(Name, …) mit Standardwerten für die übrigen Parameter
            object comp = CompositionObject(parent, composition);
            MethodInfo[] creates = comp == null ? new MethodInfo[0]
                : comp.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.Name == "Create").ToArray();
            MethodInfo withDefaults = creates.FirstOrDefault(m =>
            {
                ParameterInfo[] ps = m.GetParameters();
                return ps.Length > 0 && ps[0].ParameterType == typeof(string) && ps.Skip(1).All(x => x.HasDefaultValue);
            });
            if (withDefaults != null)
            {
                object[] args = withDefaults.GetParameters().Select((x, i) => i == 0 ? name : x.DefaultValue).ToArray();
                Protocol.Line("   anlegen: " + comp.GetType().Name + "." + Signature(withDefaults) + " mit Standardwerten");
                try { created = (IEngineeringObject)withDefaults.Invoke(comp, args); }
                catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
                Protocol.Line("   angelegt");
                return created;
            }

            string available = creates.Length == 0 ? "keine" : string.Join("; ", creates.Select(Signature));
            Protocol.Line("   kein passendes Create, vorhanden: " + available);
            throw new InvalidOperationException("„" + composition + "“ bietet kein Create(Name). Vorhanden: " + available);
        }

        private static string Signature(MethodInfo m)
        {
            return m.Name + "(" + string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")";
        }

        /// <summary>
        /// composition.Create(args…) mit so vielen Text-Parametern wie <paramref name="args"/> – false, wenn es die
        /// Komposition oder diese Überladung nicht gibt. Fehler beim Anlegen selbst werden weitergereicht.
        /// </summary>
        public static bool TryCreate(IEngineeringObject parent, string composition, out IEngineeringObject created, params string[] args)
        {
            created = null;
            object comp = CompositionObject(parent, composition);
            if (comp == null) return false;
            MethodInfo create = comp.GetType().GetMethod("Create", args.Select(a => typeof(string)).ToArray());
            if (create == null) return false;
            Protocol.Line("   anlegen: " + comp.GetType().Name + ".Create(" + string.Join(", ", args) + ")");
            try
            {
                created = (IEngineeringObject)create.Invoke(comp, args.Cast<object>().ToArray());
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException ?? ex;
            }
            Protocol.Line("   angelegt");
            return true;
        }

        /// <summary>Erster Kompositionsname aus der Liste, den das Objekt anbietet.</summary>
        /// <remarks>
        /// Ohne GetCompositionInfos() (siehe <see cref="WritableAttributes"/>): zuerst über den .NET-Typ (Unified),
        /// sonst über GetComposition(Name) – Comfort/Advanced bietet Kompositionen nicht als .NET-Eigenschaft an.
        /// </remarks>
        public static string FirstComposition(IEngineeringObject obj, params string[] candidates)
        {
            Type type = obj.GetType();
            string byType = candidates.FirstOrDefault(c => type.GetProperty(c, BindingFlags.Public | BindingFlags.Instance) != null);
            return byType ?? candidates.FirstOrDefault(c => CompositionObject(obj, c) != null);
        }

        /// <summary>
        /// Schreibbare Eigenschaften aus dem .NET-Typ (Reflection) – ohne Aufruf an TIA. GetAttributeInfos() an einer
        /// neu angelegten Unified-Variable warf in TIA V20 „Property: ConfirmationType not found“, danach stürzte TIA ab.
        /// </summary>
        public static HashSet<string> WritableAttributes(IEngineeringObject obj)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (PropertyInfo p in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetSetMethod() != null && p.GetIndexParameters().Length == 0) names.Add(p.Name);
            }
            Protocol.Line("   schreibbar (" + obj.GetType().Name + "): " + string.Join(", ", names));
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
                PropertyInfo property = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (property != null && property.GetSetMethod() != null) property.SetValue(obj, Convert(obj, name, value));
                else obj.SetAttribute(name, Convert(obj, name, value));
                Protocol.Line("   ok");
                return null;
            }
            catch (Exception ex)
            {
                Protocol.Line("   Fehler: " + Unwrap(ex).Message);
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
