<#
    The development copy of a mod, kept apart from the released one.

    Dot-sourced by build.ps1, which provides Fail and Ok. The same file sits in each mod's
    repository, so a fix here should be copied to the others.

    Why a separate copy at all:

    The released copy lives in a folder Gale manages, and Gale hardlinks the files in it to its
    own download cache and to every other profile using that version - they are one file on disk,
    not several. Writing a development build into that folder writes it into all of them. Doing
    exactly that once left Gale's cache holding an unpublished build under a published version
    number, and quietly changed the server profile too.

    So a development build goes in a folder of its own, named <Author>-<Mod>-DEV, which Gale neither
    knows about nor links. The released copy is switched off in Gale while testing, and back on
    once the new version is published.

    Why it refuses while the released copy is still on:

    Both copies carry the same plugin GUID and BepInEx loads only one plugin per GUID, choosing by
    version number. Which build ran would then depend on version numbers rather than on what you
    meant to test, and "I tested it" would not mean what it says.
#>

# Files that belong to the package page on Hexium rather than to the running mod. Left out of the
# development copy so the folder holds nothing a mod manager might mistake for a package.
$script:PackageOnlyFiles = @("manifest.json", "icon.png", "README.md", "CHANGELOG.md", "LICENSE")

function Get-DevPluginRoot([string]$ProfileName) {
    $path = "$env:APPDATA\com.kesomannen.gale\valheim\profiles\$ProfileName\BepInEx\plugins"
    if (-not (Test-Path $path)) { Fail "profile not found: $path" }
    return $path
}

function Assert-ValheimClosed {
    if (Get-Process -Name "valheim" -ErrorAction SilentlyContinue) {
        Fail "Valheim is running; close it first"
    }
}

# Every copy of the DLL outside the development folder that BepInEx would load. BepInEx loads files
# named *.dll, so a copy a mod manager has switched off - renamed or moved aside - no longer matches.
function Find-OtherCopies([string]$PluginRoot, [string]$DllName, [string]$DevDir) {
    Get-ChildItem $PluginRoot -Recurse -File -Filter $DllName -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -eq $DllName -and -not $_.DirectoryName.StartsWith($DevDir, [StringComparison]::OrdinalIgnoreCase) }
}

function Install-DevCopy {
    param(
        [string]$ProfileName,
        [string]$FolderName,
        [string]$DllName,
        [string]$Stage,
        [string]$Version
    )

    Write-Host "`ninstalling a development copy to '$ProfileName'..." -ForegroundColor Cyan

    Assert-ValheimClosed
    $pluginRoot = Get-DevPluginRoot $ProfileName
    $devDir = Join-Path $pluginRoot $FolderName

    $others = @(Find-OtherCopies $pluginRoot $DllName $devDir)
    if ($others.Count -gt 0) {
        Write-Host ""
        foreach ($copy in $others) {
            Write-Host "  still switched on: $(Split-Path $copy.DirectoryName -Leaf)\$($copy.Name)" -ForegroundColor Yellow
        }
        Fail ("another copy of $DllName would load alongside this one. Switch the released mod off in Gale " +
              "(Manage, then its toggle) and run this again. A folder Gale does not list is left over from " +
              "an older install and can be deleted.")
    }
    Ok "no other copy of $DllName is switched on"

    if (Test-Path $devDir) { Remove-Item $devDir -Recurse -Force }
    New-Item -ItemType Directory -Path $devDir | Out-Null

    # Everything the running mod needs - the DLL, and any artwork beside it - and none of the page.
    Get-ChildItem $Stage -Force | Where-Object { $script:PackageOnlyFiles -notcontains $_.Name } |
        ForEach-Object { Copy-Item $_.FullName $devDir -Recurse -Force }

    $marker = @(
        "Development copy of $FolderName, version $Version.",
        "Installed $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') by build.ps1 from $((Get-Item $Stage).Parent.FullName).",
        "",
        "The released copy should be switched off in Gale while this one is here.",
        "When finished: run  .\build.ps1 -RemoveDev  and switch the released copy back on in Gale."
    )
    Set-Content -Path (Join-Path $devDir "DEV-BUILD.txt") -Value $marker -Encoding UTF8

    $installed = Join-Path $devDir $DllName
    if (-not (Test-Path $installed)) { Fail "copy did not produce $installed" }

    Ok "installed $FolderName ($Version)"
    Write-Host "     $devDir" -ForegroundColor DarkGray
}

function Remove-DevCopy {
    param(
        [string]$ProfileName,
        [string]$FolderName,
        [string]$DllName
    )

    Write-Host "`nremoving the development copy from '$ProfileName'..." -ForegroundColor Cyan

    Assert-ValheimClosed
    $pluginRoot = Get-DevPluginRoot $ProfileName
    $devDir = Join-Path $pluginRoot $FolderName

    if (Test-Path $devDir) {
        Remove-Item $devDir -Recurse -Force
        Ok "removed $FolderName"
    }
    else {
        Ok "no development copy was installed"
    }

    $others = @(Find-OtherCopies $pluginRoot $DllName $devDir)
    if ($others.Count -eq 0) {
        Write-Host "  note  no copy of $DllName is switched on now - switch the released mod back on in Gale." -ForegroundColor Yellow
    }
    else {
        Ok "released copy is switched on: $(Split-Path $others[0].DirectoryName -Leaf)"
    }
}
