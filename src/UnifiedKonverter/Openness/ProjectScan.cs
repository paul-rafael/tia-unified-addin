using System;
using System.Collections.Generic;
using System.Linq;
using Siemens.Engineering;

namespace Prh.UnifiedKonverter.Openness
{
    public sealed class UnifiedTarget
    {
        public string Name;
        public string DeviceName;
        /// <summary>HmiSoftware – als object, weil der Siemens-Publisher Felder mit TIA-Objekttypen verbietet.</summary>
        internal object Software;

        public override string ToString()
        {
            return DeviceName == null || DeviceName == Name ? Name : DeviceName + " › " + Name;
        }
    }

    /// <summary>Findet Projekt und alle Unified-HMIs darin (auch in Gerätegruppen).</summary>
    internal static class ProjectScan
    {
        public static IEngineeringObject FindProject(IEngineeringObject obj)
        {
            IEngineeringObject current = obj;
            for (int depth = 0; current != null && depth < 50; depth++)
            {
                if (current.GetType().Name == "Project") return current;
                current = current.Parent as IEngineeringObject;
            }
            return null;
        }

        public static List<UnifiedTarget> FindUnifiedTargets(IEngineeringObject project)
        {
            List<UnifiedTarget> targets = new List<UnifiedTarget>();
            if (project == null) return targets;
            foreach (IEngineeringObject device in AllDevices(project))
            {
                string deviceName = HmiObjects.NameOf(device);
                foreach (IEngineeringObject software in SoftwareIn(device, 0))
                {
                    if (software.GetType().Name == "HmiSoftware")
                    {
                        targets.Add(new UnifiedTarget { Name = HmiObjects.NameOf(software), DeviceName = deviceName, Software = software });
                    }
                }
            }
            return targets;
        }

        private static IEnumerable<IEngineeringObject> AllDevices(IEngineeringObject project)
        {
            List<IEngineeringObject> devices = new List<IEngineeringObject>(HmiObjects.Composition(project, "Devices"));
            Queue<IEngineeringObject> groups = new Queue<IEngineeringObject>(HmiObjects.Composition(project, "DeviceGroups"));
            IEngineeringObject ungrouped = HmiObjects.Navigate(project, "UngroupedDevicesGroup") as IEngineeringObject;
            if (ungrouped != null) groups.Enqueue(ungrouped);
            int guard = 0;
            while (groups.Count > 0 && guard++ < 1000)
            {
                IEngineeringObject g = groups.Dequeue();
                devices.AddRange(HmiObjects.Composition(g, "Devices"));
                foreach (IEngineeringObject sub in HmiObjects.Composition(g, "Groups")) groups.Enqueue(sub);
            }
            return devices;
        }

        // Kein yield: Iteratoren erzeugen Felder mit TIA-Objekttypen, die der Siemens-Publisher ablehnt.
        private static List<IEngineeringObject> SoftwareIn(IEngineeringObject item, int depth)
        {
            List<IEngineeringObject> found = new List<IEngineeringObject>();
            if (depth > 6) return found;
            IEngineeringObject software = HmiObjects.SoftwareOf(item);
            if (software != null) found.Add(software);
            foreach (IEngineeringObject child in HmiObjects.Composition(item, "DeviceItems"))
            {
                found.AddRange(SoftwareIn(child, depth + 1));
            }
            return found;
        }
    }
}
