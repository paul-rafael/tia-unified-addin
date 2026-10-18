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
| **0.3** | Schritt 3: Ziel-Unified-HMI wählen, Variablen, Textlisten und Bitmeldungen übertragen (Meldungen auch aus dem TIA-Excel-Export), Vorschau, Ergebnisliste. [Vorschau](docs/vorschau/schritt3.html) |

Unterstützt: **TIA Portal V20 und V21**, je ein eigener Build.

## Dokumentation

- [docs/PLAN.md](docs/PLAN.md) – Ablauf Schritt 1–3, Stand und nächste Schritte
- [docs/MACHBARKEIT.md](docs/MACHBARKEIT.md) – was die Add-In-API für Comfort/Advanced und Unified lesen und ändern kann, mit Einschränkungen
- [docs/BAUEN_UND_TESTEN.md](docs/BAUEN_UND_TESTEN.md) – bauen, installieren und testen, Schritt für Schritt

## Kurzanleitung

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -TiaVersion 20
```

Danach im TIA Portal in der Taskkarte **Add-Ins** „Unified-Konverter“ aktivieren, Rechtsklick auf die alte HMI → **Unified-Konverter → Für Unified vorbereiten** bzw. **Zu Unified übertragen**.

## Aufbau

```
src/UnifiedKonverter/
  AddInProvider.cs, KonverterAddIn.cs   Kontextmenü
  Openness/                             Lesen der HMI-Objekte, Umbenennen in einer Transaktion
  Rules/                                Converter-Regeln (Variablen, Textlisten, Meldungen) und Prüfungen
  Transfer/                             Schritt 3: Quelle lesen, Plan, Übertragen in die Unified-HMI
  Excel/                                Lesen des TIA-Excel-Exports der Meldungen
  Report/                               CSV-Bericht
  Ui/                                   Fenster nach PRH Design Kit (Schwarz/Weiß, Dunkelrot)
  Publisher/                            Paket-Konfiguration je TIA-Version
build.ps1                               Bauen + Paketieren
```

---

<sub>Paul-Rafael Hasan-Man</sub>
