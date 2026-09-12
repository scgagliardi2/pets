using UnityEngine;
using UnityEngine.UI;
using Pets.Data;

namespace Pets.Gameplay
{
    /// <summary>The Pokédex: the whole curated roster in one browsable grid, reached from Home.
    ///
    /// Character Select only offers the low-total end of the roster
    /// (<see cref="CharacterSelectController.MaxStarterStatTotal"/>), so without this screen most
    /// of the 183 species would only ever be seen as a wild encounter. It's the same grid, filter
    /// and sort as Character Select (<see cref="SpeciesGridView"/>,
    /// <see cref="SpeciesRosterToolbar"/>) with the picking removed: a card press selects it into
    /// the detail line rather than committing to anything, which is also the only place a species'
    /// passive is readable in-game today.
    ///
    /// Read-only by design — it reaches nothing on the run and is happy with no run in progress,
    /// which is why it can hang off Home rather than the in-run menu.</summary>
    public sealed class PokedexController : MonoBehaviour
    {
        [SerializeField] private PokemonSpeciesLibrary speciesLibrary;
        [SerializeField] private RectTransform gridContainer;
        [SerializeField] private Text countText;
        [SerializeField] private Text detailText;
        [SerializeField] private Dropdown typeFilterDropdown;
        [SerializeField] private Button sortAttackButton;
        [SerializeField] private Button sortSpeedButton;
        [SerializeField] private Button sortHealthButton;
        [SerializeField] private GameObject typeIconPrefab;
        [SerializeField] private GameObject healthBarPrefab;
        [SerializeField] private GameObject speedBarPrefab;

        private SpeciesGridView grid;
        private SpeciesRosterToolbar toolbar;

        private void Start()
        {
            detailText.text = "Select a Pokemon to see its details.";
            grid = new SpeciesGridView(gridContainer, typeIconPrefab, healthBarPrefab, speedBarPrefab,
                () => ShowDetail);
            toolbar = new SpeciesRosterToolbar(typeFilterDropdown, sortAttackButton, sortSpeedButton,
                sortHealthButton, RefreshGrid);
            RefreshGrid();
        }

        public void OnSortAttackClicked() => toolbar.SortByAttack();
        public void OnSortSpeedClicked() => toolbar.SortBySpeed();
        public void OnSortHealthClicked() => toolbar.SortByHealth();
        public void OnResetClicked() => toolbar.Reset();

        /// <summary>Repopulates the grid and reports how much of the roster the current filter is
        /// showing — "24 of 183" rather than a bare count, so a filter that hides most of the
        /// roster reads as a filter rather than as a short Pokédex.</summary>
        private void RefreshGrid()
        {
            var shown = toolbar.Apply(speciesLibrary.AllSpecies);
            grid.Show(shown);
            countText.text = shown.Count == speciesLibrary.AllSpecies.Count
                ? $"{shown.Count} species"
                : $"{shown.Count} of {speciesLibrary.AllSpecies.Count} species";
        }

        /// <summary>The bottom-bar readout for the selected card. Carries the two things the card
        /// itself has no room for: the species' passive, and whether Character Select would let a
        /// run start on it.</summary>
        private void ShowDetail(PokemonSpeciesDefinitionAsset species)
        {
            string types = species.HasSecondType ? $"{species.Type1}/{species.Type2}" : species.Type1.ToString();
            string passive = species.Passive != null ? species.Passive.DisplayName : "no passive";
            string note = species.IsLegendary
                ? "Legendary"
                : CharacterSelectController.IsStarterEligible(species) ? "Starter-eligible" : "Not a starter";

            detailText.text = $"#{species.Id} {species.DisplayName}   {types}   " +
                $"ATK {species.BaseAttack}  HP {species.BaseHealth}  SPD {species.BaseSpeed}  " +
                $"(total {species.BaseStatTotal})   {passive}   {note}";
        }
    }
}
