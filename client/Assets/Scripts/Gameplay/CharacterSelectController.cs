using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        private const string GameSceneName = "Game";

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
        // line — see AddCardTypeIcons). Grow CharacterSelectSceneBuilder.CardHeight with any of
        // these.
        private const int CardSpriteHeight = 96;
        private const int CardNameFontSize = 17;
        private const int CardLineFontSize = 14;

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
            SceneManager.LoadScene(GameSceneName);
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
        /// repopulates the grid — called on every phase change and every filter/sort click, since
        /// none of those are expensive enough (roster is a few dozen entries) to warrant anything
        /// smarter than a full rebuild.</summary>
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

            PopulateGrid(ordered, currentPhaseHandler);
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

        private void PopulateGrid(List<PokemonSpeciesDefinitionAsset> species, Action<PokemonSpeciesDefinitionAsset> onChosen)
        {
            ClearGrid();
            foreach (var s in species)
            {
                CreateCard(s, onChosen);
            }
        }

        private void ClearGrid()
        {
            for (int i = gridContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(gridContainer.GetChild(i).gameObject);
            }
        }

        private void CreateCard(PokemonSpeciesDefinitionAsset species, Action<PokemonSpeciesDefinitionAsset> onChosen)
        {
            var go = new GameObject($"Card_{species.DisplayName}", typeof(RectTransform));
            go.transform.SetParent(gridContainer, false);

            var image = go.AddComponent<Image>();
            image.sprite = Theme.TextBoxSprite;
            image.type = Image.Type.Sliced;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onChosen(species));

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = 1;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            // Must be true so each line's LayoutElement.preferredHeight is actually honored —
            // see the matching note in SceneBuilderUtils.AddVerticalLayout.
            layout.childControlHeight = true;
            layout.childAlignment = TextAnchor.UpperCenter;

            AddCardSprite(go.transform, species);

            AddCardLine(go.transform, species.DisplayName, CardNameFontSize, FontStyle.Bold, Theme.TextDark);
            AddCardTypeIcons(go.transform, species);
            // All three stats on one line: it buys the height that lets the sprite go back to 96
            // and every line go up a couple of sizes, which matters more for readability than
            // giving Attack a row of its own did. Bold like the name — Handjet's Regular weight is
            // too thin to hold up at this size.
            AddCardLine(go.transform, $"ATK {species.BaseAttack}  HP {species.BaseHealth}  SPD {species.BaseSpeed}",
                CardLineFontSize, FontStyle.Bold, Theme.TextDark);
        }

        private static void AddCardSprite(Transform parent, PokemonSpeciesDefinitionAsset species)
        {
            var go = new GameObject("Sprite", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = PokemonSprites.Load(species);
            image.preserveAspect = true;
            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = CardSpriteHeight;
        }

        /// <summary>One or two Pets.UI.TypeIconView instances (Type1, and Type2 if the species
        /// has one) side by side in a row — replaces what used to be a plain "Fire" / "Fire/Flying"
        /// text line colored via Theme.GetTypeColor, now that real per-type icon art exists
        /// (Assets/Resources/Sprites/Types).</summary>
        private void AddCardTypeIcons(Transform parent, PokemonSpeciesDefinitionAsset species)
        {
            var row = new GameObject("TypesRow", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 6;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var rowLayoutElement = row.AddComponent<LayoutElement>();
            rowLayoutElement.preferredHeight = 30;

            CreateTypeIcon(row.transform, species.Type1);
            if (species.HasSecondType)
            {
                CreateTypeIcon(row.transform, species.Type2);
            }
        }

        private void CreateTypeIcon(Transform parent, PokemonType type)
        {
            var instance = Instantiate(typeIconPrefab, parent, false);
            instance.GetComponent<TypeIconView>().Type = type;
        }

        private static void AddCardLine(Transform parent, string content, int fontSize, FontStyle style, Color color)
        {
            var go = new GameObject("Line", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Theme.GameFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.text = content;
            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = fontSize + 6;
        }
    }
}
