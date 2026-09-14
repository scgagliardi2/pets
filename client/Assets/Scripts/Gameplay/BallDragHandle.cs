using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Pets.Meta;

namespace Pets.Gameplay
{
    /// <summary>One ball in the battle screen's tray: which tier it throws, and the drag plumbing
    /// that lets it be picked up and dropped on the enemy Lead (design doc §12.1).
    ///
    /// The icon itself follows the pointer rather than a ghost copy, and is snapped back by the
    /// tray rebuilding on drag end — the same approach, and the same reasoning, as the Team
    /// screen's TeamSlotView/TeamPanelController pair: this type only reports the gesture, and
    /// CatchTrayView decides what a completed drag means.
    ///
    /// Also clickable. A tap is a throw at the current Lead, so the mechanic is reachable without a
    /// drag — which matters on a screen where the target may be the size of a thumb, and makes the
    /// throw testable in PlayMode without synthesising pointer drags.</summary>
    public sealed class BallDragHandle : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        private CatchTrayView tray;

        public BallTier Tier { get; private set; }

        /// <summary>False when the run has none of this tier left, or a throw isn't legal right
        /// now — the icon is still drawn (so the tier doesn't vanish from the tray) but won't
        /// drag or click.</summary>
        public bool IsEnabled { get; private set; }

        public RectTransform Icon { get; private set; }

        public void Initialize(CatchTrayView owner, BallTier tier, bool enabled, RectTransform icon)
        {
            tray = owner;
            Tier = tier;
            IsEnabled = enabled;
            Icon = icon;
        }

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

        /// <summary>Fires after the target's OnDrop, so by now a landed throw has already been
        /// resolved and this is only tidying the icon up.</summary>
        public void OnEndDrag(PointerEventData eventData)
        {
            tray.EndBallDrag();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // A drag ends with a click event too on some input modules; ignore it so a completed
            // drag can't also fire a second throw.
            if (IsEnabled && !eventData.dragging)
            {
                tray.ThrowRequested(this);
            }
        }
    }
}
