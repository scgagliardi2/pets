using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using Pets.Gameplay;
using Pets.Simulation;
using Pets.UI;

namespace Pets.Tests
{
    /// <summary>Drives the actual saved Character Select scene: verifies every curated species
    /// gets a card, that picking a Starter removes it from the Secondary grid, and that confirming
    /// hands the chosen pair off to RunBootstrapper via PendingRunSelection (see CLAUDE.md's
    /// "for UI changes, actually click through the flow" convention — this is the automated
    /// analogue of that for a headless environment).</summary>
    public class CharacterSelectScenePlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/CharacterSelect.unity";

        /// <summary>Confirming a pair in this screen starts a real run, and ActiveRun is a static
        /// that outlives the scene — without this it leaks into whatever fixture runs next, where
        /// it looks like a run the player left in progress.</summary>
        [TearDown]
        public void TearDown()
        {
            ActiveRun.End();
            PendingRunSelection.Clear();
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(ScenePath);
#endif
            yield return null;
            yield return null;
        }

        private static Transform GridContent() => GameObject.Find("Canvas").transform.Find("SpeciesScroll/Viewport/Content");

        [UnityTest]
        public IEnumerator Grid_ShowsACardForEveryCuratedSpecies()
        {
            var buttons = GridContent().GetComponentsInChildren<Button>();
            Assert.AreEqual(28, buttons.Length);
            yield break;
        }

        /// <summary>The grid is laid out for a phone held horizontally, where width is plentiful and
        /// height is not: five small cards per row. Asserted here rather than eyeballed because a
        /// GridLayoutGroup will happily overflow its viewport, so the column count and the width
        /// actually available to it have to agree.</summary>
        [UnityTest]
        public IEnumerator Grid_LaysOutExactlyFiveCardsPerRow()
        {
            var content = GridContent().GetComponent<RectTransform>();
            var grid = content.GetComponent<GridLayoutGroup>();
            Assert.AreEqual(GridLayoutGroup.Constraint.FixedColumnCount, grid.constraint);
            Assert.AreEqual(5, grid.constraintCount);

            float required = grid.padding.left + grid.padding.right + 5 * grid.cellSize.x + 4 * grid.spacing.x;
            Assert.LessOrEqual(required, content.rect.width + 0.5f,
                "five columns plus spacing/padding must fit the grid's own width, or the last column hangs off-screen");

            var cards = GridContent().GetComponentsInChildren<Button>();
            Assert.Greater(cards.Length, 5, "need more than one row's worth of cards to prove the row width");
            float firstRowY = cards[0].GetComponent<RectTransform>().anchoredPosition.y;
            int inFirstRow = cards.Count(c => Mathf.Approximately(c.GetComponent<RectTransform>().anchoredPosition.y, firstRowY));
            Assert.AreEqual(5, inFirstRow);
            yield break;
        }

        /// <summary>Landscape-first scaling: the canvas must match the reference *width* so the
        /// grid's five columns divide a known, device-independent width, leaving only height to vary
        /// with aspect ratio (which the pixel-pinned chrome absorbs).</summary>
        [UnityTest]
        public IEnumerator Canvas_ScalesToALandscapeReferenceByWidth()
        {
            var scaler = GameObject.Find("Canvas").GetComponent<CanvasScaler>();
            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Assert.AreEqual(new Vector2(1280f, 720f), scaler.referenceResolution);
            Assert.AreEqual(0f, scaler.matchWidthOrHeight, 0.001f, "match width, so canvas width is always the reference width");

            var canvasRect = GameObject.Find("Canvas").GetComponent<RectTransform>();
            Assert.AreEqual(1280f, canvasRect.rect.width, 0.5f);
            Assert.Greater(canvasRect.rect.width, canvasRect.rect.height, "the reference canvas should be landscape");
            yield break;
        }

        [UnityTest]
        public IEnumerator EveryCard_HasItsPokemonSpriteLoaded()
        {
            var buttons = GridContent().GetComponentsInChildren<Button>();
            foreach (var button in buttons)
            {
                var sprite = button.transform.Find("Sprite")?.GetComponent<Image>();
                Assert.IsNotNull(sprite, $"{button.gameObject.name} has no Sprite child");
                Assert.IsNotNull(sprite.sprite, $"{button.gameObject.name}'s sprite failed to load");
            }
            yield break;
        }

        /// <summary>Companion to EveryCard_HasItsPokemonSpriteLoaded, for the type-badge icons
        /// (Assets/Resources/Sprites/Types via Pets.UI.TypeIconView) that replaced the old plain
        /// "Fire" / "Fire/Flying" text line.</summary>
        [UnityTest]
        public IEnumerator EveryCard_ShowsOneOrTwoLoadedTypeIcons()
        {
            var buttons = GridContent().GetComponentsInChildren<Button>();
            foreach (var button in buttons)
            {
                var typesRow = button.transform.Find("TypesRow");
                Assert.IsNotNull(typesRow, $"{button.gameObject.name} has no TypesRow child");

                var icons = typesRow.GetComponentsInChildren<TypeIconView>();
                Assert.IsTrue(icons.Length == 1 || icons.Length == 2,
                    $"{button.gameObject.name} should show 1 or 2 type icons, found {icons.Length}");

                foreach (var icon in icons)
                {
                    Assert.IsNotNull(icon.Icon.sprite,
                        $"{button.gameObject.name}'s {icon.Type} type icon failed to load a sprite");
                }
            }
            yield break;
        }

