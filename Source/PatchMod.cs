using System;
using System.Collections.Generic;
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
            var target = AccessTools.Method(typeof(AutoSequenceUtil), "TryPullIngredientsFromCandidates",
                new[] { typeof(Bill), typeof(List<Thing>), typeof(Map), typeof(ThingOwner), typeof(bool).MakeByRefType() });
            if (target == null || target.ReturnType != typeof(bool))
            {
                Log.Error("[UF Digital Patch] UF ingredient API changed; compatibility patch not installed.");
                return;
            }
            new Harmony("Nanaloveyuki.UFUniversalDigitalPatch").Patch(target,
                postfix: new HarmonyMethod(typeof(PatchMod), nameof(AfterCandidates)));
            Log.Message("[UF Digital Patch] Automatic printer warehouse bridge installed on TryPullIngredientsFromCandidates (idle and ready paths; supply diagnostics enabled).");
        }

        private sealed class Retry
        {
            internal int Tick;
            internal string LastStatus;
        }
        private static readonly ConditionalWeakTable<Bill, Retry> Retries = new ConditionalWeakTable<Bill, Retry>();

        private static void AfterCandidates(Bill bill, Map map, ThingOwner destination,
            ref bool transferAttempted, ref bool __result)
        {
            if (__result) return;
            AfterPull(bill, map, destination, ref __result);
            // UF uses this flag to invalidate its shared physical-material candidate list.
            if (__result) transferAttempted = true;
        }

        private static void AfterPull(Bill bill, Map map, ThingOwner destination, ref bool __result)
        {
            if (__result || bill?.recipe == null || map == null ||
                !(destination?.Owner is Building_SuperWorkbench bench)) return;

            Retry retry = Retries.GetValue(bill, _ => new Retry());
            int tick = Find.TickManager.TicksGame;
            if (tick < retry.Tick) return;
            retry.Tick = tick + 600;
            try
            {
                string status;
                if (!bench.fullAuto) status = "blocked: printer is not in full-auto mode";
                else if (!bench.Spawned || bench.Map != map || bench.Faction != Faction.OfPlayer)
                    status = "blocked: printer is not spawned on the player's map";
                else if (bench.ManuallyPaused) status = "blocked: printer manually paused";
                else if (!bench.CanRunNow) status = "blocked: UF CanRunNow=false (power, flick switch or breakdown)";
                else if (!bill.ShouldDoNow()) status = "blocked: bill ShouldDoNow=false";
                else if (bill.recipe.ignoreIngredientCountTakeEntireStacks) status = "blocked: whole-stack recipe requires physical input";
                else __result = WarehouseBridge.TrySupply(bill, bench, destination, out status);

                if (retry.LastStatus != status)
                {
                    retry.LastStatus = status;
                    Log.Message($"[UF Digital Patch] {bench.ThingID} bill={bill.recipe.defName}: {status}");
                }
                if (__result) retry.Tick = 0;
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[UF Digital Patch] Automatic supply failed: " + exception, 193574801);
            }
        }
    }
}
