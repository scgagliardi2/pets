using System;
using UnityEngine;
using UnityEngine.UI;
using Pets.Meta;

namespace Pets.Gameplay
{
    /// <summary>NodeType.Camp's resolution screen: grants EXP and a temporary next-fight buff
    /// (design doc §5.1's Camp), then reports back to whichever screen is hosting this overlay.
    ///
    /// The node it resolves is shown to the player as the Pokémon Center — its map art and caption
    /// both are — which settles the Camp-vs-Center question PLAN.md left open in favor of the
    /// Center: one stop per Location where the team rests. What resting *does* is still Camp's
    /// EXP + Attack buff, because the Center's own mechanics (adoption, design doc §5.2, and
    /// healing, which needs HP to persist between fights first) aren't built. Both land here when
    /// they are.
    ///
    /// <see cref="OnContinue"/> is assigned at runtime by the host (NodeResolutionController) after
    /// instantiating the CampOverlay prefab, rather than serialized, since the host is a scene-only
    /// object this prefab shouldn't know about — Camp doesn't own run-flow orchestration.</summary>
    public sealed class CampPanelController : MonoBehaviour
    {
        [SerializeField] private Text resultText;

        public Action OnContinue;

        public void Begin(RunState state)
        {
            CampResolver.Resolve(state);
            resultText.text = "Your team rests at the Pokémon Center and gains EXP.\nAttack is boosted for the next fight.";
        }

        public void OnContinueClicked()
        {
            OnContinue?.Invoke();
        }
    }
}
