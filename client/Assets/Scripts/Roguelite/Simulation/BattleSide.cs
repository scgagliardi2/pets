using System.Collections.Generic;

namespace Pets.Roguelite.Simulation
{
    /// <summary>
    /// One side's ordered line-up. Index 0 is the Lead, index 1 is the Support, everything past
    /// that is dormant (no stats/charge/passive processing — docs/battle-sim-spec.md §2). Removing
    /// a fainted entry at index 0 or 1 naturally promotes the next entry up, since this is just an
    /// ordered list.
    /// </summary>
    public sealed class BattleSide
    {
        public List<BattleMon> LineUp = new List<BattleMon>();

        public BattleMon Lead => LineUp.Count > 0 ? LineUp[0] : null;
        public BattleMon Support => LineUp.Count > 1 ? LineUp[1] : null;

        public BattleSide()
        {
        }

        public BattleSide(params BattleMon[] mons)
        {
            LineUp = new List<BattleMon>(mons);
        }

        /// <summary>The other currently-active mon on this side, or null (no Support, or `mon` isn't active).</summary>
        public BattleMon GetAllyOf(BattleMon mon)
        {
            if (Lead == mon)
            {
                return Support;
            }
            if (Support == mon)
            {
                return Lead;
            }
            return null;
        }

        /// <summary>
        /// Removes any fainted mon from the active slots (index 0/1), cascading promotions as
        /// needed — see docs/battle-sim-spec.md §3 step 4. Logs a Faint StepEvent per removal.
        /// </summary>
        public void PromoteAndRemoveFainted(List<StepEvent> events, SideId sideId)
        {
            for (int i = 0; i < 2 && i < LineUp.Count;)
            {
                var mon = LineUp[i];
                if (mon.CurrentHP <= 0)
                {
                    events.Add(new StepEvent
                    {
                        Kind = StepEventKind.Faint,
                        SourceSide = sideId,
                        SourceName = mon.DisplayName,
                    });
                    LineUp.RemoveAt(i);
                    // Don't increment i — whatever shifted into this slot needs the same check.
                }
                else
                {
                    i++;
                }
            }
        }
    }
}
