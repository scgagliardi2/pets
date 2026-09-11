using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Pets.Data;
using Pets.UI;

namespace Pets.Gameplay
{
    /// <summary>Character Select screen (design doc §3): pick a Starter, then a Secondary, from
    /// the full curated roster, then start a run with that Lead/Support pair. Design doc §3's
    /// "3 secondary options" narrowing and cosmetic customization aren't implemented — this current
    /// build shows the whole roster for both picks instead, per explicit scoping for this pass.</summary>
    public sealed class CharacterSelectController : MonoBehaviour
    {
        private const string GameSceneName = "Game";

        [SerializeField] private PokemonSpeciesLibrary speciesLibrary;
        [SerializeField] private Text promptText;
        [SerializeField] private RectTransform gridContainer;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Text confirmButtonLabel;

        private PokemonSpeciesDefinitionAsset chosenLead;
        private PokemonSpeciesDefinitionAsset chosenSupport;

        private void Start()
        {
            confirmButton.gameObject.SetActive(false);
            ShowStarterGrid();
        }

        private void ShowStarterGrid()
        {
            promptText.text = "Choose your Starter";
            PopulateGrid(speciesLibrary.AllSpecies, OnStarterChosen);
        }

        private void ShowSecondaryGrid()
        {
            promptText.text = $"{chosenLead.DisplayName} is your Starter. Choose your Secondary.";
            var remaining = speciesLibrary.AllSpecies.Where(s => s.Id != chosenLead.Id).ToList();
            PopulateGrid(remaining, OnSecondaryChosen);
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
