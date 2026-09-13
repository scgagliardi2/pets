using System;
using Pets.Simulation;

namespace Pets.Data
{
    /// <summary>What a mon's stats are, given the species it started as, the EXP it has earned and
    /// the evolutions behind it — the one formula every PokemonInstance's CurrentStats comes from
    /// (ADR 0008, retuned in ADR 0009). Lives in Data rather than next to the EXP rules in
    /// Pets.Meta.ExperienceResolver because PokemonInstanceFactory has to build a fresh mon with it,
    /// and Data can't see Meta.
    ///
    /// **A point of EXP is +1 Attack *or* +1 Health, never both.** Which one is a draw against the
    /// species' own <see cref="PokemonSpeciesDefinitionAsset.HealthGrowthPercent"/>
    /// (Pets.Data.SpeciesTier) — a Metapod puts 86% of its points into Health, a Charmander 72%, and
    /// Shedinja never gains a single one. That is what keeps two mons of the same tier from growing
    /// into the same mon.
    ///
    /// **The draw is deterministic, not random.** It is a pure hash of the mon's instance id and
    /// which point of EXP this is, so a mon's whole stat line can be rebuilt from scratch on every
    /// grant — the derived-stats rule ADR 0005 set and everything here still depends on. It behaves
    /// like luck (a 70% mon really can take Attack three times running, and two Charmanders in the
    /// same party diverge) without anything having to be stored or replayed.
    ///
    /// **Growth is measured from the base form, and an evolution is a flat bonus.** A mon that
    /// evolves keeps growing from the species it started as and gains
    /// <see cref="AttackPerEvolution"/> / <see cref="HealthPerEvolution"/> on top; the species it
    /// became contributes nothing to its stats. So a Charizard is a Charmander with EXP and two
    /// evolutions behind it, and the tier line on the Charizard asset is only ever a Pokédex entry.
    ///
    /// **Speed never changes.** Not with EXP, and — since an evolution ignores the new species'
    /// stats — not with evolution either. A mon's Speed is the one its base form was drawn with.
    /// Other ways to gain Speed are still to come; see ADR 0009's consequences.</summary>
    public static class StatGrowth
    {
        /// <summary>Attack a point of EXP is worth, when the draw picks Attack.</summary>
        public const int AttackPerExp = 1;

        /// <summary>Health a point of EXP is worth, when the draw picks Health.</summary>
        public const int HealthPerExp = 1;

        /// <summary>Attack and Health an evolution adds, flat, whatever it evolved into.</summary>
        public const int AttackPerEvolution = 3;
        public const int HealthPerEvolution = 3;

        /// <summary>The species' own tier line, before any EXP or evolution.</summary>
        public static Stats BaseOf(PokemonSpeciesDefinitionAsset species) => new Stats
        {
            Attack = species.BaseAttack,
            Health = species.BaseHealth,
            Speed = species.BaseSpeed,
        };

        /// <summary>A mon's stats. <paramref name="baseForm"/> is the species it *started* as — the
        /// root of its evolution chain, not what it is now (see the class doc).</summary>
        public static Stats AtExp(PokemonSpeciesDefinitionAsset baseForm, string instanceId, int exp, int timesEvolved)
        {
            int points = Math.Max(0, exp);
            int evolutions = Math.Max(0, timesEvolved);
            int health = HealthGainsIn(baseForm.HealthGrowthPercent, instanceId, points);
            int attack = points - health;

            return new Stats
            {
                Attack = baseForm.BaseAttack + AttackPerExp * attack + AttackPerEvolution * evolutions,
                Health = baseForm.BaseHealth + HealthPerExp * health + HealthPerEvolution * evolutions,
                Speed = baseForm.BaseSpeed,
            };
        }

        /// <summary>How many of a mon's first <paramref name="points"/> EXP went into Health.</summary>
        public static int HealthGainsIn(int healthGrowthPercent, string instanceId, int points)
        {
            int health = 0;
            for (int i = 0; i < points; i++)
            {
                if (GainsHealth(healthGrowthPercent, instanceId, i))
                {
                    health++;
                }
            }
            return health;
        }

        /// <summary>Whether a mon's <paramref name="pointIndex"/>-th point of EXP (0-based) goes to
        /// Health rather than Attack.</summary>
        public static bool GainsHealth(int healthGrowthPercent, string instanceId, int pointIndex) =>
            Draw(instanceId, pointIndex) < healthGrowthPercent;

        /// <summary>A number in 0..99 from the mon and which point this is. FNV-1a with a final
        /// avalanche, because the plain hash leaves adjacent point indices correlated and a mon's
        /// growth would come out in visible runs. Written out here rather than taken from
        /// DeterministicRandom because this isn't a stream — every point has to be answerable on its
        /// own, in any order, forever.</summary>
        private static int Draw(string instanceId, int pointIndex)
        {
            unchecked
            {
                uint hash = 2166136261u;
                if (instanceId != null)
                {
                    for (int i = 0; i < instanceId.Length; i++)
                    {
                        hash = (hash ^ instanceId[i]) * 16777619u;
                    }
                }
                hash = (hash ^ (uint)pointIndex) * 16777619u;
                hash ^= hash >> 15;
                hash *= 2246822519u;
                hash ^= hash >> 13;
                return (int)(hash % 100u);
            }
        }
    }
}
