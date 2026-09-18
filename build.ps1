<#
    Build and optionally install The Hammer of Oden for local testing.

        .\build.ps1            build only
        .\build.ps1 -Install   build, then copy into the r2modman profile

    Thunderstore packaging is deliberately not here yet; it lands when the mod is
    ready to publish and the name is settled.
#>

[CmdletBinding()]
param(
    [switch]$Install,
    [string]$Profile = "1.0 Release V0.1"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

function Fail($msg) { Write-Host "FAIL: $msg" -ForegroundColor Red; exit 1 }
function Ok($msg)   { Write-Host "  ok   $msg" -ForegroundColor Green }

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

if (Get-Process -Name "valheim" -ErrorAction SilentlyContinue) {
    Fail "Valheim is running; close it first or the DLL will be locked"
}

$pluginRoot = "$env:APPDATA\r2modmanPlus-local\Valheim\profiles\$Profile\BepInEx\plugins"
if (-not (Test-Path $pluginRoot)) { Fail "profile not found: $pluginRoot" }

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
Ok "installed to $(Split-Path $target -Leaf)"

Write-Host "`ndone.`n" -ForegroundColor Cyan
