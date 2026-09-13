using System;
using Pets.Simulation;

namespace Pets.Data
{
    /// <summary>Converts authoring ScriptableObjects into the plain-C# runtime shapes Simulation
    /// operates on — analogous to the old schema's TeamStateConverter. Never authored directly;
    /// see content-schema.md §8.</summary>
    public static class PokemonInstanceFactory
    {
        /// <summary>Builds a fresh, full-health PokemonInstance at 0 EXP, on its species' bare tier
        /// line (<see cref="StatGrowth"/>). Its passive is resolved at the species' own evolution
        /// stage — a mon caught as a Charmeleon is a stage-1 mon, and its passive's magnitude should
        /// say so (content-schema.md §1).
        ///
        /// A run should usually go through Pets.Meta.ExperienceResolver.CreateAtExp instead, which
        /// also sets the EXP and counts how far along its chain an evolved species already is — a
        /// Charmeleon built here claims to have never earned anything, which its own existence
        /// disproves.</summary>
        public static PokemonInstance Create(PokemonSpeciesDefinitionAsset species, string instanceId)
        {
            var stats = StatGrowth.BaseOf(species);
            return new PokemonInstance
            {
                InstanceId = instanceId,
                SpeciesId = species.Id,
                CurrentStats = stats,
                CurrentHP = stats.Health,
                PassiveId = species.Passive != null ? species.Passive.Id : null,
                ResolvedPassive = species.Passive != null
                    ? ResolvePassive(species.Passive, species.EvolutionStage)
                    : null
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
