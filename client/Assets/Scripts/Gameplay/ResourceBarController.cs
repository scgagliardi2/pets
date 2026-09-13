using UnityEngine;
using UnityEngine.UI;
using Pets.Meta;

namespace Pets.Gameplay
{
    /// <summary>Top-of-screen Morale/Money/Badges readout — the guide's "Run Resource Icons" row
    /// (style guide section 8), rendered as text pending real icon art (see PLAN.md). Shown in the
    /// title bar of the two screens a run is spent on: the Location Map and the Region Hub.</summary>
    public sealed class ResourceBarController : MonoBehaviour
    {
        [SerializeField] private Text moraleValue;
        [SerializeField] private Text moneyValue;

        /// <summary>Optional, so a bar built without a badge readout still refreshes.</summary>
        [SerializeField] private Text badgesValue;

        public void Refresh(RunState state)
        {
            moraleValue.text = $"Morale {state.Morale}";
            moneyValue.text = $"Money {state.Money}";
            if (badgesValue != null)
            {
                badgesValue.text = $"Badges {state.BadgeCount}/{RunProgression.BadgesToWin}";
            }
        }
    }
}
