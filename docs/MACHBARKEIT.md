# Machbarkeit – was die Add-In-API in V20/V21 kann

Stand: 16.10.2026 · Zielversionen: **TIA Portal V20 und V21** · Quellgeräte: **WinCC Comfort/Advanced** (vor der Migration mit Data2Unified) und **WinCC Unified** (danach).

Diese Datei trennt klar zwischen **belegt** (Siemens-Doku oder öffentlicher Siemens-Code), **wahrscheinlich** (Fachwissen, noch nicht im eigenen Test bestätigt) und **offen**. Der Machbarkeitstest (Version 0.1) ist so gebaut, dass er die offenen Punkte beantwortet. Er listet für jede Variable **alle** Openness-Attribute mit ihrer Zugriffsart (`Read` / `ReadWrite`) auf.

---

## 1. Rahmen der Add-In-Technik

| Thema | Befund | Status |
|---|---|---|
| Sprache / Laufzeit | C#, .NET Framework 4.8 | belegt |
| Paket | `.addin`-Datei, erzeugt mit `Siemens.Engineering.AddIn.Publisher.exe` aus DLL + Konfigurations-XML | belegt |
| Ablage | `%APPDATA%\Siemens\Automation\Portal Vxx\UserAddIns` (pro Benutzer, ohne Adminrechte); Firmen-Rollout über `C:\ProgramData\…\CorporateAddIns` | belegt (V21-Doku) |
| Aktivierung | einzeln in der Taskkarte **Add-Ins**; dabei werden die angeforderten Rechte angezeigt | belegt |
| Rechte | `TIA.ReadOnly` oder `TIA.ReadWrite`, zusätzlich Systemrechte (z. B. Dateizugriff) | belegt: `TIA.ReadWrite` (Siemens-Code), `TIA.ReadOnly` (eigener Test V20, 17.10.2026). Im Nur-Lesen-Modus verweigert TIA sogar `ExclusiveAccess.Text` – der Schutz greift also wirklich |
| Kontextmenü | `ProjectTreeAddInProvider` → `ContextMenuAddIn` → `AddActionItem<T>(Text, Aktion, Status)`; Status `Enabled` / `Disabled` / `Hidden` | belegt |
| Objektmodell | Im Add-In steht das **volle Openness-Objektmodell** zur Verfügung (dieselben Typen wie bei externen Openness-Programmen) | belegt (Siemens-Beispiele lesen Bausteine, HMI-Variablen und Meldungen) |
| Laufzeitumgebung | Add-Ins laufen in einem **eigenen Prozess** neben TIA; Fenster (WinForms/WPF) sind möglich | belegt |
| Sperre während der Ausführung | `TiaPortal.ExclusiveAccess()` mit Fortschrittstext und Abbruch-Knopf | belegt |

### V20 und V21 sind nicht binärkompatibel

| | V20 | V21 |
|---|---|---|
| Bibliotheken | eine DLL: `PublicAPI\V20.AddIn\Siemens.Engineering.AddIn.dll` | aufgeteilt: `PublicAPI\V21\net48\Siemens.Engineering.AddIn.Base.dll`, `…Base.dll`, `…WinCC.dll` (Classic-HMI), `…WinCCUnified.dll` u. a. |
| Publisher | `PublicAPI\V20.AddIn\Siemens.Engineering.AddIn.Publisher.exe` | `PublicAPI\V21\Siemens.Engineering.AddIn.Publisher.exe` |
| Konfigurations-Namensraum | `…/AddIn/Publisher/V20` | `…/AddIn/Publisher/V21` |
| Sonstiges | – | keine 32-Bit-Rechner mehr; Siemens weist auf Breaking Changes hin |

→ **Ein Quellcode, zwei Builds** (`-TiaVersion 20` / `21`). Siemens selbst pflegt dafür getrennte Zweige.

**Offen (V21):** Die Siemens-Doku nennt die Namensräume `Siemens.Engineering.AddIn.Base(.Menu)`, Siemens' eigener V21-Beispielcode nutzt weiter `Siemens.Engineering.AddIn(.Menu)`. Der Code deckt beide Varianten ab (`V21Namespaces.cs`). Der erste V21-Build zeigt, welche gilt.

---

## 2. Was sich in den HMI-Objekten lesen und ändern lässt

Legende: ✔ belegt · ◐ wahrscheinlich, Test bestätigt es · ✖ nicht möglich bzw. nicht vorgesehen · ? offen

### 2.1 HMI-Variablen

