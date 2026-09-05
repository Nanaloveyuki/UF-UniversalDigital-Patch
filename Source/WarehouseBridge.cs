using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UFRecipes;
using UniversalDigitalWarehouseLib;
using Verse;

namespace UFUniversalDigitalPatch
{
    internal static class WarehouseBridge
    {
        private sealed class Material
        {
            internal ThingDef Def;
            internal readonly List<Thing> Things = new List<Thing>();
            internal Building_DigitalWarehouse Warehouse;
            internal double Price = double.PositiveInfinity;
        }

        internal static bool TrySupply(Bill bill, Building_SuperWorkbench bench, ThingOwner destination)
        {
            var points = GameComponent_DigitalWarehousePoints.Instance;
            if (points == null) return false;
            List<Building_DigitalWarehouse> warehouses = bench.Map.listerBuildings.allBuildingsColonist
                .OfType<Building_DigitalWarehouse>().Where(IsUsable).OrderBy(w => w.thingIDNumber).ToList();
            if (warehouses.Count == 0) return false;

            var materials = new List<Material>();
            var byDef = new Dictionary<ThingDef, Material>();
            foreach (Thing thing in CollectPhysical(bill, bench, destination))
            {
                if (!byDef.TryGetValue(thing.def, out Material material))
                {
                    material = new Material { Def = thing.def };
                    materials.Add(material);
                    byDef.Add(thing.def, material);
                }
                material.Things.Add(thing);
            }

            foreach (ThingDef def in DigitalWarehouseCategoryUtility.AllowedDefsForReading())
            {
                // The warehouse itself creates raw, stuffless materials, not arbitrary equipment.
                if (def.MadeFromStuff || !bill.IsFixedOrAllowedIngredient(def)) continue;
                foreach (Building_DigitalWarehouse warehouse in warehouses)
                {
                    if (!warehouse.CanValidateOutputFor(def)) continue;
                    float price = DigitalWarehouseMaterialUtility.PointsForDefCount(def, 1, warehouse.PointMultiplier);
                    if (float.IsNaN(price) || float.IsInfinity(price) || price < 0) continue;
                    if (!byDef.TryGetValue(def, out Material material))
                    {
                        material = new Material { Def = def };
                        materials.Add(material);
                        byDef.Add(def, material);
                    }
                    if (price < material.Price)
                    {
                        material.Price = price;
                        material.Warehouse = warehouse;
                    }
                }
            }

            var requirements = new List<double[]>();
            foreach (IngredientCount ingredient in bill.recipe.ingredients)
            {
                double amount = bill.recipe.Worker.GetIngredientCount(ingredient, bill);
                if (amount <= 0) continue;
                var required = new double[materials.Count];
                for (int i = 0; i < materials.Count; i++)
                {
                    ThingDef def = materials[i].Def;
                    if (!ingredient.filter.Allows(def) || (!ingredient.IsFixedIngredient && !bill.ingredientFilter.Allows(def)))
                        continue;
                    if (bill.recipe.allowMixingIngredients)
                    {
                        double value = bill.recipe.IngredientValueGetter.ValuePerUnitOf(def);
                        if (value > 0) required[i] = amount / value;
                    }
                    else required[i] = ingredient.CountRequiredOfFor(def, bill.recipe, bill);
                }
                requirements.Add(required);
            }

            int[] available = materials.Select(m => checked(m.Things.Sum(t => t.stackCount))).ToArray();
            IngredientPlan plan = IngredientPlan.Create(available, materials.Select(m => m.Price).ToArray(),
                requirements, bill.recipe.allowMixingIngredients, double.PositiveInfinity);
            if (plan == null) return false;

            // Requote aggregate counts through the original API (including its float rounding).
            float cost = 0;
            for (int i = 0; i < materials.Count; i++)
            {
                if (plan.Buy[i] == 0) continue;
                Material material = materials[i];
                if (!IsUsable(material.Warehouse) || !material.Warehouse.CanValidateOutputFor(material.Def)) return false;
                cost += DigitalWarehouseMaterialUtility.PointsForDefCount(material.Def, plan.Buy[i], material.Warehouse.PointMultiplier);
            }
            if (float.IsNaN(cost) || float.IsInfinity(cost) || cost < 0 || cost > points.StoredPoints + 0.0001f) return false;

            using (var transfer = new SupplyTransfer(destination, bench))
            {
                for (int i = 0; i < materials.Count; i++)
                {
                    int remaining = plan.Take[i];
                    foreach (Thing thing in materials[i].Things)
                    {
                        int count = Math.Min(remaining, thing.stackCount);
                        if (count > 0) transfer.Take(thing, count);
                        remaining -= count;
                        if (remaining == 0) break;
                    }
                    if (remaining != 0) return false;
                    remaining = plan.Buy[i];
                    while (remaining > 0)
                    {
                        int count = Math.Min(remaining, Math.Max(1, materials[i].Def.stackLimit));
                        transfer.Create(materials[i].Def, count);
                        remaining -= count;
                    }
                }
                // Use UF's own selector as the final authority on special filters and recipe rules.
                // No debit occurs until all transfers and this validation have succeeded.
                if (!AutoSequenceUtil.HasIngredients(bill, destination) || !points.TrySpendPoints(cost)) return false;
                transfer.Committed = true;
                return true;
            }
        }

        private static bool IsUsable(Building_DigitalWarehouse warehouse)
        {
            return warehouse != null && warehouse.Spawned && !warehouse.Destroyed &&
                warehouse.Faction == Faction.OfPlayer && !warehouse.IsForbidden(Faction.OfPlayer) &&
                (warehouse.TryGetComp<CompPowerTrader>()?.PowerOn ?? true);
        }

        private static List<Thing> CollectPhysical(Bill bill, Building_SuperWorkbench bench, ThingOwner destination)
        {
            Map map = bench.Map;
            var found = new List<Thing>();
            var seen = new HashSet<Thing>();
            var reserved = new HashSet<Thing>(map.reservationManager.AllReservedThings());
            void Add(IEnumerable<Thing> things)
            {
                foreach (Thing thing in things)
                {
                    if (thing == null || thing.Destroyed || !seen.Add(thing) || reserved.Contains(thing) ||
                        !bill.IsFixedOrAllowedIngredient(thing) || thing.IsForbidden(Faction.OfPlayer) ||
                        Patch_BillMaterialLocks.IsLockedBillMaterial(thing)) continue;
                    if (thing.Spawned || (thing.ParentHolder != null && ThingOwnerUtility.GetRootMap(thing.ParentHolder) == map))
                        found.Add(thing);
                }
            }
            Add(destination);
            var held = new List<Thing>();
            foreach (IHaulSource source in map.haulDestinationManager.AllHaulSourcesListForReading)
            {
                if (!source.HaulSourceEnabled || !(source is Thing thing) || !thing.Spawned || thing.IsForbidden(Faction.OfPlayer)) continue;
                held.Clear();
                ThingOwnerUtility.GetAllThingsRecursively(source, held);
                Add(held);
            }
            foreach (SlotGroup group in map.haulDestinationManager.AllGroupsListForReading) Add(group.HeldThings);
            return found.OrderBy(t => t.holdingOwner == destination ? 0 : 1)
                .ThenBy(t => t.PositionHeld.DistanceToSquared(bench.Position)).ThenBy(t => t.thingIDNumber).ToList();
        }
    }
}
