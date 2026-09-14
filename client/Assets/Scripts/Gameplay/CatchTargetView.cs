using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Pets.Gameplay
{
    /// <summary>Makes the enemy Lead a drop target for a thrown ball (design doc §12.1). Added to
    /// the Lead's sprite object at runtime by BattleScreenController rather than being wired in the
    /// Battle scene, so the scene asset doesn't need editing to turn catching on.
    ///
    /// Only the Lead ever gets one of these: the design doc is explicit that Support and
    /// further-back enemies aren't valid targets until they're Lead themselves. Since the field
    /// slots are fixed objects that get re-bound as mons are promoted (see BattleScreenController's
    /// FieldSlot), the component stays put and the *mon underneath it* changes — which is exactly
    /// the rule, as long as it's only ever attached to the Lead slot.
    ///
    /// Reports the drop and nothing else; the screen decides whether a throw is legal right now and
    /// what it costs, the same division as Team's ReleaseZoneView.</summary>
    public sealed class CatchTargetView : MonoBehaviour, IDropHandler
    {
        /// <summary>Raised with the dropped ball's tier when a ball is released over this target.</summary>
        public event Action<BallDragHandle> BallDropped;

        public void OnDrop(PointerEventData eventData)
        {
            var ball = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponentInParent<BallDragHandle>()
                : null;
            if (ball != null)
            {
                BallDropped?.Invoke(ball);
            }
        }
    }
}
