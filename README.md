# Unified-Konverter

**TIA-Portal-Add-In** für die Migration von WinCC Comfort/Advanced nach WinCC Unified.

Ziel: per Rechtsklick im Projektbaum

1. **Bereinigen** – HMI-Variablen, Textlisten und HMI-Meldungen Unified-konform machen, direkt im Projekt statt über Excel-Export und -Import. Es gelten exakt die Regeln des [WinCC Unified List Converter](https://paul-rafael.github.io/wincc-unified-list-converter/). Vor jeder Änderung zeigt das Add-In eine Vorschau und wartet auf deine Bestätigung.
2. **Prüfen** – ein Projekt vor der Migration mit Data2Unified durchsehen: Sonderzeichen und Kollisionen in Namen, Erfassungszyklus 200 ms, Erfassungsart „Zyklisch fortlaufend“, UInt in Melde-/Störwörtern, String- und Array-Variablen. Die Prüfung liest nur.

## Stand

| Version | Inhalt |
|---|---|
| **0.1** | Machbarkeitstest: *Unified-Konverter → Variablen auflisten* an einer HMI-Variablentabelle. Liest alle Variablen samt Openness-Attributen und zeigt sie an, Bericht als CSV. Nur lesend. |

Unterstützt: **TIA Portal V20 und V21**, je ein eigener Build.

## Dokumentation

- [docs/MACHBARKEIT.md](docs/MACHBARKEIT.md) – was die Add-In-API für Comfort/Advanced und Unified lesen und ändern kann, mit Einschränkungen
- [docs/BAUEN_UND_TESTEN.md](docs/BAUEN_UND_TESTEN.md) – bauen, installieren und testen, Schritt für Schritt

## Kurzanleitung

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -TiaVersion 20
```

Danach im TIA Portal in der Taskkarte **Add-Ins** „Unified-Konverter“ aktivieren.

## Aufbau

```
src/UnifiedKonverter/
  AddInProvider.cs, KonverterAddIn.cs   Kontextmenü
  Openness/                             generischer, rein lesender Zugriff auf HMI-Objekte
  Report/                               CSV-Bericht
  Ui/                                   Fenster nach PRH Design Kit (Schwarz/Weiß, Dunkelrot)
  Publisher/                            Paket-Konfiguration je TIA-Version
build.ps1                               Bauen + Paketieren
```

---

<sub>Paul-Rafael Hasan-Man</sub>
