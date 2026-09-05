using System;
using UFUniversalDigitalPatch;

internal static class Program
{
    private static int passed;
    private static void Main()
    {
        Check("digital-only exact budget", () =>
        {
            var p = Plan(new[] { 0 }, new[] { 2.0 }, false, 20, new[] { 10.0 });
            return p != null && p.Buy[0] == 10 && p.Take[0] == 0 && p.Cost == 20;
        });
        Check("physical stock only pays the shortfall", () =>
        {
            var p = Plan(new[] { 7 }, new[] { 2.0 }, false, 6, new[] { 10.0 });
            return p != null && p.Take[0] == 7 && p.Buy[0] == 3 && p.Cost == 6;
        });
        Check("no-mix prefers fully available material", () =>
        {
            var p = Plan(new[] { 0, 10 }, new[] { 1.0, 20.0 }, false, 0, new[] { 10.0, 10.0 });
            return p != null && p.Take[1] == 10 && p.Cost == 0;
        });
        Check("no-mix compares total purchase costs", () =>
        {
            var p = Plan(new[] { 0, 9 }, new[] { 1.0, 2.0 }, false, 2, new[] { 10.0, 10.0 });
            return p != null && p.Take[1] == 9 && p.Buy[1] == 1;
        });
        Check("no-mix cannot combine different defs", () =>
            Plan(new[] { 5, 5 }, new[] { double.PositiveInfinity, double.PositiveInfinity }, false, 0, new[] { 10.0, 10.0 }) == null);
        Check("mix uses physical nutrition before digital", () =>
        {
            var p = Plan(new[] { 5, 0 }, new[] { 100.0, 1.0 }, true, 3, new[] { 10.0, 5.0 });
            return p != null && p.Take[0] == 5 && p.Buy[1] == 3;
        });
        Check("mix can combine physical defs", () =>
        {
            var p = Plan(new[] { 5, 5 }, new[] { double.PositiveInfinity, double.PositiveInfinity }, true, 0, new[] { 10.0, 10.0 });
            return p != null && p.Take[0] == 5 && p.Take[1] == 5 && p.Cost == 0;
        });
        Check("cannot reuse a stack across ingredient slots", () =>
        {
            var p = Plan(new[] { 10 }, new[] { 1.0 }, false, 4, new[] { 7.0 }, new[] { 7.0 });
            return p != null && p.Take[0] == 10 && p.Buy[0] == 4;
        });
        Check("mix also tracks allocations across slots", () =>
        {
            var p = Plan(new[] { 10 }, new[] { 1.0 }, true, 4, new[] { 7.0 }, new[] { 7.0 });
            return p != null && p.Take[0] == 10 && p.Buy[0] == 4;
        });
        Check("entire bill must be affordable", () =>
            Plan(new[] { 0, 0 }, new[] { 1.0, 1.0 }, false, 10, new[] { 7.0, 0.0 }, new[] { 0.0, 7.0 }) == null);
        Check("output filter exclusion", () =>
            Plan(new[] { 0 }, new[] { double.PositiveInfinity }, false, 100, new[] { 1.0 }) == null);
        Check("ingredient filter exclusion", () =>
            Plan(new[] { 100 }, new[] { 0.0 }, false, 100, new[] { 0.0 }) == null);
        Check("zero buy multiplier is supported", () =>
        {
            var p = Plan(new[] { 0 }, new[] { 0.0 }, false, 0, new[] { 50.0 });
            return p != null && p.Buy[0] == 50 && p.Cost == 0;
        });
        Check("failed plans do not modify input stock", () =>
        {
            var stock = new[] { 5 };
            return Plan(stock, new[] { 1.0 }, false, 0, new[] { 10.0 }) == null && stock[0] == 5;
        });
        Check("repeat quotes do not reserve stock or charge points", () =>
        {
            var stock = new[] { 5 };
            var a = Plan(stock, new[] { 1.0 }, false, 5, new[] { 10.0 });
            var b = Plan(stock, new[] { 1.0 }, false, 5, new[] { 10.0 });
            return a.Cost == b.Cost && a.Buy[0] == b.Buy[0] && stock[0] == 5;
        });
        Console.WriteLine($"Passed {passed} planner tests.");
        TransferTests.Run();
    }

    private static IngredientPlan Plan(int[] stock, double[] prices, bool mix, double budget, params double[][] requirements)
        => IngredientPlan.Create(stock, prices, requirements, mix, budget);

    private static void Check(string name, Func<bool> test)
    {
        if (!test()) throw new Exception("FAIL: " + name);
        passed++;
        Console.WriteLine("PASS: " + name);
    }
}
