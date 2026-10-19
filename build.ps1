<#
  Baut das Add-In und erzeugt das .addin-Paket.
  Das fertige Paket liegt danach zusätzlich im Ordner 'Ausgabe' neben diesem Skript.

  Beispiele:
    .\build.ps1                       # TIA V20, Release, Paket nach UserAddIns kopieren
    .\build.ps1 -TiaVersion 21
    .\build.ps1 -TiaVersion 20 -NoInstall
    .\build.ps1 -TiaPortalDir "E:\Program Files\Siemens\Automation\Portal V20"

  Ohne -TiaPortalDir: gemerkter Ordner, sonst Registry, sonst Standardpfad.
  Passt nichts davon, fragt das Skript nach dem Ordner und merkt ihn sich (.tia-pfad-V20.txt).
#>
param(
  [ValidateSet('20', '21')] [string] $TiaVersion = '20',
  [ValidateSet('Release', 'Debug')] [string] $Configuration = 'Release',
  [string] $TiaPortalDir = '',
  [switch] $NoInstall,
  [switch] $NoPrompt
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src\UnifiedKonverter\UnifiedKonverter.csproj'
. (Join-Path $PSScriptRoot 'tools\TiaPfad.ps1')

# TIA-Installationsordner bestimmen
if ($TiaPortalDir) {
  $dir = Resolve-TiaDir $TiaPortalDir $TiaVersion
  if (-not $dir) { throw "In '$TiaPortalDir' wurde keine TIA-Portal-V$TiaVersion-API gefunden ($(Get-TiaApiMarker $TiaVersion)). Ist TIA Portal Openness installiert?" }
} else {
  $dir = Find-TiaDir $TiaVersion
  while (-not $dir) {
    if ($NoPrompt) { throw "TIA Portal V$TiaVersion nicht gefunden. Ordner mit -TiaPortalDir angeben." }
    Write-Host ''
    Write-Host "TIA Portal V$TiaVersion wurde nicht automatisch gefunden."
    $eingabe = Read-Host 'Installationsordner eingeben (leer lassen = Ordner auswählen)'
    if (-not $eingabe) { $eingabe = Select-TiaDirDialog $TiaVersion }
    if (-not $eingabe) { throw 'Abgebrochen: kein TIA-Ordner gewählt.' }
    $dir = Resolve-TiaDir $eingabe $TiaVersion
    if (-not $dir) { Write-Host "In '$eingabe' liegt keine TIA-Portal-V$TiaVersion-API ($(Get-TiaApiMarker $TiaVersion)). Bitte nochmal." -ForegroundColor Yellow }
  }
}
Save-TiaDir $dir $TiaVersion
Write-Host "TIA Portal V${TiaVersion}: $dir"

# MSBuild von Visual Studio suchen (liest den TIA-Pfad aus der Registry)
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = $null
if (Test-Path $vswhere) {
  $msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
}

$props = @("-p:TiaVersion=$TiaVersion", "-p:Configuration=$Configuration")
$props += "-p:TiaPortalDir=$($dir.TrimEnd('\'))"
if ($NoInstall) { $props += '-p:InstallAddIn=false' }

if ($msbuild) {
  Write-Host "MSBuild: $msbuild"
  & $msbuild $project -restore -nologo -v:minimal @props
} else {
  # dotnet.exe gibt es oft nur als Laufzeit (ohne SDK) – dann bricht 'dotnet build' mit Exit-Code -2147450735 ab.
  $sdks = $null
  if (Get-Command dotnet -ErrorAction SilentlyContinue) { $sdks = & dotnet --list-sdks 2>$null }
  if (-not $sdks) {
    throw @'
Kein Build-Werkzeug gefunden: weder Visual Studio (MSBuild) noch ein .NET SDK.
Am einfachsten das .NET SDK installieren (danach PowerShell neu öffnen):
    winget install Microsoft.DotNet.SDK.8
Alternativ Visual Studio 2022 Community mit der Workload ".NET-Desktopentwicklung".
'@
  }
  Write-Host 'Visual Studio nicht gefunden – baue mit dem .NET SDK.'
  & dotnet build $project -nologo -v:minimal @props
}
if ($LASTEXITCODE -ne 0) { throw "Build fehlgeschlagen (Exit-Code $LASTEXITCODE)." }

# Paket zusätzlich in den Ordner 'Ausgabe' legen, zum manuellen Kopieren
$paket = Join-Path $PSScriptRoot "src\UnifiedKonverter\bin\V$TiaVersion\$Configuration\Unified-Konverter_V$TiaVersion.addin"
$ausgabe = Join-Path $PSScriptRoot 'Ausgabe'
New-Item -ItemType Directory -Force -Path $ausgabe | Out-Null
Copy-Item -LiteralPath $paket -Destination $ausgabe -Force
Write-Host ''
Write-Host "Fertig: $(Join-Path $ausgabe (Split-Path $paket -Leaf))" -ForegroundColor Green
