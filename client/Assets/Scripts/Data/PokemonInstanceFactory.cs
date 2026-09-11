using System;
using Pets.Simulation;

namespace Pets.Data
{
    /// <summary>Converts authoring ScriptableObjects into the plain-C# runtime shapes Simulation
    /// operates on — analogous to the old schema's TeamStateConverter. Never authored directly;
    /// see content-schema.md §8.</summary>
    public static class PokemonInstanceFactory
    {
        /// <summary>Builds a fresh, full-health PokemonInstance for battle. `stage` is the
        /// species' 0-based evolution stage (always 0 until Phase 1 adds evolution).</summary>
        public static PokemonInstance Create(PokemonSpeciesDefinitionAsset species, string instanceId, int stage = 0)
        {
            return new PokemonInstance
            {
                InstanceId = instanceId,
                SpeciesId = species.Id,
                Level = 1,
                CurrentStats = new Stats
                {
                    Attack = species.BaseAttack,
                    Health = species.BaseHealth,
                    Speed = species.BaseSpeed
                },
                CurrentHP = species.BaseHealth,
                ExpToNextLevel = BaseExpToNextLevel,
                PassiveId = species.Passive != null ? species.Passive.Id : null,
                ResolvedPassive = species.Passive != null ? ResolvePassive(species.Passive, stage) : null
            };
        }

        /// <summary>Starting EXP threshold for a fresh Level 1 instance (Meta/ExperienceResolver
        /// scales this up per level-up). No design-doc formula exists yet; a flat placeholder.</summary>
        public const int BaseExpToNextLevel = 100;

        /// <summary>Copies a persisted roster instance into a fresh-for-battle instance: same
        /// identity/leveled stats, but with all battle-only transient state (HP, charge, status,
        /// shields, buffs) reset, per line-up assembly (content-schema.md §8). Phase 0 has no
        /// between-fight HP persistence, so every fight starts at full HP.</summary>
        public static PokemonInstance ResetForBattle(PokemonInstance persisted)
        {
            return new PokemonInstance
            {
                InstanceId = persisted.InstanceId,
                SpeciesId = persisted.SpeciesId,
                Nickname = persisted.Nickname,
                Level = persisted.Level,
                Exp = persisted.Exp,
                ExpToNextLevel = persisted.ExpToNextLevel,
                CurrentStats = persisted.CurrentStats,
                CurrentHP = persisted.CurrentStats.Health,
                PassiveId = persisted.PassiveId,
                ResolvedPassive = persisted.ResolvedPassive
            };
        }

        /// <summary>Bakes a PassiveDefinitionAsset's magnitude-by-stage table into a resolved,
        /// pure Pets.Simulation.PassiveDefinition for one specific stage (content-schema.md §1,
        /// §3). The simulator never sees the asset or resolves stage scaling itself.</summary>
        public static PassiveDefinition ResolvePassive(PassiveDefinitionAsset asset, int stage)
        {
            int magnitude = 1;
            if (asset.MagnitudeByStage != null && asset.MagnitudeByStage.Length > 0)
            {
                int index = Math.Min(stage, asset.MagnitudeByStage.Length - 1);
                magnitude = asset.MagnitudeByStage[index];
            }

            var resolved = new PassiveDefinition
            {
                Id = asset.Id,
                DisplayName = asset.DisplayName,
                TypeFlavor = asset.TypeFlavor
            };
            foreach (var effect in asset.Effects)
            {
                var scaled = effect;
                scaled.Amount = effect.Amount * magnitude;
                resolved.Effects.Add(scaled);
            }
            return resolved;
        }
    }
}
