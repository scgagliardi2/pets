using UnityEngine;
using UnityEngine.UI;
using Pets.Simulation;

namespace Pets.UI
{
    /// <summary>Reusable type-badge widget — see Assets/Prefabs/UI/TypeIcon.prefab (built by
    /// Pets.EditorTools.TypeIconPrefabBuilder). Wraps Theme.TypeIconSprite so every place that
    /// wants to show a Pokémon's type (Character Select cards today; team panel/battle screen
    /// later) instantiates one component instead of resolving the Resources path itself. Type is
    /// editable directly in the Inspector (or the prefab itself) and re-applies live via
    /// OnValidate — same pattern as UiButton/UiTextBox, minus any tint ramp, since each type's art
    /// is already a complete colored badge on its own (see Theme.TypeIconSprite).</summary>
    [ExecuteAlways]
    public sealed class TypeIconView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private PokemonType type;

        public Image Icon => icon;

        public PokemonType Type
        {
            get => type;
            set
            {
                type = value;
                Apply();
            }
        }

        private void Awake() => Apply();

        private void OnEnable() => Apply();

        private void OnValidate() => Apply();

        private void Apply()
        {
            if (icon == null)
            {
                return;
            }

            icon.sprite = Theme.TypeIconSprite(type);
            icon.preserveAspect = true;
            // White: the art already carries its own color (see Theme.TypeIconSprite) — tinting
            // here as well is exactly the "double-tint" mistake UiButton's own comment warns about.
            icon.color = Color.white;
        }
    }
}
