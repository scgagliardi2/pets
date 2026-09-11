using System.Collections.Generic;
using UnityEngine;
using Pets.Simulation;

namespace Pets.Data
{
    /// <summary>Authoring asset for one passive. See content-schema.md §3 (there called
    /// "PassiveDefinition" — named …Asset here to avoid colliding with the pure, already-resolved
    /// Pets.Simulation.PassiveDefinition this asset is converted into by
    /// PokemonInstanceFactory.ResolvePassive before battle).</summary>
    [CreateAssetMenu(fileName = "NewPassive", menuName = "Pets/Passive Definition")]
    public sealed class PassiveDefinitionAsset : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        public PokemonType TypeFlavor;

        /// <summary>Applied in list order when the trigger (this mon's own charge meter filling)
        /// fires. Amount here is the base, pre-magnitude-by-stage value.</summary>
        public List<EffectDefinition> Effects = new List<EffectDefinition>();

        /// <summary>Index 0 = base-stage magnitude multiplier, index 1 = first evolution, etc.
        /// A passive persists through evolutions; only this multiplier scales — see
        /// content-schema.md §1. Phase 0 has no evolution, so this is a single-entry [1] for
        /// every species today.</summary>
        public int[] MagnitudeByStage = { 1 };
    }
}
