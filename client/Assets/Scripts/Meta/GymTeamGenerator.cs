using System;
using System.Collections.Generic;
using Pets.Data;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>Builds the Gym Leader's team for a Location's mandatory finale (design doc §4, §14).
    ///
    /// Two things make it a boss rather than one more wild encounter: it's drawn from the whole
    /// curated roster instead of the Location's type bias (a Leader's team isn't local wildlife —
    /// EncounterGenerator is what a PvE node uses), and every member carries
    /// <see cref="HealthBonusPercent"/> more HP, so the fight lasts longer than the ones leading up
    /// to it. That percentage is a placeholder balance knob in exactly the sense CampResolver's buff
    /// is — the real pass over stats and difficulty is Phase 4 (PLAN.md).</summary>
    public static class GymTeamGenerator
    {
        /// <summary>Extra HP each Gym member gets over its authored base, as a fraction.</summary>
        public const float HealthBonusPercent = 0.25f;

        /// <summary>Instance-id prefix for Gym members, so an event about one can't be mistaken for
        /// one about a player's mon or a wild encounter.</summary>
        public const string InstanceIdPrefix = "gym-";

        public static List<PokemonInstance> Generate(PokemonSpeciesLibrary library, int count, int seed)
        {
            if (library == null || library.AllSpecies.Count == 0)
            {
                throw new ArgumentException("Can't build a Gym team from an empty species library.", nameof(library));
            }
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "A Gym team needs at least one mon.");
            }

            var rng = new DeterministicRandom(seed);
            var team = new List<PokemonInstance>(count);
            for (int i = 0; i < count; i++)
            {
                var species = library.AllSpecies[rng.NextInt(library.AllSpecies.Count)];
                var mon = PokemonInstanceFactory.Create(species, $"{InstanceIdPrefix}{i}");
                mon.CurrentStats.Health += (int)(mon.CurrentStats.Health * HealthBonusPercent);
                mon.CurrentHP = mon.CurrentStats.Health;
                team.Add(mon);
            }
            return team;
        }
    }
}
