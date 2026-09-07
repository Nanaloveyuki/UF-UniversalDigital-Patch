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

# Decode actual IL instruction boundaries; byte-pattern searches can mistake operands for calls.
function Get-CalledMethods($method) {
    $opcodes = @{}
    foreach ($field in [Reflection.Emit.OpCodes].GetFields([Reflection.BindingFlags]'Public,Static')) {
        $op = $field.GetValue($null)
        $opcodes[[int]$op.Value -band 65535] = $op
    }
    $bytes = $method.GetMethodBody().GetILAsByteArray()
    $offset = 0
    while ($offset -lt $bytes.Length) {
        $code = [int]$bytes[$offset++]
        if ($code -eq 254) { $code = 65024 + [int]$bytes[$offset++] }
        $op = $opcodes[$code]
        if ($null -eq $op) { throw "Unknown IL opcode in $method" }
        switch ($op.OperandType.ToString()) {
            'InlineNone' { $size = 0 }
            'ShortInlineBrTarget' { $size = 1 }
            'ShortInlineI' { $size = 1 }
            'ShortInlineVar' { $size = 1 }
            'InlineVar' { $size = 2 }
            'InlineI8' { $size = 8 }
            'InlineR' { $size = 8 }
            'InlineSwitch' { $size = 4 + 4 * [BitConverter]::ToInt32($bytes, $offset) }
            default { $size = 4 }
        }
        if ($op.Name -in @('call', 'callvirt')) {
            $method.Module.ResolveMethod([BitConverter]::ToInt32($bytes, $offset))
        }
        $offset += $size
    }
}
$flags = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$util = $uf.GetType('UFRecipes.AutoSequenceUtil', $true)
$overloads = @($util.GetMethods($flags) | Where-Object { $_.Name -eq 'TryPullIngredientsFromCandidates' })
$target = @($overloads | Where-Object { $_.GetParameters().Count -eq 5 })
$wrapper = @($overloads | Where-Object { $_.GetParameters().Count -eq 4 })
if ($target.Count -ne 1 -or $wrapper.Count -ne 1) { throw 'UF candidate overloads changed.' }
$target = $target[0]
$wrapper = $wrapper[0]
$parameters = $target.GetParameters()
if ($target.ReturnType -ne [bool] -or $parameters[0].ParameterType.FullName -ne 'RimWorld.Bill' -or
    $parameters[1].ParameterType.GetGenericTypeDefinition() -ne [System.Collections.Generic.List``1] -or
    $parameters[1].ParameterType.GetGenericArguments()[0].FullName -ne 'Verse.Thing' -or
    $parameters[2].ParameterType.FullName -ne 'Verse.Map' -or
    $parameters[3].ParameterType.FullName -ne 'Verse.ThingOwner' -or
    $parameters[4].ParameterType -ne [bool].MakeByRefType() -or $parameters[4].Name -ne 'transferAttempted') {
    throw 'UF candidate transfer contract changed.'
}
foreach ($edge in @(
    @($bench.GetMethod('FirstFullAutoBill', $flags), $target),
    @($util.GetMethod('TryPullIngredientsFromMap'), $wrapper),
    @($wrapper, $target)
)) {
    $calls = @(Get-CalledMethods $edge[0])
    if (-not ($calls | Where-Object { $_.Module -eq $edge[1].Module -and $_.MetadataToken -eq $edge[1].MetadataToken })) {
        throw "Missing UF call edge: $($edge[0].Name) -> $($edge[1])"
    }
    Write-Host "PASS: actual IL $($edge[0].Name) -> $($edge[1])"
}
