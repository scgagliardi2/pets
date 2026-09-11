using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Pets.Data
{
    /// <summary>Registry used to resolve a PokemonInstance.PassiveId back to its authoring asset
    /// — e.g. for an item-granted passive override (content-schema.md §7, not yet implemented).
    /// Normal battle assembly doesn't need this: PokemonInstanceFactory bakes a species' passive
    /// straight from PokemonSpeciesDefinitionAsset.Passive.</summary>
    [CreateAssetMenu(fileName = "PassiveLibrary", menuName = "Pets/Passive Library")]
    public sealed class PassiveLibrary : ScriptableObject
    {
        public List<PassiveDefinitionAsset> AllPassives = new List<PassiveDefinitionAsset>();

        public PassiveDefinitionAsset GetById(string id)
        {
            return AllPassives.FirstOrDefault(p => p.Id == id);
        }
    }
}
