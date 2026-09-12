using System.Collections.Generic;
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

        public static bool HasPending => EnemyLineUp != null && EnemyLineUp.Count > 0;

        public static void Set(List<PokemonInstance> enemyLineUp, string nodeId, bool isGym, int seed)
        {
            EnemyLineUp = enemyLineUp;
            NodeId = nodeId;
            IsGym = isGym;
            Seed = seed;
        }

        public static void Clear()
        {
            EnemyLineUp = null;
            NodeId = null;
            IsGym = false;
            Seed = 0;
        }
    }
}
