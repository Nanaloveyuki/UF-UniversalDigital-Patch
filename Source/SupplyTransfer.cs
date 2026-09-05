using System;
using System.Collections.Generic;
using Verse;

namespace UFUniversalDigitalPatch
{
    internal sealed class SupplyTransfer : IDisposable
    {
        private sealed class Move
        {
            internal Thing Original;
            internal Thing Part;
            internal ThingOwner Owner;
            internal IntVec3 Position;
        }

        private readonly ThingOwner destination;
        private readonly Thing bench;
        private readonly List<Move> moves = new List<Move>();
        private readonly List<Thing> created = new List<Thing>();
        internal bool Committed;

        internal SupplyTransfer(ThingOwner destination, Thing bench)
        {
            this.destination = destination;
            this.bench = bench;
        }

        internal void Take(Thing thing, int count)
        {
            if (thing.Destroyed || count <= 0 || count > thing.stackCount)
                throw new InvalidOperationException("Selected ingredient changed before transfer.");
            if (thing.holdingOwner == destination) return;
            var move = new Move { Original = thing, Owner = thing.holdingOwner, Position = thing.PositionHeld };
            move.Part = thing.SplitOff(count);
            moves.Add(move);
            if (move.Part == null || !destination.TryAddOrTransfer(move.Part, false))
                throw new InvalidOperationException("Could not transfer a physical ingredient.");
        }

        internal void Create(ThingDef def, int count)
        {
            Thing thing = ThingMaker.MakeThing(def);
            created.Add(thing);
            thing.stackCount = count;
            if (!destination.TryAdd(thing, false))
                throw new InvalidOperationException("Could not insert a reconstructed ingredient.");
        }

        public void Dispose()
        {
            if (Committed) return;
            foreach (Thing thing in created)
            {
                if (thing.Destroyed) continue;
                thing.holdingOwner?.Remove(thing);
                thing.Destroy();
            }
            for (int i = moves.Count - 1; i >= 0; i--)
            {
                Move move = moves[i];
                Thing part = move.Part;
                if (part == null || part.Destroyed) continue;
                part.holdingOwner?.Remove(part);
                if (move.Original != part && !move.Original.Destroyed && move.Original.TryAbsorbStack(part, false)) continue;
                if (move.Owner != null && move.Owner.TryAddOrTransfer(part, false)) continue;
                if (GenPlace.TryPlaceThing(part, move.Position, bench.Map, ThingPlaceMode.Near)) continue;
                // Never destroy paid physical stock if its original holder has become unavailable.
                destination.TryAddOrTransfer(part, false);
                Log.Error("[UF Digital Patch] Could not restore an ingredient to its source; retained at printer: " + part);
            }
        }
    }
}
