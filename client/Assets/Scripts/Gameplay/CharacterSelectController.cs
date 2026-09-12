using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Pets.Data;
using Pets.Simulation;
using Pets.UI;

namespace Pets.Gameplay
{
    /// <summary>Character Select screen (design doc §3): pick a Starter, then a Secondary, from
    /// the full curated roster, then start a run with that Lead/Support pair. Design doc §3's
    /// "3 secondary options" narrowing and cosmetic customization aren't implemented — this current
    /// build shows the whole roster for both picks instead, per explicit scoping for this pass.
    ///
    /// A Type filter Dropdown ("All Types" plus each of the 18 types) and Attack/Speed/Health sort
    /// toggles sit above the grid and apply to whichever pick (Starter or Secondary) is currently
    /// showing — state carries over between the two picks rather than resetting, since a player
    /// filtering for e.g. Water types likely wants that for both picks.</summary>
    public sealed class CharacterSelectController : MonoBehaviour
    {
        // Card metrics for the 200x176 grid cell CharacterSelectSceneBuilder lays out (five columns
        // of a 1280-wide landscape canvas). Sizes are chosen against how many REAL pixels they end
        // up with, not just how they fit the cell: an element's on-screen size is
        // (units / 1280) * screenWidth, so a 13-unit line is only 13px on a 1280-wide view. Handjet
        // is a thin segmented display font and mushes into grey below roughly 16px, which is what
        // made the first pass of this card read as blurry. Everything here is therefore sized to
        // fill the cell rather than float in it — the sprite is back to the 96 the pre-landscape
        // card used, and the three text lines sit above Theme.FontSizeSmall (13), the theme's
        // documented floor for this font, instead of at it.
        //
        // Budget: 12 padding + 96 sprite + 23 name + 30 types row + 20 stats + 3 one-unit gaps =
        // 184, inside the 186 cell (the types row is two 28-unit TypeIconView icons, not a text
        // line — see Pets.UI.PokemonCardBuilder.AddTypeIcons, which builds every card on this
        // screen and on the Team screen). Grow CharacterSelectSceneBuilder.CardHeight with any of
        // these.
        private const int CardSpriteHeight = 96;
        private const int CardNameFontSize = 17;
        private const int CardLineFontSize = 14;
        private const int CardTypesRowHeight = 30;

        private static readonly PokemonType[] AllTypes = (PokemonType[])Enum.GetValues(typeof(PokemonType));

        private enum SortKey { None, Attack, Speed, Health }

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

        private PokemonSpeciesDefinitionAsset chosenLead;
        private PokemonSpeciesDefinitionAsset chosenSupport;

        private List<PokemonSpeciesDefinitionAsset> currentPhaseRoster;
        private Action<PokemonSpeciesDefinitionAsset> currentPhaseHandler;
        private int typeFilterIndex = -1; // -1 = "All Types"
        private SortKey sortKey = SortKey.None;
        private bool sortDescending = true;

        private void Start()
        {
            confirmButton.gameObject.SetActive(false);
            // Wired here rather than as a scene-builder persistent listener: Dropdown.onValueChanged
            // is a UnityEvent<int> and UnityEventTools' Editor-time helpers only support baking a
            // fixed constant int, not passing the dropdown's actual selected value through.
            typeFilterDropdown.onValueChanged.AddListener(OnTypeFilterChanged);
            ShowStarterGrid();
        }

        private void ShowStarterGrid()
        {
            promptText.text = "Choose your Starter";
            currentPhaseRoster = speciesLibrary.AllSpecies;
            currentPhaseHandler = OnStarterChosen;
            RefreshGrid();
        }

        private void ShowSecondaryGrid()
        {
            promptText.text = $"{chosenLead.DisplayName} is your Starter. Choose your Secondary.";
            currentPhaseRoster = speciesLibrary.AllSpecies.Where(s => s.Id != chosenLead.Id).ToList();
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
            ClearGrid();
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

        /// <summary>Wired to typeFilterDropdown.onValueChanged. Dropdown option 0 is "All Types";
        /// options 1..AllTypes.Length map 1:1 onto AllTypes, so the dropdown's own value is always
        /// one ahead of the AllTypes index.</summary>
        public void OnTypeFilterChanged(int dropdownValue)
        {
            typeFilterIndex = dropdownValue - 1;
            RefreshGrid();
        }

        public void OnSortAttackClicked() => OnSortClicked(SortKey.Attack);
        public void OnSortSpeedClicked() => OnSortClicked(SortKey.Speed);
        public void OnSortHealthClicked() => OnSortClicked(SortKey.Health);

        public void OnResetClicked()
        {
            // SetValueWithoutNotify rather than the value setter: we're about to call RefreshGrid
            // ourselves below, so we don't also want the dropdown's onValueChanged firing
            // OnTypeFilterChanged and triggering a second, redundant refresh.
            typeFilterDropdown.SetValueWithoutNotify(0);
            typeFilterIndex = -1;
            sortKey = SortKey.None;
            sortDescending = true;
            RefreshGrid();
        }

        private void OnSortClicked(SortKey key)
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
            RefreshGrid();
        }

