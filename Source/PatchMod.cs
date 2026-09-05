using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UFRecipes;
using Verse;

namespace UFUniversalDigitalPatch
{
    public sealed class PatchMod : Mod
    {
        public PatchMod(ModContentPack content) : base(content)
        {
            var target = AccessTools.Method(typeof(AutoSequenceUtil), "TryPullIngredientsFromMap",
                new[] { typeof(Bill), typeof(Map), typeof(ThingOwner) });
            if (target == null || target.ReturnType != typeof(bool))
            {
                Log.Error("[UF Digital Patch] UF ingredient API changed; compatibility patch not installed.");
                return;
            }
            new Harmony("Nanaloveyuki.UFUniversalDigitalPatch").Patch(target,
                postfix: new HarmonyMethod(typeof(PatchMod), nameof(AfterPull)));
            Log.Message("[UF Digital Patch] Automatic printer warehouse bridge installed.");
        }

        private sealed class Retry { internal int Tick; }
        private static readonly ConditionalWeakTable<Bill, Retry> Retries = new ConditionalWeakTable<Bill, Retry>();

        private static void AfterPull(Bill bill, Map map, ThingOwner destination, ref bool __result)
        {
            if (__result || bill?.recipe == null || map == null ||
                !(destination?.Owner is Building_SuperWorkbench bench) || !bench.fullAuto ||
                !bench.Spawned || bench.Map != map || bench.Faction != Faction.OfPlayer ||
                bench.ManuallyPaused || !bench.CanRunNow || !bill.ShouldDoNow() ||
                bill.recipe.ignoreIngredientCountTakeEntireStacks) return;

            Retry retry = Retries.GetValue(bill, _ => new Retry());
            int tick = Find.TickManager.TicksGame;
            if (tick < retry.Tick) return;
            retry.Tick = tick + 600;
            try
            {
                __result = WarehouseBridge.TrySupply(bill, bench, destination);
                if (__result) Retries.Remove(bill);
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[UF Digital Patch] Automatic supply failed: " + exception, 193574801);
            }
        }
    }
}
