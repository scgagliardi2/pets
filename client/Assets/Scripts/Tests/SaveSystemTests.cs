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
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.DeleteSave();
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
    }
}
