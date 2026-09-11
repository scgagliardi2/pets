using UnityEngine;
using UnityEngine.UI;
using Pets.Meta;

namespace Pets.Gameplay
{
    /// <summary>Camp node resolution screen (design doc §5.1): grants EXP and a temporary
    /// next-fight buff, then returns to the Map.</summary>
    public sealed class CampPanelController : MonoBehaviour
    {
        [SerializeField] private Text resultText;
        [SerializeField] private LocationFlowController flow;

        public void Begin(RunState state)
        {
            CampResolver.Resolve(state);
            resultText.text = "Your team rested and gained EXP.\nAttack is boosted for the next fight.";
        }

        public void OnContinueClicked()
        {
            flow.OnCampResolved();
        }
    }
}
