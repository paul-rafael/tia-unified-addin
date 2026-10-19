<#
  Fenster zum Bauen des Add-Ins: TIA-Version und TIA-Ordner wählen, bauen, Paket aus 'Ausgabe' holen.
  Start per Doppelklick auf Bauen.cmd. Gebaut wird mit build.ps1 in einem eigenen Prozess.
#>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
. (Join-Path $PSScriptRoot 'TiaPfad.ps1')

$root = Split-Path $PSScriptRoot -Parent
$buildScript = Join-Path $root 'build.ps1'
$ausgabe = Join-Path $root 'Ausgabe'

# Farben nach PRH Design Kit wie im Add-In (Ui/Theme.cs), Hell/Dunkel nach Windows-Einstellung
$dark = $false
try { $dark = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize' -ErrorAction Stop).AppsUseLightTheme -eq 0 } catch { }
function C([string] $hex) { [System.Drawing.ColorTranslator]::FromHtml($hex) }
if ($dark) {
  $bg = C '#0a0a0a'; $surface = C '#161616'; $border = C '#343434'; $text = C '#f0f0f0'; $text2 = C '#b5b5b5'
  $red = C '#9a0e1b'; $redHover = C '#b31220'; $redText = C '#c7232f'; $ok = C '#f0f0f0'
} else {
  $bg = C '#ffffff'; $surface = C '#f8f8f8'; $border = C '#d2d2d2'; $text = C '#121212'; $text2 = C '#4b4b4b'
  $red = C '#8a0b18'; $redHover = C '#6d0812'; $redText = C '#8a0b18'; $ok = C '#121212'
}
$fBody = New-Object System.Drawing.Font('Segoe UI', 9.75)
$fStrong = New-Object System.Drawing.Font('Segoe UI Semibold', 9.75)
$fTitle = New-Object System.Drawing.Font('Segoe UI Semibold', 16)
$fEyebrow = New-Object System.Drawing.Font('Segoe UI Semibold', 7.5)
$fMono = New-Object System.Drawing.Font('Consolas', 9.5)

function New-Label([string] $t, $font, $color, [int] $x, [int] $y) {
  $l = New-Object System.Windows.Forms.Label
  $l.Text = $t; $l.Font = $font; $l.ForeColor = $color; $l.AutoSize = $true
  $l.Location = New-Object System.Drawing.Point($x, $y)
  return $l
}
function New-Button([string] $t, [bool] $primary, [int] $x, [int] $y, [int] $w) {
  $b = New-Object System.Windows.Forms.Button
  $b.Text = $t; $b.Font = $fStrong; $b.FlatStyle = 'Flat'; $b.Cursor = 'Hand'
  $b.Size = New-Object System.Drawing.Size($w, 34); $b.Location = New-Object System.Drawing.Point($x, $y)
  if ($primary) {
    $b.BackColor = $red; $b.ForeColor = [System.Drawing.Color]::White
    $b.FlatAppearance.BorderSize = 0; $b.FlatAppearance.MouseOverBackColor = $redHover
  } else {
    $b.BackColor = $bg; $b.ForeColor = $text
    $b.FlatAppearance.BorderColor = $border; $b.FlatAppearance.MouseOverBackColor = $surface
  }
  return $b
}

$form = New-Object System.Windows.Forms.Form
$form.Text = 'Unified-Konverter bauen'
$form.Font = $fBody; $form.BackColor = $bg; $form.ForeColor = $text
$form.ClientSize = New-Object System.Drawing.Size(760, 600)
$form.MinimumSize = New-Object System.Drawing.Size(640, 520)
$form.StartPosition = 'CenterScreen'

$form.Controls.Add((New-Label 'UNIFIED-KONVERTER' $fEyebrow $redText 24 20))
$form.Controls.Add((New-Label 'Add-In bauen' $fTitle $text 22 36))

$form.Controls.Add((New-Label 'TIA-Version' $fStrong $text 24 88))
$rbV20 = New-Object System.Windows.Forms.RadioButton
$rbV20.Text = 'V20'; $rbV20.AutoSize = $true; $rbV20.Checked = $true
$rbV20.Location = New-Object System.Drawing.Point(26, 112)
$rbV21 = New-Object System.Windows.Forms.RadioButton
$rbV21.Text = 'V21'; $rbV21.AutoSize = $true
$rbV21.Location = New-Object System.Drawing.Point(96, 112)
$form.Controls.AddRange(@($rbV20, $rbV21))

$form.Controls.Add((New-Label 'TIA-Portal-Ordner (z. B. E:\Program Files\Siemens\Automation\Portal V20)' $fStrong $text 24 150))
$tbDir = New-Object System.Windows.Forms.TextBox
$tbDir.Location = New-Object System.Drawing.Point(24, 176)
$tbDir.Size = New-Object System.Drawing.Size(560, 26)
$tbDir.Anchor = 'Top, Left, Right'
$tbDir.BackColor = $surface; $tbDir.ForeColor = $text; $tbDir.BorderStyle = 'FixedSingle'
$btnBrowse = New-Button 'Durchsuchen …' $false 596 173 140
$btnBrowse.Anchor = 'Top, Right'
$form.Controls.AddRange(@($tbDir, $btnBrowse))

$lblStatus = New-Label '' $fBody $text2 24 210
$form.Controls.Add($lblStatus)

$cbInstall = New-Object System.Windows.Forms.CheckBox
$cbInstall.Text = 'Nach dem Bauen direkt in TIA installieren (UserAddIns)'
$cbInstall.AutoSize = $true; $cbInstall.Location = New-Object System.Drawing.Point(26, 242)
$form.Controls.Add($cbInstall)

$btnBuild = New-Button 'Bauen' $true 24 282 140
$btnOpen = New-Button 'Ausgabe-Ordner öffnen' $false 176 282 200
$form.Controls.AddRange(@($btnBuild, $btnOpen))

$tbLog = New-Object System.Windows.Forms.TextBox
$tbLog.Multiline = $true; $tbLog.ReadOnly = $true; $tbLog.ScrollBars = 'Both'; $tbLog.WordWrap = $false
$tbLog.Font = $fMono; $tbLog.BackColor = $surface; $tbLog.ForeColor = $text; $tbLog.BorderStyle = 'FixedSingle'
$tbLog.Location = New-Object System.Drawing.Point(24, 334)
$tbLog.Size = New-Object System.Drawing.Size(712, 242)
$tbLog.Anchor = 'Top, Bottom, Left, Right'
$form.Controls.Add($tbLog)

function Get-Version { if ($rbV21.Checked) { '21' } else { '20' } }

# Prüft den eingetragenen Ordner; bei Erfolg steht danach der echte TIA-Ordner im Feld
function Update-Status {
  $v = Get-Version
  if (-not $tbDir.Text.Trim()) {
    $lblStatus.ForeColor = $redText
    $lblStatus.Text = "TIA Portal V$v nicht automatisch gefunden. Ordner eintragen oder 'Durchsuchen …'."
    return $null
  }
  $dir = Resolve-TiaDir $tbDir.Text $v
  if ($dir) {
    if ($tbDir.Text -ne $dir) { $tbDir.Text = $dir }
    $lblStatus.ForeColor = $ok
    $lblStatus.Text = "✓ TIA-Portal-V$v-API gefunden."
  } else {
    $lblStatus.ForeColor = $redText
    $lblStatus.Text = "✗ Dort liegt keine TIA-Portal-V$v-API ($(Get-TiaApiMarker $v)). Ist TIA Portal Openness installiert?"
  }
  return $dir
}

function Set-VersionDefaults {
  $found = Find-TiaDir (Get-Version)
  $tbDir.Text = if ($found) { $found } else { '' }
  Update-Status | Out-Null
}

$rbV20.Add_CheckedChanged({ if ($rbV20.Checked) { Set-VersionDefaults } })
$rbV21.Add_CheckedChanged({ if ($rbV21.Checked) { Set-VersionDefaults } })
$tbDir.Add_Leave({ Update-Status | Out-Null })
$btnBrowse.Add_Click({
  $start = $tbDir.Text.Trim()
  $gewaehlt = Select-TiaDirDialog (Get-Version) $start
  if ($gewaehlt) { $tbDir.Text = $gewaehlt; Update-Status | Out-Null }
})
$btnOpen.Add_Click({
  New-Item -ItemType Directory -Force -Path $ausgabe | Out-Null
  Start-Process explorer.exe $ausgabe
})

# Bauen: build.ps1 in eigenem Prozess, Ausgabe über eine Logdatei ins Fenster
$script:proc = $null
$script:logFile = $null
$script:logPos = 0
$script:startTime = $null
$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 300

function Read-NewLog {
  if (-not (Test-Path -LiteralPath $script:logFile)) { return }
  $fs = [System.IO.File]::Open($script:logFile, 'Open', 'Read', 'ReadWrite')
  try {
    $n = $fs.Length - $script:logPos
    $n -= $n % 2  # UTF-16: nur ganze Zeichen lesen
    if ($n -le 0) { return }
    $buf = New-Object byte[] $n
    $fs.Position = $script:logPos
    $read = $fs.Read($buf, 0, $n)
    $script:logPos += $read
    $chunk = [System.Text.Encoding]::Unicode.GetString($buf, 0, $read).Replace([string][char]0xFEFF, '')
    $tbLog.AppendText(($chunk -replace "(?<!`r)`n", "`r`n"))
  } finally { $fs.Dispose() }
}

$timer.Add_Tick({
  Read-NewLog
  if ($script:proc -and $script:proc.HasExited) {
    $timer.Stop()
    Read-NewLog
    $v = Get-Version
    $paket = Join-Path $ausgabe "Unified-Konverter_V$v.addin"
    $erfolg = $script:proc.ExitCode -eq 0 -and (Test-Path -LiteralPath $paket) -and
              (Get-Item -LiteralPath $paket).LastWriteTime -ge $script:startTime
    $script:proc = $null
    $btnBuild.Enabled = $true; $btnBrowse.Enabled = $true; $rbV20.Enabled = $true; $rbV21.Enabled = $true
    if ($erfolg) {
      $lblStatus.ForeColor = $ok
      $lblStatus.Text = "✓ Fertig: $paket"
      if (-not $cbInstall.Checked) {
        $tbLog.AppendText("`r`nDie Datei jetzt nach %APPDATA%\Siemens\Automation\Portal V$v\UserAddIns kopieren und TIA neu starten.`r`n")
      }
      Start-Process explorer.exe "/select,`"$paket`""
    } else {
      $lblStatus.ForeColor = $redText
      $lblStatus.Text = '✗ Build fehlgeschlagen. Details unten im Protokoll (Zeilen mit error).'
    }
  }
})

$btnBuild.Add_Click({
  $dir = Update-Status
  if (-not $dir) { return }
  $v = Get-Version

  $tbLog.Clear()
  $script:logFile = Join-Path ([System.IO.Path]::GetTempPath()) "UnifiedKonverter-Build-$PID.log"
  Remove-Item -LiteralPath $script:logFile -ErrorAction SilentlyContinue
  $script:logPos = 0
  $script:startTime = Get-Date

  $q = { param($s) "'" + $s.Replace("'", "''") + "'" }
  $install = if ($cbInstall.Checked) { '' } else { ' -NoInstall' }
  $cmd = "try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch { }; " +
         "& { try { & $(& $q $buildScript) -TiaVersion $v -TiaPortalDir $(& $q $dir.TrimEnd('\')) -NoPrompt$install } " +
         "catch { Write-Output ('FEHLER: ' + `$_.Exception.Message); exit 1 } } *> $(& $q $script:logFile); exit `$LASTEXITCODE"

  $psi = New-Object System.Diagnostics.ProcessStartInfo
  $psi.FileName = 'powershell.exe'
  $psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command `"$cmd`""
  $psi.WorkingDirectory = $root
  $psi.UseShellExecute = $false
  $psi.CreateNoWindow = $true

  $btnBuild.Enabled = $false; $btnBrowse.Enabled = $false; $rbV20.Enabled = $false; $rbV21.Enabled = $false
  $lblStatus.ForeColor = $text2
  $lblStatus.Text = "Baue für TIA Portal V$v …"
  $script:proc = [System.Diagnostics.Process]::Start($psi)
  $timer.Start()
})

$form.Add_Shown({ Set-VersionDefaults })
[System.Windows.Forms.Application]::EnableVisualStyles()
[void] $form.ShowDialog()
