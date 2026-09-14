using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Pets.Meta;
using Pets.UI;

namespace Pets.Gameplay
{
    /// <summary>The battle screen's ball tray: one icon per tier with its remaining count, each
    /// draggable onto the enemy Lead (design doc §12.1).
    ///
    /// Built in code rather than as a prefab wired into the Battle scene, because the scene asset
    /// predates catching and adding a tray to it by hand is a change no test can check. Everything
    /// here is ordinary uGUI assembled at runtime against Theme, the same vocabulary the scene
    /// uses, so it looks like the rest of the screen without the scene having to know about it.
    ///
    /// Owns presentation and the drag gesture only. Whether a throw is legal, what it costs and
    /// what it does are BattleScreenController's, reached through <see cref="ThrowRequested"/>.</summary>
    public sealed class CatchTrayView : MonoBehaviour
    {
        private const float DraggedIconAlpha = 0.7f;
        private const int IconSize = 48;

        /// <summary>Raised when the player asks to throw a given tier — by dropping it on the Lead
        /// or by tapping it.</summary>
        public event Action<BallTier> ThrowAttempted;

        private RectTransform row;
        private RectTransform dragLayer;
        private RectTransform draggedIcon;
        private Vector2 draggedIconSize;
        private BallInventory inventory;
        private Func<BallTier, string> oddsLabel;
        private bool throwsAllowed;

        private readonly List<BallDragHandle> handles = new List<BallDragHandle>();

        /// <summary>Builds the tray under <paramref name="parent"/> and binds it to the run's balls.
        /// <paramref name="oddsLabel"/> supplies the per-tier odds caption; it's a callback rather
        /// than a value because the odds change with the Lead's HP and status every Step, and the
        /// tray shouldn't have to be told each time.</summary>
        public void Build(RectTransform parent, RectTransform dragLayerRoot, BallInventory balls,
            Func<BallTier, string> oddsLabel)
        {
            this.inventory = balls;
            this.oddsLabel = oddsLabel;
            dragLayer = dragLayerRoot;

            row = new GameObject("CatchTray", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(parent, false);
            row.anchorMin = new Vector2(0.5f, 0f);
            row.anchorMax = new Vector2(0.5f, 0f);
            row.pivot = new Vector2(0.5f, 0f);
            row.anchoredPosition = new Vector2(0f, 96f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            var fitter = row.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Rebuild();
        }

        /// <summary>Whether throwing is possible at all right now — false while a Step is drawing,
        /// once the fight is over, and in Gym/PvP fights where catching isn't offered. Dims the
        /// whole tray rather than hiding it, so the balls a player is carrying stay visible.</summary>
        public void SetThrowsAllowed(bool allowed)
        {
            throwsAllowed = allowed;
            Rebuild();
        }

        public void Rebuild()
        {
            if (row == null)
            {
                return;
            }
            for (int i = row.childCount - 1; i >= 0; i--)
            {
                Destroy(row.GetChild(i).gameObject);
            }
            handles.Clear();

            foreach (var tier in BallCatalog.AllTiers)
            {
                BuildEntry(tier);
            }
        }

        private void BuildEntry(BallTier tier)
        {
            int count = inventory != null ? inventory.CountOf(tier) : 0;
            bool enabled = throwsAllowed && count > 0;

            var entry = new GameObject($"Ball_{tier}", typeof(RectTransform)).GetComponent<RectTransform>();
            entry.SetParent(row, false);
            var entryLayout = entry.gameObject.AddComponent<VerticalLayoutGroup>();
            entryLayout.spacing = 2f;
            entryLayout.childAlignment = TextAnchor.LowerCenter;
            entryLayout.childForceExpandWidth = false;
            entryLayout.childForceExpandHeight = false;
            entryLayout.childControlWidth = true;
            entryLayout.childControlHeight = true;

            // Odds caption, above the icon — the "weaken it first" loop is only legible if the
            // player can see the number move as the Lead's HP drops.
            var odds = BuildLabel(entry, oddsLabel != null ? oddsLabel(tier) : string.Empty, 14,
                enabled ? Theme.TextDark : Theme.TextMuted);
            odds.name = "Odds";

            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(BallDragHandle));
            var icon = iconObject.GetComponent<RectTransform>();
            icon.SetParent(entry, false);
            var layoutElement = iconObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = IconSize;
            layoutElement.preferredHeight = IconSize;

            var image = iconObject.GetComponent<Image>();
            image.sprite = Theme.PokeballSprite;
            image.preserveAspect = true;
            // Tint carries the tier, since there's one ball sprite and three tiers. Greyed when the
            // tier is spent or throwing isn't legal.
            image.color = enabled ? TintFor(tier) : Theme.ButtonDisabledBg;

            var handle = iconObject.GetComponent<BallDragHandle>();
            handle.Initialize(this, tier, enabled, icon);
            handles.Add(handle);

            var countLabel = BuildLabel(entry, $"x{count}", 16, enabled ? Theme.TextDark : Theme.TextMuted);
            countLabel.name = "Count";
        }

        private static Text BuildLabel(RectTransform parent, string value, int fontSize, Color color)
        {
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.GetComponent<RectTransform>().SetParent(parent, false);
            var text = labelObject.GetComponent<Text>();
            text.text = value;
            text.font = Theme.GameFont;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Color TintFor(BallTier tier)
        {
            switch (tier)
            {
                case BallTier.Great: return Theme.ButtonPrimaryBg;
                case BallTier.Ultra: return Theme.ButtonConfirmBg;
                default: return Theme.ButtonDangerBg;
            }
        }

        internal void BeginBallDrag(BallDragHandle handle, PointerEventData eventData)
        {
            if (dragLayer == null)
            {
                return;
            }
            draggedIcon = handle.Icon;
            draggedIconSize = draggedIcon.rect.size;

            draggedIcon.SetParent(dragLayer, false);
            draggedIcon.anchorMin = draggedIcon.anchorMax = new Vector2(0.5f, 0.5f);
            draggedIcon.pivot = new Vector2(0.5f, 0.5f);
            draggedIcon.sizeDelta = draggedIconSize;

            var group = draggedIcon.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = draggedIcon.gameObject.AddComponent<CanvasGroup>();
            }
            // Reads as picked up, and stops absorbing the raycast so the Lead underneath receives
            // the drop rather than the ball itself.
            group.alpha = DraggedIconAlpha;
            group.blocksRaycasts = false;

            DragBall(handle, eventData);
        }

        internal void DragBall(BallDragHandle handle, PointerEventData eventData)
        {
            if (draggedIcon == null)
            {
                return;
            }
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragLayer, eventData.position, eventData.pressEventCamera, out var local))
            {
                draggedIcon.anchoredPosition = local;
            }
        }

        /// <summary>Ends the gesture. Rebuilding is what snaps the icon home — a throw that landed
        /// has already changed the counts, and one that missed the target has changed nothing, and
        /// both want the same redraw.</summary>
        internal void EndBallDrag()
        {
            draggedIcon = null;
            Rebuild();
        }

        internal void ThrowRequested(BallDragHandle handle)
        {
            if (handle.IsEnabled)
            {
                ThrowAttempted?.Invoke(handle.Tier);
            }
        }
    }
}
