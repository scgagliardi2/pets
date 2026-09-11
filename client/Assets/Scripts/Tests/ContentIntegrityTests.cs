using System.Linq;
using NUnit.Framework;
using Pets.Data;
using UnityEditor;

namespace Pets.Tests
{
    /// <summary>
    /// Guards the content *pipeline* rather than the content's balance: every authored
    /// ScriptableObject under Assets/Content has to be reachable at runtime, and every runtime
    /// lookup key it carries has to actually resolve. These are the failure modes a data-driven
    /// content layer has that code doesn't — an asset authored but never added to its library, a
    /// duplicated Id that makes GetById order-dependent, or a SpriteSource string pointing at a
    /// file that was moved or renamed. None of those break compilation, and the existing
    /// PokemonContentTests (which walk the library, not the folder) can't see them, because an
    /// unregistered asset is exactly the thing that isn't in the library.
    /// </summary>
    public class ContentIntegrityTests
    {
        private const string SpeciesFolder = "Assets/Content/Species";
        private const string PassivesFolder = "Assets/Content/Passives";

        private static T LoadSingle<T>() where T : UnityEngine.Object
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets/Content" });
            Assume.That(guids.Length, Is.EqualTo(1), $"Expected exactly one {typeof(T).Name} under Assets/Content.");
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static T[] LoadFolder<T>(string folder) where T : UnityEngine.Object =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .ToArray();

        [Test]
        public void EverySpeciesAssetOnDisk_IsRegisteredInTheSpeciesLibrary()
        {
            var library = LoadSingle<PokemonSpeciesLibrary>();
            var onDisk = LoadFolder<PokemonSpeciesDefinitionAsset>(SpeciesFolder);

            Assert.IsNotEmpty(onDisk, $"No species assets found under {SpeciesFolder}.");
            var unregistered = onDisk.Where(s => !library.AllSpecies.Contains(s)).Select(s => s.name);
            CollectionAssert.IsEmpty(unregistered.ToArray(),
                "Species assets exist on disk but aren't in PokemonSpeciesLibrary, so they're invisible at runtime");
        }

        [Test]
        public void SpeciesLibrary_HasNoNullOrDuplicateEntries()
        {
            var library = LoadSingle<PokemonSpeciesLibrary>();

            CollectionAssert.DoesNotContain(library.AllSpecies, null, "PokemonSpeciesLibrary has an empty slot");
            var duplicateIds = library.AllSpecies.GroupBy(s => s.Id).Where(g => g.Count() > 1)
                .Select(g => $"Id {g.Key}: {string.Join(", ", g.Select(s => s.DisplayName))}");
            CollectionAssert.IsEmpty(duplicateIds.ToArray(), "Duplicate species Ids make GetById order-dependent");
        }

        [Test]
        public void EverySpecies_ResolvesItsSprite()
        {
            var library = LoadSingle<PokemonSpeciesLibrary>();

            foreach (var species in library.AllSpecies)
            {
                Assert.IsNotEmpty(species.SpriteSource, $"{species.DisplayName} has no SpriteSource");
                Assert.IsNotNull(PokemonSprites.Load(species),
                    $"{species.DisplayName}'s SpriteSource '{species.SpriteSource}' doesn't resolve to a Sprite " +
                    "under Assets/Resources — the path is a plain string, so a moved or renamed file fails silently at runtime");
            }
        }

        [Test]
        public void EveryPassiveAssetOnDisk_IsRegisteredInThePassiveLibrary()
        {
            var library = LoadSingle<PassiveLibrary>();
            var onDisk = LoadFolder<PassiveDefinitionAsset>(PassivesFolder);

            Assert.IsNotEmpty(onDisk, $"No passive assets found under {PassivesFolder}.");
            var unregistered = onDisk.Where(p => !library.AllPassives.Contains(p)).Select(p => p.name);
            CollectionAssert.IsEmpty(unregistered.ToArray(),
                "Passive assets exist on disk but aren't in PassiveLibrary, so GetById can't resolve them");
        }

        [Test]
        public void PassiveLibrary_HasNoNullOrDuplicateIds()
        {
            var library = LoadSingle<PassiveLibrary>();

            CollectionAssert.DoesNotContain(library.AllPassives, null, "PassiveLibrary has an empty slot");
            foreach (var passive in library.AllPassives)
            {
                Assert.IsNotEmpty(passive.Id, $"{passive.name} has no Id");
            }

            var duplicateIds = library.AllPassives.GroupBy(p => p.Id).Where(g => g.Count() > 1).Select(g => g.Key);
            CollectionAssert.IsEmpty(duplicateIds.ToArray(), "Duplicate passive Ids make PassiveLibrary.GetById ambiguous");
        }

        [Test]
        public void EverySpeciesPassive_IsResolvableThroughThePassiveLibrary()
        {
            var speciesLibrary = LoadSingle<PokemonSpeciesLibrary>();
            var passiveLibrary = LoadSingle<PassiveLibrary>();

            foreach (var species in speciesLibrary.AllSpecies)
            {
                Assert.IsNotNull(species.Passive, $"{species.DisplayName} has no passive assigned");
                Assert.AreSame(species.Passive, passiveLibrary.GetById(species.Passive.Id),
                    $"{species.DisplayName}'s passive '{species.Passive.Id}' isn't the one PassiveLibrary resolves for that Id " +
                    "(an item-granted passive override, content-schema.md §7, would resolve the wrong asset)");
            }
        }
    }
}
