using System;
using UFUniversalDigitalPatch;
using Verse;

internal static class TransferTests
{
    internal static void Run()
    {
        RunCase("committed supply stays inside printer", (source, destination, stock) =>
        {
            using (var transfer = new SupplyTransfer(destination, new Thing()))
            {
                transfer.Take(stock, 4);
                transfer.Create(new ThingDef(), 6);
                transfer.Committed = true;
            }
            Assert(stock.stackCount == 6 && destination.Things.Count == 2);
        });
        RunCase("validation or final debit rejection rolls back all supply", (source, destination, stock) =>
        {
            using (var transfer = new SupplyTransfer(destination, new Thing()))
            {
                transfer.Take(stock, 4);
                transfer.Create(new ThingDef(), 6);
                // Same disposal path as failed validation or insufficient points at final debit.
            }
            Assert(stock.stackCount == 10 && source.Things.Count == 1 && destination.Things.Count == 0);
        });
        RunCase("full-stack transfer returns to original holder", (source, destination, stock) =>
        {
            using (var transfer = new SupplyTransfer(destination, new Thing())) transfer.Take(stock, 10);
            Assert(stock.holdingOwner == source && stock.stackCount == 10 && destination.Things.Count == 0);
        });
        RunCase("failed physical insertion restores original stack", (source, destination, stock) =>
        {
            destination.RejectNext = true;
            bool threw = false;
            try
            {
                using (var transfer = new SupplyTransfer(destination, new Thing())) transfer.Take(stock, 4);
            }
            catch (InvalidOperationException) { threw = true; }
            Assert(threw && stock.stackCount == 10 && destination.Things.Count == 0);
        });
        RunCase("failed reconstructed insertion restores physical stock", (source, destination, stock) =>
        {
            bool threw = false;
            try
            {
                using (var transfer = new SupplyTransfer(destination, new Thing()))
                {
                    transfer.Take(stock, 4);
                    destination.RejectNext = true;
                    transfer.Create(new ThingDef(), 6);
                }
            }
            catch (InvalidOperationException) { threw = true; }
            Assert(threw && stock.stackCount == 10 && destination.Things.Count == 0);
        });
        RunCase("existing printer stock is never split or removed", (source, destination, stock) =>
        {
            destination.TryAddOrTransfer(stock, false);
            using (var transfer = new SupplyTransfer(destination, new Thing())) transfer.Take(stock, 4);
            Assert(stock.holdingOwner == destination && stock.stackCount == 10 && destination.Things.Count == 1);
        });
        Console.WriteLine("Passed 6 transfer tests using holder doubles (not game integration).");
    }

    private static void RunCase(string name, Action<ThingOwner, ThingOwner, Thing> test)
    {
        var source = new ThingOwner();
        var destination = new ThingOwner();
        var stock = new Thing { stackCount = 10 };
        source.TryAdd(stock, false);
        test(source, destination, stock);
        Console.WriteLine("PASS: " + name);
    }
    private static void Assert(bool result) { if (!result) throw new Exception("Transfer test failed."); }
}
