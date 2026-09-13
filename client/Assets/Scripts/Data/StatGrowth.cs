using System;
using Pets.Simulation;

namespace Pets.Data
{
    /// <summary>What a species' stats are at a given amount of EXP — the one formula every
    /// PokemonInstance's CurrentStats comes from (ADR 0008). Lives in Data rather than next to the
    /// EXP rules in Pets.Meta.ExperienceResolver because PokemonInstanceFactory has to build a fresh
    /// mon with it, and Data can't see Meta.
    ///
    /// **One point of EXP is +1 Attack and +1 Health. That is the whole model.** A Charmander is
    /// 2/2/1, wins a fight and is 3/3/1. There is no curve, no percentage and no per-species
    /// weighting: a species' identity is its tier line (Pets.Data.SpeciesTier), and growth on top of
    /// it is the same for everyone, which is what makes a card readable at a glance. ADR 0007's
    /// proportional curve did the opposite — it needed a level, a rising cost table and a Health
    /// multiplier before anyone could say why a mon had the numbers it had.
    ///
    /// **Speed never grows.** It only changes when the mon evolves into a species the tier table
    /// gives more Speed to, which is rare by design. Speed drives the charge meter against a
    /// three-point threshold (battle-sim-spec.md §4), so a Speed that crept up with EXP would have
    /// every mon firing its passive every Step by mid-run — the concern ADR 0006 raised and ADR 0007
    /// left open, settled here.
    ///
    /// **A consequence worth knowing:** Attack and Health grow in lockstep, so the number of Steps a
    /// mon survives against an equal opponent never changes — a 2/2/1 and a 40/40/1 both fall in one
    /// exchange. Fights stay about one Step per mon for the whole run. That is what makes Speed and
    /// passives the things that decide a fight, and it is the first knob to look at when balancing
    /// (PLAN.md §10).</summary>
    public static class StatGrowth
    {
        /// <summary>Attack a point of EXP is worth.</summary>
        public const int AttackPerExp = 1;

        /// <summary>Health a point of EXP is worth.</summary>
        public const int HealthPerExp = 1;

        /// <summary>The species' own tier line, before any EXP.</summary>
        public static Stats BaseOf(PokemonSpeciesDefinitionAsset species) => new Stats
        {
            Attack = species.BaseAttack,
            Health = species.BaseHealth,
            Speed = species.BaseSpeed,
        };

        /// <summary>A species' stats carrying <paramref name="exp"/> points of EXP.</summary>
        public static Stats AtExp(PokemonSpeciesDefinitionAsset species, int exp)
        {
            int points = Math.Max(0, exp);
            return new Stats
            {
                Attack = species.BaseAttack + AttackPerExp * points,
                Health = species.BaseHealth + HealthPerExp * points,
                Speed = species.BaseSpeed,
            };
        }
    }
}
