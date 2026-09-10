using NUnit.Framework;
using Pets.Data;
using Pets.Simulation;
using UnityEditor;

namespace Pets.Tests
{
    /// <summary>
    /// Smoke-tests the actual generated starter content (client/Assets/Content, produced by
    /// Editor/ContentSeeder.cs) through TeamStateConverter and BattleSimulator — the only tests
    /// that exercise real ScriptableObject assets rather than hand-built Simulation POCOs.
    /// </summary>
    public class BotRosterContentTests
    {
        [Test]
        public void StarterRosterExists()
        {
            var guids = AssetDatabase.FindAssets("t:BotTeamDefinition", new[] { "Assets/Content/BotRoster" });
            Assert.IsTrue(guids.Length > 0, "Run Pets/Generate Starter Content first.");
        }

        [Test]
        public void EveryBotRoundConvertsAndRunsAgainstItself()
        {
            var guids = AssetDatabase.FindAssets("t:BotTeamDefinition", new[] { "Assets/Content/BotRoster" });
            Assume.That(guids.Length, Is.GreaterThan(0), "Run Pets/Generate Starter Content first.");

            foreach (var guid in guids)
            {
                var botTeam = AssetDatabase.LoadAssetAtPath<BotTeamDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                var teamA = TeamStateConverter.ToTeamState(botTeam, "A");
                var teamB = TeamStateConverter.ToTeamState(botTeam, "B");

                Assert.AreEqual(botTeam.Slots.Count, teamA.Slots.Count, $"round {botTeam.Round}: slot count mismatch");

                var log = BattleSimulator.Run(teamA, teamB, seed: botTeam.Round);

                // A team mirrored against itself is symmetric, so it can never be a loss for
                // either side outright — the point of this test is that conversion + a full
                // battle run against real content completes cleanly, not any specific outcome.
                Assert.IsTrue(log.Outcome == BattleOutcome.TeamAWins
                    || log.Outcome == BattleOutcome.TeamBWins
                    || log.Outcome == BattleOutcome.Draw);
            }
        }
    }
}
