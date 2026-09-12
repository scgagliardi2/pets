using System.Collections.Generic;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>Plain-C# run state (design doc §4, §7) — no MonoBehaviour/scene dependency so it
    /// can be unit-tested and, later, serialized straight to local save. Owned by RunBootstrapper
    /// in Gameplay, mutated by the Meta resolvers.</summary>
    public sealed class RunState
    {
        /// <summary>How many mons the active line-up can hold. The line-up is a "train" (design
        /// doc §7) rather than a fixed board, so this is a carry limit, not a board size — only
        /// the front two are ever mechanically active however many are behind them.</summary>
        public const int MaxPartySize = 6;

        /// <summary>Ordered active line-up — front (index 0) is the Lead, index 1 is the Support.
        /// Only these two are ever mechanically active (design doc §7).</summary>
        public List<PokemonInstance> LineUp = new List<PokemonInstance>();

        /// <summary>Caught/adopted mons not currently in the active line-up.</summary>
        public List<PokemonInstance> Box = new List<PokemonInstance>();

        public int Money;

        /// <summary>The run's life total (design doc §4). Hitting 0 ends the run.</summary>
        public int Morale = 3;

        /// <summary>The Location's generated node-map, and where the player stands on it. Held
        /// here rather than by the map screen so it survives navigating away and back —
        /// RegionMapTraversal.ForRun binds a traversal to these.</summary>
        public RegionMap LocationMap;

        /// <summary>The nodes walked so far, oldest first. The last entry is where the player
        /// stands — RegionMapTraversal derives its position from this rather than storing it
        /// twice.</summary>
        public List<string> VisitedMapNodeIds = new List<string>();

        /// <summary>Fixed per-run seed feeding both encounter generation and battle simulation, so
        /// a run is fully reproducible end to end (design doc §10.5).</summary>
        public int RunSeed;

        /// <summary>Set by a Camp node, consumed by the next PvE fight's line-up assembly
        /// (design doc §5.1: "temporary buff for the next fight"). 0 = no active buff.</summary>
        public float NextBattleAttackBonusPercent;

        public bool IsRunOver => Morale <= 0;

        /// <summary>Swaps which of the two active mons leads. The only line-up edit the Team
        /// screen offers so far: with exactly two active slots (design doc §7) it's unambiguous,
        /// where promoting from the Box needs the Box management rules that are still Phase 1.
        /// No-op below two mons so a run that lost one doesn't need a special case at the call
        /// site.</summary>
        public void SwapLeadAndSupport()
        {
            if (LineUp.Count < 2)
            {
                return;
            }
            (LineUp[0], LineUp[1]) = (LineUp[1], LineUp[0]);
        }

        /// <summary>Moves the mon at <paramref name="fromIndex"/> of one collection into
        /// <paramref name="toIndex"/> of another (or the same one) — what a drag between two Team
        /// screen slots means. Returns false, changing nothing, when the move isn't allowed, so
        /// the caller can just snap the card back.
        ///
        /// Two cases, chosen because they're the two a player can predict from looking at the
        /// slots:
        /// - The target slot holds a mon: the two trade places. That covers every reorder,
        ///   including promoting a Box mon straight into the Lead slot.
        /// - The target slot is empty: the mon moves out of its collection and onto the end of
        ///   the target one. Both lists stay gap-free, so dropping onto the fifth empty slot of a
        ///   two-mon party lands the mon in the third — slots fill from the front.
        ///
        /// The one hard rule: a run always keeps at least one mon in the line-up. Moving the last
        /// one out to the Box is refused (swapping it for a Box mon is fine — the party still has
        /// one).</summary>
        public bool MoveMon(RosterGroup fromGroup, int fromIndex, RosterGroup toGroup, int toIndex)
        {
            var source = CollectionFor(fromGroup);
            var target = CollectionFor(toGroup);

            if (fromIndex < 0 || fromIndex >= source.Count || toIndex < 0)
            {
                return false;
            }
            if (fromGroup == toGroup && fromIndex == toIndex)
            {
                return false;
            }
            if (toGroup == RosterGroup.Party && toIndex >= MaxPartySize)
            {
                return false;
            }

            if (toIndex < target.Count)
            {
                var moved = source[fromIndex];
                source[fromIndex] = target[toIndex];
                target[toIndex] = moved;
                return true;
            }

            // Past the end of the target collection: this empties one side by one rather than
            // trading, so it's the case the party-size rules apply to.
            if (fromGroup == RosterGroup.Party && toGroup == RosterGroup.Box && source.Count <= 1)
            {
                return false;
            }
            if (toGroup == RosterGroup.Party && target.Count >= MaxPartySize)
            {
                return false;
            }

            var mon = source[fromIndex];
            source.RemoveAt(fromIndex);
            target.Add(mon);
            return true;
        }

        /// <summary>Whether the mon in <paramref name="index"/> of <paramref name="group"/> can
        /// be let go. Separate from ReleaseMon so a screen can say *why* nothing will happen
        /// before the player commits to it, rather than only refusing afterwards.</summary>
        public bool CanReleaseMon(RosterGroup group, int index)
        {
            var collection = CollectionFor(group);
            if (index < 0 || index >= collection.Count)
            {
                return false;
            }
            // Same rule the drag between collections obeys: a run always keeps someone to send
            // out. Releasing from the Box is never blocked by it.
            return group != RosterGroup.Party || collection.Count > 1;
        }

        /// <summary>Lets a mon go for good — it leaves the run entirely rather than moving to the
        /// Box (design doc §7 has no separate storage behind the Box). Irreversible, which is why
        /// the Team screen asks first; this only enforces the rules.</summary>
        public bool ReleaseMon(RosterGroup group, int index)
        {
            if (!CanReleaseMon(group, index))
            {
                return false;
            }
            CollectionFor(group).RemoveAt(index);
            return true;
        }

        public List<PokemonInstance> CollectionFor(RosterGroup group) =>
            group == RosterGroup.Party ? LineUp : Box;
    }
}
