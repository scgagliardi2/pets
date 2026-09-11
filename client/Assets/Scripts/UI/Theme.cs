using System.Collections.Generic;
using UnityEngine;
using Pets.Simulation;

namespace Pets.UI
{
    /// <summary>Shared color/typography tokens for the code-generated UI (see
    /// Assets/Editor/SceneBuilderUtils.cs and the scene builders that use it), matching the
    /// "Monster Trails" style-guide palette: cream panels, dark-navy chrome, blue/orange/gray
    /// button states. Deliberately plain, runtime-accessible C# (no MonoBehaviour) so both the
    /// Editor scene builders and Gameplay controllers that recolor UI at runtime (e.g. a win/loss
    /// outcome text, a selected tab) share one source of truth instead of duplicating hex values.
    /// This is a styling pass only — see PLAN.md for the actual imagery/asset gaps it doesn't
    /// attempt to close (no sprites here, just flat-color approximations of the guide's shapes).</summary>
    public static class Theme
    {
        // Screen / panel backgrounds
        public static readonly Color ScreenBg = new Color(0.90f, 0.93f, 0.87f);
        public static readonly Color PanelBg = new Color(0.95f, 0.93f, 0.85f);
        public static readonly Color PanelHeaderBg = new Color(0.16f, 0.22f, 0.29f);

        // Navigation chrome (tab bar, resource bar)
        public static readonly Color ChromeBg = new Color(0.16f, 0.22f, 0.29f);
        public static readonly Color TabSelectedBg = new Color(0.95f, 0.68f, 0.30f);
        public static readonly Color TabUnselectedBg = new Color(0.26f, 0.33f, 0.40f);

        // Text
        public static readonly Color TextDark = new Color(0.14f, 0.16f, 0.19f);
        public static readonly Color TextLight = new Color(0.97f, 0.97f, 0.95f);
        public static readonly Color TextMuted = new Color(0.5f, 0.53f, 0.55f);

        // Button states (guide section 2: Primary/Secondary/Selected/Pressed/Disabled/Danger)
        public static readonly Color ButtonPrimaryBg = new Color(0.30f, 0.56f, 0.85f);
        public static readonly Color ButtonSecondaryBg = new Color(0.97f, 0.96f, 0.92f);
        public static readonly Color ButtonConfirmBg = new Color(0.95f, 0.68f, 0.22f);
        public static readonly Color ButtonDangerBg = new Color(0.82f, 0.30f, 0.30f);
        public static readonly Color ButtonDisabledBg = new Color(0.72f, 0.72f, 0.70f);

        // Semantic accents (battle outcomes, status)
        public static readonly Color Positive = new Color(0.36f, 0.62f, 0.36f);
        public static readonly Color Danger = new Color(0.82f, 0.30f, 0.30f);
        public static readonly Color Special = new Color(0.62f, 0.46f, 0.82f);

        // Typography scale (guide section 14: Screen Title / Section Heading / Body / Small Label)
        public const int FontSizeTitle = 26;
        public const int FontSizeHeading = 20;
        public const int FontSizeBody = 16;
        public const int FontSizeSmall = 13;

        private static Font gameFont;

        /// <summary>The game's standard font (Handjet-Regular, OFL-licensed — see
        /// Assets/Resources/Fonts/Handjet/OFL.txt), lazily loaded once and shared by every piece
        /// of code-generated UI (both the Editor scene builders and runtime controllers that spawn
        /// Text at play time, e.g. PvEClashController's catch buttons). Falls back to Unity's
        /// built-in font only if the asset is somehow missing, so a broken import doesn't take the
        /// whole UI down with it. Bold/italic variants aren't separately imported — legacy
        /// UI.Text.fontStyle synthesizes those from this one weight, same as it did with
        /// LegacyRuntime.ttf before this pass.</summary>
        public static Font GameFont =>
            gameFont != null ? gameFont : gameFont = Resources.Load<Font>("Fonts/Handjet/Handjet-Regular")
                ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private static Sprite buttonBlueSprite;
        private static Sprite buttonGreenSprite;
        private static Sprite buttonRedSprite;
        private static Sprite buttonGraySprite;
        private static Sprite textBoxSprite;

        // Same lazy Resources.Load pattern as GameFont, backing the 9-sliced button/panel art.
        // All five share one 12x12 / 5px-border geometry — see tools/generate_ui_sprites.py for
        // the art itself and UiSpriteImportProcessor for the import settings that make them render
        // correctly as Image.Type.Sliced.
        public static Sprite ButtonBlueSprite =>
            buttonBlueSprite != null ? buttonBlueSprite : buttonBlueSprite = Resources.Load<Sprite>("Sprites/UI/ButtonBlue");

        public static Sprite ButtonGreenSprite =>
            buttonGreenSprite != null ? buttonGreenSprite : buttonGreenSprite = Resources.Load<Sprite>("Sprites/UI/ButtonGreen");

        public static Sprite ButtonRedSprite =>
            buttonRedSprite != null ? buttonRedSprite : buttonRedSprite = Resources.Load<Sprite>("Sprites/UI/ButtonRed");

