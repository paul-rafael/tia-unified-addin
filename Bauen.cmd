@echo off
rem Doppelklick: Fenster zum Bauen des Add-Ins oeffnen
start "" powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File "%~dp0tools\BauenFenster.ps1"
