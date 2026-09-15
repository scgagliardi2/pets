using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Pets.Meta;
using Pets.UI;

namespace Pets.Gameplay
{
    /// <summary>The ball column beside the Throw button: one row per tier, stacked, each showing its
    /// remaining count and its live catch odds against the current enemy Lead (design doc §12.1).
    ///
    /// Three ways to throw, all ending in the same place:
    /// - **Throw button** — throws the selected tier. The main trigger.
    /// - **Tap a row** — selects that tier (it does *not* throw; selecting and throwing are separate
    ///   so a tap near the edge of a small row can't spend a ball by accident).
    /// - **Drag a row's ball onto the enemy Lead** — throws that tier directly, selection or no.
    ///
    /// Rows are built once and then **updated in place**. They used to be destroyed and rebuilt on
    /// every refresh, and since a refresh happens after every Step, that destroyed the very icon a
    /// player was dragging — the drop then had nothing to land on and no catch ever resolved. Build
    /// once, mutate the labels, is also just less work per Step.</summary>
    public sealed class CatchTrayView : MonoBehaviour
    {
        // Three rows at 46 plus two 6px gaps is exactly the 150 the column is tall, and the widths
        // below are sized for its 140 — see BattleSceneBuilder's BallColumnSize for why it's 140.
        private const float RowHeight = 46f;
        private const float RowGap = 6f;
        private const float BallSize = 30f;
        private const float CountWidth = 34f;
        private const float DraggedIconAlpha = 0.7f;

        private sealed class Row
        {
            public BallTier Tier;
            public RectTransform Root;
            public Image Frame;
            public Image Ball;
            public Text Odds;
            public Text Count;
            public BallDragHandle Handle;
            public RectTransform BallHome;
            public int BallSiblingIndex;
        }

        private readonly List<Row> rows = new List<Row>();
        private RectTransform container;
        private RectTransform dragLayer;
        private BallInventory inventory;
        private Func<BallTier, int> oddsPercent;
        private bool throwsAllowed;
        private RectTransform draggedIcon;
        private Row draggedRow;

        /// <summary>The tier the Throw button will send. Defaults to the weakest stocked tier, so the
        /// button never quietly spends an Ultra Ball before the player has chosen to.</summary>
        public BallTier? Selected { get; private set; }

        /// <summary>True while a ball is being dragged — refreshes leave the rows alone until it's
        /// over, so nothing moves under the pointer mid-gesture.</summary>
        public bool IsDragging => draggedIcon != null;

        public void Build(RectTransform parent, RectTransform dragLayerRoot, BallInventory balls,
            Func<BallTier, int> oddsPercent)
        {
            container = parent;
            dragLayer = dragLayerRoot;
            inventory = balls;
            this.oddsPercent = oddsPercent;

            foreach (var tier in BallCatalog.AllTiers)
            {
                rows.Add(BuildRow(tier, rows.Count));
            }
            SelectDefault();
            Refresh();
        }

        private Row BuildRow(BallTier tier, int index)
        {
            var root = new GameObject($"Ball_{tier}", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            root.SetParent(container, false);
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.offsetMin = new Vector2(0f, 0f);
            root.offsetMax = new Vector2(0f, 0f);
            root.sizeDelta = new Vector2(0f, RowHeight);
            root.anchoredPosition = new Vector2(0f, -index * (RowHeight + RowGap));

            var frame = root.GetComponent<Image>();
            frame.sprite = Theme.SlotDarkSprite;
            frame.type = Image.Type.Sliced;
            // The row is the click target for selecting, so it has to take the raycast.
            frame.raycastTarget = true;

            var selector = root.gameObject.AddComponent<BallRowSelector>();
            selector.Initialize(this, tier);

            var ballGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(BallDragHandle));
            var ball = ballGo.GetComponent<RectTransform>();
            ball.SetParent(root, false);
            ball.anchorMin = ball.anchorMax = new Vector2(0f, 0.5f);
            ball.pivot = new Vector2(0f, 0.5f);
            ball.sizeDelta = new Vector2(BallSize, BallSize);
            ball.anchoredPosition = new Vector2(5f, 0f);

            var ballImage = ballGo.GetComponent<Image>();
            ballImage.sprite = Theme.PokeballSprite;
            ballImage.preserveAspect = true;
            ballImage.raycastTarget = true;

            var handle = ballGo.GetComponent<BallDragHandle>();
            handle.Initialize(this, tier, enabled: false, icon: ball);

            var odds = CreateLabel(root, "Odds", 17, TextAnchor.MiddleLeft);
            odds.rectTransform.anchorMin = new Vector2(0f, 0f);
            odds.rectTransform.anchorMax = new Vector2(1f, 1f);
            odds.rectTransform.offsetMin = new Vector2(BallSize + 10f, 0f);
            odds.rectTransform.offsetMax = new Vector2(-(CountWidth + 4f), 0f);

            var count = CreateLabel(root, "Count", 15, TextAnchor.MiddleRight);
            count.rectTransform.anchorMin = new Vector2(1f, 0f);
            count.rectTransform.anchorMax = new Vector2(1f, 1f);
            count.rectTransform.pivot = new Vector2(1f, 0.5f);
            count.rectTransform.sizeDelta = new Vector2(CountWidth, 0f);
            count.rectTransform.anchoredPosition = new Vector2(-5f, 0f);

            return new Row
            {
                Tier = tier,
                Root = root,
                Frame = frame,
                Ball = ballImage,
                Odds = odds,
                Count = count,
                Handle = handle,
                BallHome = root,
                BallSiblingIndex = ball.GetSiblingIndex()
            };
        }

