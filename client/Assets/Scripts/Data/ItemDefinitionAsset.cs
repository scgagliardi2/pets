using UnityEngine;
using Pets.Simulation;

namespace Pets.Data
{
    /// <summary>A holdable item (design doc §13, content-schema.md §7): bought at the Pokémon Center,
    /// kept in the run's bag, and put on one mon from the Team screen. Only the flat stat modifiers are
    /// implemented — `passiveOverride` and `lockable` from the schema aren't, since nothing sold yet
    /// needs them.</summary>
    [CreateAssetMenu(fileName = "Item", menuName = "Pets/Item Definition")]
    public sealed class ItemDefinitionAsset : ScriptableObject
    {
        public string Id;
        public string DisplayName;

        [TextArea]
        public string Description;

        /// <summary>What the Pokémon Center charges for one.</summary>
        public int Price;

        /// <summary>Flat, additive, applied on top of the holder's derived stats for as long as it
        /// holds the item.</summary>
        public Stats StatModifiers;

        public Sprite Icon;
    }
}
