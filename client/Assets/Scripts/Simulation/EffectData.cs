using System;

namespace Pets.Simulation
{
    /// <summary>One effect within an ability. See content-schema.md §2 for the field shapes.</summary>
    [Serializable]
    public struct EffectData
    {
        public EffectType Type;
        public TargetSelector Target;
        public int Amount;

        /// <summary>Only set when Type == Summon; Target/Amount are unused in that case.</summary>
        public CreatureTemplate SummonTemplate;
    }
}
