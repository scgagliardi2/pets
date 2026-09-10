using System.Collections.Generic;
using NUnit.Framework;
using Pets.Data;
using UnityEngine;

namespace Pets.Tests
{
    public class TeamStateConverterTests
    {
        [Test]
        public void ToTeamState_WithBonusStats_AddsBonusesOnTopOfLevelScaling()
        {
            var definition = ScriptableObject.CreateInstance<CreatureDefinition>();
            definition.Id = "bonus-test";
            definition.BaseAttack = 2;
            definition.BaseHealth = 3;
            definition.LevelAttackBonus = 1;
            definition.LevelHealthBonus = 1;

            var slots = new List<(CreatureDefinition creature, int level, int bonusAttack, int bonusHealth)>
            {
                (definition, 2, 5, 4),
            };

            var team = TeamStateConverter.ToTeamState(slots, "player");

            var creature = team.Slots[0];
            // Level 2 base: 2 + 1*1 = 3 attack, 3 + 1*1 = 4 health, plus the shop-earned bonuses.
            Assert.AreEqual(3 + 5, creature.Attack);
            Assert.AreEqual(4 + 4, creature.Health);
            Assert.AreEqual(4 + 4, creature.MaxHealth);
        }

        [Test]
        public void ToTeamState_WithoutBonusTuple_DefaultsBonusesToZero()
        {
            var definition = ScriptableObject.CreateInstance<CreatureDefinition>();
            definition.Id = "no-bonus-test";
            definition.BaseAttack = 4;
            definition.BaseHealth = 6;

            var slots = new List<(CreatureDefinition creature, int level)> { (definition, 1) };

            var team = TeamStateConverter.ToTeamState(slots, "bot");

            Assert.AreEqual(4, team.Slots[0].Attack);
            Assert.AreEqual(6, team.Slots[0].Health);
        }
    }
}
