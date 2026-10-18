# Bauen, installieren, testen (Windows)

Gilt für Version 0.2 (Schritt 1 „Für Unified vorbereiten“). Dauer: etwa 20 Minuten beim ersten Mal.

## 01 · Voraussetzungen prüfen

- TIA Portal **V20** und/oder **V21** mit installiertem **TIA Portal Openness** (bei der TIA-Installation auswählbar)
- Windows-Benutzer in der Gruppe **Siemens TIA Openness** (nach dem Hinzufügen einmal ab- und wieder anmelden)
- Ein Build-Werkzeug, eins von beiden reicht:
  - **.NET SDK 8** (am schnellsten): `winget install Microsoft.DotNet.SDK.8`, danach PowerShell neu öffnen. Der erste Build lädt einmalig ein NuGet-Paket aus dem Internet.
  - **Visual Studio 2022 Community** mit der Workload **„.NET-Desktopentwicklung“**

## 02 · Projekt auf den Windows-Rechner holen

Mit Git: `git clone https://github.com/paul-rafael/tia-unified-addin.git C:\Projekte\tia-unified-addin`

Ohne Git: auf [github.com/paul-rafael/tia-unified-addin](https://github.com/paul-rafael/tia-unified-addin) **Code → Download ZIP**, entpacken z. B. nach `C:\Projekte\tia-unified-addin`. Danach Rechtsklick auf `build.ps1` → Eigenschaften → „Zulassen“ anhaken, falls angeboten.

## 03 · Bauen

**Variante A – Skript (empfohlen).** Startmenü → „Developer PowerShell for VS 2022“:

```powershell
cd C:\Projekte\tia-unified-addin
powershell -ExecutionPolicy Bypass -File .\build.ps1 -TiaVersion 20
```

Für V21 entsprechend `-TiaVersion 21`. Weitere Schalter:
- `-NoInstall`: Paket nur bauen, nicht nach `UserAddIns` kopieren
- `-TiaPortalDir "D:\…\Portal V20\"`: wenn TIA nicht unter `C:\Program Files\Siemens\Automation\` liegt (normalerweise wird der Pfad aus der Registry gelesen)

**Variante B – Visual Studio.** `src\UnifiedKonverter\UnifiedKonverter.csproj` öffnen, Konfiguration **Release**, „Erstellen“. Ohne weitere Angabe baut Visual Studio für **V20**. Für V21 in der `.csproj` die Zeile `<TiaVersion …>20</TiaVersion>` auf `21` ändern.

**Ergebnis:**
- `src\UnifiedKonverter\bin\V20\Release\Unified-Konverter_V20.addin`
- automatisch kopiert nach `%APPDATA%\Siemens\Automation\Portal V20\UserAddIns\`
- Protokoll des Paketierens: `…\bin\V20\Release\Publisher.log`

## 04 · Im TIA Portal aktivieren

1. Eine **Kopie** eines Projekts öffnen. Das Add-In ändert nichts (nur Leserecht), sicher ist sicher.
2. Rechts die Taskkarte **Add-Ins** öffnen. Dort steht „Unified-Konverter“.
3. Add-In auswählen → **Aktivieren**. TIA zeigt die angeforderten Rechte: *TIA lesen und schreiben* (fürs Umbenennen; ohne deine Bestätigung im Fenster wird nichts geändert) und *uneingeschränkter Zugriff* (nur zum Speichern des Berichts unter „Dokumente“). Bestätigen.

Nach jedem neuen Build: TIA Portal neu starten oder das Add-In in der Taskkarte deaktivieren und wieder aktivieren.

## 05 · Testen

**Mit einer Projektkopie arbeiten** – ab Version 0.2 kann das Add-In nach Bestätigung umbenennen.

1. Im Projektbaum mit der rechten Maustaste auf das **alte HMI-Gerät** (Comfort/Advanced) klicken (Mac-Touchpad: Gerät anklicken, dann **Shift + F10**).
2. **Unified-Konverter → Für Unified vorbereiten …**
3. Links die Variablentabellen anklicken, oben die Filter *Mit Hinweisen* und *Umbenennen* ausprobieren.
4. **Bericht speichern** → der Ordner öffnet sich.
5. **Umbenennen übernehmen** → Rückfrage lesen → *Jetzt umbenennen*. Danach:
   - Sind die Variablen im TIA-Variableneditor umbenannt?
   - Ist ein Bild, das eine umbenannte Variable verwendet, noch richtig verknüpft?
   - Nimmt **Bearbeiten → Rückgängig** alles in einem Schritt zurück?
6. Gegenprobe: Rechtsklick auf eine SPS → „Für Unified vorbereiten“ meldet „Keine HMI-Variablen gefunden“.

## 06 · Was ich von dir zurück brauche

Bitte schick mir (hier im Chat):

- [ ] Hat der Build geklappt? Falls nicht: die **komplette** Fehlerausgabe und `Publisher.log`
- [ ] Erscheint das Add-In in der Taskkarte, und ließ es sich aktivieren?
- [ ] Je Gerätetyp (Comfort/Advanced, wenn möglich auch Unified) aus dem Berichtsordner: **`Attribute.csv`** und **`Struktur.txt`** (`Pruefung.csv` nur, wenn keine vertraulichen Namen drin sind)
- [ ] Ergebnis von „Umbenennen übernehmen“: Meldung im Fenster, Verweise in Bildern, Rückgängig
- [ ] Einen **Screenshot** des Fensters, damit ich das Design mit der [Vorschau](vorschau/schritt1.html) vergleichen kann
- [ ] Wie lange das Lesen einer großen Tabelle gedauert hat (grob)

## Wenn etwas nicht klappt

| Meldung | Ursache / Abhilfe |
|---|---|
| `TIA-Portal-API nicht gefunden: …` | TIA liegt woanders → `-TiaPortalDir` angeben |
| `CS0246: Typ oder Namespace … nicht gefunden` (nur V21) | Namensräume in V21 anders als erwartet → Fehlerausgabe schicken. Zusätzlich hilft eine Liste der DLLs: `dir "C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48"` |
| `CS0012: … in einer nicht referenzierten Assembly definiert` | eine weitere Siemens-DLL fehlt in der `.csproj` → Meldung schicken, ich ergänze sie |
| Add-In fehlt in der Taskkarte | liegt die `.addin`-Datei in `%APPDATA%\Siemens\Automation\Portal V20\UserAddIns`? TIA neu starten |
| Fehler beim Ausführen | TIA zeigt ihn im Inspektorfenster unter *Info*. Das Add-In zeigt zusätzlich ein eigenes Fehlerfenster mit Details zum Kopieren. |
