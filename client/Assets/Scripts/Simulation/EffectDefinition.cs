using System;

namespace Pets.Simulation
{
    /// <summary>One effect within a passive. See content-schema.md §3. A plain, non-polymorphic
    /// shape (one struct, not a subtype per EffectType) so it round-trips through Unity's
    /// ScriptableObject/JsonUtility serializers without any custom converter.</summary>
    [Serializable]
    public struct EffectDefinition
    {
        public EffectType Type;
        public TargetSelector Target;

        /// <summary>Base magnitude before magnitude-by-stage scaling. Ignored for effects that
        /// don't take a magnitude (ClearStatus, and ApplyStatus when applying Paralyzed/Asleep).
        /// For ApplyStatus with Poisoned/Burned, this is the tick damage that status will deal.</summary>
        public int Amount;

        /// <summary>Only meaningful when Type is ApplyStatus; ignored otherwise (there's no
        /// Nullable&lt;StatusType&gt; here because Unity's serializer can't represent it in an
        /// inspector-authored field, mirroring how Amount is simply unused when not applicable).</summary>
        public StatusType Status;
    }
}
