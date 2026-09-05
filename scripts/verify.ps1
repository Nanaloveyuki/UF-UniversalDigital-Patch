[CmdletBinding()]
param()
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
& dotnet build (Join-Path $root "Source\UFUniversalDigitalPatch.csproj") -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed." }
& dotnet run --project (Join-Path $root "Tests\Tests.csproj") -c Release
if ($LASTEXITCODE -ne 0) { throw "Planner tests failed." }
# Use the game's .NET Framework runtime for reflection, not PowerShell 7's CoreCLR.
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "verify-contracts.ps1")
if ($LASTEXITCODE -ne 0) { throw "Installed assembly contracts failed." }
$about = [xml](Get-Content -LiteralPath (Join-Path $root "About\About.xml") -Raw)
$folders = [xml](Get-Content -LiteralPath (Join-Path $root "LoadFolders.xml") -Raw)
if ($folders.loadFolders.'v1.6'.li -ne "1.6") { throw "Invalid load folder." }
foreach ($id in @("KindSeal.UFLI", "Chezhou.UniversalDigitalWarehouse", "brrainz.harmony")) {
    $dependency = @($about.ModMetaData.modDependencies.li | Where-Object { $_.packageId -eq $id })
    if ($dependency.Count -ne 1 -or -not $dependency[0].steamWorkshopUrl -or $about.ModMetaData.loadAfter.li -notcontains $id) {
        throw "Missing dependency URL or load order: $id"
    }
}
$dlls = @(Get-ChildItem -LiteralPath (Join-Path $root "1.6\Assemblies") -Filter *.dll)
if ($dlls.Count -ne 1 -or $dlls[0].Name -ne "UFUniversalDigitalPatch.dll") { throw "Unexpected bundled dependency DLL." }
Write-Host "PASS: metadata, load order, dependency URLs and assembly layout"
