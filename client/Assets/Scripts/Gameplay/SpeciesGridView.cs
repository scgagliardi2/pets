using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Pets.Data;
using Pets.UI;

namespace Pets.Gameplay
{
    /// <summary>The scrollable grid of species cards shared by Character Select and the Pokédex —
    /// the card layout, the pool that backs it, and the rebinding. A plain C# class rather than a
    /// MonoBehaviour (it never needs scene attachment; the screen controller owns it) and in
    /// Gameplay rather than UI because it takes content assets, which Pets.UI deliberately doesn't
    /// reference.
    ///
    /// It exists because the two screens are the same grid: the Pokédex is Character Select
    /// without the picking, and the two drifting apart card-by-card is exactly the kind of
    /// duplication that makes one of them quietly stop matching its PlayMode test.
    ///
    /// Card metrics below are sized against how many REAL pixels they end up with, not just how
    /// they fit the cell: an element's on-screen size is (units / 1280) * screenWidth, so a
    /// 13-unit line is only 13px on a 1280-wide view. Handjet is a thin segmented display font and
    /// mushes into grey below roughly 16px, which is what made the first pass of this card read as
    /// blurry. Everything here therefore fills the cell rather than floating in it, and the text
    /// sits above Theme.FontSizeSmall (13), the theme's documented floor for this font.
    ///
    /// Laid out like a battle panel: name with a sword + attack at its right end, then HP and SPD
    /// as bars. Budget: 12 padding + 96 sprite + 24 name row (the 24-unit sword sets it) + 30
    /// types row + 18 HP bar + 18 SPD bar + 4 one-unit gaps = 202, inside the 205-unit cell (the
    /// types row is two 28-unit TypeIconView icons — see PokemonCardBuilder.AddTypeIcons; each
    /// bar's 18 is its prefab's own LayoutElement, set in UiPrefabBuilder). Grow the scene
    /// builders' CardHeight alongside any of these.</summary>
    public sealed class SpeciesGridView
    {
        /// <summary>The cell height the card metrics below add up to. Both scene builders read it
        /// rather than each hand-typing 205, so a card that grows can't silently overflow one
        /// screen's cell while fitting the other's.</summary>
        public const float CardHeight = 205f;

        private const int CardSpriteHeight = 96;
        private const int CardNameFontSize = 17;
        private const int CardTypesRowHeight = 30;
        // 2x the sword's 12px art, so its pixels stay square.
        private const int CardAttackIconSize = 24;
        // The card's 6 padding plus this clears the TextBox art's 10-unit border, so the rows that
        // run edge to edge (name/attack, the bars) don't sit on the bevel.
        private const int CardSideInset = 6;

        /// <summary>One reusable card in the grid, holding the pieces a rebind has to write to.
        /// The alternative — finding them by name or index on the card each time — is the kind of
        /// thing that breaks silently when a line is added to the card.</summary>
        private sealed class CardView
        {
            public GameObject Root;
            public Image Sprite;
            public PokemonCardBuilder.NameAttackRow NameRow;
            public PokemonCardBuilder.TypeIconRow TypeIcons;
            public HealthBarView HealthBar;
            public StatBarView SpeedBar;
            public PokemonSpeciesDefinitionAsset Species;
        }

        private readonly RectTransform container;
        private readonly GameObject typeIconPrefab;
        private readonly GameObject healthBarPrefab;
        private readonly GameObject speedBarPrefab;
        private readonly Func<Action<PokemonSpeciesDefinitionAsset>> clickHandlerSource;

        /// <summary>Cards are built once and reused, hidden rather than destroyed.
        ///
        /// A filter or sort click used to destroy the whole grid and rebuild it: at 28 species
        /// that was around 210 GameObjects and 700 components torn down and recreated per click,
        /// and Destroy is deferred to end of frame, so both sets existed at once while the layout
        /// system rebuilt. It was a visible hitch on a phone at 28 and the roster is now 183.</summary>
        private readonly List<CardView> pool = new List<CardView>();

        /// <param name="clickHandlerSource">Resolved at click time rather than captured, because a
        /// card built during Character Select's Starter pick is reused for the Secondary pick, when
        /// what a click should do has changed. Return null from it for a grid whose cards do
        /// nothing.</param>
        public SpeciesGridView(RectTransform container, GameObject typeIconPrefab, GameObject healthBarPrefab,
            GameObject speedBarPrefab, Func<Action<PokemonSpeciesDefinitionAsset>> clickHandlerSource)
        {
            this.container = container;
            this.typeIconPrefab = typeIconPrefab;
            this.healthBarPrefab = healthBarPrefab;
            this.speedBarPrefab = speedBarPrefab;
            this.clickHandlerSource = clickHandlerSource;
        }

        /// <summary>Points the grid at <paramref name="species"/>, growing the pool only when the
        /// longest result so far grows.</summary>
        public void Show(IReadOnlyList<PokemonSpeciesDefinitionAsset> species)
        {
            for (int i = 0; i < species.Count; i++)
            {
                if (i == pool.Count)
                {
                    pool.Add(CreateCard());
                }
                Bind(pool[i], species[i]);
            }

            // Whatever the previous, longer result left over. Kept alive for the next refresh that
            // needs it rather than destroyed.
            for (int i = species.Count; i < pool.Count; i++)
            {
                pool[i].Root.SetActive(false);
            }
        }

        public void Clear()
        {
            foreach (var card in pool)
            {
                card.Root.SetActive(false);
            }
        }

        /// <summary>Builds one empty card. The click listener is attached here exactly once —
        /// re-adding one per rebind would stack them up and fire the handler once per refresh the
        /// card had survived — so it resolves both the species *and* the handler at click
        /// time.</summary>
        private CardView CreateCard()
        {
            var go = PokemonCardBuilder.CreateCard(container, "Card");

            var button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();

            var view = new CardView
            {
                Root = go,
                Sprite = PokemonCardBuilder.AddSprite(go.transform, null, CardSpriteHeight),
                // Bold — Handjet's Regular weight is too thin to hold up at card sizes.
                NameRow = PokemonCardBuilder.AddNameAttackRow(go.transform, CardNameFontSize, Theme.TextDark, CardSideInset, CardAttackIconSize),
                TypeIcons = PokemonCardBuilder.AddTypeIconRow(go.transform, typeIconPrefab, CardTypesRowHeight),
                HealthBar = PokemonCardBuilder.AddStatBar<HealthBarView>(go.transform, healthBarPrefab, CardSideInset),
                SpeedBar = PokemonCardBuilder.AddStatBar<StatBarView>(go.transform, speedBarPrefab, CardSideInset),
            };

            button.onClick.AddListener(() =>
            {
                if (view.Species != null)
                {
                    clickHandlerSource?.Invoke()?.Invoke(view.Species);
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
            card.NameRow.Name.text = species.DisplayName;
            card.NameRow.Attack.text = species.BaseAttack.ToString();
            card.TypeIcons.SetTypes(species.Type1, species.HasSecondType, species.Type2);
            // A mon shown here hasn't fought, so its HP bar is full — the readout is what tells the
            // species apart. Speed, by contrast, is drawn against the roster-wide cap, so the bar
            // itself compares species.
            card.HealthBar.SetHealth(species.BaseHealth, species.BaseHealth);
            card.SpeedBar.SetValue(species.BaseSpeed, PokemonSpeciesDefinitionAsset.MaxBaseSpeed);
            card.Root.SetActive(true);
        }
    }
}
