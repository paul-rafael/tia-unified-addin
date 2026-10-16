# Bauen, installieren, testen (Windows)

Gilt für Version 0.1 (Machbarkeitstest). Dauer: etwa 20 Minuten beim ersten Mal.

## 01 · Voraussetzungen prüfen

- TIA Portal **V20** und/oder **V21** mit installiertem **TIA Portal Openness** (bei der TIA-Installation auswählbar)
- Windows-Benutzer in der Gruppe **Siemens TIA Openness** (nach dem Hinzufügen einmal ab- und wieder anmelden)
- **Visual Studio 2022 Community** mit der Workload **„.NET-Desktopentwicklung“**. Im Visual Studio Installer unter „Einzelne Komponenten“ prüfen, ob **„.NET Framework 4.8 – Zielpaket“** angehakt ist.

## 02 · Projekt auf den Windows-Rechner holen

Der Ordner liegt noch nicht auf GitHub. Kopiere den Ordner `tia-unified-addin` per USB oder OneDrive, z. B. nach `C:\Projekte\tia-unified-addin`.

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
3. Add-In auswählen → **Aktivieren**. TIA zeigt die angeforderten Rechte: *TIA nur lesen* und *uneingeschränkter Zugriff* (dieser wird nur zum Speichern des Berichts unter „Dokumente“ gebraucht). Bestätigen.

Nach jedem neuen Build: TIA Portal neu starten oder das Add-In in der Taskkarte deaktivieren und wieder aktivieren.

## 05 · Testen

1. Im Projektbaum ein **Comfort/Advanced-Panel** → *HMI-Variablen* → eine Variablentabelle mit der rechten Maustaste anklicken.
2. **Unified-Konverter → Variablen auflisten**.
3. Es öffnet sich das Fenster mit den Reitern *Variablen* und *Attribute*.
4. **Bericht speichern** → der Bericht landet in `Dokumente\Unified-Konverter\Berichte\<Datum_Uhrzeit>\`.
5. Dasselbe mit einer Variablentabelle eines **Unified-Panels**, falls im Projekt vorhanden.
6. Gegenprobe: Rechtsklick auf eine **PLC-Variablentabelle**. Der Eintrag muss dort ausgegraut sein.

## 06 · Was ich von dir zurück brauche

Bitte schick mir (hier im Chat):

- [ ] Hat der Build geklappt? Falls nicht: die **komplette** Fehlerausgabe und `Publisher.log`
- [ ] Erscheint das Add-In in der Taskkarte, und ließ es sich aktivieren?
- [ ] Je Gerätetyp (Comfort/Advanced, Unified) den Berichtsordner: **`Attribute.csv`** und **`Struktur.txt`** (die `Variablen.csv` nur, wenn keine vertraulichen Namen drin sind)
- [ ] Einen **Screenshot** des Fensters (beide Reiter), damit ich das Design prüfen kann
- [ ] Wie lange das Lesen einer großen Tabelle gedauert hat (grob)

## Wenn etwas nicht klappt

| Meldung | Ursache / Abhilfe |
|---|---|
| `TIA-Portal-API nicht gefunden: …` | TIA liegt woanders → `-TiaPortalDir` angeben |
| `CS0246: Typ oder Namespace … nicht gefunden` (nur V21) | Namensräume in V21 anders als erwartet → Fehlerausgabe schicken. Zusätzlich hilft eine Liste der DLLs: `dir "C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48"` |
| `CS0012: … in einer nicht referenzierten Assembly definiert` | eine weitere Siemens-DLL fehlt in der `.csproj` → Meldung schicken, ich ergänze sie |
| Publisher meldet Schemafehler zu `TIA.ReadOnly` | dann ersetze ich es durch `TIA.ReadWrite` (das Add-In schreibt trotzdem nichts) |
| Add-In fehlt in der Taskkarte | liegt die `.addin`-Datei in `%APPDATA%\Siemens\Automation\Portal V20\UserAddIns`? TIA neu starten |
| Fehler beim Ausführen | TIA zeigt ihn im Inspektorfenster unter *Info*. Das Add-In zeigt zusätzlich ein eigenes Fehlerfenster mit Details zum Kopieren. |
