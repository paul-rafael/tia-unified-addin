using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.HW.Features;

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

        private static bool IsHmiNamespace(Type t)
        {
            // PLC-Objekte liegen unter Siemens.Engineering.SW – nur HMI zulassen.
            string ns = t.Namespace ?? string.Empty;
            return ns.IndexOf("Hmi", StringComparison.Ordinal) >= 0 || ns.IndexOf("WinCC", StringComparison.Ordinal) >= 0;
        }

        public static bool IsHmiTagTable(IEngineeringObject obj)
        {
            if (obj == null) return false;
            Type t = obj.GetType();
            return TagTableTypeNames.Contains(t.Name) && IsHmiNamespace(t);
        }

        /// <summary>Ordner „HMI-Variablen“ bzw. Unterordner (Classic: TagSystemFolder/TagUserFolder, Unified: Tabellengruppen).</summary>
        public static bool IsHmiTagFolder(IEngineeringObject obj)
        {
            if (obj == null) return false;
            Type t = obj.GetType();
            string n = t.Name;
            return n.IndexOf("Tag", StringComparison.Ordinal) >= 0
                && (n.IndexOf("Folder", StringComparison.Ordinal) >= 0 || n.IndexOf("Group", StringComparison.Ordinal) >= 0)
                && IsHmiNamespace(t);
        }

        public static bool IsHmiSoftware(IEngineeringObject obj)
        {
            if (obj == null) return false;
            string n = obj.GetType().Name;
            return n == "HmiTarget" || n == "HmiSoftware";
        }

        public static bool IsDevice(IEngineeringObject obj)
        {
            if (obj == null) return false;
            string n = obj.GetType().Name;
            return n == "Device" || n == "DeviceItem";
        }

        public static string NameOf(IEngineeringObject obj)
        {
            object value;
            return TryGetAttribute(obj, "Name", out value) && value != null
                ? Convert.ToString(value, CultureInfo.InvariantCulture)
                : obj.GetType().Name;
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

        /// <summary>Navigation, die nicht als Attribut angeboten wird (z. B. HmiTarget.TagFolder): Attribut, sonst öffentliche Eigenschaft.</summary>
        public static object Navigate(IEngineeringObject obj, string name)
        {
            object value;
            if (TryGetAttribute(obj, name, out value) && value != null) return value;
            try
            {
                System.Reflection.PropertyInfo p = obj.GetType().GetProperty(name);
                return p == null ? null : p.GetValue(obj, null);
            }
            catch (Exception)
            {
                return null;
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
            return items == null ? Enumerable.Empty<IEngineeringObject>() : items.OfType<IEngineeringObject>().ToList();
        }

        /// <summary>Software eines Geräteelements (HmiTarget, HmiSoftware, PlcSoftware …) oder null.</summary>
        public static IEngineeringObject SoftwareOf(IEngineeringObject obj)
        {
            try
            {
                IEngineeringServiceProvider provider = obj as IEngineeringServiceProvider;
                SoftwareContainer container = provider == null ? null : provider.GetService<SoftwareContainer>();
                return container == null ? null : container.Software as IEngineeringObject;
            }
            catch (Exception)
            {
                return null;
            }
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

        /// <summary>Erstes HMI-Gerät (HmiTarget/HmiSoftware) in einer Auswahl aus Gerät, HMI, Ordner oder Tabelle.</summary>
        public static IEngineeringObject FindHmiIn(IEnumerable<IEngineeringObject> selection)
        {
            foreach (IEngineeringObject obj in selection)
            {
                if (IsHmiSoftware(obj)) return obj;
                HmiKind kind;
                IEngineeringObject up = FindHmiSoftware(obj, out kind);
                if (up != null) return up;
                if (IsDevice(obj))
                {
                    IEngineeringObject found = FindHmiInDevice(obj, 0);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private static IEngineeringObject FindHmiInDevice(IEngineeringObject item, int depth)
        {
            if (depth > 6) return null;
            IEngineeringObject software = SoftwareOf(item);
            if (software != null && IsHmiSoftware(software)) return software;
            foreach (IEngineeringObject child in Composition(item, "DeviceItems"))
            {
                IEngineeringObject found = FindHmiInDevice(child, depth + 1);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Ordnerpfad zwischen HMI-Gerät und Objekt, z. B. „HMI-Variablen › Antriebe“.</summary>
        public static string FolderPath(IEngineeringObject obj)
        {
            List<string> names = new List<string>();
            IEngineeringObject current = obj.Parent as IEngineeringObject;
            for (int depth = 0; current != null && depth < 30 && !IsHmiSoftware(current); depth++)
            {
                names.Add(NameOf(current));
                current = current.Parent as IEngineeringObject;
            }
            names.Reverse();
            return string.Join(" › ", names);
        }

        /// <summary>Lesbare Darstellung eines Attributwerts für Liste und CSV.</summary>
        public static string Format(object value)
        {
            if (value == null) return string.Empty;
            string s = value as string;
            if (s != null) return s;
            IEngineeringObject eo = value as IEngineeringObject;
            if (eo != null)
            {
                object n;
                return TryGetAttribute(eo, "Name", out n) && n != null
                    ? Convert.ToString(n, CultureInfo.InvariantCulture)
                    : "(" + eo.GetType().Name + ")";
            }
            IFormattable f = value as IFormattable;
            if (f != null) return f.ToString(null, CultureInfo.InvariantCulture);
            IEnumerable e = value as IEnumerable;
            if (e != null) return "[" + string.Join(", ", e.Cast<object>().Take(20).Select(Format)) + "]";
            return value.ToString();
        }
    }
}
