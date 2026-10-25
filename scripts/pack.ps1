<#
.SYNOPSIS
    Builds CameraMode, checks its version, packs the release zip, and optionally installs it.

.DESCRIPTION
    Run this through PowerShell, not Bash: Bash mangles 'H:\SPT4.1.X' into 'H:SPT4.1.X'.

    The zip is unpacked over the SPT root, so it carries the full path BepInEx/plugins/... .
    It holds CameraMode.dll and nothing else: no README or other docs.
    Entries are written by hand with forward slashes: PowerShell 5.1's Compress-Archive writes
    backslashes, which some extractors turn into file names instead of folders.

.PARAMETER SPTPath
    The SPT install root to compile against (and install into, with -Install).

.PARAMETER Install
    Also copy CameraMode.dll into the install's BepInEx\plugins. Never deletes anything.

.EXAMPLE
    scripts\pack.ps1 -SPTPath H:\SPT4.1.X
    scripts\pack.ps1 -SPTPath H:\SPT4.1.X -Install
#>
param(
    [Parameter(Mandatory = $true)][string] $SPTPath,
    [switch] $Install
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\CameraMode\CameraMode.csproj'
$source = Join-Path $root 'src\CameraMode\CameraModePlugin.cs'

if (-not (Test-Path (Join-Path $SPTPath 'BepInEx\core\BepInEx.dll'))) {
    throw "SPTPath '$SPTPath' is not an SPT install root: no BepInEx\core\BepInEx.dll."
}

# ---- version agreement -----------------------------------------------------------
$csprojVersion = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ }
$sourceVersion = (Select-String -Path $source -Pattern 'PluginVersion\s*=\s*"([^"]+)"').Matches[0].Groups[1].Value
if ($csprojVersion -ne $sourceVersion) {
    throw "Version mismatch: CameraMode.csproj '$csprojVersion', CameraModePlugin.PluginVersion '$sourceVersion'."
}
$version = $csprojVersion
Write-Host "Version $version" -ForegroundColor Cyan

# ---- config keys -----------------------------------------------------------------
# BepInEx throws in Awake on these characters in a section or key name, and the plugin then
# never loads (and logs nothing to LogOutput.log).
$settings = Join-Path $root 'src\CameraMode\Settings.cs'
$keys = Select-String -Path $settings -Pattern 'Bind\(config, \w+, "([^"]*)"' -AllMatches |
    ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value }
$sections = Select-String -Path $settings -Pattern 'const string \w+ = "([^"]*)"' -AllMatches |
    ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value }
if (-not $keys) { throw "Found no Bind(config, ...) calls in $settings; the key check is out of date." }
$bad = @($keys) + @($sections) | Where-Object { $_ -match '[=\n\t\\"''\[\]]' }
if ($bad) { throw "Config names BepInEx rejects: $($bad -join '; ')" }
Write-Host "Config names OK ($(@($keys).Count) keys, $(@($sections).Count) sections)"

# ---- build -----------------------------------------------------------------------
$built = Join-Path $root 'src\CameraMode\bin\Release\CameraMode.dll'
dotnet build $project -c Release "-p:SPTPath=$SPTPath" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

# ---- zip -------------------------------------------------------------------------
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$zip = Join-Path $dist "CameraMode-$version.zip"
$entries = [ordered]@{ 'BepInEx/plugins/CameraMode.dll' = $built }

if (Test-Path $zip) { Remove-Item $zip -Force }
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($e in $entries.GetEnumerator()) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $e.Value, $e.Key, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $archive.Dispose()
}

$check = [System.IO.Compression.ZipFile]::OpenRead($zip)
try {
    $names = $check.Entries | ForEach-Object { $_.FullName }
}
finally {
    $check.Dispose()
}
$missing = $entries.Keys | Where-Object { $names -notcontains $_ }
if ($missing) { throw "$zip is missing: $($missing -join ', ')" }
$extra = $names | Where-Object { $entries.Keys -notcontains $_ }
if ($extra) { throw "$zip has unexpected entries: $($extra -join ', ')" }
Write-Host "Packed $zip" -ForegroundColor Green
$names | ForEach-Object { Write-Host "  $_" }

# ---- install ---------------------------------------------------------------------
if ($Install) {
    $dest = Join-Path $SPTPath 'BepInEx\plugins'
    Copy-Item $built $dest -Force
    Write-Host "Installed CameraMode.dll to $dest" -ForegroundColor Green
}