        [UnityTest]
        public IEnumerator PickingAStarterThenASecondary_ThenConfirming_StartsARunWithThatExactPair()
        {
            var starterButtons = GridContent().GetComponentsInChildren<Button>();
            string starterCardName = starterButtons[0].gameObject.name;
            starterButtons[0].onClick.Invoke();
            yield return null;

            var secondaryButtons = GridContent().GetComponentsInChildren<Button>();
            Assert.AreEqual(27, secondaryButtons.Length, "the chosen Starter should not reappear in the Secondary grid");
            Assert.IsFalse(secondaryButtons.Any(b => b.gameObject.name == starterCardName));

            secondaryButtons[0].onClick.Invoke();
            yield return null;

            var canvas = GameObject.Find("Canvas").transform;
            var confirmButton = canvas.Find("ConfirmButton").GetComponent<Button>();
            Assert.IsTrue(confirmButton.gameObject.activeInHierarchy);

            string summaryBeforeConfirm = canvas.Find("PromptText").GetComponent<Text>().text;

            confirmButton.onClick.Invoke();

            // Confirming fades out and loads asynchronously (ScreenFade), so the next scene is not
            // up on the following frame the way a synchronous LoadScene left it. Polling for the
            // arriving scene's bootstrapper rather than waiting a fixed number of frames, so this
            // doesn't quietly become a race if the fade duration changes.
            yield return SceneTransitionWait.UntilExists<RunBootstrapper>(
                "confirming should have loaded the Map scene and its RunBootstrapper");
            var state = RunBootstrapper.Instance.State;
            var library = RunBootstrapper.Instance.SpeciesLibrary;
            string leadName = library.GetById(state.LineUp[0].SpeciesId).DisplayName;
            string supportName = library.GetById(state.LineUp[1].SpeciesId).DisplayName;

            StringAssert.Contains(leadName, summaryBeforeConfirm);
            StringAssert.Contains(supportName, summaryBeforeConfirm);
        }

        [UnityTest]
        public IEnumerator SelectingATypeInTheFilterDropdown_NarrowsTheGridToOnlyThatType()
        {
            int allCount = GridContent().GetComponentsInChildren<Button>().Length;

            var typeFilterDropdown = GameObject.Find("Canvas").transform.Find("ToolbarBar/TypeFilterDropdown").GetComponent<Dropdown>();
            Assert.AreEqual("All Types", typeFilterDropdown.options[0].text, "option 0 should be the unfiltered 'All Types' entry");
            Assert.Greater(typeFilterDropdown.options.Count, 1, "the dropdown should list every PokemonType, not just All Types");

            // Selecting option 1 (the first real PokemonType) the same way a player's click on the
            // popup list would — Dropdown.value's setter fires onValueChanged exactly like a real
            // selection does, so this exercises the same code path without needing to open the
            // popup and click a generated item in a headless run.
            typeFilterDropdown.value = 1;
            yield return null;

            var activeType = (PokemonType)System.Enum.Parse(typeof(PokemonType), typeFilterDropdown.options[1].text);

            var filteredButtons = GridContent().GetComponentsInChildren<Button>();
            Assert.Less(filteredButtons.Length, allCount, "filtering by a single type should narrow the grid");
            Assert.Greater(filteredButtons.Length, 0, "the first type in the enum should have at least one curated species");

            foreach (var button in filteredButtons)
            {
                var icons = button.GetComponentsInChildren<TypeIconView>();
                Assert.IsTrue(icons.Any(icon => icon.Type == activeType),
                    $"{button.gameObject.name} should have a type icon matching the active filter ({activeType})");
            }

            // Back to "All Types" should restore the full, unfiltered grid.
            typeFilterDropdown.value = 0;
            yield return null;
            Assert.AreEqual(allCount, GridContent().GetComponentsInChildren<Button>().Length);
        }

        [UnityTest]
        public IEnumerator ClickingSortAttack_OrdersCardsHighestFirst_ThenLowestFirstOnSecondClick()
        {
            var canvas = GameObject.Find("Canvas").transform;
            var sortAttackButton = canvas.Find("ToolbarBar/SortAttackButton").GetComponent<Button>();

            sortAttackButton.onClick.Invoke();
            yield return null;
            AssertOrderedByAttack(descending: true);

            sortAttackButton.onClick.Invoke();
            yield return null;
            AssertOrderedByAttack(descending: false);
        }

