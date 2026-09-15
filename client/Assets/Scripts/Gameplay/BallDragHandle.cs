using UnityEngine;
using UnityEngine.EventSystems;
using Pets.Meta;

namespace Pets.Gameplay
{
    /// <summary>The draggable ball in one row of the tray: which tier it throws, and the drag
    /// plumbing that carries it to the enemy Lead (design doc §12.1).
    ///
    /// Drag only. A tap on the row selects the tier for the Throw button (BallRowSelector) rather
    /// than throwing it — spending a ball should take either a deliberate drag or a press of the
    /// button that says Throw on it, not a stray tap on a 46-pixel row.
    ///
    /// Reports the gesture and nothing else; CatchTrayView owns what a completed drag means, the
    /// same split as the Team screen's TeamSlotView/TeamPanelController.</summary>
    public sealed class BallDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private CatchTrayView tray;

        public BallTier Tier { get; private set; }

        /// <summary>False when the run has none of this tier left, or throwing isn't legal right
        /// now. The ball is still drawn, greyed, so the tier doesn't disappear from the column.</summary>
        public bool IsEnabled { get; private set; }

        public RectTransform Icon { get; private set; }

        public void Initialize(CatchTrayView owner, BallTier tier, bool enabled, RectTransform icon)
        {
            tray = owner;
            Tier = tier;
            IsEnabled = enabled;
            Icon = icon;
        }

        /// <summary>Updated in place every refresh. Rows are built once and mutated rather than
        /// rebuilt, so that a refresh mid-drag can't destroy the icon under the pointer.</summary>
        public void SetEnabled(bool enabled) => IsEnabled = enabled;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (IsEnabled)
            {
                tray.BeginBallDrag(this, eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (IsEnabled)
            {
                tray.DragBall(this, eventData);
            }
        }

        /// <summary>Fires after the drop target's OnDrop, so a landed throw has already resolved and
        /// this only puts the icon back.</summary>
        public void OnEndDrag(PointerEventData eventData)
        {
            tray.EndBallDrag();
        }
    }
}
