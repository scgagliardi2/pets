using UnityEngine;
using UnityEngine.EventSystems;
using Pets.Meta;

namespace Pets.Gameplay
{
    /// <summary>Tapping a ball row selects that tier for the Throw button. Selecting only — it
    /// deliberately doesn't throw, so a mistimed tap on a small row can't spend a ball. Throwing is
    /// the Throw button or a drag onto the enemy Lead.
    ///
    /// Sits on the row frame rather than the ball icon, so the whole row is the target; the ball
    /// itself carries BallDragHandle, which uGUI finds first for a drag and this for a click.</summary>
    public sealed class BallRowSelector : MonoBehaviour, IPointerClickHandler
    {
        private CatchTrayView tray;
        private BallTier tier;

        public void Initialize(CatchTrayView owner, BallTier ballTier)
        {
            tray = owner;
            tier = ballTier;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!eventData.dragging)
            {
                tray.Select(tier);
            }
        }
    }
}
