using System.Collections.Generic;

namespace Pets.Roguelite.Simulation
{
    /// <summary>
    /// A caught/adopted Pokémon's persistent state (Box/line-up level, not battle-transient) —
    /// mirrors docs/pokemon-roguelite-autobattler-design-doc.md §9's PokemonInstance interface
    /// and docs/content-schema.md §8. Produced from a species definition + save data by a
    /// converter in the real content pipeline; sandbox content builds these directly.
    ///
    /// Battle-transient state (charge meter, shields, temporary buffs) deliberately does NOT live
    /// here — see <see cref="BattleMon"/>, which wraps one of these for the duration of a single
    /// battle and is discarded afterward.
    /// </summary>
    public sealed class PokemonInstance
    {
        public string InstanceId;
        public int SpeciesId;
        public string DisplayName;
        public string Nickname;
        public PokemonType Type1;
        public PokemonType? Type2;
        public int Level = 1;
        public int Exp;
        public int ExpToNextLevel;
        public Stats CurrentStats;
        public int CurrentHP;
        public StatusType? Status;
        public string PassiveId;
        public List<string> EquippedItemIds = new List<string>();
        public int CaughtWithBallTier;
    }
}