        public static Sprite ButtonGraySprite =>
            buttonGraySprite != null ? buttonGraySprite : buttonGraySprite = Resources.Load<Sprite>("Sprites/UI/ButtonGray");

        public static Sprite TextBoxSprite =>
            textBoxSprite != null ? textBoxSprite : textBoxSprite = Resources.Load<Sprite>("Sprites/UI/TextBox");

        public enum ButtonStyle { Primary, Secondary, Confirm, Danger, Disabled }

        /// <summary>The 9-sliced sprite for a given button style. Each style gets art in its own
        /// palette rather than a tint over shared blue art: tinting a sprite whose whole read is a
        /// baked 1px highlight/shadow bevel darkens the bevel along with the fill, so a "grey"
        /// button came out as muddy blue instead of neutral grey. Secondary and Disabled share the
        /// slate art and are separated by the Selectable tint in UiButton instead.</summary>
        public static Sprite ButtonSprite(ButtonStyle style)
        {
            switch (style)
            {
                case ButtonStyle.Confirm: return ButtonGreenSprite;
                case ButtonStyle.Danger: return ButtonRedSprite;
                case ButtonStyle.Secondary: return ButtonGraySprite;
                case ButtonStyle.Disabled: return ButtonGraySprite;
                default: return ButtonBlueSprite;
            }
        }

        // Selectable.colors multipliers for the sprite-backed buttons. The art already carries its
        // own colour, so these stay near-white and only shade it: Normal sits slightly below white
        // so that Highlighted has somewhere brighter to go, and Pressed darkens enough to read as
        // a press alongside UiButton's 2px label nudge.
        public static readonly Color ButtonTintNormal = new Color(0.93f, 0.93f, 0.93f);
        public static readonly Color ButtonTintHighlighted = Color.white;
        public static readonly Color ButtonTintPressed = new Color(0.74f, 0.74f, 0.74f);
        public static readonly Color ButtonTintSelected = new Color(0.93f, 0.93f, 0.93f);
        public static readonly Color ButtonTintDisabled = new Color(0.62f, 0.62f, 0.62f, 0.65f);

        public static Color ButtonBackground(ButtonStyle style)
        {
            switch (style)
            {
                case ButtonStyle.Primary: return ButtonPrimaryBg;
                case ButtonStyle.Secondary: return ButtonSecondaryBg;
                case ButtonStyle.Confirm: return ButtonConfirmBg;
                case ButtonStyle.Danger: return ButtonDangerBg;
                case ButtonStyle.Disabled: return ButtonDisabledBg;
                default: return ButtonSecondaryBg;
            }
        }

        public static Color ButtonText(ButtonStyle style)
        {
            switch (style)
            {
                case ButtonStyle.Secondary: return TextDark;
                case ButtonStyle.Disabled: return TextMuted;
                default: return TextLight;
            }
        }

        /// <summary>Approximate standard Pokémon type colors (guide section 5's Type Icons /
        /// Type Label rows only show a handful; this fills in the full 18-type set the same way so
        /// every species card's type label reads consistently).</summary>
        private static readonly Dictionary<PokemonType, Color> TypeColors = new Dictionary<PokemonType, Color>
        {
            { PokemonType.Normal, new Color(0.66f, 0.64f, 0.58f) },
            { PokemonType.Fire, new Color(0.90f, 0.42f, 0.24f) },
            { PokemonType.Water, new Color(0.34f, 0.56f, 0.87f) },
            { PokemonType.Electric, new Color(0.93f, 0.78f, 0.22f) },
            { PokemonType.Grass, new Color(0.42f, 0.68f, 0.36f) },
            { PokemonType.Ice, new Color(0.55f, 0.82f, 0.82f) },
            { PokemonType.Fighting, new Color(0.72f, 0.40f, 0.24f) },
            { PokemonType.Poison, new Color(0.60f, 0.36f, 0.68f) },
            { PokemonType.Ground, new Color(0.75f, 0.62f, 0.38f) },
            { PokemonType.Flying, new Color(0.58f, 0.68f, 0.90f) },
            { PokemonType.Psychic, new Color(0.88f, 0.42f, 0.58f) },
            { PokemonType.Bug, new Color(0.60f, 0.68f, 0.24f) },
            { PokemonType.Rock, new Color(0.62f, 0.54f, 0.36f) },
            { PokemonType.Ghost, new Color(0.44f, 0.36f, 0.58f) },
            { PokemonType.Dragon, new Color(0.38f, 0.36f, 0.78f) },
            { PokemonType.Dark, new Color(0.36f, 0.32f, 0.30f) },
            { PokemonType.Steel, new Color(0.62f, 0.66f, 0.70f) },
            { PokemonType.Fairy, new Color(0.90f, 0.62f, 0.78f) },
        };

        public static Color GetTypeColor(PokemonType type) =>
            TypeColors.TryGetValue(type, out var color) ? color : TextMuted;
    }
}
