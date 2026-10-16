using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Siemens.Engineering;

namespace Prh.UnifiedKonverter.Openness
{
    public enum HmiKind
    {
        Unbekannt,
        /// <summary>WinCC Comfort/Advanced (Openness-Typ HmiTarget)</summary>
        Classic,
        /// <summary>WinCC Unified (Openness-Typ HmiSoftware)</summary>
        Unified,
    }

    /// <summary>
    /// Zugriff über die generische Openness-Schnittstelle (IEngineeringObject), damit derselbe Code
    /// ohne Typ-Abhängigkeiten für Comfort/Advanced und Unified sowie für V20 und V21 baut.
    /// </summary>
    internal static class HmiObjects
    {
        // Comfort/Advanced: Siemens.Engineering.Hmi.Tag.TagTable – Unified: Siemens.Engineering.HmiUnified.HmiTags.HmiTagTable
        private static readonly string[] TagTableTypeNames = { "TagTable", "HmiTagTable" };

        public static bool IsHmiTagTable(IEngineeringObject obj)
        {
            if (obj == null) return false;
            Type t = obj.GetType();
            if (!TagTableTypeNames.Contains(t.Name)) return false;
            string ns = t.Namespace ?? string.Empty;
            // PLC-Variablentabellen heißen PlcTagTable und liegen unter Siemens.Engineering.SW – nur HMI zulassen.
            return ns.IndexOf("Hmi", StringComparison.Ordinal) >= 0 || ns.IndexOf("WinCC", StringComparison.Ordinal) >= 0;
        }

        public static string NameOf(IEngineeringObject obj)
        {
            return TryGetAttribute(obj, "Name", out object value) ? Convert.ToString(value, CultureInfo.InvariantCulture) : obj.GetType().Name;
        }

        public static bool TryGetAttribute(IEngineeringObject obj, string name, out object value)
        {
            try
            {
                value = obj.GetAttribute(name);
                return true;
            }
            catch (Exception)
            {
                value = null;
                return false;
            }
        }

        public static IEnumerable<IEngineeringObject> Composition(IEngineeringObject obj, string name)
        {
            IEnumerable items;
            try
            {
                items = obj.GetComposition(name) as IEnumerable;
            }
            catch (Exception)
            {
                items = null;
            }
            return items == null ? Enumerable.Empty<IEngineeringObject>() : items.OfType<IEngineeringObject>();
        }

        /// <summary>Sucht nach oben das HMI-Gerät (HmiTarget bzw. HmiSoftware).</summary>
        public static IEngineeringObject FindHmiSoftware(IEngineeringObject obj, out HmiKind kind)
        {
            IEngineeringObject current = obj;
            for (int depth = 0; current != null && depth < 30; depth++)
            {
                string typeName = current.GetType().Name;
                if (typeName == "HmiTarget") { kind = HmiKind.Classic; return current; }
                if (typeName == "HmiSoftware") { kind = HmiKind.Unified; return current; }
                current = current.Parent as IEngineeringObject;
            }
            kind = HmiKind.Unbekannt;
            return null;
        }

        /// <summary>Lesbare Darstellung eines Attributwerts für Liste und CSV.</summary>
        public static string Format(object value)
        {
            if (value == null) return string.Empty;
            if (value is string s) return s;
            if (value is IEngineeringObject eo)
            {
                return TryGetAttribute(eo, "Name", out object n) && n != null
                    ? Convert.ToString(n, CultureInfo.InvariantCulture)
                    : "(" + eo.GetType().Name + ")";
            }
            if (value is IFormattable f) return f.ToString(null, CultureInfo.InvariantCulture);
            if (value is IEnumerable e)
            {
                return "[" + string.Join(", ", e.Cast<object>().Take(20).Select(Format)) + "]";
            }
            return value.ToString();
        }
    }
}
