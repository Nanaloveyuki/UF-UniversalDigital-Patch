using System.Collections.Generic;

// Minimal holder doubles for exercising the production rollback code, not game integration.
namespace Verse
{
    public struct IntVec3 { }
    public enum ThingPlaceMode { Near }
    public sealed class ThingDef { }
    public class Thing
    {
        public bool Destroyed;
        public int stackCount;
        public ThingOwner holdingOwner;
        public IntVec3 PositionHeld;
        public object Map;
        public Thing SplitOff(int count)
        {
            if (count == stackCount)
            {
                holdingOwner?.Remove(this);
                return this;
            }
            stackCount -= count;
            return new Thing { stackCount = count };
        }
        public bool TryAbsorbStack(Thing other, bool respectStackLimit)
        {
            stackCount += other.stackCount;
            other.stackCount = 0;
            other.Destroy();
            return true;
        }
        public void Destroy() { holdingOwner?.Remove(this); Destroyed = true; }
    }
    public class ThingOwner
    {
        public readonly List<Thing> Things = new List<Thing>();
        public bool RejectNext;
        public bool TryAdd(Thing thing, bool merge)
        {
            if (RejectNext) { RejectNext = false; return false; }
            Things.Add(thing);
            thing.holdingOwner = this;
            return true;
        }
        public bool TryAddOrTransfer(Thing thing, bool merge)
        {
            thing.holdingOwner?.Remove(thing);
            return TryAdd(thing, merge);
        }
        public void Remove(Thing thing) { Things.Remove(thing); thing.holdingOwner = null; }
    }
    public static class ThingMaker { public static Thing MakeThing(ThingDef def) => new Thing(); }
    public static class GenPlace
    {
        public static bool TryPlaceThing(Thing thing, IntVec3 position, object map, ThingPlaceMode mode) => false;
    }
    public static class Log { public static void Error(string message) => System.Console.WriteLine(message); }
}
