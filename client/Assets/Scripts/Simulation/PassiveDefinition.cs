using System;
using System.Collections.Generic;

namespace Pets.Simulation
{
    /// <summary>A single mon's passive, already resolved to its final (magnitude-by-stage-scaled)
    /// numbers for one specific PokemonInstance. Built by the content layer (Pets.Data) from a
    /// PassiveDefinition ScriptableObject before battle assembly — see content-schema.md §3. The
    /// simulator only ever sees this baked, plain-C# shape, never the ScriptableObject.</summary>
    [Serializable]
    public sealed class PassiveDefinition
    {
        public string Id;
        public string DisplayName;
        public PokemonType TypeFlavor;
        public List<EffectDefinition> Effects = new List<EffectDefinition>();
    }
}
