[CmdletBinding()]
param(
    [string]$GamePath = "D:\Appdata\Steam\steamapps\common\RimWorld",
    [string]$WorkshopRoot = "D:\Appdata\Steam\steamapps\workshop\content\294100"
)
$ErrorActionPreference = "Stop"
$managed = Join-Path $GamePath "RimWorldWin64_Data\Managed"
foreach ($name in @("UnityEngine.CoreModule.dll", "Assembly-CSharp.dll")) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $managed $name))
}
$uf = [Reflection.Assembly]::LoadFrom((Join-Path $WorkshopRoot "3651799915\1.6\Assemblies\UFRecipes.dll"))
$warehouse = [Reflection.Assembly]::LoadFrom((Join-Path $WorkshopRoot "3740898940\1.6\Assemblies\UniversalDigitalWarehouseLib.dll"))
$checks = @(
    @($uf, "UFRecipes.AutoSequenceUtil", "TryPullIngredientsFromMap", "System.Boolean", "RimWorld.Bill,Verse.Map,Verse.ThingOwner"),
    @($uf, "UFRecipes.AutoSequenceUtil", "HasIngredients", "System.Boolean", "RimWorld.Bill,Verse.ThingOwner"),
    @($warehouse, "UniversalDigitalWarehouseLib.DigitalWarehouseMaterialUtility", "PointsForDefCount", "System.Single", "Verse.ThingDef,System.Int32,System.Single"),
    @($warehouse, "UniversalDigitalWarehouseLib.GameComponent_DigitalWarehousePoints", "TrySpendPoints", "System.Boolean", "System.Single"),
    @($warehouse, "UniversalDigitalWarehouseLib.Building_DigitalWarehouse", "CanValidateOutputFor", "System.Boolean", "Verse.ThingDef")
)
foreach ($check in $checks) {
    $type = $check[0].GetType($check[1], $true)
    $method = $type.GetMethod($check[2])
    if ($null -eq $method -or $method.ReturnType.FullName -ne $check[3]) { throw "Contract changed: $($check[1]).$($check[2])" }
    $parameters = ($method.GetParameters() | ForEach-Object { $_.ParameterType.FullName }) -join ','
    if ($parameters -ne $check[4]) { throw "Parameter contract changed: $($check[2]): $parameters" }
    Write-Host "PASS: $($check[1]).$($check[2])"
}
$bench = $uf.GetType("UFRecipes.Building_SuperWorkbench", $true)
foreach ($name in @("fullAuto", "ManuallyPaused", "CanRunNow")) {
    if ($bench.GetProperty($name).PropertyType -ne [bool]) { throw "Missing bench boolean: $name" }
}
if ($bench.GetField("innerContainer").FieldType.FullName -ne "Verse.ThingOwner") { throw "Container contract changed." }
Write-Host "PASS: printer mode and container contracts"
