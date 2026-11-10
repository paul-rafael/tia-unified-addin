# Unified-Konverter

**TIA-Portal-Add-In** für die Migration von WinCC Comfort/Advanced nach WinCC Unified.

Ablauf einer Migration:

1. **Für Unified vorbereiten** (dieses Add-In): Rechtsklick auf die alte HMI → prüfen, Vorschau, bereinigen nach Bestätigung. Es gelten exakt die Regeln des [WinCC Unified List Converter](https://paul-rafael.github.io/wincc-unified-list-converter/).
2. **Migration mit Data2Unified** (nicht Teil dieses Projekts).
3. **Zu Unified übertragen** (dieses Add-In): Rechtsklick auf die alte HMI → Ziel-Unified-HMI wählen → Variablen, Meldungen, Textlisten automatisch übertragen.

## Stand

| Version | Inhalt |
|---|---|
| 0.1 | Machbarkeitstest: Variablen einer Tabelle auflisten (nur lesend) |
| 0.2 | Schritt 1 für HMI-Variablen: alle Tabellen eines Geräts, Prüfung vor Data2Unified, Umbenennen nach Bestätigung, Bericht. [Vorschau](docs/vorschau/schritt1.html) |
| 0.3 | Schritt 3: Ziel-Unified-HMI wählen, Variablen, Textlisten und Bitmeldungen übertragen (Meldungen auch aus dem TIA-Excel-Export), Vorschau, Ergebnisliste. [Vorschau](docs/vorschau/schritt3.html) |
| 0.4 | Schritt 1 exportiert die bereinigten Namen als Excel-Liste (`HMITagList_MIGUNI.xlsx`, Blatt „Hmi Tags“), weil sich Comfort/Advanced-Variablen per Openness nicht umbenennen lassen. Schritt 3 schreibt ein Laufprotokoll (Dokumente\Unified-Konverter\Protokolle). |
| 0.5 | Schritt 1: Textlisten direkt aus dem Projekt als `TextLists_MIGUNI.xlsx` (Regel „Textlisten“), HMI-Meldungen aus dem TIA-Export als `Unified_HMI-Alarms.xlsx` (Regel „HMI-Meldungen“, zellgleich mit dem Web-Converter 1.2.0). Schritt 3: alle markierten alten HMIs mit allen Tabellen, doppelte Namen werden übersprungen. |
| **0.6** | **HMI Event Exporter:** eigenes Kontextmenü am HMI-Gerät, exportiert alle Variablen einzeln als SimaticML-XML (inkl. Ereignisse) mit Manifest und Bericht in ein ZIP für den HMI Event Converter. [Beschreibung](docs/HMI_EVENT_EXPORTER.md) |

Unterstützt: **TIA Portal V20 und V21**, je ein eigener Build.

## Dokumentation

- [docs/PLAN.md](docs/PLAN.md) – Ablauf Schritt 1–3, Stand und nächste Schritte
- [docs/MACHBARKEIT.md](docs/MACHBARKEIT.md) – was die Add-In-API für Comfort/Advanced und Unified lesen und ändern kann, mit Einschränkungen
- [docs/BAUEN_UND_TESTEN.md](docs/BAUEN_UND_TESTEN.md) – bauen, installieren und testen, Schritt für Schritt
- [docs/HMI_EVENT_EXPORTER.md](docs/HMI_EVENT_EXPORTER.md) – HMI Event Exporter: Aufbau, ZIP-Format, Stand der Phasen, Prüfung der Event-Daten
- Projektstand, Entscheidungen und Log außerhalb des Repos: Obsidian-Vault, `Projekte/TIA-Unified-AddIn/Übersicht.md` und `Log.md`

## Kurzanleitung

Doppelklick auf **`Bauen.cmd`** → TIA-Version wählen, TIA-Ordner prüfen oder über *Durchsuchen …* wählen → **Bauen**. Das fertige Paket liegt danach in `Ausgabe\`; von dort nach `%APPDATA%\Siemens\Automation\Portal V20\UserAddIns\` kopieren (oder im Fenster „direkt in TIA installieren“ anhaken).

Ohne Fenster:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -TiaVersion 20
```

Findet das Skript TIA nicht, fragt es nach dem Ordner und merkt ihn sich.

Danach im TIA Portal in der Taskkarte **Add-Ins** „Unified-Konverter“ aktivieren, Rechtsklick auf die alte HMI → **Unified-Konverter → Für Unified vorbereiten** bzw. **Zu Unified übertragen**.

## Aufbau

```
src/UnifiedKonverter/
  AddInProvider.cs, KonverterAddIn.cs   Kontextmenü
  Openness/                             Lesen der HMI-Objekte, Schreiben in die Unified-HMI
  Rules/                                Converter-Regeln (Variablen, Textlisten, Meldungen) und Prüfungen
  Transfer/                             Schritt 3: Quelle lesen, Plan, Übertragen in die Unified-HMI
  EventExport/                          HMI Event Exporter: Variablen einzeln als XML, Manifest, Bericht, ZIP
  Excel/                                Excel lesen und schreiben (TIA-Exporte, Listen für Unified)
  Report/                               CSV-Bericht, Excel-Variablenliste, Laufprotokoll
  Ui/                                   Fenster nach PRH Design Kit (Schwarz/Weiß, neutraler Akzent)
  Publisher/                            Paket-Konfiguration je TIA-Version
build.ps1                               Bauen + Paketieren
Bauen.cmd, tools/BauenFenster.ps1       Fenster zum Bauen (Doppelklick)
tools/TiaPfad.ps1                       TIA-Ordner finden, prüfen, merken
```

---

<sub>Paul-Rafael Hasan-Man</sub>
