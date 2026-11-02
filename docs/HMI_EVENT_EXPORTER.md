# HMI Event Exporter

Teil des Add-Ins (gleiches `.addin`-Paket, eigenes Kontextmenü). Exportiert alle HMI-Variablen eines
**WinCC-Comfort/Advanced**-Geräts einzeln als SimaticML-XML über `Tag.Export(FileInfo, ExportOptions.WithDefaults)` –
inklusive der an den Variablen konfigurierten Ereignisse, soweit TIA sie in den Export schreibt – und packt alles in ein ZIP
für den HMI Event Converter.

**Bedienung:** Rechtsklick auf das HMI-Gerät → **HMI Event Exporter → Variablen inklusive Events exportieren …** →
Fenster zeigt Gerät, Tabellen und Variablen → **Export starten** → Speicherort wählen → Ergebnis und Pfad.
Fortschritt und Abbrechen zeigt der TIA-Dialog „Exklusiver Zugriff“. Das Projekt wird nicht verändert.

## Stand der Phasen

| Phase | Stand |
|---|---|
| 1 · Proof of Concept | **Offen – braucht einen Test in TIA V20.** Der Exporter selbst liefert die XML-Dateien dafür (siehe unten). |
| 2 · Vollständiger Export | umgesetzt (`src/UnifiedKonverter/EventExport/`), mit nachgebautem Gerät getestet |
| 3 · TIA-Add-In | umgesetzt (`EventExporterAddIn.cs`, `Ui/EventExportForm.cs`), Paketierung über `build.ps1` / `Bauen.cmd` |
| 4 · Web-Converter | **noch nicht begonnen** – erst nach bestätigter Phase 1 (Aufbau der Event-Daten im XML muss bekannt sein) |

## Aufbau

| Spezifikation | Umsetzung |
|---|---|
| `ProjectTreeProvider` | `AddInProvider.cs` (liefert Unified-Konverter und HMI Event Exporter) |
| `HmiExportMenu` | `EventExporterAddIn.cs` – Menüpunkt, Auswahl prüfen, Export im TIA-Thread unter „Exklusiver Zugriff“ |
| `HmiDeviceScanner` | `EventExport/HmiDeviceScanner.cs` – genau ein HmiTarget aus Gerät, HMI, Ordner oder Tabelle; Unified und Mehrfachauswahl werden abgelehnt |
| `TagTableScanner` | `EventExport/TagTableScanner.cs` – `TagFolder` → `TagTables` + `Folders` rekursiv, leere Tabellen inklusive |
| `TagXmlExporter` | `EventExport/TagXmlExporter.cs` – `Export(FileInfo, ExportOptions.WithDefaults)` per Reflection (gleicher Code für V20/V21), `FileNamer` |
| `ManifestWriter` | `EventExport/ManifestWriter.cs`, `EventExport/Json.cs` |
| `ZipExporter` / Engine | `EventExport/ExportEngine.cs` – Scan, Export, Manifest, Bericht, ZIP; ohne Oberflächen-Code |
| Modelle | `EventExport/Models.cs` |
| `ExportProgressWindow` | `Ui/EventExportForm.cs` |

## Archiv

```
HMI_Event_Export_<Gerät>_<yyyy-MM-dd_HH-mm>.zip
├── manifest.json
├── Tags/<Tabelle>__<Ordner1_Ordner2>__<Variable>.xml   (ohne Ordner: <Tabelle>__<Variable>.xml)
└── Report/export-report.json
```

- **Dateinamen:** ungültige Zeichen → `_`, reservierte Windows-Namen (`CON`, `NUL` …) mit `_` davor, über 150 Zeichen gekürzt
  mit Prüfsumme (`~1a2b3c4d`), Kollisionen (auch nur in Groß/Klein) → `~2`, `~3` …; die echten Namen stehen im Manifest.
- **manifest.json:** `format` = `hmi-event-export`, `formatVersion` = 1, `tiaPortalVersion`, `exporter`, `exportOptions`,
  `projectName`, `hmiDeviceName` (Name der HMI-Software, z. B. `HMI_RT_1`), `exportedAt`, `complete` (false bei Abbruch),
  `tableCount`, `tagCount`, `successfulExports`, `failedExports`, `files[]` mit `path` (null bei Fehler), `table`, `folder`
  (Ordnerpfad mit `/`), `tagName`, `status` (`success`/`failed`), `error`.
- **export-report.json:** Tabellen mit Ordner und Anzahl, gefundene/erfolgreiche/fehlgeschlagene Exporte, Dauer, Fehlerliste
  (auch nicht lesbare Ordner/Tabellen).
- Ein Fehler bei einer Variable bricht den Export nicht ab. Bei „Abbrechen“ entsteht ein Archiv mit `complete: false`.
- Zusätzlich: Laufprotokoll `Dokumente\Unified-Konverter\Protokolle\EventExport_….log`.

## Phase 1 – so prüfen

1. In einer Projektkopie an einer HMI-Variable mehrere Ereignisse konfigurieren (Wertänderung, Bei Überschreitung,
   Bei Unterschreitung), jeweils mit Funktionen und Parametern (z. B. `ActivateScreen`, `SetBit`, Skript), auch mehrere
   Funktionen in einer Liste.
2. Exporter starten, ZIP öffnen, die XML-Datei dieser Variable ansehen.
3. Prüfen und notieren: Ereignistyp/Trigger, Funktionsname, Parameter (Werte und Variablen), Reihenfolge und Anzahl der
   Funktionsaufrufe, Grenzwerte.
4. Die XML-Datei (oder das ganze ZIP einer Projektkopie ohne vertrauliche Namen) zurückschicken. Erst danach wird
   Phase 4 (Parser und Unified-Skript im Web-Converter) gebaut.

## Risiken

| Thema | Stand |
|---|---|
| Event-Daten im XML | unbestätigt – Phase 1 |
| `Tag.Export` in der Add-In-API | über Reflection aufgerufen; fehlt die Methode, steht „bietet kein Export(…) an“ je Variable im Bericht |
| Große Projekte | Fortschritt im TIA-Dialog, Abbrechen möglich, Fehler je Variable |
| Schemaänderungen | `formatVersion` im Manifest, TIA-Version und Exporter-Version mitgeschrieben |
