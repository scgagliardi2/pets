using UnityEngine;
using Pets.Simulation;

namespace Pets.Data
{
    /// <summary>Authoring asset for one species. See content-schema.md §2 (there called
    /// "PokemonSpeciesDefinition" — named …Asset here purely to avoid colliding with
    /// Pets.Simulation.PassiveDefinition-style pure types when both are in scope in the same
    /// file, e.g. PokemonInstanceFactory).</summary>
    [CreateAssetMenu(fileName = "NewSpecies", menuName = "Pets/Pokemon Species Definition")]
    public sealed class PokemonSpeciesDefinitionAsset : ScriptableObject
    {
        [Header("Identity")]
        public int Id;
        public string DisplayName;

        [Header("Typing")]
        public PokemonType Type1;
        public bool HasSecondType;
        public PokemonType Type2;

        /// <summary>The ceiling for BaseSpeed. Speed bars (Character Select's cards) are drawn as a
        /// fraction of it, so a species above it would silently pin a full bar —
        /// ContentIntegrityTests fails any asset that exceeds it.</summary>
        public const int MaxBaseSpeed = SpeciesTier.MaxSpeed;

        /// <summary>Which tier this species sits in, 1..<see cref="SpeciesTier.MaxTier"/> — the
        /// band its *real* base-stat total falls in, raised where needed so an evolution always
        /// lands at least one tier above what it came from (ADR 0008). Every species in a tier
        /// spends the same number of points across the three stats below, so the tier is the
        /// species' power and the three stats are only how it spends it.
        ///
        /// Set by the roster importer from docs/pokemon_stats_unique.xlsx, never by hand — see
        /// <see cref="Pets.Data.SpeciesTier"/> for the rule and RosterImportTests for the check
        /// that the assets still agree with the sheet.</summary>
        [Header("Tier")]
        public int Tier = SpeciesTier.MinTier;

        /// <summary>What <see cref="Tier"/> is allowed to spend. <see cref="BaseStatTotal"/> must
        /// equal this for every species — ContentIntegrityTests fails any asset where it doesn't,
        /// since a tier whose members don't cost the same is not a tier.</summary>
        public int TierStatTotal => SpeciesTier.TotalFor(Tier);

        [Header("Base stats (derived from docs/pokemon_stats_unique.xlsx by SpeciesTier)")]
        public int BaseAttack;
        public int BaseHealth;
        public int BaseSpeed;

        /// <summary>How likely a mon of this species is to put a point of EXP into Health rather
        /// than Attack, in percent — a point is worth +1 of one or the other, never both (ADR 0009,
        /// <see cref="Pets.Data.StatGrowth"/>). 50 or more for every species, because every mon
        /// should out-last its own mirror rather than trade lethal blows with it; 0 for the one
        /// species the real games give a single hit point.
        ///
        /// Derived from the species' real Attack:Health ratio by
        /// <see cref="SpeciesTier.HealthGrowthPercentFor"/> and written by the roster importer, like
        /// the tier line above it — never authored.</summary>
        [Range(0, 100)]
        public int HealthGrowthPercent = SpeciesTier.MinHealthGrowthPercent;

        /// <summary>The three base stats added up — which, because these are tier points rather
        /// than real Pokémon stats, is just <see cref="TierStatTotal"/> restated from the asset's
        /// own fields. Kept as the thing callers compare against, so a species whose stats were
        /// edited out of line with its tier still reads as the strength it actually is.</summary>
        public int BaseStatTotal => BaseAttack + BaseHealth + BaseSpeed;

        [Header("Passive")]
        public PassiveDefinitionAsset Passive;

        [Header("Evolution")]

        /// <summary>What this species becomes when a mon carrying it crosses its evolution
        /// threshold (Pets.Meta.ExperienceResolver). Null for a final form — and also for the
        /// three roster species whose line branches (Eevee, Tyrogue, Nincada), since one reference
        /// can't express "becomes one of seven"; picking a branch is its own feature. Set by the
        /// roster importer from the cached PokeAPI chains, restricted to the roster.
        ///
        /// There is deliberately no per-species evolution threshold field: how much EXP an evolution
        /// costs is a run-layer rule (ExperienceResolver.ExpPerEvolution), and nothing in the roster
        /// sheet or PokeAPI gives a per-species number to put here.</summary>
        public PokemonSpeciesDefinitionAsset EvolvesInto;

        /// <summary>How many evolution steps deep this species sits in its real Pokémon chain —
        /// 0 for a base form, 1 for a first evolution, and so on. Counted against the true chain
        /// even when the earlier form isn't in the roster, so Pikachu is stage 1 (Pichu exists, it
        /// just isn't curated).
        ///
        /// This is the index into a passive's MagnitudeByStage table (content-schema.md §1, §3) —
        /// the "same passive, bigger numbers" rule — and nothing else. It is *not* what decides
        /// when a mon evolves: that's counted per instance, from how many times that particular mon
        /// has already evolved, so a curated base form like Pikachu still evolves on its first
        /// threshold rather than starting a step behind.</summary>
        public int EvolutionStage;

        /// <summary>Direct reference to the species' artwork under Assets/Art/Pokemon (see
        /// content-schema.md §2). A reference rather than a Resources path string so that only the
        /// sprites curated species actually point at are pulled into a build, and so a typo is a
        /// compile-visible missing reference instead of a silent null at runtime. Read through
        /// Pets.Data.PokemonSprites.Load rather than directly.</summary>
        [Header("Presentation")]
        public Sprite Sprite;

        /// <summary>The game-style battle sprite seen from the front, under
        /// Assets/Art/Pokemon-Sprites. This is what every screen that shows a Pokémon draws — cards,
        /// the Pokédex, the party strip, the foe's side of the battlefield — in place of the
        /// official artwork in <see cref="Sprite"/>, which is a different look entirely (smooth
        /// illustration rather than pixel art) and now serves as the fallback.
        ///
        /// Nullable, and deliberately so: the sprite set doesn't quite cover the roster (see
        /// SpeciesRosterImporter.AssignBattleSpritesIfMissing), and a species without one should
        /// fall back to its artwork rather than draw nothing. Read through
        /// Pets.Data.PokemonSprites.LoadFront, which does that.</summary>
        public Sprite FrontSprite;

        /// <summary>The same sprite seen from behind, drawn only for the player's own Lead and
        /// Support on the battlefield — the over-the-shoulder view the main-series games use, where
        /// your mon faces away and the foe faces you. Every other place a Pokémon appears uses
        /// <see cref="FrontSprite"/>.
        ///
        /// Nullable on the same terms as FrontSprite; read through Pets.Data.PokemonSprites.LoadBack.</summary>
        public Sprite BackSprite;

        public bool IsLegendary;
    }
}
