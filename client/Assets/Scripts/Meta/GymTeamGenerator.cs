using System;
using System.Collections.Generic;
using Pets.Data;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>Builds the Gym Leader's team for a Location's mandatory finale (design doc §4, §14).
    ///
    /// A Leader specialises in their Location's types, the way mainline Gym Leaders do, so the team
    /// draws from the same type-biased pool as the wild encounters (EncounterPool). What makes it the
    /// boss is level and size: it sits above the Location's baseline and fields at least as many mons
    /// as the player brings (RunProgression.GymLevel, GymTeamSize). The final Leader may field a
    /// Legendary.
    ///
    /// There's no extra Health bonus any more: with the team matched to the player's line-up, the old
    /// +25% made every Gym a coin flip in the run simulations (ADR 0007).</summary>
    public static class GymTeamGenerator
    {
        /// <summary>Instance-id prefix for Gym members, so an event about one can't be mistaken for
        /// one about a player's mon or a wild encounter.</summary>
        public const string InstanceIdPrefix = "gym-";

        public static List<PokemonInstance> Generate(PokemonSpeciesLibrary library, PokemonType[] typeBias,
            int badges, int lineUpCount, int seed)
        {
            if (library == null || library.AllSpecies.Count == 0)
            {
                throw new ArgumentException("Can't build a Gym team from an empty species library.", nameof(library));
            }

            var pool = EncounterPool.For(library, typeBias, badges, isGym: true);
            int size = RunProgression.GymTeamSize(badges, lineUpCount);
            int level = RunProgression.GymLevel(badges);
            var rng = new DeterministicRandom(seed);
            var team = new List<PokemonInstance>(size);
            for (int i = 0; i < size; i++)
            {
                var species = pool[rng.NextInt(pool.Count)];
                team.Add(ExperienceResolver.CreateAtLevel(species, $"{InstanceIdPrefix}{i}", level, library));
            }
            return team;
        }
    }
}
