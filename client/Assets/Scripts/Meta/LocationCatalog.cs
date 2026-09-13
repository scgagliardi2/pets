using System;
using System.Collections.Generic;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>Design doc §4's Location table as data: what each kind of Location is called, what
    /// it says about itself, and which Pokémon types its wild encounters lean toward.
    ///
    /// This replaces ForestLocationFactory, which held exactly one Location's type bias back when a
    /// run was one hand-authored Forest. A run is six Locations now (RegionTier.RegionsPerRun) and
    /// the player picks each one, so the bias has to be a property of the Location they chose
    /// (RunState.CurrentLocation) rather than a constant.
    ///
    /// Only the type bias is mechanical today. The table's other columns — more Events in a Town,
    /// trainer-heavy Cities, rare loot in a Dungeon — need node-weighting and a Shop that don't
    /// exist, so the flavor line says what a Location is *meant* to feel like without the map
    /// pretending it already does.</summary>
    public static class LocationCatalog
    {
        public readonly struct Entry
        {
            public readonly LocationType Type;
            public readonly string DisplayName;
            public readonly string Flavor;
            public readonly PokemonType[] TypeBias;

            public Entry(LocationType type, string displayName, string flavor, PokemonType[] typeBias)
            {
                Type = type;
                DisplayName = displayName;
                Flavor = flavor;
                TypeBias = typeBias;
            }
        }

        private static readonly Dictionary<LocationType, Entry> Entries = new Dictionary<LocationType, Entry>
        {
            [LocationType.Town] = new Entry(LocationType.Town, "Quiet Town",
                "Low danger, and people about.",
                new[] { PokemonType.Normal, PokemonType.Fairy }),
            [LocationType.City] = new Entry(LocationType.City, "Busy City",
                "Trainers everywhere.",
                new[] { PokemonType.Normal, PokemonType.Steel, PokemonType.Electric, PokemonType.Psychic }),
            [LocationType.Dungeon] = new Entry(LocationType.Dungeon, "Old Dungeon",
                "Dark, and something is in it.",
                new[] { PokemonType.Ghost, PokemonType.Dark, PokemonType.Poison, PokemonType.Steel }),
            [LocationType.Cave] = new Entry(LocationType.Cave, "Winding Cave",
                "Cramped enough to be ambushed in.",
                new[] { PokemonType.Rock, PokemonType.Ground, PokemonType.Poison, PokemonType.Dark }),
            [LocationType.Forest] = new Entry(LocationType.Forest, "Deep Forest",
                "Dense, and full of wildlife.",
                new[] { PokemonType.Grass, PokemonType.Bug, PokemonType.Flying }),
            [LocationType.Sea] = new Entry(LocationType.Sea, "Open Sea",
                "Coastal water, and what lives under it.",
                new[] { PokemonType.Water, PokemonType.Ice }),
            [LocationType.Plains] = new Entry(LocationType.Plains, "Wide Plains",
                "Open ground, frequent encounters.",
                new[] { PokemonType.Normal, PokemonType.Flying, PokemonType.Grass, PokemonType.Electric }),
            [LocationType.Desert] = new Entry(LocationType.Desert, "Burning Desert",
                "Sparse, and everything here is tough.",
                new[] { PokemonType.Ground, PokemonType.Fire, PokemonType.Rock }),
            [LocationType.Mountain] = new Entry(LocationType.Mountain, "High Mountain",
                "Harsh going, and a strong Gym at the top.",
                new[] { PokemonType.Rock, PokemonType.Ground, PokemonType.Flying, PokemonType.Ice, PokemonType.Fighting }),
        };

        /// <summary>Every Location type, in declaration order — what the Region Hub draws its
        /// offers from.</summary>
        public static readonly LocationType[] AllTypes =
            (LocationType[])Enum.GetValues(typeof(LocationType));

        public static Entry For(LocationType type) => Entries[type];

        /// <summary>The types a Location's wild encounters lean toward (EncounterGenerator).</summary>
        public static PokemonType[] TypeBiasFor(LocationType type) => Entries[type].TypeBias;

        public static string DisplayNameFor(LocationType type) => Entries[type].DisplayName;
    }
}
