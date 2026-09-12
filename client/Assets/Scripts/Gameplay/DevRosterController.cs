using UnityEngine;
using UnityEngine.UI;
using Pets.Data;
using Pets.Meta;
using Pets.UI;

namespace Pets.Gameplay
{
    /// <summary>The dev roster screen: a Character Select-style grid of the whole curated roster
    /// where clicking a card drops that species straight into the run in progress. Exists to make
    /// the rest of the game testable by hand — team management, type synergy, a long Box — without
    /// having to play far enough to catch anything. Duplicates are deliberately allowed; a run
    /// with three Charmanders is a perfectly good test case.
    ///
    /// A dev tool, so it behaves like one: it writes directly to ActiveRun with no cost, no
    /// confirmation and no limit beyond the party's own capacity, and it doesn't pretend to be a
    /// Pokémon Center (adoption with real rules is PLAN.md Phase 1, design doc §12).</summary>
    public sealed class DevRosterController : MonoBehaviour
    {
        // Same card metrics as Character Select's grid, which this screen is a copy of in layout
        // terms — see CharacterSelectController's note on why they're sized the way they are.
        private const int CardSpriteHeight = 96;
        private const int CardNameFontSize = 17;
        private const int CardLineFontSize = 14;
        private const int CardTypesRowHeight = 30;

        [SerializeField] private PokemonSpeciesLibrary speciesLibrary;
        [SerializeField] private RectTransform gridContainer;
        [SerializeField] private Text statusText;
        [SerializeField] private Button partyTargetButton;
        [SerializeField] private Button boxTargetButton;
        [SerializeField] private Text emptyStateText;
        [SerializeField] private GameObject typeIconPrefab;

        private RosterGroup target = RosterGroup.Party;

        /// <summary>Counts everything this screen has added, so repeated adds of the same species
        /// still get distinct instance ids (the Meta resolvers use the same "what it is plus where
        /// it landed" scheme — see CatchResolver).</summary>
        private int addedCount;

        private void Start()
        {
            bool hasRun = ActiveRun.HasRun;
            emptyStateText.gameObject.SetActive(!hasRun);
            gridContainer.gameObject.SetActive(hasRun);
            partyTargetButton.interactable = hasRun;
            boxTargetButton.interactable = hasRun;

            if (!hasRun)
            {
                statusText.text = string.Empty;
                return;
            }

            PopulateGrid();
            UpdateStatus();
            UpdateTargetButtons();
        }

        public void OnTargetPartyClicked() => SetTarget(RosterGroup.Party);

        public void OnTargetBoxClicked() => SetTarget(RosterGroup.Box);

        private void SetTarget(RosterGroup group)
        {
            target = group;
            UpdateTargetButtons();
            UpdateStatus();
        }

        /// <summary>Adds one fresh instance of <paramref name="species"/> to the chosen
        /// collection. A full party falls through to the Box rather than refusing the click — on a
        /// dev screen, "it went somewhere and the status line says where" beats a dead card.</summary>
        private void AddToRun(PokemonSpeciesDefinitionAsset species)
        {
            var state = ActiveRun.State;
            addedCount++;
            var mon = PokemonInstanceFactory.Create(species, $"dev-{species.Id}-{addedCount}");

            bool partyFull = state.LineUp.Count >= RunState.MaxPartySize;
            var landedIn = target == RosterGroup.Party && !partyFull ? RosterGroup.Party : RosterGroup.Box;
            state.CollectionFor(landedIn).Add(mon);

            string where = landedIn == RosterGroup.Party ? "party" : "Box";
            string note = target == RosterGroup.Party && partyFull ? " — party is full" : string.Empty;
            UpdateStatus($"Added {species.DisplayName} to the {where}{note}.");
        }

        private void UpdateStatus(string message = null)
        {
            var state = ActiveRun.State;
            string counts = $"Party {state.LineUp.Count} / {RunState.MaxPartySize}    Box {state.Box.Count}" +
                $"    Adding to {(target == RosterGroup.Party ? "party" : "Box")}";
            statusText.text = string.IsNullOrEmpty(message) ? counts : $"{counts}    {message}";
        }

        /// <summary>Swaps the selected target button's sprite rather than tinting it, for the
        /// reason CharacterSelectController's sort buttons do the same: the art carries its own
        /// colour and a tint over it muddies rather than highlights.</summary>
        private void UpdateTargetButtons()
        {
            partyTargetButton.image.sprite = target == RosterGroup.Party ? Theme.ButtonGreenSprite : Theme.ButtonBlueSprite;
            boxTargetButton.image.sprite = target == RosterGroup.Box ? Theme.ButtonGreenSprite : Theme.ButtonBlueSprite;
        }

        private void PopulateGrid()
        {
            foreach (var species in speciesLibrary.AllSpecies)
            {
                CreateCard(species);
            }
        }

        private void CreateCard(PokemonSpeciesDefinitionAsset species)
        {
            var go = PokemonCardBuilder.CreateCard(gridContainer, $"Card_{species.DisplayName}");

            var button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            button.onClick.AddListener(() => AddToRun(species));

            PokemonCardBuilder.AddSprite(go.transform, PokemonSprites.Load(species), CardSpriteHeight);
            PokemonCardBuilder.AddLine(go.transform, species.DisplayName, CardNameFontSize, FontStyle.Bold, Theme.TextDark);
            PokemonCardBuilder.AddTypeIcons(go.transform, typeIconPrefab, CardTypesRowHeight,
                species.Type1, species.HasSecondType, species.Type2);
            PokemonCardBuilder.AddLine(go.transform,
                $"ATK {species.BaseAttack}  HP {species.BaseHealth}  SPD {species.BaseSpeed}",
                CardLineFontSize, FontStyle.Bold, Theme.TextDark);
        }
    }
}
