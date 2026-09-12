using System.Linq;
using NUnit.Framework;
using Pets.Data;
using Pets.EditorTools;
using UnityEditor;

namespace Pets.Tests
{
    /// <summary>
    /// Guards the other half of what ContentIntegrityTests covers. That suite asks whether the
    /// authored assets are internally consistent; this one asks whether they still say what
    /// <c>docs/pokemon_stats_unique.xlsx</c> says.
    ///
    /// The failure it exists for is the one CLAUDE.md calls out by name: stats edited in the sheet
    /// (or in an asset) and never reconciled, which nothing else notices — the game runs fine on
    /// whatever the assets happen to hold. Re-running
    /// <c>Pets &gt; Content &gt; Import Species From Roster Sheet</c> is the fix for every failure
    /// here.
    /// </summary>
    public class RosterImportTests
    {
        private static PokemonSpeciesLibrary LoadLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<PokemonSpeciesLibrary>(
                "Assets/Content/PokemonSpeciesLibrary.asset");
            Assume.That(library, Is.Not.Null, "Expected a PokemonSpeciesLibrary under Assets/Content.");
            return library;
        }

        [Test]
        public void EveryRosterSheetRow_HasASpeciesAssetInTheLibrary()
        {
            var library = LoadLibrary();
            var byId = library.AllSpecies.ToDictionary(s => s.Id);

            var missing = SpeciesRosterImporter.ReadRoster()
                .Where(row => !byId.ContainsKey(row.Id))
                .Select(row => $"{row.DisplayName} (id {row.Id})")
                .ToArray();

            CollectionAssert.IsEmpty(missing,
                "roster sheet rows with no species asset — run Pets > Content > Import Species From Roster Sheet");
        }

        [Test]
        public void EverySpeciesAsset_MatchesItsRosterSheetRow()
        {
            var library = LoadLibrary();
            var byId = library.AllSpecies.ToDictionary(s => s.Id);

            var mismatches = new System.Collections.Generic.List<string>();
            foreach (var row in SpeciesRosterImporter.ReadRoster())
            {
                if (!byId.TryGetValue(row.Id, out var species))
                {
                    continue; // reported by the test above
                }

                if (species.DisplayName != row.DisplayName)
                {
                    mismatches.Add($"id {row.Id}: name '{species.DisplayName}' != sheet '{row.DisplayName}'");
                }
                if (species.Type1 != row.Type1 || species.HasSecondType != row.HasSecondType ||
                    (row.HasSecondType && species.Type2 != row.Type2))
                {
                    mismatches.Add($"{row.DisplayName}: typing differs from the sheet");
                }
                if (species.BaseAttack != row.Attack || species.BaseHealth != row.Health || species.BaseSpeed != row.Speed)
                {
                    mismatches.Add($"{row.DisplayName}: stats {species.BaseAttack}/{species.BaseHealth}/" +
                                   $"{species.BaseSpeed} != sheet {row.Attack}/{row.Health}/{row.Speed}");
                }
            }

            CollectionAssert.IsEmpty(mismatches,
                "species assets have drifted from the roster sheet — fix the sheet, then re-import");
        }

        /// <summary>The evolution links are what `Pets.Meta.ExperienceResolver` follows when a mon
        /// earns an evolution, and they come from a cache rather than the sheet — a stale or
        /// unregenerated cache would show up in play as "nothing ever evolves", which is
        /// indistinguishable from the feature being broken.</summary>
        [Test]
        public void EverySpeciesAsset_MatchesItsCachedEvolutionChain()
        {
            var library = LoadLibrary();
            var byId = library.AllSpecies.ToDictionary(s => s.Id);

            var mismatches = new System.Collections.Generic.List<string>();
            int linked = 0;
            foreach (var row in SpeciesRosterImporter.ReadRoster())
            {
                if (!byId.TryGetValue(row.Id, out var species))
                {
                    continue; // reported by EveryRosterSheetRow_HasASpeciesAssetInTheLibrary
                }

                if (species.EvolutionStage != row.EvolutionStage)
                {
                    mismatches.Add($"{row.DisplayName}: stage {species.EvolutionStage} != cached {row.EvolutionStage}");
                }

                int linkedId = species.EvolvesInto != null ? species.EvolvesInto.Id : 0;
                if (linkedId != row.EvolvesIntoId)
                {
                    mismatches.Add($"{row.DisplayName}: evolves into {linkedId} != cached {row.EvolvesIntoId}");
                }
                if (linkedId != 0)
                {
                    linked++;
                }
            }

            CollectionAssert.IsEmpty(mismatches,
                "species assets disagree with docs/roster_evolution_chains.json — re-run the roster importer");
            Assert.Greater(linked, 0,
                "no species can evolve at all, so the evolution threshold is unreachable in play");
        }

        /// <summary>A species can't evolve into itself or backwards down its own chain — either
        /// would loop ExperienceResolver's evolve-until-done pass.</summary>
        [Test]
        public void EvolutionLinks_AlwaysMoveForwardThroughTheChain()
        {
            var bad = LoadLibrary().AllSpecies
                .Where(s => s.EvolvesInto != null &&
                            (s.EvolvesInto == s || s.EvolvesInto.EvolutionStage <= s.EvolutionStage))
                .Select(s => $"{s.DisplayName} (stage {s.EvolutionStage}) -> " +
                             $"{s.EvolvesInto.DisplayName} (stage {s.EvolvesInto.EvolutionStage})")
                .ToArray();

            CollectionAssert.IsEmpty(bad, "an evolution that doesn't advance a stage would loop");
        }

        /// <summary>The importer rebuilds the library in Dex order, and things downstream lean on
        /// it: the Pokédex and Character Select present the roster in library order, and
        /// RunBootstrapper's fallback starters are AllSpecies[0] and [1].</summary>
        [Test]
        public void SpeciesLibrary_IsOrderedByDexId()
        {
            var ids = LoadLibrary().AllSpecies.Select(s => s.Id).ToArray();
            CollectionAssert.AreEqual(ids.OrderBy(id => id).ToArray(), ids,
                "PokemonSpeciesLibrary should be in Dex order — re-run the roster importer");
        }
    }
}
