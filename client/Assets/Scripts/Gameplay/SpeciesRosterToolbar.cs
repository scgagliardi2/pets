using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using Pets.Data;
using Pets.Simulation;
using Pets.UI;

namespace Pets.Gameplay
{
    /// <summary>The Type filter + Attack/Speed/Health sort toolbar that sits above a species grid,
    /// shared by Character Select and the Pokédex: it owns the filter/sort state, applies it to a
    /// roster, and paints the sort buttons.
    ///
    /// Split out from the screen controllers alongside <see cref="SpeciesGridView"/>, for the same
    /// reason: the two screens present the same roster the same way, and the toolbar's behaviour
    /// (clicking the active key flips direction, Reset returns to unsorted *and* unfiltered) is the
    /// kind of detail that quietly diverges when it's written twice.
    ///
    /// A plain C# class — it holds three fields of state and touches Buttons it was handed; there's
    /// nothing for a MonoBehaviour to add.</summary>
    public sealed class SpeciesRosterToolbar
    {
        private static readonly PokemonType[] AllTypes = (PokemonType[])Enum.GetValues(typeof(PokemonType));

        private enum SortKey { None, Attack, Speed, Health }

        private readonly Dropdown typeFilterDropdown;
        private readonly Button sortAttackButton;
        private readonly Button sortSpeedButton;
        private readonly Button sortHealthButton;
        private readonly Action onChanged;

        private int typeFilterIndex = -1; // -1 = "All Types"
        private SortKey sortKey = SortKey.None;
        private bool sortDescending = true;

        public SpeciesRosterToolbar(Dropdown typeFilterDropdown, Button sortAttackButton, Button sortSpeedButton,
            Button sortHealthButton, Action onChanged)
        {
            this.typeFilterDropdown = typeFilterDropdown;
            this.sortAttackButton = sortAttackButton;
            this.sortSpeedButton = sortSpeedButton;
            this.sortHealthButton = sortHealthButton;
            this.onChanged = onChanged;

            // Wired here rather than as a scene-builder persistent listener: Dropdown.onValueChanged
            // is a UnityEvent<int> and UnityEventTools' Editor-time helpers only support baking a
            // fixed constant int, not passing the dropdown's actual selected value through.
            typeFilterDropdown.onValueChanged.AddListener(OnTypeFilterChanged);
        }

        /// <summary>Dropdown option 0 is "All Types"; options 1..AllTypes.Length map 1:1 onto
        /// AllTypes, so the dropdown's own value is always one ahead of the AllTypes index.</summary>
        private void OnTypeFilterChanged(int dropdownValue)
        {
            typeFilterIndex = dropdownValue - 1;
            onChanged();
        }

        public void SortByAttack() => SortBy(SortKey.Attack);

        public void SortBySpeed() => SortBy(SortKey.Speed);

        public void SortByHealth() => SortBy(SortKey.Health);

        public void Reset()
        {
            // SetValueWithoutNotify rather than the value setter: onChanged fires once at the end
            // of this method, so we don't also want the dropdown's onValueChanged triggering a
            // second, redundant refresh.
            typeFilterDropdown.SetValueWithoutNotify(0);
            typeFilterIndex = -1;
            sortKey = SortKey.None;
            sortDescending = true;
            onChanged();
        }

        private void SortBy(SortKey key)
        {
            if (sortKey == key)
            {
                sortDescending = !sortDescending;
            }
            else
            {
                sortKey = key;
                sortDescending = true;
            }
            onChanged();
        }

        /// <summary>Applies the active Type filter and stat sort to <paramref name="roster"/>, and
        /// repaints the sort buttons to match.</summary>
        public List<PokemonSpeciesDefinitionAsset> Apply(IEnumerable<PokemonSpeciesDefinitionAsset> roster)
        {
            IEnumerable<PokemonSpeciesDefinitionAsset> filtered = roster;
            if (typeFilterIndex >= 0)
            {
                var type = AllTypes[typeFilterIndex];
                filtered = filtered.Where(s => s.Type1 == type || (s.HasSecondType && s.Type2 == type));
            }

            List<PokemonSpeciesDefinitionAsset> ordered;
            switch (sortKey)
            {
                case SortKey.Attack:
                    ordered = Sort(filtered, s => s.BaseAttack);
                    break;
                case SortKey.Speed:
                    ordered = Sort(filtered, s => s.BaseSpeed);
                    break;
                case SortKey.Health:
                    ordered = Sort(filtered, s => s.BaseHealth);
                    break;
                default:
                    ordered = filtered.ToList();
                    break;
            }

            UpdateVisuals();
            return ordered;
        }

        private List<PokemonSpeciesDefinitionAsset> Sort(IEnumerable<PokemonSpeciesDefinitionAsset> species,
            Func<PokemonSpeciesDefinitionAsset, int> key) =>
            (sortDescending ? species.OrderByDescending(key) : species.OrderBy(key)).ToList();

        private void UpdateVisuals()
        {
            SetSortButtonVisual(sortAttackButton, SortKey.Attack, "ATK");
            SetSortButtonVisual(sortSpeedButton, SortKey.Speed, "SPD");
            SetSortButtonVisual(sortHealthButton, SortKey.Health, "HP");
        }

        private void SetSortButtonVisual(Button button, SortKey key, string label)
        {
            bool active = sortKey == key;
            button.GetComponentInChildren<Text>().text = active ? $"{label} {(sortDescending ? "▼" : "▲")}" : label;
            // Swaps the 9-sliced sprite itself (green = active, blue = inactive) rather than
            // tinting button.image.color — these buttons carry their own art color, and a color
            // multiply over it (e.g. the old orange "selected" tint) muddies rather than
            // highlights it.
            button.image.sprite = active ? Theme.ButtonGreenSprite : Theme.ButtonBlueSprite;
        }
    }
}
