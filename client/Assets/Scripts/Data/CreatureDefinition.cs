using System.Collections.Generic;
using UnityEngine;

namespace Pets.Data
{
    /// <summary>Source-of-truth authoring asset for one creature. See content-schema.md §3.</summary>
    [CreateAssetMenu(fileName = "NewCreature", menuName = "Pets/Creature Definition")]
    public sealed class CreatureDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public int Tier = 1;
        public int BaseAttack;
        public int BaseHealth;
        public int LevelAttackBonus;
        public int LevelHealthBonus;
        public Color PlaceholderColor = Color.white;
        public List<AbilityDefinition> Abilities = new List<AbilityDefinition>();
    }
}
