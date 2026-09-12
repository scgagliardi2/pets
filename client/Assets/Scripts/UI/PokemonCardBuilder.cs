using UnityEngine;
using UnityEngine.UI;
using Pets.Simulation;

namespace Pets.UI
{
    /// <summary>The one place a Pokémon "card" — bordered box, sprite, name line, type-icon row,
    /// stat bars, stat line — is assembled, shared by the screens that show one: Character Select's roster
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
            var text = CreateText(card, "Line", fontSize, style, color, TextAnchor.MiddleCenter);
            text.text = content;
            var layoutElement = text.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = fontSize + 6;
            return text;
        }

        /// <summary>A row of two Pets.UI.TypeIconView instances from the TypeIcon prefab the
        /// calling screen was handed by its scene builder. Both are always created and the second
        /// is hidden for a single-typed species, so the same row can be re-pointed at a different
        /// species later without instantiating anything — see <see cref="TypeIconRow.SetTypes"/>
        /// and the pooled grid in CharacterSelectController.</summary>
        public readonly struct TypeIconRow
        {
            public readonly RectTransform Row;
            public readonly TypeIconView First;
            public readonly TypeIconView Second;

            public TypeIconRow(RectTransform row, TypeIconView first, TypeIconView second)
            {
                Row = row;
                First = first;
                Second = second;
            }

            public void SetTypes(PokemonType type1, bool hasSecondType, PokemonType type2)
            {
                First.Type = type1;
                if (hasSecondType)
                {
                    Second.Type = type2;
                }
                Second.gameObject.SetActive(hasSecondType);
            }
        }

        public static TypeIconRow AddTypeIconRow(Transform card, GameObject typeIconPrefab, int height)
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

            return new TypeIconRow(
                row.GetComponent<RectTransform>(),
                AddTypeIcon(row.transform, typeIconPrefab),
                AddTypeIcon(row.transform, typeIconPrefab));
        }

        /// <summary>Builds the row and points it at one species in a single call, for the screens
        /// that build a card once and throw it away.</summary>
        public static RectTransform AddTypeIcons(Transform card, GameObject typeIconPrefab, int height,
            PokemonType type1, bool hasSecondType, PokemonType type2)
        {
            var row = AddTypeIconRow(card, typeIconPrefab, height);
            row.SetTypes(type1, hasSecondType, type2);
            return row.Row;
        }

        /// <summary>One stat bar (HealthBar.prefab → HealthBarView, SpeedBar.prefab → StatBarView)
        /// from a prefab the calling screen was handed by its scene builder, same arrangement as the
        /// TypeIcon prefab. Its height comes from the prefab's own LayoutElement, so the bar's
        /// proportions live only in UiPrefabBuilder — but the caller still has to budget that height
        /// into its card.
        ///
        /// Wrapped in a row that insets it <paramref name="sideInset"/> units beyond the card's
        /// padding: unlike the centred lines, a bar runs edge to edge, so without extra room its
        /// pill and readout sit on the TextBox art's bevel.</summary>
        public static T AddStatBar<T>(Transform card, GameObject barPrefab, float sideInset) where T : Component
        {
            var row = new GameObject($"{barPrefab.name}Row", typeof(RectTransform));
            row.transform.SetParent(card, false);

            var instance = Object.Instantiate(barPrefab, row.transform, false);
            row.AddComponent<LayoutElement>().preferredHeight = instance.GetComponent<LayoutElement>().preferredHeight;

            var rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(sideInset, 0f);
            rect.offsetMax = new Vector2(-sideInset, 0f);

            return instance.GetComponent<T>();
        }

        public readonly struct NameAttackRow
        {
            public readonly Text Name;
            public readonly Text Attack;

            public NameAttackRow(Text name, Text attack)
            {
                Name = name;
                Attack = attack;
            }
        }

        // Wide enough for a three-digit attack at card font sizes; left-aligned inside it so the
        // number hugs the sword and a shorter one leaves its slack at the card edge.
        private const float AttackValueWidth = 30f;
        private const float AttackIconGap = 2f;
        private const float NameToAttackGap = 4f;

        /// <summary>The name, left-aligned, with a sword icon and the attack value at the right end
        /// of the same row — the battle-panel layout, so attack doesn't cost a line of its own.
        /// Named children ("Name", "AttackIcon", "AttackValue") so tests can find them. Long names
        /// shrink (best fit, down to Theme.FontSizeSmall) rather than running under the sword.</summary>
        public static NameAttackRow AddNameAttackRow(Transform card, int fontSize, Color color, float sideInset, float iconSize)
        {
            var row = new GameObject("NameRow", typeof(RectTransform));
            row.transform.SetParent(card, false);
            row.AddComponent<LayoutElement>().preferredHeight = Mathf.Max(fontSize + 6, iconSize);

            var attack = CreateText(row.transform, "AttackValue", fontSize, FontStyle.Bold, color, TextAnchor.MiddleLeft);
            var attackRect = attack.rectTransform;
            attackRect.anchorMin = new Vector2(1f, 0f);
            attackRect.anchorMax = new Vector2(1f, 1f);
            attackRect.pivot = new Vector2(1f, 0.5f);
            attackRect.sizeDelta = new Vector2(AttackValueWidth, 0f);
            attackRect.anchoredPosition = new Vector2(-sideInset, 0f);

            var iconGo = new GameObject("AttackIcon", typeof(RectTransform));
            iconGo.transform.SetParent(row.transform, false);
            var icon = iconGo.AddComponent<Image>();
            icon.sprite = Theme.AttackIconSprite;
            icon.raycastTarget = false;
            var iconRect = icon.rectTransform;
            iconRect.anchorMin = new Vector2(1f, 0.5f);
            iconRect.anchorMax = new Vector2(1f, 0.5f);
            iconRect.pivot = new Vector2(1f, 0.5f);
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            float attackBlockWidth = AttackValueWidth + AttackIconGap + iconSize;
            iconRect.anchoredPosition = new Vector2(-(sideInset + AttackValueWidth + AttackIconGap), 0f);

            var name = CreateText(row.transform, "Name", fontSize, FontStyle.Bold, color, TextAnchor.MiddleLeft);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = Theme.FontSizeSmall;
            name.resizeTextMaxSize = fontSize;
            var nameRect = name.rectTransform;
            nameRect.anchorMin = Vector2.zero;
            nameRect.anchorMax = Vector2.one;
            nameRect.offsetMin = new Vector2(sideInset, 0f);
            nameRect.offsetMax = new Vector2(-(sideInset + attackBlockWidth + NameToAttackGap), 0f);

            return new NameAttackRow(name, attack);
        }

        private static Text CreateText(Transform parent, string name, int fontSize, FontStyle style, Color color, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Theme.GameFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }

        private static TypeIconView AddTypeIcon(Transform parent, GameObject typeIconPrefab)
        {
            var instance = Object.Instantiate(typeIconPrefab, parent, false);
            return instance.GetComponent<TypeIconView>();
        }
    }
}
