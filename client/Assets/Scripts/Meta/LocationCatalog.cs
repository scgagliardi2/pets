using System.Collections.Generic;
using System.Linq;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>Design doc §4's Location table — what each LocationType is called, which Pokémon
    /// types its wild encounters and Gym Leader lean toward, and its one-line flavor — plus the Region
    /// Hub's offer of three Locations to choose between (§5.2).
    ///
    /// A static table rather than LocationTypeDefinition assets (content-schema.md §9): nine fixed
    /// rows straight out of the design doc, no art, and nothing an asset pipeline would buy yet.
    /// Replaces ForestLocationFactory, which held the one row that existed. Recorded in ADR 0006 as a
    /// deviation to revisit when Locations gain content of their own.</summary>
    public static class LocationCatalog
    {
        public sealed class Entry
        {
            public readonly LocationType Type;
            public readonly string DisplayName;
            public readonly PokemonType[] TypeBias;
            public readonly string Flavor;

            public Entry(LocationType type, string displayName, string flavor, params PokemonType[] typeBias)
            {
                Type = type;
                DisplayName = displayName;
                Flavor = flavor;
                TypeBias = typeBias;
            }
        }

        /// <summary>How many Locations the Region Hub offers at a time.</summary>
        public const int OfferCount = 3;

        /// <summary>The Location a map resolves against when its run hasn't chosen one — a Map scene
        /// opened on its own in the Editor or a test. The Forest, which is what the game's one
        /// Location used to be.</summary>
        public const LocationType Fallback = LocationType.Forest;

        public static readonly IReadOnlyList<Entry> All = new[]
        {
            new Entry(LocationType.Town, "Town", "Quiet streets and friendly faces.",
                PokemonType.Normal, PokemonType.Fairy),
            new Entry(LocationType.City, "City", "Trainers on every corner.",
                PokemonType.Normal, PokemonType.Steel, PokemonType.Electric, PokemonType.Psychic),
            new Entry(LocationType.Dungeon, "Dungeon", "Dark halls, rare finds.",
                PokemonType.Ghost, PokemonType.Dark, PokemonType.Poison, PokemonType.Steel),
            new Entry(LocationType.Cave, "Cave", "Cramped tunnels, sudden ambushes.",
                PokemonType.Rock, PokemonType.Ground, PokemonType.Poison, PokemonType.Dark),
            new Entry(LocationType.Forest, "Forest", "Dense with wild Pokémon.",
                PokemonType.Grass, PokemonType.Bug, PokemonType.Flying),
            new Entry(LocationType.Sea, "Sea", "Waves, spray and cold currents.",
                PokemonType.Water, PokemonType.Ice),
            new Entry(LocationType.Plains, "Plains", "Open ground, frequent encounters.",
                PokemonType.Normal, PokemonType.Flying, PokemonType.Grass, PokemonType.Electric),
            new Entry(LocationType.Desert, "Desert", "Sparse, but what lives here is tough.",
                PokemonType.Ground, PokemonType.Fire, PokemonType.Rock),
            new Entry(LocationType.Mountain, "Mountain", "Harsh climbs and strong Gym Leaders.",
                PokemonType.Rock, PokemonType.Ground, PokemonType.Flying, PokemonType.Ice, PokemonType.Fighting),
        };

        public static Entry Get(LocationType type) => All.First(e => e.Type == type);

        /// <summary>The Location the run is in, or <see cref="Fallback"/> when it hasn't chosen one.</summary>
        public static Entry CurrentFor(RunState state) => Get(state?.CurrentLocation ?? Fallback);

        /// <summary>The Locations the Region Hub offers the run right now: <see cref="OfferCount"/>
        /// distinct types, never the one just completed, shuffled from the run seed and badge count.
        /// Derived rather than stored, so leaving the hub for the Team screen and coming back shows the
        /// same three — and so a future save doesn't need to carry them.</summary>
        public static List<LocationType> OffersFor(RunState state)
        {
            var candidates = All.Select(e => e.Type).ToList();
            if (state.CompletedLocations.Count > 0)
            {
                candidates.Remove(state.CompletedLocations[state.CompletedLocations.Count - 1]);
            }

            var rng = new DeterministicRandom(state.RunSeed ^ ((state.BadgeCount + 1) * 7919));
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }
            return candidates.Take(OfferCount).ToList();
        }
    }
}