        /// <summary>Re-applies the active Type filter and stat sort to currentPhaseRoster and
        /// repoints the grid at the result — called on every phase change and every filter/sort
        /// click.</summary>
        private void RefreshGrid()
        {
            IEnumerable<PokemonSpeciesDefinitionAsset> filtered = currentPhaseRoster;
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

            PopulateGrid(ordered);
            UpdateToolbarVisuals();
        }

        private List<PokemonSpeciesDefinitionAsset> Sort(IEnumerable<PokemonSpeciesDefinitionAsset> species, Func<PokemonSpeciesDefinitionAsset, int> key) =>
            (sortDescending ? species.OrderByDescending(key) : species.OrderBy(key)).ToList();

        private void UpdateToolbarVisuals()
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

        /// <summary>One reusable card in the grid, holding the pieces a rebind has to write to.
        /// The alternative — finding them by name or index on the card each time — is the kind of
        /// thing that breaks silently when a line is added to the card.</summary>
        private sealed class CardView
        {
            public GameObject Root;
            public Button Button;
            public Image Sprite;
            public Text NameLine;
            public PokemonCardBuilder.TypeIconRow TypeIcons;
            public Text StatsLine;
            public PokemonSpeciesDefinitionAsset Species;
        }

        /// <summary>Cards are built once and reused, hidden rather than destroyed.
        ///
        /// A filter or sort click used to Destroy the whole grid and rebuild it: at 28 species
        /// that's around 210 GameObjects and 700 components torn down and recreated per click, and
        /// Destroy is deferred to end of frame, so both sets existed at once while the layout
        /// system rebuilt. It's a visible hitch on a phone today and gets linearly worse as the
        /// roster grows toward the full 183 (PLAN.md §8). Rebinding touches only the handful of
        /// fields that actually differ.</summary>
        private readonly List<CardView> cardPool = new List<CardView>();

        private void PopulateGrid(List<PokemonSpeciesDefinitionAsset> species)
        {
            for (int i = 0; i < species.Count; i++)
            {
                if (i == cardPool.Count)
                {
                    cardPool.Add(CreateCard());
                }
                Bind(cardPool[i], species[i]);
            }

            // Whatever the previous, longer result left over. Kept alive for the next refresh that
            // needs it rather than destroyed.
            for (int i = species.Count; i < cardPool.Count; i++)
            {
                cardPool[i].Root.SetActive(false);
            }
        }

        private void ClearGrid()
        {
            foreach (var card in cardPool)
            {
                card.Root.SetActive(false);
            }
        }

        /// <summary>Builds one empty card. The click listener is attached here exactly once —
        /// re-adding one per rebind would stack them up and fire the handler once per refresh the
        /// card had survived — so it resolves both the species *and* the handler at click time.
        /// Capturing either would be wrong: a card built during the Starter pick gets reused for
        /// the Secondary pick, when both the species it shows and what a click should do have
        /// changed.</summary>
        private CardView CreateCard()
        {
            var go = PokemonCardBuilder.CreateCard(gridContainer, "Card");

            var button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();

            var view = new CardView
            {
                Root = go,
                Button = button,
                Sprite = PokemonCardBuilder.AddSprite(go.transform, null, CardSpriteHeight),
                NameLine = PokemonCardBuilder.AddLine(go.transform, string.Empty, CardNameFontSize, FontStyle.Bold, Theme.TextDark),
                TypeIcons = PokemonCardBuilder.AddTypeIconRow(go.transform, typeIconPrefab, CardTypesRowHeight),
            };
            // All three stats on one line: it buys the height that lets the sprite go back to 96
            // and every line go up a couple of sizes, which matters more for readability than
            // giving Attack a row of its own did. Bold like the name — Handjet's Regular weight is
            // too thin to hold up at this size.
            view.StatsLine = PokemonCardBuilder.AddLine(go.transform, string.Empty, CardLineFontSize, FontStyle.Bold, Theme.TextDark);

            button.onClick.AddListener(() =>
            {
                if (view.Species != null)
                {
                    currentPhaseHandler?.Invoke(view.Species);
                }
            });

            return view;
        }

        private static void Bind(CardView card, PokemonSpeciesDefinitionAsset species)
        {
            card.Species = species;
            // Named for the species so the PlayMode tests (and anyone reading the hierarchy) can
            // still tell the cards apart by object name.
            card.Root.name = $"Card_{species.DisplayName}";
            card.Sprite.sprite = PokemonSprites.Load(species);
            card.NameLine.text = species.DisplayName;
            card.TypeIcons.SetTypes(species.Type1, species.HasSecondType, species.Type2);
            card.StatsLine.text = $"ATK {species.BaseAttack}  HP {species.BaseHealth}  SPD {species.BaseSpeed}";
            card.Root.SetActive(true);
        }
    }
}
