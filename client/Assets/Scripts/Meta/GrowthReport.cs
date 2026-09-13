using System.Collections.Generic;
using System.Linq;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>What one grant of EXP did — levels gained and evolutions set off — so the screen that
    /// granted it can tell the player. Every way a run grows (a win, a rest, a combine) returns one.</summary>
    public sealed class GrowthReport
    {
        public readonly struct LevelUp
        {
            public readonly PokemonInstance Mon;

            /// <summary>The mon's name before it grew, so a level-up and an evolution in the same
            /// report read in order ("Charmander Lv 8", then "Charmander evolved into Charmeleon").</summary>
            public readonly string Name;

            public readonly int FromLevel;
            public readonly int ToLevel;

            public LevelUp(PokemonInstance mon, string name, int fromLevel, int toLevel)
            {
                Mon = mon;
                Name = name;
                FromLevel = fromLevel;
                ToLevel = toLevel;
            }
        }

        /// <summary>EXP each mon was granted. Zero for a report built only from catch-up.</summary>
        public int ExpGranted;

        public readonly List<LevelUp> LevelUps = new List<LevelUp>();
        public readonly List<ExperienceResolver.Evolution> Evolutions = new List<ExperienceResolver.Evolution>();

        public void Merge(GrowthReport other)
        {
            if (other == null)
            {
                return;
            }
            LevelUps.AddRange(other.LevelUps);
            Evolutions.AddRange(other.Evolutions);
        }

        /// <summary>Player-facing lines: every level-up on one line (a six-mon party would otherwise
        /// push everything else off a result panel), then one line per evolution. Empty when nothing
        /// changed.</summary>
        public string Describe()
        {
            var lines = new List<string>();
            if (LevelUps.Count > 0)
            {
                lines.Add("Level up: " + string.Join(", ", LevelUps.Select(l => $"{l.Name} Lv {l.ToLevel}")));
            }
            foreach (var evolution in Evolutions)
            {
                lines.Add($"{evolution.FromName} evolved into {evolution.ToName}!");
            }
            return string.Join("\n", lines);
        }
    }
}
