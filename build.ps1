<#
    Build and optionally install The Hammer of Oden for local testing.

        .\build.ps1              build, validate and package
        .\build.ps1 -Install     also install a development copy into the Gale client profile
        .\build.ps1 -RemoveDev   take the development copy out again

    Testing a change: switch the released mod off in Gale, run -Install, play. When the new
    version is published, run -RemoveDev and switch the released mod back on in Gale.
    The development copy lives in its own folder, PicSoul-TheHammerOfOden-DEV, and never
    touches the folder Gale manages - see devcopy.ps1 for why that matters.

    Packaging targets Hexium, which reads the Thunderstore package format: a zip
    with manifest.json, icon.png (exactly 256x256) and README.md at its root.
    Published versions are immutable there as they are on Thunderstore, so bump
    version_number in manifest.json AND <Version> in the .csproj AND PluginVersion
    in HammerOfOdenPlugin.cs before repackaging. This script refuses to build if
    those three ever disagree.
#>

[CmdletBinding()]
param(
    [switch]$Install,
    [switch]$SkipPatchCheck,
    [switch]$RemoveDev,
    [string]$Profile = "1.0 Release Client Mods"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

function Fail($msg) { Write-Host "FAIL: $msg" -ForegroundColor Red; exit 1 }
function Ok($msg)   { Write-Host "  ok   $msg" -ForegroundColor Green }

function PluginRoot {
    $path = "$env:APPDATA\com.kesomannen.gale\valheim\profiles\$Profile\BepInEx\plugins"
    if (-not (Test-Path $path)) { Fail "profile not found: $path" }
    return $path
}

function RequireValheimClosed {
    if (Get-Process -Name "valheim" -ErrorAction SilentlyContinue) {
        Fail "Valheim is running; close it first"
    }
}

# Two mods driving the placement ghost at once produces nonsense. Refuse rather
# than let it look like our bug.
function RequireNoRivalGizmo {
    $gizmo = Get-ChildItem (PluginRoot) -Directory -ErrorAction SilentlyContinue |
             Where-Object { Test-Path (Join-Path $_.FullName "ComfyGizmo.dll") }
    if ($gizmo) {
        Fail "ComfyGizmo is installed at '$($gizmo.Name)'. Switch it off in Gale first - both mods rotate the placement ghost."
    }
    Ok "no conflicting rotation mod installed"
}

. "$root\devcopy.ps1"
$devFolder = "PicSoul-TheHammerOfOden-DEV"
$devDll = "TheHammerOfOden.dll"

if ($RemoveDev) {
    Remove-DevCopy -ProfileName $Profile -FolderName $devFolder -DllName $devDll
    Write-Host "`ndone.`n" -ForegroundColor Cyan
    exit 0
}

# ---------------------------------------------------------------- version sync
$manifest = Get-Content "$root\manifest.json" -Raw | ConvertFrom-Json
$csproj = [xml](Get-Content "$root\TheHammerOfOden.csproj")
$version = $csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

Write-Host "`nThe Hammer of Oden $($manifest.version_number)" -ForegroundColor Cyan

if ($version -ne $manifest.version_number) {
    Fail "version mismatch: manifest.json says $($manifest.version_number), csproj says $version"
}

# The BepInPlugin attribute needs a compile-time constant, so the version is
# declared a third time in the plugin source. Catch it drifting.
$pluginSource = Get-Content "$root\src\HammerOfOdenPlugin.cs" -Raw
if ($pluginSource -notmatch 'PluginVersion\s*=\s*"([^"]+)"') { Fail "could not find PluginVersion in HammerOfOdenPlugin.cs" }
if ($Matches[1] -ne $manifest.version_number) {
    Fail "version mismatch: manifest.json says $($manifest.version_number), HammerOfOdenPlugin.cs says $($Matches[1])"
}
Ok "version $($manifest.version_number) matches in manifest.json, csproj and HammerOfOdenPlugin.cs"

# The changelog should mention the version being packaged.
$changelog = Get-Content "$root\CHANGELOG.md" -Raw
if ($changelog -notmatch [regex]::Escape("## $($manifest.version_number)")) {
    Fail "CHANGELOG.md has no '## $($manifest.version_number)' section"
}
Ok "CHANGELOG.md documents this version"

Write-Host "`nbuilding..." -ForegroundColor Cyan
$log = & dotnet build "$root\TheHammerOfOden.csproj" -c Release -v minimal --nologo 2>&1
if ($LASTEXITCODE -ne 0) { $log; Fail "build failed" }
Ok "compiled"

$dll = "$root\bin\Release\TheHammerOfOden.dll"
if (-not (Test-Path $dll)) { Fail "expected output missing: $dll" }

# ------------------------------------------------------- verify patch targets
# Every Harmony target named by a string is checked against the installed game. The
# compiler cannot check those, and a wrong one is silent: patching class by class means a
# bad target costs one feature rather than the whole mod, so the feature simply never
# works and the log says so where nobody is reading. It has happened twice here - a
# PlacePiece overload that does not exist, and CraftingStation.Awake, which does not
# either - and both times the feature was written, shipped and tested while its patch had
# never applied once. See tools/PatchCheck.
if (-not $SkipPatchCheck) {
    Write-Host "`nverifying patch targets against the installed game..." -ForegroundColor Cyan

    $props = @{}
    if (Test-Path "$root\Local.props") {
        $local = [xml](Get-Content "$root\Local.props")
        foreach ($pg in $local.Project.PropertyGroup) {
            foreach ($node in $pg.ChildNodes) { if ($node.NodeType -eq "Element") { $props[$node.Name] = $node.InnerText } }
        }
    }
    $valheim = $props["ValheimInstall"]
    if (-not $valheim) { $valheim = $env:VALHEIM_INSTALL }
    if (-not $valheim) { $valheim = "C:\Program Files (x86)\Steam\steamapps\common\Valheim" }

    $bepinex = $props["BepInExCore"]
    if ($bepinex) { $bepinex = $bepinex.Replace('$(AppData)', $env:APPDATA) }
    if (-not $bepinex) { $bepinex = $env:BEPINEX_CORE }
    if (-not $bepinex) { $bepinex = "$valheim\BepInEx\core" }

    $checkOutput = & dotnet run --project "$root\tools\PatchCheck\patchcheck.csproj" -c Release -- `
        $dll "$valheim\valheim_Data\Managed" $bepinex 2>&1
    if ($LASTEXITCODE -ne 0) { $checkOutput; Fail "one or more patch targets do not exist in this build of Valheim" }
    Ok ($checkOutput | Select-String "patch targets checked" | Select-Object -First 1).ToString().Trim()
}

# --------------------------------------------------------------------- stage
$stage = "$root\package"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null

Copy-Item $dll $stage
foreach ($f in @("manifest.json", "icon.png", "README.md", "CHANGELOG.md", "LICENSE")) {
    if (Test-Path "$root\$f") { Copy-Item "$root\$f" $stage }
}

# ------------------------------------------------------------------ validate
# Hexium reads the Thunderstore package format, so these are its rules: the three
# required files at the zip root, a name of letters, digits and underscores only,
# semver, a description within the limit, and an icon of exactly 256x256.
Write-Host "`nvalidating against Hexium package rules..." -ForegroundColor Cyan

foreach ($required in @("manifest.json", "icon.png", "README.md")) {
    if (-not (Test-Path "$stage\$required")) { Fail "$required missing from package root" }
}
Ok "manifest.json, icon.png, README.md present at package root"

if ($manifest.name -notmatch '^[a-zA-Z0-9_]+$') { Fail "name '$($manifest.name)' has illegal characters (allowed: a-z A-Z 0-9 _)" }
if ($manifest.name.Length -gt 128)              { Fail "name exceeds 128 characters" }
Ok "name '$($manifest.name)' is valid"

if ($manifest.version_number -notmatch '^\d+\.\d+\.\d+$') { Fail "version_number must be Major.Minor.Patch" }
Ok "version_number is valid semver"

# Hexium allows 256; Thunderstore allows 250. The tighter limit keeps the same
# package publishable in both places.
if ($manifest.description.Length -gt 250) { Fail "description is $($manifest.description.Length) chars (max 250)" }
Ok "description is $($manifest.description.Length)/250 chars"

if ($null -eq $manifest.website_url) { Fail "website_url must be present (use an empty string if unused)" }
Ok "website_url present"

foreach ($dep in $manifest.dependencies) {
    if ($dep -notmatch '^[a-zA-Z0-9_]+-[a-zA-Z0-9_]+-\d+\.\d+\.\d+$') {
        Fail "dependency '$dep' is not in {team}-{package}-{major.minor.patch} form"
    }
}
Ok "$($manifest.dependencies.Count) dependency string(s) well-formed"

Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Image]::FromFile("$stage\icon.png")
$w = $img.Width; $h = $img.Height
$img.Dispose()
if ($w -ne 256 -or $h -ne 256) { Fail "icon.png is ${w}x${h}, must be exactly 256x256" }
Ok "icon.png is 256x256"

try { [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes("$stage\README.md")) | Out-Null }
catch { Fail "README.md is not valid UTF-8" }
Ok "README.md is UTF-8"

# ---------------------------------------------------------------------- zip
$zip = "$root\$($manifest.name)-$($manifest.version_number).zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$stage\*" -DestinationPath $zip
Ok "packaged $(Split-Path $zip -Leaf) ($([math]::Round((Get-Item $zip).Length / 1KB, 1)) KB)"

if (-not $Install) { Write-Host "`ndone.`n" -ForegroundColor Cyan; exit 0 }

RequireNoRivalGizmo
Install-DevCopy -ProfileName $Profile -FolderName $devFolder -DllName $devDll `
    -Stage $stage -Version $manifest.version_number

Write-Host "`ndone.`n" -ForegroundColor Cyan
