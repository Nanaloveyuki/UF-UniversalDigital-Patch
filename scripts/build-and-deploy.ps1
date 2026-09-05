[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")][string]$Configuration = "Release",
    [string]$GameModPath = "D:\Appdata\Steam\steamapps\common\RimWorld\Mods\UFUniversalDigitalPatch",
    [switch]$SkipBuild
)
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$target = [IO.Path]::GetFullPath($GameModPath).TrimEnd([char[]]"\/")
$parent = Split-Path -Parent $target
if ((Split-Path -Leaf $parent) -ne "Mods" -or -not (Test-Path -LiteralPath $parent -PathType Container)) {
    throw "Deployment target must be a direct child of an existing RimWorld Mods directory."
}
if (Get-Process -Name "RimWorldWin64", "RimWorldWin", "RimWorld" -ErrorAction SilentlyContinue) {
    throw "Exit RimWorld before deploying this assembly."
}
if (-not $SkipBuild) {
    & dotnet build (Join-Path $root "Source\UFUniversalDigitalPatch.csproj") -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed." }
}
$files = @("About\About.xml", "LoadFolders.xml", "1.6\Assemblies\UFUniversalDigitalPatch.dll")
foreach ($file in $files) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $file) -PathType Leaf)) { throw "Missing deployment file: $file" }
}
$targetAbout = Join-Path $target "About\About.xml"
if (Test-Path -LiteralPath $targetAbout) {
    $about = [xml](Get-Content -LiteralPath $targetAbout -Raw)
    if ($about.ModMetaData.packageId -ne "Nanaloveyuki.UFUniversalDigitalPatch") { throw "Target belongs to another mod." }
} elseif ((Test-Path -LiteralPath $target) -and @(Get-ChildItem -LiteralPath $target -Force).Count -gt 0) {
    throw "Refusing to overwrite a nonempty directory without matching mod metadata."
}
foreach ($file in $files) {
    $source = Join-Path $root $file
    $destination = Join-Path $target $file
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash) {
        throw "Deployment hash mismatch: $file"
    }
    Write-Host "Verified: $file"
}
Write-Host "Deployed to: $target"
Write-Host "Enable UF Universal Digital Patch after both required mods. ModsConfig.xml was not changed."
