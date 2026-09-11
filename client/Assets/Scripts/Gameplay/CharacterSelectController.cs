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
            button.image.color = active ? Theme.TabSelectedBg : Theme.ButtonSecondaryBg;
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
            image.color = new Color(0.97f, 0.96f, 0.90f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onChosen(species));

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 2;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            // Must be true so each line's LayoutElement.preferredHeight is actually honored —
            // see the matching note in SceneBuilderUtils.AddVerticalLayout.
            layout.childControlHeight = true;
            layout.childAlignment = TextAnchor.UpperCenter;

            AddCardSprite(go.transform, species);

            string typeLabel = species.HasSecondType ? $"{species.Type1}/{species.Type2}" : species.Type1.ToString();
            AddCardLine(go.transform, species.DisplayName, 18, FontStyle.Bold, Theme.TextDark);
            AddCardLine(go.transform, typeLabel, 14, FontStyle.Bold, Theme.GetTypeColor(species.Type1));
            AddCardLine(go.transform, $"ATK {species.BaseAttack}", 14, FontStyle.Normal, Theme.TextDark);
            AddCardLine(go.transform, $"HP {species.BaseHealth}", 14, FontStyle.Normal, Theme.TextDark);
            AddCardLine(go.transform, $"SPD {species.BaseSpeed}", 14, FontStyle.Normal, Theme.TextDark);
        }

        private static void AddCardSprite(Transform parent, PokemonSpeciesDefinitionAsset species)
        {
            var go = new GameObject("Sprite", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = PokemonSprites.Load(species);
            image.preserveAspect = true;
            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 96;
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
