using System.Collections.Generic;

namespace Pets.Roguelite.Simulation
{
    /// <summary>
    /// A Pokémon's passive. Triggers on exactly one condition — its owner's own charge meter
    /// filling (docs/battle-sim-spec.md §3-§4) — there's no Lead/Support distinction and no other
    /// trigger in this game.
    ///
    /// Simplification vs. the full docs/content-schema.md §3 shape: this sandbox pass doesn't yet
    /// implement <c>magnitudeByStage</c> (the "same passive, bigger numbers per evolution stage"
    /// rule) — <see cref="Effects"/> amounts are used as-is. That's a real gap, not a silent
    /// divergence: fold in stage-scaling once evolution stages actually exist as content.
    /// </summary>
    public sealed class PassiveDefinition
    {
        public string Id;
        public string DisplayName;
        public PokemonType TypeFlavor;
        public List<EffectDefinition> Effects = new List<EffectDefinition>();

        public PassiveDefinition(string id, string displayName, PokemonType typeFlavor, params EffectDefinition[] effects)
        {
            Id = id;
            DisplayName = displayName;
            TypeFlavor = typeFlavor;
            Effects = new List<EffectDefinition>(effects);
        }
    }
}
