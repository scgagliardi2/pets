namespace Pets.Meta
{
    /// <summary>Pokéball tiers (design doc §12.1). Ordered weakest to strongest — the order is
    /// meaningful, so a tray can show them in it and a comparison can ask "is this ball at least a
    /// Great Ball" without a lookup table.</summary>
    public enum BallTier
    {
        Poke,
        Great,
        Ultra
    }

    /// <summary>What each ball tier is worth. Every number here is a placeholder in the same sense
    /// as the rest of the game's balance values (PLAN.md §10) — they're a first pass chosen to make
    /// the loop legible (weaken it, status it, then throw), not a tuned curve.
    ///
    /// Lives next to the enum rather than as fields on it so there's one place to retune, and so
    /// the enum stays a plain serializable value for the inventory and save data.</summary>
    public static class BallCatalog
    {
        /// <summary>Every tier, weakest first — what a tray iterates to draw itself.</summary>
        public static readonly BallTier[] AllTiers = { BallTier.Poke, BallTier.Great, BallTier.Ultra };

        public static string DisplayName(BallTier tier)
        {
            switch (tier)
            {
                case BallTier.Great: return "Great Ball";
                case BallTier.Ultra: return "Ultra Ball";
                default: return "Poké Ball";
            }
        }

        /// <summary>Catch chance against a full-health, status-free target, as a fraction. The
        /// floor the other factors build on — see <see cref="CatchOdds"/>.</summary>
        public static float BaseChance(BallTier tier)
        {
            switch (tier)
            {
                case BallTier.Great: return 0.20f;
                case BallTier.Ultra: return 0.35f;
                default: return 0.10f;
            }
        }

        /// <summary>The most EXP a mon caught with this tier keeps — design doc §12.1's rule that
        /// "a low-tier ball can still succeed on a high-level Pokémon, but yields an under-levelled
        /// catch; only higher-tier balls guarantee the target's true level".
        ///
        /// Expressed in EXP because EXP *is* this game's level (ADR 0008: stats are derived from
        /// EXP, and there's no separate level field to cap). Null means "no cap" — an Ultra Ball
        /// always yields the mon at its true strength.</summary>
        public static int? ExpCap(BallTier tier)
        {
            switch (tier)
            {
                case BallTier.Ultra: return null;
                case BallTier.Great: return 12;
                default: return 6;
            }
        }
    }
}
