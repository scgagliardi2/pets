using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Pets.Simulation;

namespace Pets.UI
{
    /// <summary>The two ways a side's team type synergies (Simulation/TeamSynergy, ADR 0015) are
    /// drawn on the battle screen, sharing one place so they can't disagree:
    /// - a **chip row** — a type badge and a count per active synergy, small enough to leave on
    ///   screen for the whole fight;
    /// - a **detail row** — badge, name and the exact effect at that count, for the panel behind the
    ///   Synergies button, where there's room for the numbers.
    ///
    /// Both build into a container the scene builder made and are rebuilt only when the line-up's
    /// synergies change, which in practice is once, as the fight opens: the counts are taken at
    /// battle start and don't move (battle-sim-spec.md §8).</summary>
    public static class SynergyListView
    {
        // Sized against real pixels, not just the space available: an element's on-screen size is
        // (units / 1280) * screenWidth, and Handjet is a thin segmented display font that mushes
        // into grey below roughly 16px (see Theme.GameFont and SpeciesGridView's note). The first
        // pass of this view drew counts at 13 and effect text best-fit down to 9, which rendered as
        // unreadable smudges over the battlefield.
        public const float ChipHeight = 26f;
        public const float ChipIconSize = 24f;
        public const float ChipGap = 6f;
        public const float ChipCountWidth = 30f;

        // Two lines per row — the name over the effect — rather than one: the effects run to a
        // sentence ("the Lead pays 2 HP; the rest +1 Attack and Health") and a single line would
        // either overflow the column or shrink the text past reading size.
        public const float DetailRowHeight = 44f;
        public const float DetailIconSize = 26f;
        public const float DetailTextLeft = DetailIconSize + ChipGap;

        /// <summary>A compact "badge x2" chip per synergy, laid left to right. Returns how many were
        /// drawn, so a caller can hide an empty row rather than leave a bare label on the field.</summary>
        public static int BuildChips(RectTransform row, GameObject typeIconPrefab,
            IReadOnlyList<TeamSynergy.Active> synergies, float startX)
        {
            Clear(row);
            float x = startX;
            foreach (var synergy in synergies)
            {
                var chip = NewRow(row, $"Chip_{synergy.Type}", ChipHeight);
                chip.anchorMin = chip.anchorMax = new Vector2(0f, 0.5f);
                chip.pivot = new Vector2(0f, 0.5f);
                chip.sizeDelta = new Vector2(ChipIconSize + ChipCountWidth, ChipHeight);
                chip.anchoredPosition = new Vector2(x, 0f);

                AddIcon(chip, typeIconPrefab, synergy.Type, ChipIconSize);
                var count = AddLabel(chip, "Count", $"x{synergy.Count}", Theme.FontSizeBody, TextAnchor.MiddleLeft, onField: true);
                Place(count.rectTransform, ChipIconSize + 2f, ChipCountWidth);

                x += ChipIconSize + ChipCountWidth + ChipGap;
            }
            return synergies.Count;
        }

        /// <summary>A row per synergy: badge, "Ember Burst x2", and what that is worth in full.
        /// Stacked downward from the top of <paramref name="column"/>.</summary>
        public static int BuildDetailRows(RectTransform column, GameObject typeIconPrefab,
            IReadOnlyList<TeamSynergy.Active> synergies, string emptyLabel)
        {
            Clear(column);
            if (synergies.Count == 0)
            {
                var none = AddLabel(column, "NoSynergies", emptyLabel, Theme.FontSizeSmall, TextAnchor.UpperLeft, onField: false);
                Stretch(none.rectTransform);
                none.color = Theme.TextMuted;
                return 0;
            }

            for (int i = 0; i < synergies.Count; i++)
            {
                var synergy = synergies[i];
                var row = NewRow(column, $"Synergy_{synergy.Type}", DetailRowHeight);
                row.anchorMin = new Vector2(0f, 1f);
                row.anchorMax = new Vector2(1f, 1f);
                row.pivot = new Vector2(0.5f, 1f);
                row.offsetMin = new Vector2(0f, 0f);
                row.offsetMax = new Vector2(0f, 0f);
                row.sizeDelta = new Vector2(0f, DetailRowHeight);
                row.anchoredPosition = new Vector2(0f, -i * DetailRowHeight);

                AddIcon(row, typeIconPrefab, synergy.Type, DetailIconSize);

                var title = AddLabel(row, "Title", synergy.Title, Theme.FontSizeBody, TextAnchor.LowerLeft, onField: false);
                PlaceLine(title.rectTransform, top: true);

                // Shrinks to fit rather than overflowing its column, since how long this reads
                // depends on which synergy it is — but never below the font's legibility floor. If a
                // line ever needs less than this to fit, widen the panel instead.
                var effect = AddLabel(row, "Effect", synergy.Effect, Theme.FontSizeSmall, TextAnchor.UpperLeft, onField: false);
                PlaceLine(effect.rectTransform, top: false);
                effect.color = Theme.TextMuted;
                effect.horizontalOverflow = HorizontalWrapMode.Wrap;
                effect.resizeTextForBestFit = true;
                effect.resizeTextMinSize = Theme.FontSizeSmall - 1;
                effect.resizeTextMaxSize = Theme.FontSizeSmall;
            }
            return synergies.Count;
        }

        private static void Clear(RectTransform container)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(container.GetChild(i).gameObject);
            }
        }

        private static RectTransform NewRow(RectTransform parent, string name, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            return rect;
        }

        private static void AddIcon(RectTransform row, GameObject typeIconPrefab, PokemonType type, float size)
        {
            if (typeIconPrefab == null)
            {
                return;
            }
            var instance = Object.Instantiate(typeIconPrefab, row, false);
            instance.name = "Icon";
            var view = instance.GetComponent<TypeIconView>();
            if (view != null)
            {
                view.Type = type;
            }
            var rect = (RectTransform)instance.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = Vector2.zero;
        }

        /// <param name="onField">True for the chips, which sit over the battlefield backdrop and so
        /// need the light-with-a-dark-outline treatment the field's other labels get; false for the
        /// panel's rows, which sit on the light TextBox art and read as ordinary dark text.</param>
        private static Text AddLabel(RectTransform parent, string name, string content, int fontSize, TextAnchor anchor, bool onField)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = Theme.GameFont;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = anchor;
            text.color = onField ? Theme.TextLight : Theme.TextDark;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            if (onField)
            {
                var outline = go.AddComponent<Outline>();
                outline.effectColor = Theme.ChromeBg;
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }
            return text;
        }

        /// <summary>The top or bottom half of a detail row, to the right of its badge.</summary>
        private static void PlaceLine(RectTransform rect, bool top)
        {
            rect.anchorMin = new Vector2(0f, top ? 0.5f : 0f);
            rect.anchorMax = new Vector2(1f, top ? 1f : 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = new Vector2(DetailTextLeft, 0f);
            rect.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rect, float x, float width)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(width, ChipHeight);
            rect.anchoredPosition = new Vector2(x, 0f);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
