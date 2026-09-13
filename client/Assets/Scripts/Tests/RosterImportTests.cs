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
                if (species.Tier != row.Tier)
                {
                    mismatches.Add($"{row.DisplayName}: tier {species.Tier} != the {row.Tier} its sheet " +
                                   $"stats ({row.Attack}/{row.Health}/{row.Speed}) resolve to");
                }
                if (species.HealthGrowthPercent != row.HealthGrowthPercent)
                {
                    mismatches.Add($"{row.DisplayName}: health growth {species.HealthGrowthPercent}% != " +
                                   $"the {row.HealthGrowthPercent}% its sheet stats resolve to");
                }
                if (species.BaseAttack != row.TierStats.Attack || species.BaseHealth != row.TierStats.Health ||
                    species.BaseSpeed != row.TierStats.Speed)
                {
                    mismatches.Add($"{row.DisplayName}: stats {species.BaseAttack}/{species.BaseHealth}/" +
                                   $"{species.BaseSpeed} != the {row.TierStats.Attack}/{row.TierStats.Health}/" +
                                   $"{row.TierStats.Speed} the sheet resolves to");
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

        /// <summary>The tier table is derived, not authored, so the rule that derives it is worth
        /// pinning independently of whether the assets happen to match: every species sits at least
        /// in the band its real base-stat total falls in (the "an evolution is at least one tier up"
        /// pass only ever raises one), spends exactly its tier's points, and the two examples the
        /// design was written against come out exactly as specified.</summary>
        [Test]
        public void TheTierTable_FollowsFromTheSheetsRealStats()
        {
            var roster = SpeciesRosterImporter.ReadRoster();

            foreach (var row in roster)
            {
                int band = SpeciesTier.ForBaseStatTotal(row.Attack + row.Health + row.Speed);
                Assert.GreaterOrEqual(row.Tier, band,
                    $"{row.DisplayName} sits below the band its own stats put it in");
                Assert.AreEqual(SpeciesTier.TotalFor(row.Tier),
                    row.TierStats.Attack + row.TierStats.Health + row.TierStats.Speed,
                    $"{row.DisplayName} doesn't spend its tier's points");
            }

            var byName = roster.ToDictionary(r => r.DisplayName);
            AssertLine(byName["Charmander"], tier: 1, attack: 3, health: 4, speed: 1);
            AssertLine(byName["Magikarp"], tier: 1, attack: 2, health: 5, speed: 1);
        }

        /// <summary>Health always beats Attack at the base line, so no mon can trade a lethal blow
        /// with its own mirror before it has earned anything.</summary>
        [Test]
        public void EveryTierLine_PutsMoreInHealthThanAttack()
        {
            var offenders = SpeciesRosterImporter.ReadRoster()
                .Where(r => r.TierStats.Health <= r.TierStats.Attack)
                .Select(r => $"{r.DisplayName} {r.TierStats.Attack}/{r.TierStats.Health}")
                .ToArray();

            CollectionAssert.IsEmpty(offenders, "SpeciesTier.Distribute should cap Attack below Health");
        }

        /// <summary>The growth values have to actually separate a wall from a glass cannon, and the
        /// one species the real games give a single hit point never gains any.</summary>
        [Test]
        public void GrowthValues_FavourHealth_AndSpreadAcrossTheRoster()
        {
            var roster = SpeciesRosterImporter.ReadRoster();
            var byName = roster.ToDictionary(r => r.DisplayName);

            Assert.AreEqual(0, byName["Shedinja"].HealthGrowthPercent, "Shedinja never gains Health");
            Assert.Greater(byName["Metapod"].HealthGrowthPercent, byName["Charmander"].HealthGrowthPercent,
                "a cocoon should bank more of its growth into Health than a starter does");

            foreach (var row in roster.Where(r => r.DisplayName != "Shedinja"))
            {
                Assert.GreaterOrEqual(row.HealthGrowthPercent, SpeciesTier.MinHealthGrowthPercent,
                    $"{row.DisplayName} favours Attack over Health");
                Assert.LessOrEqual(row.HealthGrowthPercent, 100, row.DisplayName);
            }
        }

        private static void AssertLine(SpeciesRosterImporter.RosterRow row, int tier, int attack, int health, int speed)
        {
            Assert.AreEqual(tier, row.Tier, $"{row.DisplayName} tier");
            Assert.AreEqual($"{attack}/{health}/{speed}",
                $"{row.TierStats.Attack}/{row.TierStats.Health}/{row.TierStats.Speed}",
                $"{row.DisplayName} stat line");
        }

        /// <summary>Most of the roster is Speed 1, and that has to stay true: Speed drives the charge
        /// meter against a three-point threshold, so it's the difference between one passive a fight
        /// and three, and a roster where everything is fast is a roster where Speed says nothing.</summary>
        [Test]
        public void SpeedStaysScarce_AcrossTheRoster()
        {
            var roster = SpeciesRosterImporter.ReadRoster();
            var speeds = roster.Select(r => r.TierStats.Speed).ToArray();

            Assert.IsTrue(speeds.All(s => s >= 1 && s <= SpeciesTier.MaxSpeed), "Speed is a number from 1 to 3");
            Assert.Greater(speeds.Count(s => s == 1), roster.Count / 2, "most of the roster should be Speed 1");
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
