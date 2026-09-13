using System.Collections.Generic;
using System.Linq;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>What one grant of EXP did — the stat lines it moved and the evolutions it set off —
    /// so the screen that granted it can tell the player. Every way a run grows (a win, a rest, a
    /// combine) returns one.</summary>
    public sealed class GrowthReport
    {
        /// <summary>One mon's stat line before and after. There are no levels to report any more
        /// (ADR 0008): a point of EXP is +1 Attack and +1 Health, so the stat line *is* the news.</summary>
        public readonly struct StatGain
        {
            public readonly PokemonInstance Mon;

            /// <summary>The mon's name before it grew, so a gain and an evolution in the same report
            /// read in order ("Charmander 7/7/1", then "Charmander evolved into Charmeleon").</summary>
            public readonly string Name;

            public readonly Stats From;
            public readonly Stats To;

            public StatGain(PokemonInstance mon, string name, Stats from, Stats to)
            {
                Mon = mon;
                Name = name;
                From = from;
                To = to;
            }
        }

        /// <summary>EXP each mon was granted. Zero for a report built only from catch-up.</summary>
        public int ExpGranted;

        public readonly List<StatGain> Gains = new List<StatGain>();
        public readonly List<ExperienceResolver.Evolution> Evolutions = new List<ExperienceResolver.Evolution>();

        public void Merge(GrowthReport other)
        {
            if (other == null)
            {
                return;
            }
            Gains.AddRange(other.Gains);
            Evolutions.AddRange(other.Evolutions);
        }

        /// <summary>Player-facing lines: every stat gain on one line (a six-mon party would otherwise
        /// push everything else off a result panel), then one line per evolution. Empty when nothing
        /// changed.</summary>
        public string Describe()
        {
            var lines = new List<string>();
            if (Gains.Count > 0)
            {
                lines.Add("Grew: " + string.Join(", ", Gains.Select(g => $"{g.Name} {Line(g.To)}")));
            }
            foreach (var evolution in Evolutions)
            {
                lines.Add($"{evolution.FromName} evolved into {evolution.ToName}!");
            }
            return string.Join("\n", lines);
        }

        /// <summary>A stat line as the game writes it everywhere: Attack/Health/Speed.</summary>
        public static string Line(Stats stats) => $"{stats.Attack}/{stats.Health}/{stats.Speed}";
    }
}
