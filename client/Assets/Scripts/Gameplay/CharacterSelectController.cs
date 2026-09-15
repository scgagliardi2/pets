using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Pets.Data;
using Pets.UI;

namespace Pets.Gameplay
{
    /// <summary>Character Select screen (design doc §3): pick a Starter, then a Secondary, look over
    /// the team those two make, then start a run with that Lead/Support pair — or start over and pick
    /// again. Design doc §3's "3 secondary options" narrowing and cosmetic customization aren't
    /// implemented — both picks show the same grid instead.
    ///
    /// **Not the whole roster.** The library holds all 183 species (PLAN.md §8), and a run has to
    /// start on something a player can grow out of, so this screen offers only the bottom tier of
    /// it — see <see cref="MaxStarterTier"/>. The Pokédex (<see cref="PokedexController"/>) is
    /// where the full roster is browsable.
    ///
    /// A Type filter and Attack/Speed/Health sort toggles sit above the grid
    /// (<see cref="SpeciesRosterToolbar"/>) and apply to whichever pick is currently showing —
    /// state carries over between the two picks rather than resetting, since a player filtering for
    /// e.g. Water types likely wants that for both.
    ///
    /// **The team preview** replaces the grid and toolbar once both picks are in: the pair as the
    /// same cards the grid draws, labelled Lead and Support, with Begin Adventure and Start Over under
    /// them. Nothing is committed until Begin Adventure.</summary>
    public sealed class CharacterSelectController : MonoBehaviour
    {
        /// <summary>Highest species tier offerable as a Starter or Secondary.
        ///
        /// The roster is base forms *and* their evolutions all the way to Legendaries, so without a
        /// cap the screen would let a run open on Groudon. Tier 1 is the whole five-point end of the
        /// roster (54 of the 183 today) — every one of them a 2/2/1-shaped mon with a run's worth of
        /// growing to do, which is exactly the pick this screen is for. It lives here rather than as
        /// a flag on the asset because it's this screen's rule, not a property of the species.</summary>
        public const int MaxStarterTier = SpeciesTier.MinTier;

        public const string StarterPrompt = "Choose your Starter";

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

        [Header("Team preview")]
        [SerializeField] private GameObject toolbarBar;
        [SerializeField] private GameObject speciesScroll;
        [SerializeField] private GameObject teamPreview;
        [SerializeField] private RectTransform teamPreviewCards;
        [SerializeField] private Button startOverButton;

        private PokemonSpeciesDefinitionAsset chosenLead;
        private PokemonSpeciesDefinitionAsset chosenSupport;

        private SpeciesGridView grid;
        private SpeciesGridView previewCards;
        private SpeciesRosterToolbar toolbar;
        private List<PokemonSpeciesDefinitionAsset> currentPhaseRoster;
        private Action<PokemonSpeciesDefinitionAsset> currentPhaseHandler;

        /// <summary>True while the chosen pair is on show, waiting on Begin Adventure or Start Over.</summary>
        public bool IsShowingTeamPreview => teamPreview.activeSelf;

        /// <summary>The species this screen is allowed to offer. Public so the Pokédex can label
        /// which of its cards are startable and the PlayMode tests can check the grid against the
        /// same rule the screen applies, rather than against a hardcoded count that goes stale the
        /// next time the roster sheet changes.</summary>
        public static bool IsStarterEligible(PokemonSpeciesDefinitionAsset species) =>
            species.Tier <= MaxStarterTier;

        private void Start()
        {
            grid = new SpeciesGridView(gridContainer, typeIconPrefab, healthBarPrefab, speedBarPrefab,
                () => currentPhaseHandler);
            // The preview's cards do nothing when pressed — the two buttons under them are the choice.
            previewCards = new SpeciesGridView(teamPreviewCards, typeIconPrefab, healthBarPrefab, speedBarPrefab, () => null);
            toolbar = new SpeciesRosterToolbar(typeFilterDropdown, sortAttackButton, sortSpeedButton,
                sortHealthButton, RefreshGrid);
            ShowStarterGrid();
        }

        private void ShowStarterGrid()
        {
            chosenLead = null;
            chosenSupport = null;
            SetPreviewVisible(false);
            promptText.text = StarterPrompt;
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
            currentPhaseHandler = null;
            grid.Clear();
            promptText.text = $"Your team   Lead: {chosenLead.DisplayName}   Support: {chosenSupport.DisplayName}";
            confirmButtonLabel.text = "Begin Adventure";
            SetPreviewVisible(true);
            previewCards.Show(new[] { chosenLead, chosenSupport });
        }

        public void OnConfirmClicked()
        {
            if (chosenLead == null || chosenSupport == null)
            {
                return;
            }
            PendingRunSelection.Lead = chosenLead;
            PendingRunSelection.Support = chosenSupport;
            // To the Region Hub, which bootstraps the run from this pair and offers its first
            // Location (design doc §3 step 5).
            ScreenFade.TransitionTo(SceneNames.RegionHub);
        }

        /// <summary>Drops both picks and goes back to choosing a Starter. The toolbar's filter and sort
        /// are kept, for the same reason they carry over between the two picks.</summary>
        public void OnStartOverClicked() => ShowStarterGrid();

        public void OnSortAttackClicked() => toolbar.SortByAttack();
        public void OnSortSpeedClicked() => toolbar.SortBySpeed();
        public void OnSortHealthClicked() => toolbar.SortByHealth();
        public void OnResetClicked() => toolbar.Reset();

        /// <summary>The grid and toolbar, or the preview and its two buttons — never both.</summary>
        private void SetPreviewVisible(bool preview)
        {
            teamPreview.SetActive(preview);
            confirmButton.gameObject.SetActive(preview);
            startOverButton.gameObject.SetActive(preview);
            toolbarBar.SetActive(!preview);
            speciesScroll.SetActive(!preview);
            if (!preview)
            {
                previewCards?.Clear();
            }
        }

        private void RefreshGrid()
        {
            // The toolbar is hidden during the preview, but a queued filter change mustn't bring the
            // grid back over it.
            if (currentPhaseHandler == null)
            {
                return;
            }
            grid.Show(toolbar.Apply(currentPhaseRoster));
        }
    }
}
