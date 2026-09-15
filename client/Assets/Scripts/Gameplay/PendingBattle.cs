using System.Collections.Generic;
using Pets.Meta;
using Pets.Simulation;

namespace Pets.Gameplay
{
    /// <summary>Scene-to-scene handoff for a fight a map node started (Map -&gt; Battle), in the same
    /// spirit as PendingRunSelection: plain static state, read and cleared by the Battle screen on
    /// its next Start.
    ///
    /// Its presence is also what tells the Battle screen *which* fight it is. With nothing pending,
    /// the screen rolls the random dev battle it has always rolled (Team's "Dev: Random Battle");
    /// with something pending, it fights this line-up and writes the result back to the run —
    /// Morale, EXP, a catch. See BattleScreenController.</summary>
    public static class PendingBattle
    {
        /// <summary>The opposing line-up, in order, with its passives intact (unlike the dev random
        /// battle, which strips them).</summary>
        public static List<PokemonInstance> EnemyLineUp;

        /// <summary>The map node that started this fight, so a Gym retry can re-roll the same
        /// node's encounter.</summary>
        public static string NodeId;

        /// <summary>A Gym finale rather than a wild PvE encounter: no catching, bigger EXP, and
        /// winning it completes the Location (design doc §14).</summary>
        public static bool IsGym;

        /// <summary>Seeds the fight itself, and is the same seed the encounter was rolled from — so
        /// the node and the battle it leads to are one reproducible unit (design doc §10.5).</summary>
        public static int Seed;

        /// <summary>Set for an Event node's Legendary fight (Meta/RoadEvents): what beating it pays on
        /// top of an ordinary win. Its presence is also what tells the Battle screen the fight is a
        /// Legendary's — no catching, and no Gym rules.</summary>
        public static LegendaryBounty Bounty;

        public static bool HasPending => EnemyLineUp != null && EnemyLineUp.Count > 0;

        public static void Set(List<PokemonInstance> enemyLineUp, string nodeId, bool isGym, int seed)
        {
            EnemyLineUp = enemyLineUp;
            NodeId = nodeId;
            IsGym = isGym;
            Seed = seed;
            Bounty = null;
        }

        public static void SetLegendary(List<PokemonInstance> enemyLineUp, string nodeId, int seed, LegendaryBounty bounty)
        {
            Set(enemyLineUp, nodeId, isGym: false, seed);
            Bounty = bounty;
        }

        public static void Clear()
        {
            EnemyLineUp = null;
            NodeId = null;
            IsGym = false;
            Seed = 0;
            Bounty = null;
        }
    }
}
