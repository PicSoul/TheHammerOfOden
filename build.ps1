<#
    Build and optionally install The Hammer of Oden for local testing.

        .\build.ps1            build only
        .\build.ps1 -Install   build, then copy into the r2modman profile
        .\build.ps1 -Disable   stop BepInEx loading it, without deleting it
        .\build.ps1 -Enable    load it again

    -Disable and -Enable exist because this mod is copied into the profile rather
    than installed by r2modman, so r2modman does not list it and has no toggle for
    it. They rename the DLL to .old and back, which is the same trick r2modman uses
    on the mods it does manage.

    Thunderstore packaging is deliberately not here yet; it lands when the mod is
    ready to publish and the name is settled.
#>

[CmdletBinding()]
param(
    [switch]$Install,
    [switch]$Disable,
    [switch]$Enable,
    [string]$Profile = "1.0 Release V0.1"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

function Fail($msg) { Write-Host "FAIL: $msg" -ForegroundColor Red; exit 1 }
function Ok($msg)   { Write-Host "  ok   $msg" -ForegroundColor Green }

function PluginRoot {
    $path = "$env:APPDATA\r2modmanPlus-local\Valheim\profiles\$Profile\BepInEx\plugins"
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
function SetLoaded([bool]$loaded) {
    RequireValheimClosed
    $dir = Join-Path (PluginRoot) "PICS0UL-TheHammerOfOden"
    if (-not (Test-Path $dir)) { Fail "not installed; run .\build.ps1 -Install first" }

    $live = Join-Path $dir "TheHammerOfOden.dll"
    $off = "$live.old"

    if ($loaded) {
        if (Test-Path $live) { Ok "already enabled"; return }
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

$csproj = [xml](Get-Content "$root\TheHammerOfOden.csproj")
$version = $csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
Write-Host "`nThe Hammer of Oden $version" -ForegroundColor Cyan

Write-Host "`nbuilding..." -ForegroundColor Cyan
$log = & dotnet build "$root\TheHammerOfOden.csproj" -c Release -v minimal --nologo 2>&1
if ($LASTEXITCODE -ne 0) { $log; Fail "build failed" }
Ok "compiled"

$dll = "$root\bin\Release\TheHammerOfOden.dll"
if (-not (Test-Path $dll)) { Fail "expected output missing: $dll" }

if (-not $Install) { Write-Host "`ndone.`n" -ForegroundColor Cyan; exit 0 }

Write-Host "`ninstalling to profile '$Profile'..." -ForegroundColor Cyan

RequireValheimClosed
$pluginRoot = PluginRoot

# Two mods driving the placement ghost at once produces nonsense. Refuse rather than
# let it look like our bug.
$gizmo = Get-ChildItem $pluginRoot -Directory -ErrorAction SilentlyContinue |
         Where-Object { Test-Path (Join-Path $_.FullName "ComfyGizmo.dll") }
if ($gizmo) {
    Fail "ComfyGizmo is still installed at '$($gizmo.Name)'. Disable it in r2modman before testing - both mods rotate the placement ghost."
}
Ok "no conflicting rotation mod installed"

$target = Join-Path $pluginRoot "PICS0UL-TheHammerOfOden"
if (-not (Test-Path $target)) { New-Item -ItemType Directory -Path $target | Out-Null }
Copy-Item $dll $target -Force

# A stale .old beside a fresh DLL would leave -Enable and -Disable disagreeing
# about which file is the real one; installing always means enabled.
Remove-Item (Join-Path $target "TheHammerOfOden.dll.old") -Force -ErrorAction SilentlyContinue
Ok "installed to $(Split-Path $target -Leaf)"

Write-Host "`ndone.`n" -ForegroundColor Cyan
