<#
  TIA-Portal-Installationsordner finden, prüfen und merken.
  Wird von build.ps1 und BauenFenster.ps1 eingebunden (dot-source).
#>

# Datei, die je TIA-Version im Installationsordner liegen muss
function Get-TiaApiMarker([string] $TiaVersion) {
  if ($TiaVersion -eq '20') { return 'PublicAPI\V20.AddIn\Siemens.Engineering.AddIn.dll' }
  return "PublicAPI\V$TiaVersion\net48\Siemens.Engineering.AddIn.Base.dll"
}

function Test-TiaDir([string] $Dir, [string] $TiaVersion) {
  if (-not $Dir) { return $false }
  return Test-Path -LiteralPath (Join-Path $Dir (Get-TiaApiMarker $TiaVersion))
}

<#
  Macht aus einem beliebigen ausgewählten Ordner den TIA-Installationsordner:
  den Ordner selbst, einen übergeordneten (z. B. '…\PublicAPI\V20.AddIn' ausgewählt)
  oder einen untergeordneten bis zwei Ebenen tief (z. B. 'E:\Program Files' ausgewählt).
  Gibt $null zurück, wenn dort keine TIA-API der gewünschten Version liegt.
#>
function Resolve-TiaDir([string] $Path, [string] $TiaVersion) {
  if (-not $Path) { return $null }
  $Path = $Path.Trim().Trim('"')
  if (-not (Test-Path -LiteralPath $Path -PathType Container)) { return $null }

  $dir = (Resolve-Path -LiteralPath $Path).ProviderPath
  for ($d = $dir; $d; $d = Split-Path $d -Parent) {
    if (Test-TiaDir $d $TiaVersion) { return $d.TrimEnd('\') + '\' }
  }
  $treffer = Get-ChildItem -LiteralPath $dir -Directory -Recurse -Depth 2 -ErrorAction SilentlyContinue |
    Where-Object { Test-TiaDir $_.FullName $TiaVersion } | Select-Object -First 1
  if ($treffer) { return $treffer.FullName.TrimEnd('\') + '\' }
  return $null
}

function Get-TiaDirFile([string] $TiaVersion) {
  return Join-Path (Split-Path $PSScriptRoot -Parent) ".tia-pfad-V$TiaVersion.txt"
}

function Save-TiaDir([string] $Dir, [string] $TiaVersion) {
  Set-Content -LiteralPath (Get-TiaDirFile $TiaVersion) -Value $Dir -Encoding UTF8
}

# Gemerkter Pfad → Registry → Standardpfad; $null, wenn nichts davon passt
function Find-TiaDir([string] $TiaVersion) {
  $kandidaten = @()
  $datei = Get-TiaDirFile $TiaVersion
  if (Test-Path -LiteralPath $datei) { $kandidaten += (Get-Content -LiteralPath $datei -TotalCount 1) }
  try {
    $kandidaten += (Get-ItemProperty "HKLM:\SOFTWARE\Siemens\Automation\_InstalledSW\TIAP$TiaVersion\EditionMain" -ErrorAction Stop).Path
  } catch { }
  $kandidaten += Join-Path $env:ProgramW6432 "Siemens\Automation\Portal V$TiaVersion"

  foreach ($k in $kandidaten) {
    if (Test-TiaDir $k $TiaVersion) { return $k.TrimEnd('\') + '\' }
  }
  return $null
}

function Select-TiaDirDialog([string] $TiaVersion, [string] $Start = '') {
  Add-Type -AssemblyName System.Windows.Forms
  $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
  $dlg.Description = "TIA-Portal-V$TiaVersion-Installationsordner wählen (z. B. 'E:\Program Files\Siemens\Automation\Portal V$TiaVersion')"
  $dlg.ShowNewFolderButton = $false
  if ($Start -and (Test-Path -LiteralPath $Start)) { $dlg.SelectedPath = $Start }
  if ($dlg.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { return $dlg.SelectedPath }
  return $null
}
