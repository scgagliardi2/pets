using System.Collections.Generic;

namespace Pets.Simulation
{
    /// <summary>
    /// Plain runtime state for one creature in a battle. Built from a CreatureDefinition by
    /// Data/TeamStateConverter.cs — never authored directly. See battle-sim-spec.md §2 for the
    /// effective-stat formula.
    /// </summary>
    public sealed class CreatureState
    {
        public string InstanceId;
        public string TemplateId;
        public string DisplayName;
        public int Attack;
        public int Health;
        public int MaxHealth;
        public int Level = 1;
        public List<AbilityData> Abilities = new List<AbilityData>();

        public bool IsAlive => Health > 0;

        public List<AbilityData> AbilitiesWithTrigger(TriggerType trigger)
        {
            var result = new List<AbilityData>();
            foreach (var ability in Abilities)
            {
                if (ability.Trigger == trigger)
                {
                    result.Add(ability);
                }
            }
            return result;
        }
    }
}
