using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Pets.Meta;

namespace Pets.Gameplay
{
    /// <summary>Makes the enemy Lead a drop target for a thrown ball (design doc §12.1). Added to
    /// the Lead's sprite object at runtime by BattleScreenController, which also turns on that
    /// Image's raycastTarget — nothing else on the battlefield is interactive, so it's off by
    /// default and a drop would otherwise pass straight through.
    ///
    /// Only the Lead ever gets one. The design doc is explicit that Support and further-back
    /// enemies aren't valid targets until they're Lead themselves, and since the field slots are
    /// fixed objects that get re-bound as mons are promoted (BattleScreenController's FieldSlot),
    /// the component stays put while the mon underneath it changes — which is exactly the rule, as
    /// long as it's only ever attached to the Lead slot.
    ///
    /// Reports the drop and nothing else; the screen decides whether a throw is legal right now and
    /// what it costs, the same division as Team's ReleaseZoneView.</summary>
    public sealed class CatchTargetView : MonoBehaviour, IDropHandler
    {
        /// <summary>Raised with the dropped ball's tier when one is released over this target.</summary>
        public event Action<BallTier> BallDropped;

        public void OnDrop(PointerEventData eventData)
        {
            var ball = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponentInParent<BallDragHandle>()
                : null;
            if (ball != null && ball.IsEnabled)
            {
                BallDropped?.Invoke(ball.Tier);
            }
        }
    }
}
