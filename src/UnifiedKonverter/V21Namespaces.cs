// Für TIA Portal V21 nennt die Siemens-Doku die Namensräume Siemens.Engineering.AddIn.Base(.Menu),
// Siemens' eigene V21-Beispiele verwenden weiterhin Siemens.Engineering.AddIn(.Menu).
// Diese leeren Platzhalter sorgen dafür, dass beide using-Anweisungen kompilieren – die Typen werden
// dann aus dem Namensraum genommen, in dem sie tatsächlich liegen. Entfernen, sobald der erste
// V21-Build bestätigt hat, welcher Namensraum gilt.
#if TIA_V21
namespace Siemens.Engineering.AddIn { internal static class PrhNamespaceAnchor { } }
namespace Siemens.Engineering.AddIn.Menu { internal static class PrhNamespaceAnchor { } }
namespace Siemens.Engineering.AddIn.Base { internal static class PrhNamespaceAnchor { } }
namespace Siemens.Engineering.AddIn.Base.Menu { internal static class PrhNamespaceAnchor { } }
#endif
