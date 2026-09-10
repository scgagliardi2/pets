using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Pets.Data
{
    /// <summary>
    /// Runtime-safe registry of every creature, since AssetDatabase lookups (as used by the
    /// Editor-only content tests) don't work in a built player. Populated by
    /// Editor/ContentSeeder.cs.
    /// </summary>
    [CreateAssetMenu(fileName = "CreatureLibrary", menuName = "Pets/Creature Library")]
    public sealed class CreatureLibrary : ScriptableObject
    {
        public List<CreatureDefinition> AllCreatures = new List<CreatureDefinition>();

        public List<CreatureDefinition> GetByMaxTier(int tier)
        {
            return AllCreatures.Where(c => c.Tier >= 1 && c.Tier <= tier).ToList();
        }

        public CreatureDefinition GetById(string id)
        {
            return AllCreatures.FirstOrDefault(c => c.Id == id);
        }
    }
}
