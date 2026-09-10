using System.Collections.Generic;
using System.Linq;
using Pets.Simulation;

namespace Pets.Data
{
    /// <summary>
    /// The one place ScriptableObject authoring data meets the pure Simulation layer. See
    /// content-schema.md §6.
    /// </summary>
    public static class TeamStateConverter
    {
        public static TeamState ToTeamState(BotTeamDefinition botTeam, string teamLabel)
        {
            var slots = botTeam.Slots.Select(s => (s.Creature, s.Level)).ToList();
            return ToTeamState(slots, teamLabel);
        }

        public static TeamState ToTeamState(IReadOnlyList<(CreatureDefinition creature, int level)> slots, string teamLabel)
        {
            var team = new TeamState();
            for (int i = 0; i < slots.Count; i++)
            {
                var (creature, level) = slots[i];
                team.Slots.Add(ToCreatureState(creature, level, $"{teamLabel}:{creature.Id}#{i}"));
            }
            return team;
        }

        public static CreatureState ToCreatureState(CreatureDefinition definition, int level, string instanceId)
        {
            int effectiveLevel = level < 1 ? 1 : level;
            int attack = definition.BaseAttack + (effectiveLevel - 1) * definition.LevelAttackBonus;
            int health = definition.BaseHealth + (effectiveLevel - 1) * definition.LevelHealthBonus;

            return new CreatureState
            {
                InstanceId = instanceId,
                TemplateId = definition.Id,
                DisplayName = definition.DisplayName,
                Attack = attack,
                Health = health,
                MaxHealth = health,
                Level = effectiveLevel,
                Abilities = definition.Abilities.Select(ToAbilityData).ToList(),
            };
        }

        public static AbilityData ToAbilityData(AbilityDefinition definition)
        {
            return new AbilityData
            {
                Trigger = definition.Trigger,
                Effects = definition.Effects.Select(ToEffectData).ToList(),
            };
        }

        public static EffectData ToEffectData(EffectDefinition definition)
        {
            return new EffectData
            {
                Type = definition.Type,
                Target = definition.Target,
                Amount = definition.Amount,
                SummonTemplate = definition.SummonTemplate == null
                    ? null
                    : new CreatureTemplate
                    {
                        Id = definition.SummonTemplate.Id,
                        DisplayName = definition.SummonTemplate.DisplayName,
                        Attack = definition.SummonTemplate.BaseAttack,
                        Health = definition.SummonTemplate.BaseHealth,
                    },
            };
        }
    }
}
