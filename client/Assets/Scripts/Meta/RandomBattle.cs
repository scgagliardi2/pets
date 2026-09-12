using System;
using System.Collections.Generic;
using Pets.Data;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>The two line-ups for a battle against a randomly rolled enemy team — what the
    /// Battle screen fights until node resolution (PLAN.md §11 item 2) hands it a real encounter.
    ///
    /// The enemy team is the same size as the player's party, drawn uniformly (repeats allowed)
    /// from the whole curated roster rather than a Location's type bias (EncounterGenerator's job),
    /// since there's no Location behind a random battle.
    ///
    /// **Abilities are deliberately switched off for now:** both sides' combatants are built with
    /// no passive, so a fight is purely the Lead-vs-Lead attack exchange, faints and promotion.
    /// That's done here, at line-up assembly on the battle's own copies (battle-sim-spec.md §2),
    /// not in the simulator, which still runs passives for anything that has one — delete the
    /// stripping when the battle screen is ready to show them.</summary>
    public sealed class RandomBattle
    {
        /// <summary>Instance-id prefix for rolled enemies, so an event can never be mistaken for one
        /// about a player mon.</summary>
        public const string EnemyInstanceIdPrefix = "enemy-";

        public List<BattleCombatant> PlayerLineUp { get; }
        public List<BattleCombatant> EnemyLineUp { get; }

        /// <summary>Rolled the enemy team, and seeds the runner's PRNG.</summary>
        public int Seed { get; }

        private RandomBattle(List<BattleCombatant> playerLineUp, List<BattleCombatant> enemyLineUp, int seed)
        {
            PlayerLineUp = playerLineUp;
            EnemyLineUp = enemyLineUp;
            Seed = seed;
        }

        /// <param name="playerLineUp">The run's line-up, in order. Copied, never modified — see
        /// BattleCombatant for why a battle can't write to the roster.</param>
        public static RandomBattle Create(IReadOnlyList<PokemonInstance> playerLineUp, PokemonSpeciesLibrary library, int seed)
        {
            if (playerLineUp == null || playerLineUp.Count == 0)
            {
                throw new ArgumentException("A battle needs at least one mon in the player's line-up.", nameof(playerLineUp));
            }

            var enemies = GenerateEnemyLineUp(library, playerLineUp.Count, seed);
            return new RandomBattle(WithoutPassives(playerLineUp), WithoutPassives(enemies), seed);
        }

        public static List<PokemonInstance> GenerateEnemyLineUp(PokemonSpeciesLibrary library, int count, int seed)
        {
            if (library == null || library.AllSpecies.Count == 0)
            {
                throw new ArgumentException("Can't roll an enemy team from an empty species library.", nameof(library));
            }

            var rng = new DeterministicRandom(seed);
            var lineUp = new List<PokemonInstance>(count);
            for (int i = 0; i < count; i++)
            {
                var species = library.AllSpecies[rng.NextInt(library.AllSpecies.Count)];
                lineUp.Add(PokemonInstanceFactory.Create(species, $"{EnemyInstanceIdPrefix}{i}"));
            }
            return lineUp;
        }

        private static List<BattleCombatant> WithoutPassives(IReadOnlyList<PokemonInstance> lineUp)
        {
            var combatants = BattleCombatant.FromLineUp(lineUp);
            foreach (var combatant in combatants)
            {
                combatant.ResolvedPassive = null;
            }
            return combatants;
        }
    }
}