        private static Text CreateLabel(RectTransform parent, string name, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var text = go.GetComponent<Text>();
            go.GetComponent<RectTransform>().SetParent(parent, false);
            text.font = Theme.GameFont;
            text.fontSize = fontSize;
            text.color = Theme.TextLight;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>Whether throwing is legal right now — false while a Step is drawing, once the
        /// fight is over, and when the run has no balls. Rows stay visible either way, so a player
        /// can see what they're out of.</summary>
        public void SetThrowsAllowed(bool allowed)
        {
            throwsAllowed = allowed;
            Refresh();
        }

        /// <summary>Updates counts, odds and enabled state in place. Cheap enough to call after
        /// every Step, which is what keeps the odds honest as the target weakens.</summary>
        public void Refresh()
        {
            if (rows.Count == 0 || IsDragging)
            {
                return;
            }

            // A tier that has run out shouldn't stay selected — the Throw button would sit there
            // disabled with balls still in the column.
            if (Selected.HasValue && !inventory.Has(Selected.Value))
            {
                SelectDefault();
            }

            foreach (var row in rows)
            {
                int count = inventory.CountOf(row.Tier);
                bool usable = throwsAllowed && count > 0;

                row.Handle.SetEnabled(usable);
                row.Count.text = $"x{count}";
                row.Odds.text = count > 0 && oddsPercent != null ? $"{oddsPercent(row.Tier)}%" : "--";

                bool isSelected = Selected.HasValue && Selected.Value == row.Tier;
                row.Frame.sprite = isSelected ? Theme.SlotGoldSprite : Theme.SlotDarkSprite;
                row.Ball.color = count > 0 ? TintFor(row.Tier) : Theme.ButtonDisabledBg;
                row.Odds.color = usable ? Theme.TextLight : Theme.TextMuted;
                row.Count.color = usable ? Theme.TextLight : Theme.TextMuted;
            }
        }

        /// <summary>Picks the weakest tier the run still has. Weakest rather than best so the Throw
        /// button's default never spends a good ball the player was saving.</summary>
        private void SelectDefault()
        {
            Selected = null;
            foreach (var tier in BallCatalog.AllTiers)
            {
                if (inventory.Has(tier))
                {
                    Selected = tier;
                    return;
                }
            }
        }

        internal void Select(BallTier tier)
        {
            if (inventory.Has(tier))
            {
                Selected = tier;
                Refresh();
            }
        }

        /// <summary>A tier's colour. Shared with the Pokémon Center's shelf (PokemonCenterController), so
        /// a ball looks the same where it's bought as where it's thrown.</summary>
        internal static Color TintFor(BallTier tier)
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
            draggedRow = rows.Find(r => r.Handle == handle);
            draggedIcon = handle.Icon;

            draggedIcon.SetParent(dragLayer, false);
            draggedIcon.anchorMin = draggedIcon.anchorMax = new Vector2(0.5f, 0.5f);
            draggedIcon.pivot = new Vector2(0.5f, 0.5f);
            draggedIcon.sizeDelta = new Vector2(BallSize, BallSize);

            var group = draggedIcon.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = draggedIcon.gameObject.AddComponent<CanvasGroup>();
            }
            // Stops the ball absorbing its own raycast, so the enemy Lead underneath receives the drop.
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

        /// <summary>Puts the dragged ball back in its row. Runs whether or not the drop landed on
        /// anything — a throw has already been resolved by the target's OnDrop by this point.</summary>
        internal void EndBallDrag()
        {
            if (draggedIcon == null)
            {
                return;
            }

            var group = draggedIcon.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f;
                group.blocksRaycasts = true;
            }

            if (draggedRow != null)
            {
                draggedIcon.SetParent(draggedRow.BallHome, false);
                draggedIcon.SetSiblingIndex(draggedRow.BallSiblingIndex);
                draggedIcon.anchorMin = draggedIcon.anchorMax = new Vector2(0f, 0.5f);
                draggedIcon.pivot = new Vector2(0f, 0.5f);
                draggedIcon.sizeDelta = new Vector2(BallSize, BallSize);
                draggedIcon.anchoredPosition = new Vector2(5f, 0f);
            }

            draggedIcon = null;
            draggedRow = null;
            Refresh();
        }
    }
}