| | Comfort/Advanced (`HmiTarget`) | Unified (`HmiSoftware`) |
|---|---|---|
| Tabellen und Variablen durchlaufen | ✔ `TagFolder` → `TagTables` → `Tags` | ✔ `TagTables` / `Tags` (`HmiTag`) |
| Name, Datentyp, Verbindung, PLC-Variable lesen | ◐ über Attribute; **welche Attribute es gibt, zeigt der Test** | ✔ `Name`, `DataType`, `Connection`, `PlcTag` |
| Erfassungszyklus, Erfassungsart lesen | ◐ Attributnamen voraussichtlich `AcquisitionCycle` / `AcquisitionMode`, Test bestätigt | ✔ `AcquisitionCycle`, `AcquisitionMode` |
| Lesen über XML-Export (Ausweichweg) | ✔ `TagTable.Export(...)` schreibt eine XML-Datei, **ändert das Projekt nicht** | ✔ ebenfalls möglich, aber nicht nötig |
| **Umbenennen** | ? **Kernfrage des Tests:** ist `Name` als `ReadWrite` gemeldet? | ◐ `Name` schreibbar; ob Verweise (Bilder, Skripte, Meldungen) mitgezogen werden, muss ein Test zeigen |
| Erfassungszyklus/-art ändern | ? Zugriffsart laut Test | ◐ schreibbar |
| Datentyp ändern | ? meist an die PLC-Variable gebunden; Ursache liegt dann in der PLC | ◐ schreibbar, sofern nicht von der PLC-Variable vorgegeben |

**Wichtig zu Comfort/Advanced:** Falls sich Namen nicht direkt setzen lassen, bleibt nur der Weg „XML exportieren → bearbeiten → mit Überschreiben importieren“. Ein Import mit geändertem Namen legt aber eine **neue** Variable an und löscht die alte. Verweise in Bildern, Skripten und Meldungen gehen dabei verloren. Diesen Weg setze ich **nicht** ohne eigenen Test und deine Freigabe ein.

### 2.2 Textlisten

| | Comfort/Advanced | Unified |
|---|---|---|
| Lesen | ◐ Textlisten und Einträge über das Objektmodell bzw. ✔ XML-Export | ✔ `TextLists` → `HmiTextList` → Einträge mit mehrsprachigem Text |
| Ändern (Namen, Texte) | ? ggf. nur über XML-Import, gleiches Verweis-Risiko wie oben | ◐ direkt |

### 2.3 HMI-Meldungen

| | Comfort/Advanced | Unified |
|---|---|---|
| Bitmeldungen/Analogmeldungen lesen | ? Openness bietet für Classic-Meldungen nach meinem Kenntnisstand keinen Export; der Test listet die Kompositionen des HMI-Geräts auf (z. B. ob `DiscreteAlarms` vorhanden ist) | ✔ `DiscreteAlarms`, `AnalogAlarms`, `AlarmClasses` |
| Ändern | ? | ◐ direkt |
| Ausweichweg Classic | TIA-eigener Excel-Export der Meldungen → Add-In bzw. Web-Converter liest die Datei (wie bisher) | – |

### 2.4 Prüfpunkte für Data2Unified (Funktion 2)

Alle Prüfungen sind **rein lesend**. Damit erfüllen sie die Vorgabe „bestehende Projekte nur lesen“.

| Prüfung | Datenquelle | Einschätzung |
|---|---|---|
| Sonderzeichen in Namen (`[ ] / # .`, Umlaute, Leerzeichen …) | Namen von Variablen, Tabellen, Textlisten | machbar, sobald Namen lesbar sind (Test) |
| Doppelte bzw. kollidierende Namen | dito, zusätzlich **nach** Anwendung der Bereinigungsregel (`A.B` und `A_B` ergeben beide `A_B`) | machbar |
| Erfassungszyklus 200 ms (Unified min. 250 ms) | `AcquisitionCycle` | machbar, wenn das Attribut lesbar ist; sonst über XML-Export |
| Erfassungsart „Zyklisch fortlaufend“ | `AcquisitionMode` (Wert voraussichtlich `CyclicContinuous`) | dito |
| UInt als Datentyp für Melde-/Störwörter (Ersatz: Word) | Datentyp der Trigger-Variablen von Bitmeldungen | Datentyp: machbar. Zuordnung „ist Meldewort“: Classic-Meldungen evtl. nicht lesbar (2.3), dann Hinweis auf alle UInt-Variablen oder Abgleich mit dem Meldungs-Export |
| String- und Array-Variablen | Datentyp | machbar |
| Sprung zum Objekt | Add-In-API bietet nach meinem Kenntnisstand **kein** „Objekt im Projektbaum markieren/öffnen“ | ? → Ersatz: vollständiger Pfad (Gerät › Ordner › Tabelle › Variable) zum Kopieren |
| Export der Ergebnisliste | CSV/Excel-taugliche Datei | machbar (Test schreibt bereits CSV) |

---

