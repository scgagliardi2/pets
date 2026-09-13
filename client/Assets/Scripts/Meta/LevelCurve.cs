using System;

namespace Pets.Meta
{
    /// <summary>What a level costs, and therefore how fast a run grows (ADR 0006). The pacing half
    /// of the growth model; Pets.Data.StatGrowth is the stats half.
    ///
    /// **The curve is sized against the map generator's actual output, not guessed.** A Location is
    /// five choice layers plus a Gym (RegionMapGenerator), and its node-type weights pay an expected
    /// ~22 EXP per Location at BattleRewardResolver's rates. Six Locations is therefore ~132 EXP,
    /// which is exactly <see cref="TotalExpForLevel"/>(<see cref="MaxLevel"/>) — so a run that
    /// walks six Locations finishes at the cap, and one that walks five finishes a level or two
    /// short. That relationship is the design, and GrowthAndEvolutionTests asserts it rather than leaving it
    /// to drift the first time a node weight changes.
    ///
    /// Costs rise with level (<see cref="ExpToAdvanceFrom"/> is `6 + level`, so 7 EXP for the
    /// first level and 17 for the last) against a roughly flat supply, which front-loads the run:
    /// about two and a half levels in the first Location, about one and a half in the sixth.</summary>
    public static class LevelCurve
    {
        /// <summary>Where every mon starts, and the floor under any level arithmetic.</summary>
        public const int StartingLevel = Pets.Data.StatGrowth.StartingLevel;

        /// <summary>The cap. Reached at the end of a full six-Location run and not before — a mon
        /// at 12 has roughly 2.1x its species' base Attack and Health, on top of whatever its
        /// evolutions did to that base.</summary>
        public const int MaxLevel = 12;

        /// <summary>The constant in the per-level cost. See the class remarks for why it's this
        /// and not something rounder: it's what makes six Locations' EXP land on
        /// <see cref="MaxLevel"/>.</summary>
        public const int ExpCostBase = 6;

        /// <summary>EXP needed to go from <paramref name="level"/> to the next one. Rising, so
        /// early levels arrive fast and late ones are earned.</summary>
        public static int ExpToAdvanceFrom(int level) => ExpCostBase + Math.Max(StartingLevel, level);

        /// <summary>Total EXP a mon must have earned to be <paramref name="level"/>. 0 for
        /// <see cref="StartingLevel"/>; 132 for <see cref="MaxLevel"/>.</summary>
        public static int TotalExpForLevel(int level)
        {
            int total = 0;
            for (int l = StartingLevel; l < level; l++)
            {
                total += ExpToAdvanceFrom(l);
            }
            return total;
        }

        /// <summary>The level <paramref name="exp"/> buys, capped at <see cref="MaxLevel"/>. EXP
        /// past the cap is simply banked and does nothing — deliberately, rather than being refused
        /// at the source: a mon that caps in Region 5 should still be paid for the fights it wins in
        /// Region 6 without every award site needing to know about the cap.</summary>
        public static int LevelForExp(int exp)
        {
            int level = StartingLevel;
            int spent = 0;
            while (level < MaxLevel)
            {
                int cost = ExpToAdvanceFrom(level);
                if (exp < spent + cost)
                {
                    break;
                }
                spent += cost;
                level++;
            }
            return level;
        }

        /// <summary>How far into its current level <paramref name="exp"/> is, and what the level
        /// costs — the "7 / 11" a Team card shows. At <see cref="MaxLevel"/> the cost is 0 and the
        /// caller should show the mon as capped rather than drawing an empty bar.</summary>
        public static (int into, int cost) ProgressInLevel(int exp)
        {
            int level = LevelForExp(exp);
            if (level >= MaxLevel)
            {
                return (0, 0);
            }
            return (exp - TotalExpForLevel(level), ExpToAdvanceFrom(level));
        }
    }
}
