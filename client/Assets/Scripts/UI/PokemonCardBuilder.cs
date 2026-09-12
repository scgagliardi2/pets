using UnityEngine;
using UnityEngine.UI;
using Pets.Simulation;

namespace Pets.UI
{
    /// <summary>The one place a Pokémon "card" — bordered box, sprite, name line, type-icon row,
    /// stat line — is assembled, shared by the screens that show one: Character Select's roster
    /// grid and the Team screen's party/Box slots. Not a widget or a prefab, just the construction
    /// steps: each caller still owns its own card's height budget, which lines it wants and in
    /// what order, so the two screens can differ (Team adds a role line, Character Select adds a
    /// Button) without this growing options for every difference.
    ///
    /// Runtime code rather than an Editor helper because both callers build their cards at play
    /// time from whatever the roster or the run holds.</summary>
    public static class PokemonCardBuilder
    {
        /// <summary>The card body: the 9-sliced TextBox art as a background plus the vertical
        /// column the Add* calls below fill, top to bottom. Caller adds a Button/LayoutElement on
        /// top of the returned object if its screen needs one.</summary>
        public static GameObject CreateCard(Transform parent, string name, int padding = 6, int spacing = 1)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.sprite = Theme.TextBoxSprite;
            image.type = Image.Type.Sliced;

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            // Must be true so each line's LayoutElement.preferredHeight is actually honored —
            // see the matching note in SceneBuilderUtils.AddVerticalLayout.
            layout.childControlHeight = true;
            layout.childAlignment = TextAnchor.UpperCenter;

            return go;
        }

        public static Image AddSprite(Transform card, Sprite sprite, int height)
        {
            var go = new GameObject("Sprite", typeof(RectTransform));
            go.transform.SetParent(card, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = height;
            return image;
        }

        /// <summary>One text line. Bold by default at every call site so far — Handjet's Regular
        /// weight is too thin to hold up at card sizes (see Theme.GameFont).</summary>
        public static Text AddLine(Transform card, string content, int fontSize, FontStyle style, Color color)
        {
            var go = new GameObject("Line", typeof(RectTransform));
            go.transform.SetParent(card, false);
            var text = go.AddComponent<Text>();
            text.font = Theme.GameFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = content;
            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = fontSize + 6;
            return text;
        }

        /// <summary>One or two Pets.UI.TypeIconView instances — Type1, and Type2 if the species
        /// has one — side by side in a row, from the TypeIcon prefab the calling screen was handed
        /// by its scene builder.</summary>
        public static RectTransform AddTypeIcons(Transform card, GameObject typeIconPrefab, int height,
            PokemonType type1, bool hasSecondType, PokemonType type2)
        {
            var row = new GameObject("TypesRow", typeof(RectTransform));
            row.transform.SetParent(card, false);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 6;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var rowLayoutElement = row.AddComponent<LayoutElement>();
            rowLayoutElement.preferredHeight = height;

            AddTypeIcon(row.transform, typeIconPrefab, type1);
            if (hasSecondType)
            {
                AddTypeIcon(row.transform, typeIconPrefab, type2);
            }

            return row.GetComponent<RectTransform>();
        }

        private static void AddTypeIcon(Transform parent, GameObject typeIconPrefab, PokemonType type)
        {
            var instance = Object.Instantiate(typeIconPrefab, parent, false);
            instance.GetComponent<TypeIconView>().Type = type;
        }
    }
}
