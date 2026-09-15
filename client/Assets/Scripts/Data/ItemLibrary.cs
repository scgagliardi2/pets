using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Pets.Data
{
    /// <summary>Runtime registry of every item, so a PokemonInstance.HeldItemId or an id in the run's
    /// bag can be resolved back to its asset in a built player (content-schema.md §11). Also what the
    /// Pokémon Center stocks: every item here is for sale.</summary>
    [CreateAssetMenu(fileName = "ItemLibrary", menuName = "Pets/Item Library")]
    public sealed class ItemLibrary : ScriptableObject
    {
        public List<ItemDefinitionAsset> AllItems = new List<ItemDefinitionAsset>();

        public ItemDefinitionAsset GetById(string id)
        {
            return string.IsNullOrEmpty(id) ? null : AllItems.FirstOrDefault(i => i != null && i.Id == id);
        }
    }
}
