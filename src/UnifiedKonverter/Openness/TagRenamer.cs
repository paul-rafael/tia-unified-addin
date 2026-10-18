using System;
using System.Collections.Generic;
using System.Linq;
using Siemens.Engineering;

namespace Prh.UnifiedKonverter.Openness
{
    public sealed class RenameOutcome
    {
        public HmiTagInfo Tag;
        public string OldName;
        public string NewName;
        public bool Success;
        public string Error;
    }

    /// <summary>
    /// Schreibt die bereinigten Namen ins Projekt – nur nach Bestätigung im Fenster.
    /// Alles läuft in einer Transaktion: in TIA mit einem „Rückgängig“ zurücknehmbar.
    /// Variablen mit Kollision werden nie umbenannt.
    /// </summary>
    internal static class TagRenamer
    {
        public static List<HmiTagInfo> Candidates(IEnumerable<HmiTagInfo> tags)
        {
            return tags.Where(t => t.IsRenamed && !t.Findings.Any(f => f.Kind == FindingKind.Kollision)).ToList();
        }

        public static List<RenameOutcome> Apply(TiaPortal tiaPortal, IList<HmiTagInfo> tags, IDictionary<HmiTagInfo, object> sources)
        {
            List<RenameOutcome> outcomes = new List<RenameOutcome>();
            if (tags.Count == 0) return outcomes;

            using (ExclusiveAccess access = tiaPortal.ExclusiveAccess())
            {
                access.Text = "Für Unified vorbereiten: " + tags.Count + " Variablen werden umbenannt …";
                ITransactionSupport project = FindProject((IEngineeringObject)sources[tags[0]]);
                Transaction transaction = project == null ? null : access.Transaction(project, "Für Unified vorbereiten (" + tags.Count + " Variablen umbenannt)");
                try
                {
                    foreach (HmiTagInfo tag in tags)
                    {
                        if (access.IsCancellationRequested) break;
                        RenameOutcome o = new RenameOutcome { Tag = tag, OldName = tag.Name, NewName = tag.UnifiedName };
                        try
                        {
                            ((IEngineeringObject)sources[tag]).SetAttribute("Name", tag.UnifiedName);
                            o.Success = true;
                            tag.Name = tag.UnifiedName;
                        }
                        catch (Exception ex)
                        {
                            o.Error = ex.Message;
                        }
                        outcomes.Add(o);
                    }
                    if (transaction != null) transaction.CommitOnDispose();
                }
                finally
                {
                    if (transaction != null) transaction.Dispose();
                }
            }
            return outcomes;
        }

        private static ITransactionSupport FindProject(IEngineeringObject obj)
        {
            IEngineeringObject current = obj;
            for (int depth = 0; current != null && depth < 40; depth++)
            {
                ITransactionSupport project = current as ITransactionSupport;
                if (project != null && current.GetType().Name == "Project") return project;
                current = current.Parent as IEngineeringObject;
            }
            return null;
        }
    }
}
