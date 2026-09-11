using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Pets.Simulation;

namespace Pets.Data
{
    /// <summary>Runtime-safe registry of every curated species, since AssetDatabase lookups (as
    /// used by Editor-only content tests) don't work in a built player. See content-schema.md
    /// §11.</summary>
    [CreateAssetMenu(fileName = "PokemonSpeciesLibrary", menuName = "Pets/Pokemon Species Library")]
    public sealed class PokemonSpeciesLibrary : ScriptableObject
    {
        public List<PokemonSpeciesDefinitionAsset> AllSpecies = new List<PokemonSpeciesDefinitionAsset>();

        public PokemonSpeciesDefinitionAsset GetById(int id)
        {
            return AllSpecies.FirstOrDefault(s => s.Id == id);
        }

        public List<PokemonSpeciesDefinitionAsset> GetByType(PokemonType type)
        {
            return AllSpecies.Where(s => s.Type1 == type || (s.HasSecondType && s.Type2 == type)).ToList();
        }
    }
}
