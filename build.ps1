<#
    Build and optionally install The Hammer of Oden for local testing.

        .\build.ps1            build, validate and package
        .\build.ps1 -Install   also copy into the local Gale profile
        .\build.ps1 -Disable   stop BepInEx loading it, without deleting it
        .\build.ps1 -Enable    load it again

    -Disable and -Enable exist because this mod is copied into the profile rather
    than installed by r2modman, so r2modman does not list it and has no toggle for
    it. They rename the DLL to .old and back, which is the same trick r2modman uses
    on the mods it does manage.

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
    [switch]$Disable,
    [switch]$Enable,
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

# Renaming to .old is what r2modman does to the mods it manages: BepInEx loads
# *.dll and nothing else, so the file stays put and stops being loaded.
# Two mods driving the placement ghost at once produces nonsense. Refuse rather
# than let it look like our bug.
function RequireNoRivalGizmo {
    $gizmo = Get-ChildItem (PluginRoot) -Directory -ErrorAction SilentlyContinue |
             Where-Object { Test-Path (Join-Path $_.FullName "ComfyGizmo.dll") }
    if ($gizmo) {
        Fail "ComfyGizmo is installed at '$($gizmo.Name)'. Disable it in r2modman first - both mods rotate the placement ghost."
    }
    Ok "no conflicting rotation mod installed"
}

function SetLoaded([bool]$loaded) {
    RequireValheimClosed
    $dir = Join-Path (PluginRoot) "PICS0UL-TheHammerOfOden"
    if (-not (Test-Path $dir)) { Fail "not installed; run .\build.ps1 -Install first" }

    $live = Join-Path $dir "TheHammerOfOden.dll"
    $off = "$live.old"

    if ($loaded) {
        if (Test-Path $live) { Ok "already enabled"; return }
        RequireNoRivalGizmo
        if (-not (Test-Path $off)) { Fail "nothing to enable in $dir" }
        Move-Item $off $live -Force
        Ok "enabled - BepInEx will load it on next launch"
        return
    }

    if (-not (Test-Path $live)) {
        if (Test-Path $off) { Ok "already disabled"; return }
        Fail "nothing to disable in $dir"
    }

    Move-Item $live $off -Force
    Ok "disabled - the DLL is kept as $(Split-Path $off -Leaf)"
}

if ($Disable -and $Enable) { Fail "pick one of -Disable or -Enable" }

if ($Disable) {
    Write-Host "`ndisabling..." -ForegroundColor Cyan
    SetLoaded $false
    Write-Host "`ndone.`n" -ForegroundColor Cyan
    exit 0
}

if ($Enable) {
    Write-Host "`nenabling..." -ForegroundColor Cyan
    SetLoaded $true
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

Write-Host "`ninstalling to profile '$Profile'..." -ForegroundColor Cyan

RequireValheimClosed
$pluginRoot = PluginRoot

RequireNoRivalGizmo

$target = Join-Path $pluginRoot "PICS0UL-TheHammerOfOden"
if (-not (Test-Path $target)) { New-Item -ItemType Directory -Path $target | Out-Null }
Copy-Item $dll $target -Force

# A stale .old beside a fresh DLL would leave -Enable and -Disable disagreeing
# about which file is the real one; installing always means enabled.
Remove-Item (Join-Path $target "TheHammerOfOden.dll.old") -Force -ErrorAction SilentlyContinue
Ok "installed to $(Split-Path $target -Leaf)"

Write-Host "`ndone.`n" -ForegroundColor Cyan
