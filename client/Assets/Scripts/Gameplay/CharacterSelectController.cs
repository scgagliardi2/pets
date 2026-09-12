using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Pets.Data;
using Pets.UI;

namespace Pets.Gameplay
{
    /// <summary>Character Select screen (design doc §3): pick a Starter, then a Secondary, then
    /// start a run with that Lead/Support pair. Design doc §3's "3 secondary options" narrowing and
    /// cosmetic customization aren't implemented — both picks show the same grid instead.
    ///
    /// **Not the whole roster.** The library holds all 183 species (PLAN.md §8), and a run has to
    /// start on something a player can grow out of, so this screen offers only the low-total end of
    /// it — see <see cref="MaxStarterStatTotal"/>. The Pokédex (<see cref="PokedexController"/>) is
    /// where the full roster is browsable.
    ///
    /// A Type filter and Attack/Speed/Health sort toggles sit above the grid
    /// (<see cref="SpeciesRosterToolbar"/>) and apply to whichever pick is currently showing —
    /// state carries over between the two picks rather than resetting, since a player filtering for
    /// e.g. Water types likely wants that for both.</summary>
    public sealed class CharacterSelectController : MonoBehaviour
    {
        /// <summary>A species is offerable as a Starter or Secondary only while
        /// BaseAttack + BaseHealth + BaseSpeed is under this.
        ///
        /// The roster is base forms *and* their evolutions all the way to Legendaries, so without a
        /// cap the screen would let a run open on Groudon. 180 is the line that keeps the pick to
        /// first-stage-shaped mons (68 of the 183 today) — it isn't derived from anything in the
        /// sim, so it's a balance number like the stats themselves, and it lives here rather than as
        /// a flag on the asset because it's this screen's rule, not a property of the species.
        /// Evolution (PLAN.md Phase 1) is what's eventually meant to gate this properly.</summary>
        public const int MaxStarterStatTotal = 180;

        [SerializeField] private PokemonSpeciesLibrary speciesLibrary;
        [SerializeField] private Text promptText;
        [SerializeField] private RectTransform gridContainer;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Text confirmButtonLabel;
        [SerializeField] private Dropdown typeFilterDropdown;
        [SerializeField] private Button sortAttackButton;
        [SerializeField] private Button sortSpeedButton;
        [SerializeField] private Button sortHealthButton;
        [SerializeField] private GameObject typeIconPrefab;
        [SerializeField] private GameObject healthBarPrefab;
        [SerializeField] private GameObject speedBarPrefab;

        private PokemonSpeciesDefinitionAsset chosenLead;
        private PokemonSpeciesDefinitionAsset chosenSupport;

        private SpeciesGridView grid;
        private SpeciesRosterToolbar toolbar;
        private List<PokemonSpeciesDefinitionAsset> currentPhaseRoster;
        private Action<PokemonSpeciesDefinitionAsset> currentPhaseHandler;

        /// <summary>The species this screen is allowed to offer. Public so the Pokédex can label
        /// which of its cards are startable and the PlayMode tests can check the grid against the
        /// same rule the screen applies, rather than against a hardcoded count that goes stale the
        /// next time the roster sheet changes.</summary>
        public static bool IsStarterEligible(PokemonSpeciesDefinitionAsset species) =>
            species.BaseStatTotal < MaxStarterStatTotal;

        private void Start()
        {
            confirmButton.gameObject.SetActive(false);
            grid = new SpeciesGridView(gridContainer, typeIconPrefab, healthBarPrefab, speedBarPrefab,
                () => currentPhaseHandler);
            toolbar = new SpeciesRosterToolbar(typeFilterDropdown, sortAttackButton, sortSpeedButton,
                sortHealthButton, RefreshGrid);
            ShowStarterGrid();
        }

        private void ShowStarterGrid()
        {
            promptText.text = "Choose your Starter";
            currentPhaseRoster = speciesLibrary.AllSpecies.Where(IsStarterEligible).ToList();
            currentPhaseHandler = OnStarterChosen;
            RefreshGrid();
        }

        private void ShowSecondaryGrid()
        {
            promptText.text = $"{chosenLead.DisplayName} is your Starter. Choose your Secondary.";
            currentPhaseRoster = speciesLibrary.AllSpecies
                .Where(s => IsStarterEligible(s) && s.Id != chosenLead.Id)
                .ToList();
            currentPhaseHandler = OnSecondaryChosen;
            RefreshGrid();
        }

        private void OnStarterChosen(PokemonSpeciesDefinitionAsset species)
        {
            chosenLead = species;
            ShowSecondaryGrid();
        }

        private void OnSecondaryChosen(PokemonSpeciesDefinitionAsset species)
        {
            chosenSupport = species;
            grid.Clear();
            promptText.text = $"Lead: {chosenLead.DisplayName}   Support: {chosenSupport.DisplayName}";
            confirmButtonLabel.text = "Begin Adventure";
            confirmButton.gameObject.SetActive(true);
        }

        public void OnConfirmClicked()
        {
            PendingRunSelection.Lead = chosenLead;
            PendingRunSelection.Support = chosenSupport;
            ScreenFade.TransitionTo(SceneNames.Map);
        }

        public void OnSortAttackClicked() => toolbar.SortByAttack();
        public void OnSortSpeedClicked() => toolbar.SortBySpeed();
        public void OnSortHealthClicked() => toolbar.SortByHealth();
        public void OnResetClicked() => toolbar.Reset();

        private void RefreshGrid() => grid.Show(toolbar.Apply(currentPhaseRoster));
    }
}
