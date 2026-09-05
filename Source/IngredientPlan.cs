using System;
using System.Collections.Generic;

namespace UFUniversalDigitalPatch
{
    // Numeric planning is independent of live Things: failed quotes cannot consume items or points.
    internal sealed class IngredientPlan
    {
        internal readonly int[] Take;
        internal readonly int[] Buy;
        internal double Cost;

        private IngredientPlan(int count)
        {
            Take = new int[count];
            Buy = new int[count];
        }

        // Each row gives the required units of each candidate; zero means disallowed.
        internal static IngredientPlan Create(int[] available, double[] prices,
            IList<double[]> requirements, bool allowMixing, double budget)
        {
            var plan = new IngredientPlan(available.Length);
            foreach (double[] required in requirements)
            {
                if (!allowMixing)
                {
                    int best = -1;
                    double bestCost = double.PositiveInfinity;
                    for (int i = 0; i < available.Length; i++)
                    {
                        if (!ValidRequirement(required[i])) continue;
                        int count = (int)Math.Ceiling(required[i]);
                        int buy = Math.Max(0, count - (available[i] - plan.Take[i]));
                        double cost = buy == 0 ? 0 : buy * prices[i];
                        if (cost < bestCost)
                        {
                            best = i;
                            bestCost = cost;
                        }
                    }
                    if (best < 0) return null;
                    int needed = (int)Math.Ceiling(required[best]);
                    int take = Math.Min(needed, available[best] - plan.Take[best]);
                    plan.Take[best] += take;
                    plan.Buy[best] = checked(plan.Buy[best] + needed - take);
                    plan.Cost += bestCost;
                }
                else
                {
                    double remaining = 1;
                    for (int i = 0; i < available.Length && remaining > 0.0000001; i++)
                    {
                        if (!ValidRequirement(required[i])) continue;
                        int take = Math.Min(available[i] - plan.Take[i], Units(remaining, required[i]));
                        plan.Take[i] += take;
                        remaining -= take / required[i];
                    }
                    if (remaining > 0.0000001)
                    {
                        int best = -1;
                        double bestCost = double.PositiveInfinity;
                        for (int i = 0; i < available.Length; i++)
                        {
                            if (!ValidRequirement(required[i])) continue;
                            double cost = Units(remaining, required[i]) * prices[i];
                            if (cost < bestCost)
                            {
                                best = i;
                                bestCost = cost;
                            }
                        }
                        if (best < 0) return null;
                        plan.Buy[best] = checked(plan.Buy[best] + Units(remaining, required[best]));
                        plan.Cost += bestCost;
                    }
                }
                if (double.IsNaN(plan.Cost) || double.IsInfinity(plan.Cost) || plan.Cost > budget + 0.0001)
                    return null;
            }
            return plan;
        }

        private static bool ValidRequirement(double value) => value > 0 && value <= int.MaxValue;
        private static int Units(double fraction, double required) => (int)Math.Ceiling(fraction * required - 0.0000001);
    }
}
