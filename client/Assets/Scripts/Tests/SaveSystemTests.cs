using System.Collections.Generic;
using NUnit.Framework;
using Pets.Data;
using Pets.Gameplay;
using UnityEngine;

namespace Pets.Tests
{
    public class SaveSystemTests
    {
        private CreatureLibrary library;
        private CreatureDefinition creatureA;
        private CreatureDefinition creatureB;

        [SetUp]
        public void SetUp()
        {
            creatureA = ScriptableObject.CreateInstance<CreatureDefinition>();
            creatureA.Id = "save-test-a";
            creatureA.Tier = 1;

            creatureB = ScriptableObject.CreateInstance<CreatureDefinition>();
            creatureB.Id = "save-test-b";
            creatureB.Tier = 2;

            library = ScriptableObject.CreateInstance<CreatureLibrary>();
            library.AllCreatures = new List<CreatureDefinition> { creatureA, creatureB };

            SaveSystem.DeleteSave();
            SaveSystem.DeleteHistory();
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.DeleteSave();
            SaveSystem.DeleteHistory();
        }

        [Test]
        public void SaveThenLoad_RoundTripsGoldLivesRoundBoardAndShop()
        {
            var state = new RunState
            {
                Gold = 7,
                Lives = 2,
                Round = 4,
                Phase = GamePhase.Shop,
                Victory = false,
            };
            state.Board.Add(new BoardCreature { Definition = creatureA, Level = 2, BonusAttack = 3, BonusHealth = 1 });
            state.Board.Add(new BoardCreature { Definition = creatureB, Level = 1 });
            state.ShopSlots.Add(new ShopSlot { Offer = creatureB, Frozen = true });
            state.ShopSlots.Add(new ShopSlot { Offer = null, Frozen = false });

            SaveSystem.Save(state);
            bool loaded = SaveSystem.TryLoad(library, out var restored);

            Assert.IsTrue(loaded);
            Assert.AreEqual(7, restored.Gold);
            Assert.AreEqual(2, restored.Lives);
            Assert.AreEqual(4, restored.Round);
            Assert.AreEqual(GamePhase.Shop, restored.Phase);

            Assert.AreEqual(2, restored.Board.Count);
            Assert.AreEqual(creatureA, restored.Board[0].Definition);
            Assert.AreEqual(2, restored.Board[0].Level);
            Assert.AreEqual(3, restored.Board[0].BonusAttack);
            Assert.AreEqual(1, restored.Board[0].BonusHealth);
            Assert.AreEqual(creatureB, restored.Board[1].Definition);
            Assert.AreEqual(1, restored.Board[1].Level);

            Assert.AreEqual(2, restored.ShopSlots.Count);
            Assert.AreEqual(creatureB, restored.ShopSlots[0].Offer);
            Assert.IsTrue(restored.ShopSlots[0].Frozen);
            Assert.IsNull(restored.ShopSlots[1].Offer);
        }

        [Test]
        public void TryLoad_ReturnsFalseWhenNoSaveExists()
        {
            Assert.IsFalse(SaveSystem.TryLoad(library, out var state));
            Assert.IsNull(state);
        }

        [Test]
        public void DeleteSave_RemovesFileSoNextLoadFails()
        {
            SaveSystem.Save(new RunState());
            SaveSystem.DeleteSave();

            Assert.IsFalse(SaveSystem.TryLoad(library, out _));
        }

        [Test]
        public void LoadHistory_ReturnsEmptyListWhenNoHistoryExists()
        {
            var history = SaveSystem.LoadHistory();

            Assert.IsNotNull(history);
            Assert.AreEqual(0, history.Count);
        }

        [Test]
        public void AppendHistory_RoundTripsEntriesInOrder()
        {
            SaveSystem.AppendHistory(new RunHistoryEntry { CompletedAtUtc = "2026-01-01T00:00:00.0000000Z", RoundReached = 3, Victory = false });
            SaveSystem.AppendHistory(new RunHistoryEntry { CompletedAtUtc = "2026-01-02T00:00:00.0000000Z", RoundReached = 12, Victory = true });

            var history = SaveSystem.LoadHistory();

            Assert.AreEqual(2, history.Count);
            Assert.AreEqual(3, history[0].RoundReached);
            Assert.IsFalse(history[0].Victory);
            Assert.AreEqual(12, history[1].RoundReached);
            Assert.IsTrue(history[1].Victory);
        }

        [Test]
        public void AppendHistory_CapsToMostRecentEntriesDroppingOldest()
        {
            for (int i = 0; i < 101; i++)
            {
                SaveSystem.AppendHistory(new RunHistoryEntry { CompletedAtUtc = $"entry-{i}", RoundReached = i, Victory = false });
            }

            var history = SaveSystem.LoadHistory();

            Assert.AreEqual(100, history.Count);
            Assert.AreEqual(1, history[0].RoundReached, "Oldest entry (round 0) should have been dropped");
            Assert.AreEqual(100, history[history.Count - 1].RoundReached, "Most recent entry should be kept");
        }

        [Test]
        public void DeleteHistory_RemovesFileSoNextLoadIsEmpty()
        {
            SaveSystem.AppendHistory(new RunHistoryEntry { CompletedAtUtc = "2026-01-01T00:00:00.0000000Z", RoundReached = 1, Victory = true });
            SaveSystem.DeleteHistory();

            Assert.AreEqual(0, SaveSystem.LoadHistory().Count);
        }
    }
}
