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
- **Ob Comfort/Advanced das Umbenennen per Openness zulässt**, zeigt der erste Test („Umbenennen übernehmen“ an einer Projektkopie). Falls nicht, steht pro Variable der Fehler im Fenster. Dann brauchen wir einen anderen Weg (siehe [MACHBARKEIT.md](MACHBARKEIT.md)).
- Ob TIA beim Umbenennen die Verweise in Bildern, Skripten und Meldungen mitzieht – im Test an einem Bild mit einer umbenannten Variable prüfen.
- Erfassungszyklus/-art automatisch korrigieren (statt nur melden) – sobald `Attribute.csv` zeigt, wie die Werte bei Classic aussehen und ob sie schreibbar sind.
- Textlisten (Regel *Textlisten · Aktuell* ist schon im Code: `ConverterRules.CleanTextListName`) und Meldungen in Schritt 1 aufnehmen.

## Schritt 3 – Plan

Ziel: alte HMI → ausgewählte Unified-HMI, ohne Excel-Umweg.

| Objekt | Lesen (alte HMI) | Schreiben (Unified) | Regel |
|---|---|---|---|
| HMI-Variablen | ✔ (Schritt 1 liest schon alles) | Unified-Openness: Tabellen und Variablen anlegen (`TagTables.Create`, `Tags.Create`), Verbindung, PLC-Variable, Zyklus setzen | Converter *HMI-Variablen* |
| Textlisten | ◐ prüfen (`Struktur.txt` zeigt, ob `TextLists` angeboten wird) | `TextLists.Create`, Einträge mit mehrsprachigem Text | Converter *Textlisten · Klassisch / Aktuell* |
| Bitmeldungen | ? bei Classic evtl. nicht per Openness lesbar → sonst TIA-Excel-Export einlesen | `DiscreteAlarms.Create` mit Trigger-Variable/-Bit, Klasse, Text, Parametern | Converter *HMI-Meldungen* (FieldInfo → `<ref id …>`), *Meldetexte umbrechen* |
| Analogmeldungen, Meldeklassen, Verbindungen | später | später | – |

Vorgehen:
1. Auswahldialog „Ziel-Unified-HMI“ (listet alle Unified-Geräte im Projekt).
2. Vorschau wie in Schritt 1: was wird angelegt, was existiert schon (überspringen oder überschreiben?), Konflikte.
3. Übertragen in einer Transaktion, Ergebnisliste mit Fehlern.

Dafür brauche ich aus dem Test von Schritt 1: `Struktur.txt` eines Comfort-Panels **und** eines Unified-Panels. Darin steht, welche Objekte Openness auf beiden Seiten anbietet.
