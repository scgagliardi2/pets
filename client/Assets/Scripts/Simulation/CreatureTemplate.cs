using System;

namespace Pets.Simulation
{
    /// <summary>
    /// A fixed stat block used to spawn a new creature mid-battle (Summon effects). Resolved
    /// once by Data/TeamStateConverter.cs from a CreatureDefinition asset — see
    /// content-schema.md §2 for why this is a separate, already-resolved shape rather than an id
    /// the simulator would have to look up.
    /// </summary>
    [Serializable]
    public sealed class CreatureTemplate
    {
        public string Id;
        public string DisplayName;
        public int Attack;
        public int Health;
    }
}