        [UnityTest]
        public IEnumerator ClickingReset_RestoresTheUnfilteredUnsortedGrid()
        {
            int allCount = GridContent().GetComponentsInChildren<Button>().Length;
            string[] originalOrder = GridContent().GetComponentsInChildren<Button>().Select(b => b.gameObject.name).ToArray();

            var canvas = GameObject.Find("Canvas").transform;
            var typeFilterDropdown = canvas.Find("ToolbarBar/TypeFilterDropdown").GetComponent<Dropdown>();
            var sortAttackButton = canvas.Find("ToolbarBar/SortAttackButton").GetComponent<Button>();
            var resetButton = canvas.Find("ToolbarBar/ResetFiltersButton").GetComponent<Button>();

            typeFilterDropdown.value = 1;
            sortAttackButton.onClick.Invoke();
            yield return null;

            // Sanity-check the filter+sort actually did something before relying on Reset to undo it.
            Assert.Less(GridContent().GetComponentsInChildren<Button>().Length, allCount);

            resetButton.onClick.Invoke();
            yield return null;

            Assert.AreEqual(0, typeFilterDropdown.value, "Reset should put the Type filter back to 'All Types'");
            string[] resetOrder = GridContent().GetComponentsInChildren<Button>().Select(b => b.gameObject.name).ToArray();
            CollectionAssert.AreEqual(originalOrder, resetOrder, "Reset should restore both the full roster and its original (unsorted) order");
        }

        private static void AssertOrderedByAttack(bool descending)
        {
            var buttons = GridContent().GetComponentsInChildren<Button>();
            Assert.Greater(buttons.Length, 1, "need at least two cards to prove an ordering");

            int previous = descending ? int.MaxValue : int.MinValue;
            foreach (var button in buttons)
            {
                // Matched by content ("ATK 49  HP 45  SPD 45") rather than a fixed child index —
                // the exact set and order of a card's Text children has already shifted once (the
                // type line became icons instead), so pinning to a position here is exactly what
                // broke.
                string statLine = button.GetComponentsInChildren<Text>().Select(t => t.text).First(t => t.StartsWith("ATK "));
                int attack = int.Parse(statLine.Substring("ATK ".Length).Split(' ')[0]);
                if (descending)
                {
                    Assert.LessOrEqual(attack, previous, "cards should be sorted by Attack, highest first");
                }
                else
                {
                    Assert.GreaterOrEqual(attack, previous, "cards should be sorted by Attack, lowest first");
                }
                previous = attack;
            }
        }

        /// <summary>Regression test for a real bug: the dropdown's popup list rendered with every
        /// option scrolled out of the visible viewport (Content's computed height came out ~2x-4x
        /// too tall — first from a stray LayoutGroup fighting Dropdown.Show()'s own item-layout
        /// code, then from Content's pre-Show() sizeDelta being wrong), so opening the Type filter
        /// showed an empty-looking box with no visible option text. Nothing else in this suite
        /// actually opens the dropdown, so this is the only test that would have caught it.</summary>
        [UnityTest]
        public IEnumerator OpeningTheTypeFilterDropdown_LaysOutEveryOptionWithinItsScrollContent()
        {
            var canvas = GameObject.Find("Canvas").transform;
            var dropdown = canvas.Find("ToolbarBar/TypeFilterDropdown").GetComponent<Dropdown>();
            int optionCount = dropdown.options.Count;

            dropdown.Show();
            yield return null;
            yield return null;

            var dropdownList = GameObject.Find("Dropdown List");
            Assert.IsNotNull(dropdownList, "Show() should have created the popup 'Dropdown List'");

            const float itemHeight = 28f;
            var content = dropdownList.transform.Find("Viewport/Content").GetComponent<RectTransform>();
            Assert.AreEqual(optionCount * itemHeight, content.rect.height, 1f,
                "Content's laid-out height should be exactly option count * item height — an inflated " +
                "height here means every option gets scrolled out of the visible viewport");

            var activeToggles = dropdownList.GetComponentsInChildren<Toggle>(true)
                .Where(t => t.gameObject.activeInHierarchy)
                .OrderByDescending(t => t.GetComponent<RectTransform>().anchoredPosition.y)
                .ToArray();
            Assert.AreEqual(optionCount, activeToggles.Length, "every option should produce one visible item");

            for (int i = 1; i < activeToggles.Length; i++)
            {
                float gap = activeToggles[i - 1].GetComponent<RectTransform>().anchoredPosition.y
                    - activeToggles[i].GetComponent<RectTransform>().anchoredPosition.y;
                Assert.AreEqual(itemHeight, gap, 0.5f, $"items {i - 1} and {i} should be exactly one item-height apart, not overlapping or gapped");
            }

            var topItemLabel = activeToggles[0].GetComponentInChildren<Text>();
            Assert.AreEqual("All Types", topItemLabel.text, "the first (unfiltered) option should be the topmost, visible item");
        }
    }
}
