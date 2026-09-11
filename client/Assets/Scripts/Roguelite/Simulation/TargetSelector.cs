namespace Pets.Roguelite.Simulation
{
    /// <summary>
    /// See docs/content-schema.md §5. Narrower than the old 5-slot model's selector list, since
    /// only two mons per side are ever active.
    /// </summary>
    public enum TargetSelector
    {
        /// <summary>The mon whose passive/item effect is firing.</summary>
        Self,

        /// <summary>The other currently-active mon on the same side (Lead's Support, or vice versa).</summary>
        Ally,

        /// <summary>The opposing side's current Lead.</summary>
        EnemyLead,

        /// <summary>The opposing side's current Support.</summary>
        EnemySupport
    }
}
