using System;
using System.Linq;
using UnityEngine;

namespace Pets.Data
{
    /// <summary>
    /// JsonUtility-based export matching the shape documented in content-schema.md §5. No
    /// Newtonsoft dependency — the data model is deliberately non-polymorphic so JsonUtility is
    /// sufficient.
    /// </summary>
    public static class ContentJsonExporter
    {
        public static string ToJson(CreatureDefinition creature)
        {
            var export = new CreatureExport
            {
                id = creature.Id,
                displayName = creature.DisplayName,
                tier = creature.Tier,
                baseAttack = creature.BaseAttack,
                baseHealth = creature.BaseHealth,
                levelAttackBonus = creature.LevelAttackBonus,
                levelHealthBonus = creature.LevelHealthBonus,
                abilities = creature.Abilities.Select(ToAbilityExport).ToArray(),
            };
            return JsonUtility.ToJson(export, true);
        }

        public static string ToJson(BotTeamDefinition botTeam)
        {
            var export = new BotTeamExport
            {
                round = botTeam.Round,
                slots = botTeam.Slots.Select(s => new BotSlotExport
                {
                    creatureId = s.Creature != null ? s.Creature.Id : "",
                    level = s.Level,
                }).ToArray(),
            };
            return JsonUtility.ToJson(export, true);
        }

        private static AbilityExport ToAbilityExport(AbilityDefinition ability)
        {
            return new AbilityExport
            {
                trigger = ability.Trigger.ToString(),
                effects = ability.Effects.Select(ToEffectExport).ToArray(),
            };
        }

        private static EffectExport ToEffectExport(EffectDefinition effect)
        {
            return new EffectExport
            {
                type = effect.Type.ToString(),
                target = effect.Target.ToString(),
                amount = effect.Amount,
                summonTemplateId = effect.SummonTemplate != null ? effect.SummonTemplate.Id : "",
            };
        }

        [Serializable]
        private sealed class CreatureExport
        {
            public string id;
            public string displayName;
            public int tier;
            public int baseAttack;
            public int baseHealth;
            public int levelAttackBonus;
            public int levelHealthBonus;
            public AbilityExport[] abilities;
        }

        [Serializable]
        private sealed class AbilityExport
        {
            public string trigger;
            public EffectExport[] effects;
        }

        [Serializable]
        private sealed class EffectExport
        {
            public string type;
            public string target;
            public int amount;
            public string summonTemplateId;
        }

        [Serializable]
        private sealed class BotTeamExport
        {
            public int round;
            public BotSlotExport[] slots;
        }

        [Serializable]
        private sealed class BotSlotExport
        {
            public string creatureId;
            public int level;
        }
    }
}
