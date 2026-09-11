using UnityEngine;
using UnityEngine.UI;
using Pets.Meta;

namespace Pets.Gameplay
{
    /// <summary>Top-of-screen Morale/Money readout — the guide's "Run Resource Icons" row (style
    /// guide section 8), rendered as text pending real icon art (see PLAN.md). Sits outside the
    /// Location Hub's tabs/overlays so it stays visible across tab switches and PvE/Camp overlays,
    /// matching every key-screen mockup's persistent top bar.</summary>
    public sealed class ResourceBarController : MonoBehaviour
    {
        [SerializeField] private Text moraleValue;
        [SerializeField] private Text moneyValue;

        public void Refresh(RunState state)
        {
            moraleValue.text = $"Morale {state.Morale}";
            moneyValue.text = $"Money {state.Money}";
        }
    }
}
