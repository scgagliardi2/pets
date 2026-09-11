using System.Linq;
using NUnit.Framework;
using Pets.Data;
using UnityEditor;

namespace Pets.Tests
{
    /// <summary>
    /// Content-linting checks against the real curated-species/passive assets under
    /// Assets/Content that PokemonContentTests.cs doesn't already cover (that file checks passive
    /// presence, stat conversion, and a real fight resolving — this file is about authoring
    /// mistakes that wouldn't show up there, like a duplicated id silently shadowing another
    /// species via GetById's FirstOrDefault). See docs/testing-harness-plan.md §3.
    /// </summary>
    public class ContentValidationTests
    {
        private static PokemonSpeciesLibrary LoadSpeciesLibrary()
        {
            var guids = AssetDatabase.FindAssets("t:PokemonSpeciesLibrary", new[] { "Assets/Content" });
            Assume.That(guids.Length, Is.GreaterThan(0), "PokemonSpeciesLibrary.asset not found under Assets/Content.");
            return AssetDatabase.LoadAssetAtPath<PokemonSpeciesLibrary>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static PassiveLibrary LoadPassiveLibrary()
        {
            var guids = AssetDatabase.FindAssets("t:PassiveLibrary", new[] { "Assets/Content" });
            Assume.That(guids.Length, Is.GreaterThan(0), "PassiveLibrary.asset not found under Assets/Content.");
            return AssetDatabase.LoadAssetAtPath<PassiveLibrary>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        [Test]
        public void SpeciesIds_AreUniqueAndPositive()
        {
            var library = LoadSpeciesLibrary();
            Assume.That(library.AllSpecies.Count, Is.GreaterThan(0), "Assets/Content has no curated species.");

            foreach (var species in library.AllSpecies)
            {
                Assert.Greater(species.Id, 0, $"{species.name} has a non-positive Id ({species.Id}) — PokemonSpeciesLibrary.GetById(0) would be ambiguous");
            }

            var duplicates = library.AllSpecies.GroupBy(s => s.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.IsEmpty(duplicates, $"Duplicate species ids (GetById would silently return only the first): {string.Join(", ", duplicates)}");
        }

        [Test]
        public void PassiveIds_AreUniqueAndNonEmpty()
        {
            var library = LoadPassiveLibrary();
            Assume.That(library.AllPassives.Count, Is.GreaterThan(0), "Assets/Content has no passives.");

            foreach (var passive in library.AllPassives)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(passive.Id), $"{passive.name} has an empty Id");
            }

            var duplicates = library.AllPassives.GroupBy(p => p.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.IsEmpty(duplicates, $"Duplicate passive ids (PassiveLibrary.GetById would silently return only the first): {string.Join(", ", duplicates)}");
        }

        [Test]
        public void EverySpecies_HasPositiveBaseStats()
        {
            var library = LoadSpeciesLibrary();
            Assume.That(library.AllSpecies.Count, Is.GreaterThan(0), "Assets/Content has no curated species.");

            foreach (var species in library.AllSpecies)
            {
                Assert.Greater(species.BaseAttack, 0, $"{species.DisplayName} has non-positive BaseAttack");
                Assert.Greater(species.BaseHealth, 0, $"{species.DisplayName} has non-positive BaseHealth — it would already be fainted entering battle");
                Assert.Greater(species.BaseSpeed, 0, $"{species.DisplayName} has non-positive BaseSpeed — it would never accrue charge");
            }
        }

        [Test]
        public void Species_WithASecondType_HasADifferentSecondType()
        {
            var library = LoadSpeciesLibrary();
            Assume.That(library.AllSpecies.Count, Is.GreaterThan(0), "Assets/Content has no curated species.");

            foreach (var species in library.AllSpecies.Where(s => s.HasSecondType))
            {
                Assert.AreNotEqual(species.Type1, species.Type2,
                    $"{species.DisplayName} has HasSecondType set but Type2 == Type1 — likely a copy-paste authoring mistake");
            }
        }
    }
}
