using System;
using System.Collections.Generic;
using System.Linq;
using Siemens.Engineering;

namespace Prh.UnifiedKonverter.Openness
{
    public sealed class AttributeValue
    {
        public string Name;
        /// <summary>Openness-Zugriffsart, z. B. Read oder ReadWrite – zeigt, ob das Attribut per Add-In änderbar ist.</summary>
        public string Access;
        public string Value;
        /// <summary>Fehlermeldung, falls das Lesen fehlschlug.</summary>
        public string Error;
    }

    public sealed class TagRow
    {
        public string Name;
        public string TypeName;
        public List<AttributeValue> Attributes = new List<AttributeValue>();

        /// <summary>Erster vorhandener Wert aus einer Liste möglicher Attributnamen (Classic und Unified heißen teils verschieden).</summary>
        public string Get(params string[] candidates)
        {
            foreach (string c in candidates)
            {
                AttributeValue a = Attributes.FirstOrDefault(x => string.Equals(x.Name, c, StringComparison.OrdinalIgnoreCase));
                if (a != null && a.Error == null) return a.Value;
            }
            return null;
        }
    }

    public sealed class TagTableSnapshot
    {
        public string TableName;
        public string TableTypeName;
        public string DeviceName;
        public HmiKind Kind;
        public List<TagRow> Tags = new List<TagRow>();
        /// <summary>Kompositionen (z. B. TextLists, DiscreteAlarms) und Attribute des HMI-Geräts – zeigt, was Openness dort anbietet.</summary>
        public List<string> DeviceCompositions = new List<string>();
        public List<string> DeviceAttributes = new List<string>();
        public List<string> Problems = new List<string>();
    }

    /// <summary>Liest eine HMI-Variablentabelle vollständig aus. Ändert nichts am Projekt.</summary>
    internal static class TagTableReader
    {
        public static TagTableSnapshot Read(IEngineeringObject table)
        {
            TagTableSnapshot snap = new TagTableSnapshot
            {
                TableName = HmiObjects.NameOf(table),
                TableTypeName = table.GetType().FullName,
            };

            IEngineeringObject device = HmiObjects.FindHmiSoftware(table, out snap.Kind);
            if (device != null)
            {
                snap.DeviceName = HmiObjects.NameOf(device);
                Probe(device, snap);
            }

            foreach (IEngineeringObject tag in HmiObjects.Composition(table, "Tags"))
            {
                snap.Tags.Add(ReadTag(tag, snap.Problems));
            }
            return snap;
        }

        private static TagRow ReadTag(IEngineeringObject tag, List<string> problems)
        {
            TagRow row = new TagRow { Name = HmiObjects.NameOf(tag), TypeName = tag.GetType().FullName };
            IEnumerable<EngineeringAttributeInfo> infos;
            try
            {
                infos = tag.GetAttributeInfos();
            }
            catch (Exception ex)
            {
                problems.Add(row.Name + ": Attributliste nicht lesbar – " + ex.Message);
                return row;
            }

            foreach (EngineeringAttributeInfo info in infos)
            {
                AttributeValue a = new AttributeValue { Name = info.Name, Access = info.AccessMode.ToString() };
                try
                {
                    a.Value = HmiObjects.Format(tag.GetAttribute(info.Name));
                }
                catch (Exception ex)
                {
                    a.Error = ex.GetType().Name + ": " + ex.Message;
                }
                row.Attributes.Add(a);
            }
            return row;
        }

        private static void Probe(IEngineeringObject device, TagTableSnapshot snap)
        {
            try
            {
                snap.DeviceCompositions.AddRange(device.GetCompositionInfos().Select(c => c.Name));
            }
            catch (Exception ex)
            {
                snap.Problems.Add("Kompositionen des HMI-Geräts nicht lesbar – " + ex.Message);
            }
            try
            {
                snap.DeviceAttributes.AddRange(device.GetAttributeInfos().Select(a => a.Name + " (" + a.AccessMode + ")"));
            }
            catch (Exception ex)
            {
                snap.Problems.Add("Attribute des HMI-Geräts nicht lesbar – " + ex.Message);
            }
        }
    }
}
