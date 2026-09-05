# UF Universal Digital Patch

RimWorld 1.6 compatibility mod connecting UF Light Industries fully automatic
printers to Universal Digital Warehouse. Enable it after Harmony and both mods.

- [UF Light Industries](https://steamcommunity.com/sharedfiles/filedetails/?id=3651799915)
- [Universal Digital Warehouse](https://steamcommunity.com/sharedfiles/filedetails/?id=3740898940)

## Behavior

The existing UF physical-material pull runs first, unchanged. If it fails, this
patch plans the complete bill using available physical stock plus digital raw
materials. It observes bill ingredient filters, warehouse output filters,
warehouse power, faction and forbidden state. Warehouses must be on the printer's
map; points retain the warehouse's existing game-wide sharing behavior.

Purchases use `DigitalWarehouseMaterialUtility.PointsForDefCount`, including the
warehouse point multiplier, buy multiplier and difficulty pricing. Physical stock
is used first for mixed recipes; non-mixed recipes choose a single allowed material
per ingredient slot with the lowest additional purchase cost. This is sequential
ingredient selection, not a global optimizer for overlapping custom filters.

Material enters the printer directly, with no pawn or ground output. A complete
transfer and UF's own ingredient validation must succeed before one final point
debit. On failure, newly created materials are removed and physical transfers are
rolled back. Successful purchases become ordinary physical ingredients inside UF's
saved container, so save/load and subsequent printing need no custom reservation
state. Cancelling a bill follows UF's normal physical-material return behavior;
there is no automatic point refund after a successful purchase.

Failed digital supply retries at most once every 600 game ticks per bill. Normal UF
pause/recheck scheduling still applies. Power or point changes can therefore take
up to a normal retry interval to be noticed. Semi-automatic mode is untouched.
Work time, quality, maintenance and final product generation remain UF behavior.

Whole-stack transformation recipes (`ignoreIngredientCountTakeEntireStacks`) use
UF's original physical-only behavior. Digital material creation is limited to
warehouse-supported, stuffless raw materials. Special recipe workers still have
to pass UF's ingredient validation; this patch does not bypass it.

## Build And Deploy

Requires .NET SDK 9 for the test runner. The mod targets .NET Framework 4.8 and
uses the same RimWorld/Harmony reference package versions as OhYouAreSoFast.
UFRecipes and warehouse assemblies are build-only references, never bundled.
Default game and Workshop locations match the local Steam installation.

```powershell
.\scripts\verify.ps1
.\scripts\build-and-deploy.ps1 -SkipBuild
```

For another installation, set MSBuild `WorkshopRoot`, or `UFAssembly` and
`WarehouseAssembly`, when building. The contract script accepts `GamePath` and
`WorkshopRoot`; deployment accepts `GameModPath`.

Deployment copies only this mod's About.xml, LoadFolders.xml and DLL, checks the
target package ID and verifies SHA-256 for every file. Exit RimWorld first.
Deployment does not enable the mod or modify ModsConfig.xml. Generated DLLs and
build intermediates are not committed.

## In-Game Acceptance

Automated verification covers numeric planning, rollback with holder doubles,
installed dependency API signatures, and metadata. It does not run a colony.

1. Enable this patch after both mods; check the log for
   `[UF Digital Patch] Automatic printer warehouse bridge installed.`
2. With no physical stock and sufficient points, queue a simple steel recipe in
   full-auto mode. Confirm materials enter the printer, points decrease once,
   and the finished product has the usual work time and quality.
3. Repeat with partial physical stock. Only the missing quantity should be bought.
4. Queue a multi-material recipe with insufficient total points. Nothing should
   be bought; add points and wait for the retry to resume production.
5. Change warehouse buy multiplier and output filters. Confirm price changes take
   effect and a disallowed material is not bought. Test a powered-off warehouse.
6. Test a made-from-stuff product with an explicit bill material filter, a mixed
   nutrition recipe, two competing printers, and repeated production.
7. Save/load during work, cancel a purchased bill, and switch to semi-auto. Check
   no duplicate charges or materials; semi-auto should behave as before.
8. If a printer remains stuck despite sufficient points and allowed materials,
   record the recipe defName, printer inspection state and Player.log. Custom
   recipe workers and other Harmony patches still need real-game compatibility
   evidence.
