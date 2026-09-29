# Package the Unity Windows build (FoxGame/Builds/Windows) into an MSIX for Microsoft Store submission.
# Usage (repo root):  powershell -ExecutionPolicy Bypass -File Store/make-msix.ps1
#   1) Unity: Tools > Fox > Windows Store Build (WindowsStoreBuild.Run) -> FoxGame/Builds/Windows/FoxGame.exe
#   2) this script -> FoxGame/Builds/Store/<identityName>_<version>_x64.msix (unsigned; the Store signs it on upload)
# Identity values come from Store/store-config.json (replace them with the Partner Center values before uploading).
# Requires the Windows 10/11 SDK (makeappx.exe). ASCII only: Windows PowerShell 5.1 misreads UTF-8 scripts without BOM.
param(
    [string]$Config = "Store/store-config.json",
    [string]$BuildDir = "FoxGame/Builds/Windows",
    [string]$Icon = "Art/app_icon_transparent.png",
    [string]$OutDir = "FoxGame/Builds/Store"
)
$ErrorActionPreference = "Stop"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
function P($rel) { if ([System.IO.Path]::IsPathRooted($rel)) { $rel } else { Join-Path $repo $rel } }

$cfg = Get-Content (P $Config) -Raw -Encoding UTF8 | ConvertFrom-Json
$build = P $BuildDir
if (-not (Test-Path (Join-Path $build $cfg.executable))) { throw "No build at $build - run Unity menu Tools > Fox > Windows Store Build first." }

$makeappx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1
if (-not $makeappx) { throw "makeappx.exe not found. Install the Windows SDK (Visual Studio Installer > Individual components > Windows 11 SDK)." }

# 1) Layout: the game files (without debug-only folders) + tile images + manifest
$layout = Join-Path (P $OutDir) "layout"
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
New-Item -ItemType Directory -Force $layout | Out-Null
Get-ChildItem $build | Where-Object { $_.Name -notlike "*_DoNotShip" -and $_.Extension -ne ".pdb" } |
    ForEach-Object { Copy-Item $_.FullName $layout -Recurse }

# 2) Tile / logo images from the icon (high quality resize, transparent corners kept)
Add-Type -AssemblyName System.Drawing
$src = [System.Drawing.Image]::FromFile((P $Icon))
$assets = Join-Path $layout "Assets"
New-Item -ItemType Directory -Force $assets | Out-Null
function Save-Logo([string]$name, [int]$w, [int]$h, [double]$scale = 1.0) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = "HighQualityBicubic"; $g.SmoothingMode = "HighQuality"; $g.PixelOffsetMode = "HighQuality"
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = [int]([Math]::Min($w, $h) * $scale)
    $g.DrawImage($src, [int](($w - $s) / 2), [int](($h - $s) / 2), $s, $s)
    $g.Dispose()
    $bmp.Save((Join-Path $assets $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}
Save-Logo "StoreLogo.png" 50 50
Save-Logo "Square44x44Logo.png" 44 44
Save-Logo "Square44x44Logo.targetsize-256_altform-unplated.png" 256 256
Save-Logo "Square150x150Logo.png" 150 150 0.8
Save-Logo "Wide310x150Logo.png" 310 150 0.8
Save-Logo "Square310x310Logo.png" 310 310 0.8
$src.Dispose()

# 3) AppxManifest.xml (full-trust desktop app)
function X([string]$s) { [System.Security.SecurityElement]::Escape($s) }
$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
         xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
         xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
         IgnorableNamespaces="uap rescap">
  <Identity Name="$(X $cfg.identityName)" Publisher="$(X $cfg.publisher)" Version="$(X $cfg.version)" ProcessorArchitecture="x64" />
  <Properties>
    <DisplayName>$(X $cfg.displayName)</DisplayName>
    <PublisherDisplayName>$(X $cfg.publisherDisplayName)</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="$(X $cfg.minWindowsVersion)" MaxVersionTested="10.0.26100.0" />
  </Dependencies>
  <Resources>
    <Resource Language="$(X $cfg.language)" />
  </Resources>
  <Applications>
    <Application Id="FoxGame" Executable="$(X $cfg.executable)" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements DisplayName="$(X $cfg.displayName)" Description="$(X $cfg.description)"
                          BackgroundColor="$(X $cfg.backgroundColor)"
                          Square150x150Logo="Assets\Square150x150Logo.png" Square44x44Logo="Assets\Square44x44Logo.png">
        <uap:DefaultTile Wide310x150Logo="Assets\Wide310x150Logo.png" Square310x310Logo="Assets\Square310x310Logo.png" ShortName="$(X $cfg.displayName)" />
      </uap:VisualElements>
    </Application>
  </Applications>
  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>
"@
[System.IO.File]::WriteAllText((Join-Path $layout "AppxManifest.xml"), $manifest, (New-Object System.Text.UTF8Encoding $false))

# 4) Pack (makeappx also validates the manifest)
$msix = Join-Path (P $OutDir) ("{0}_{1}_x64.msix" -f $cfg.identityName, $cfg.version)
& $makeappx.FullName pack /d $layout /p $msix /o
if ($LASTEXITCODE -ne 0) { throw "makeappx failed ($LASTEXITCODE)" }
$mb = (Get-Item $msix).Length / 1MB
Write-Host ("MSIX: {0} ({1:N1} MB, unsigned)" -f $msix, $mb)
if ($cfg.publisher -like "*Temp*") { Write-Warning "store-config.json still has TEMP identity values - replace them with the Partner Center values before uploading." }
