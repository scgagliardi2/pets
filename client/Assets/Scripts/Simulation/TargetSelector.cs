namespace Pets.Simulation
{
    /// <summary>See content-schema.md §5. Only two mons per side are ever active.</summary>
    public enum TargetSelector
    {
        /// <summary>The mon whose passive is firing.</summary>
        Self,

        /// <summary>The other currently-active mon on the same side. No-op if there's no Support.</summary>
        Ally,

        /// <summary>The opposing side's current Lead.</summary>
        EnemyLead,

        /// <summary>The opposing side's current Support. No-op if the enemy has no Support.</summary>
        EnemySupport
    }
}
