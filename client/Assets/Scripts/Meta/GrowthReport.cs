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

        /// <summary>The compact form: every stat gain on one line, then one line per evolution. For
        /// somewhere with only a couple of lines to spare — the Pokémon Center overlay. Empty when
        /// nothing changed.</summary>
        public string Describe()
        {
            var lines = new List<string>();
            if (Gains.Count > 0)
            {
                lines.Add("Grew: " + string.Join(", ", Gains.Select(g => $"{g.Name} {Line(g.To)}")));
            }
            lines.AddRange(EvolutionLines());
            return string.Join("\n", lines);
        }

        /// <summary>The long form: one line per mon saying what it actually gained — "Charmander
        /// 3/4/1 → 3/5/1  (+1 Health)" — then one line per evolution. Empty when nothing changed.
        ///
        /// Worth the extra lines wherever there's room for them (the battle result panel): a point of
        /// EXP buys Attack *or* Health by a draw the player doesn't control (ADR 0009), so which one
        /// it bought is the only part of a win that isn't already known before the fight starts. The
        /// compact <see cref="Describe"/> shows the new stat line and leaves the player to diff it
        /// against a number they no longer have in front of them.</summary>
        public IReadOnlyList<string> GainLines()
        {
            var lines = Gains.Select(g => $"{g.Name}  {Line(g.From)} → {Line(g.To)}  ({Delta(g.From, g.To)})").ToList();
            lines.AddRange(EvolutionLines());
            return lines;
        }

        /// <summary>A stat line as the game writes it everywhere: Attack/Health/Speed.</summary>
        public static string Line(Stats stats) => $"{stats.Attack}/{stats.Health}/{stats.Speed}";

        /// <summary>What changed between two stat lines, named: "+1 Health", or "+4 Attack, +3 Health"
        /// for a grant that also paid for an evolution. "no change" is unreachable from a StatGain
        /// (one is only recorded when something moved) but is the honest answer if a caller asks
        /// anyway.</summary>
        public static string Delta(Stats from, Stats to)
        {
            var parts = new List<string>();
            Add(parts, "Attack", to.Attack - from.Attack);
            Add(parts, "Health", to.Health - from.Health);
            Add(parts, "Speed", to.Speed - from.Speed);
            return parts.Count > 0 ? string.Join(", ", parts) : "no change";
        }

        private static void Add(List<string> parts, string name, int change)
        {
            if (change != 0)
            {
                parts.Add($"{(change > 0 ? "+" : "")}{change} {name}");
            }
        }

        private IEnumerable<string> EvolutionLines() =>
            Evolutions.Select(e => $"{e.FromName} evolved into {e.ToName}!");
    }
}
