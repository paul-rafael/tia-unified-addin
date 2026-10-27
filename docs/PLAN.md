# Ablauf und Plan

Stand: 18.10.2026

## Der Ablauf einer Migration

| Schritt | Wer | Was |
|---|---|---|
| **1 · Für Unified vorbereiten** | dieses Add-In | Rechtsklick auf die **alte HMI** (Comfort/Advanced) → *Unified-Konverter → Für Unified vorbereiten*. Prüft alles, was Data2Unified stört, zeigt eine Vorschau und bereinigt das Projekt erst nach Bestätigung. |
| **2 · Migration** | Data2Unified (nicht von uns) | Export und Migration laufen ohne Fehler, weil Schritt 1 die Ursachen vorher beseitigt hat. |
| **3 · Zu Unified übertragen** | dieses Add-In | Rechtsklick auf die **alte HMI** → *Zu Unified übertragen* → **Ziel-Unified-HMI auswählen** → Variablen, Meldungen, Textlisten usw. werden automatisch in die neue Unified-HMI übertragen, ähnlich wie Data2Unified. |

## Schritt 1 – Stand Version 0.2

Umgesetzt:
- Einstieg über das **HMI-Gerät**, den Ordner „HMI-Variablen“ oder einzelne Tabellen. Das Add-In sammelt alle Variablentabellen des Geräts, auch in Unterordnern.
- Fenster: links die **Variablentabellen zum Anklicken**, rechts Kennzahlen, Filter (*Alle / Mit Hinweisen / Umbenennen*), Suche und die Variablenliste mit *Name in Unified* und Hinweisen. Vorschau: [vorschau/schritt1.html](vorschau/schritt1.html)
- Prüfungen: Sonderzeichen (Converter-Regel), **Kollisionen nach der Bereinigung** (z. B. `A.B` und `A_B`), Erfassungszyklus unter 250 ms, Erfassungsart „Zyklisch fortlaufend“, UInt, String- und Array-Variablen.
- **Umbenennen übernehmen**: nur nach Rückfrage, in **einer Transaktion** (in TIA mit einem „Rückgängig“ zurücknehmbar). Variablen mit Kollision werden ausgelassen. Schlägt eine Variable fehl, steht der Grund in einer Liste.
- **Bericht speichern**: `Pruefung.csv`, `Attribute.csv`, `Struktur.txt`.

Offen bzw. als Nächstes:
- **Ergebnis Test V20 (0.3.1): Comfort/Advanced lässt das Umbenennen per Openness nicht zu** (`set_Name` nicht unterstützt). Weg dafür offen, siehe [MACHBARKEIT.md](MACHBARKEIT.md). Ursprüngliche Frage: Ob Comfort/Advanced das Umbenennen per Openness zulässt, zeigt der erste Test („Umbenennen übernehmen“ an einer Projektkopie). Falls nicht, steht pro Variable der Fehler im Fenster. Dann brauchen wir einen anderen Weg (siehe [MACHBARKEIT.md](MACHBARKEIT.md)).
- Ob TIA beim Umbenennen die Verweise in Bildern, Skripten und Meldungen mitzieht – im Test an einem Bild mit einer umbenannten Variable prüfen.
- Erfassungszyklus/-art automatisch korrigieren (statt nur melden) – sobald `Attribute.csv` zeigt, wie die Werte bei Classic aussehen und ob sie schreibbar sind.
- Textlisten (Regel *Textlisten · Aktuell* ist schon im Code: `ConverterRules.CleanTextListName`) und Meldungen in Schritt 1 aufnehmen.

## Schritt 3 – Stand Version 0.3

Rechtsklick auf die **alte HMI** → *Unified-Konverter → Zu Unified übertragen …* Vorschau: [vorschau/schritt3.html](vorschau/schritt3.html)

Umgesetzt:
- **Ziel-Unified-HMI auswählen** (alle Unified-HMIs im Projekt, auch in Gerätegruppen).
- **Umfang** per Karte: HMI-Variablen, Textlisten, Bitmeldungen; Option „Vorhandene Objekte aktualisieren“ (sonst bleiben z. B. von Data2Unified angelegte Objekte unverändert).
- **Vorschau** je Objekt: wird angelegt / vorhanden / gesperrt (Kollision). Erst nach Rückfrage wird geschrieben, alles in **einer Transaktion** (ein „Rückgängig“).
- **Ergebnisliste** je Objekt (angelegt, aktualisiert, übersprungen, Fehler mit Grund), speicherbar als CSV.

| Objekt | Quelle (alte HMI) | Ziel (Unified) | Regel |
|---|---|---|---|
| HMI-Variablen | Openness (wie Schritt 1) | Tabelle gleichen Namens (wird angelegt), Variable mit Verbindung, PLC-Variable, Datentyp, Länge, Zyklus (≥ 250 ms, z. B. „200 ms“ → `T250ms`), Erfassungsart („zyklisch fortlaufend“ → `CyclicInOperation`) | Converter *HMI-Variablen*; Kollisionen werden nicht übertragen |
| Textlisten | Openness, falls angeboten | Textliste, Einträge mit allen gleichnamigen Attributen und allen Sprachen | Converter *Textlisten · Aktuell* für den Namen |
| Bitmeldungen | Openness, falls angeboten – sonst **TIA-Excel-Export** der Meldungen wählen | Bitmeldung mit ID, Klasse, Trigger-Variable (umbenannt wie die Variablen), Trigger-Bit, Meldetext, Parameter 1 | Converter *HMI-Meldungen* (FieldInfo/Parameter nach VBA-Regel) |

Bewusst anders als die Excel-Datei des Converters: Der Bereich „HMI_RT_2::Alarming“ wird nicht gesetzt (er gehörte zu einem bestimmten Gerät); die Unified-HMI vergibt ihn selbst.

## Was nur der Test unter Windows klären kann

Der Code ist auf dem Mac **gegen Platzhalter** der Siemens-Bibliotheken kompiliert (V20 und V21, ohne Fehler) und die Regeln sind gegen die Testfälle des Converters geprüft. Nicht geprüft werden konnte, ob die echte Openness-API die verwendeten **Namen** anbietet. Deshalb ist alles so gebaut, dass Abweichungen nicht abstürzen, sondern pro Objekt als Hinweis erscheinen:

- Attribut- und Kompositionsnamen bei Unified (`TagTables`, `Tags`, `TextLists`, `DiscreteAlarms`, `RaisedStateTag`, `TriggerBitAddress`, `EventText` …) und bei Classic (`TextLists`, Meldungen).
- Werteformate: Unified-Zyklusnamen (`T250ms`, `T1s` …), Erfassungsart (`CyclicInOperation`), Datentypen und Verbindungsnamen nach Data2Unified.
- Meldetext-Parameter: Der Text wird mit `<field ref … />` übernommen; ob Unified daraus den Parameter erkennt, zeigt der Test.

Jeder Hinweis in der Ergebnisliste nennt Attribut, Wert und Fehlermeldung von TIA, damit ich gezielt nachbessern kann.
