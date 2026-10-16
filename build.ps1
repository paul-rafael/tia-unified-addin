<#
  Baut das Add-In und erzeugt das .addin-Paket.

  Beispiele:
    .\build.ps1                       # TIA V20, Release, Paket nach UserAddIns kopieren
    .\build.ps1 -TiaVersion 21
    .\build.ps1 -TiaVersion 20 -NoInstall
    .\build.ps1 -TiaPortalDir "D:\Siemens\Automation\Portal V20\"
#>
param(
  [ValidateSet('20', '21')] [string] $TiaVersion = '20',
  [ValidateSet('Release', 'Debug')] [string] $Configuration = 'Release',
  [string] $TiaPortalDir = '',
  [switch] $NoInstall
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src\UnifiedKonverter\UnifiedKonverter.csproj'

# MSBuild von Visual Studio suchen (liest den TIA-Pfad aus der Registry)
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = $null
if (Test-Path $vswhere) {
  $msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
}

$props = @("-p:TiaVersion=$TiaVersion", "-p:Configuration=$Configuration")
if ($TiaPortalDir) { $props += "-p:TiaPortalDir=$TiaPortalDir" }
if ($NoInstall) { $props += '-p:InstallAddIn=false' }

if ($msbuild) {
  Write-Host "MSBuild: $msbuild"
  & $msbuild $project -restore -nologo -v:minimal @props
} elseif (Get-Command dotnet -ErrorAction SilentlyContinue) {
  Write-Host 'Visual Studio nicht gefunden – baue mit dotnet.'
  & dotnet build $project -nologo -v:minimal @props
} else {
  throw 'Weder Visual Studio (MSBuild) noch das .NET SDK gefunden. Bitte Visual Studio 2022 mit „.NET-Desktopentwicklung“ installieren.'
}
if ($LASTEXITCODE -ne 0) { throw "Build fehlgeschlagen (Exit-Code $LASTEXITCODE)." }