## 3. Bereinigungsregeln des Converters im Projekt anwenden (Funktion 1)

Maßgeblich sind die VBA-Regeln (`docs/ANALYSE_UND_PLAN.md` im Converter). Sie sind für **Excel-Exporte** formuliert. Im Projekt gilt:

| Converter-Funktion | Regel | Übertragbarkeit ins Projekt |
|---|---|---|
| HMI-Variablen | jedes Zeichen außer `[A-Za-z0-9]` → `_` (UTF-16-Einheit, Länge bleibt gleich) | 1:1 auf den Variablennamen anwendbar. Die Kopfzeile aus Excel entfällt. Zahlen-/Datumssonderfälle (`CStr`) entfallen, weil Namen im Projekt immer Text sind. |
| Textlisten · Aktuell | Name der Textliste (Blatt `TextList`, Spalte A) und Spalte B in `TextListEntry`: `#`→`_`, `/`→`_`, `.`→`_`, `[`→`(`, `]`→`)` | Textlisten-Name: 1:1. Spalte B von `TextListEntry` muss ich noch einem Objekt-Attribut zuordnen (dafür brauche ich einen echten Export). |
| Textlisten · Klassisch | arbeitet auf Spalten E–K des Exports (`<no value>`, `<field ref="0" />`, FieldInfo …) | Das sind Export-Darstellungen von Texten mit Feldern. Die Übertragung auf mehrsprachige Texte im Objektmodell ist **offen** und braucht Beispieldaten. |
| HMI-Meldungen | erzeugt eine **neue** Unified-Meldeliste (`DiscreteAlarms`) | Im Projekt hieße das: Meldungen im **Unified**-Gerät anlegen. Machbar (Unified-Openness), aber erst nach der Migration sinnvoll. Für Classic gilt der Ausweichweg aus 2.3. |
| Meldetexte umbrechen | Zeilenumbruch-Logik | später |

**Ablauf beim Ändern (wie vereinbart):** Erst eine Vorschau aller geplanten Änderungen (alt → neu, Kollisionen markiert). Geschrieben wird nur nach ausdrücklicher Bestätigung, mit Hinweis, vorher eine Projektkopie bzw. ein Archiv anzulegen. Das Paket der Prüf-Funktion fordert nur `TIA.ReadOnly` an.

---

## 4. Was der Machbarkeitstest klärt

1. Bauen und Paketieren für V20 und V21 funktioniert (Bibliotheken, Namensräume, Publisher, `TIA.ReadOnly`).
2. Kontextmenü an HMI-Variablentabellen von **Comfort/Advanced** und **Unified**.
3. Welche Attribute eine Classic-Variable über Openness liefert und **welche schreibbar sind** (`Attribute.csv`, Reiter „Attribute“). Das entscheidet über Funktion 1 bei Comfort/Advanced.
4. Welche Kompositionen das HMI-Gerät anbietet (`Struktur.txt`), z. B. `TextLists` oder `DiscreteAlarms`. Daran sehen wir, ob Textlisten und Meldungen bei Classic erreichbar sind.

## 5. Quellen

- Siemens TIA-Portal-Doku V21 (Add-Ins): [Grundlagen](https://docs.tia.siemens.cloud/r/it-it/v21/introduzione-al-tia-portal/ampliamento-delle-funzioni-di-tia-portal-tramite-add-ins/nozioni-di-base-sugli-add-ins), [Kontextmenüs](https://docs.tia.siemens.cloud/r/it-it/v21/introduzione-al-tia-portal/ampliamento-delle-funzioni-di-tia-portal-tramite-add-ins/programmazione-di-add-ins/programmazione-dei-menu-di-scelta-rapida), [View Provider](https://docs.tia.siemens.cloud/r/it-it/v21/introduzione-al-tia-portal/ampliamento-delle-funzioni-di-tia-portal-tramite-add-ins/programmazione-di-add-ins/programmazione-del-view-provider)
- Siemens-Beispiel-Add-In (V20-Zweig und V21): [tia-portal-applications/tia-addin-opc-ua-modelled-interface](https://github.com/tia-portal-applications/tia-addin-opc-ua-modelled-interface) – Projektdatei, Publisher-Aufruf, Konfigurations-XML
- Community-Add-In mit HMI-Zugriff (V18/V19): [caprican/TIA-HMI-Alarms](https://github.com/caprican/TIA-HMI-Alarms) – Status-Callback, Selektion, Unterscheidung `HmiTarget`/`HmiSoftware`
- V21-Bibliotheksaufteilung: [controlbyte.tech – Openness V21](https://controlbyte.tech/blog/tia-portal-mcp-server-ai-automation/) (Blog, nicht Siemens; am eigenen `PublicAPI`-Ordner zu prüfen)
