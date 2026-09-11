using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Pets.Meta;

namespace Pets.Gameplay
{
    /// <summary>Location Map tab (design doc §5.1): shows the Forest's linear node sequence and a
    /// "Go" button for the current, not-yet-cleared node. Branching-path selection is Phase 1.</summary>
    public sealed class MapPanelController : MonoBehaviour
    {
        [SerializeField] private Text nodeSequenceText;
        [SerializeField] private Text currentNodeText;
        [SerializeField] private Button goButton;
        [SerializeField] private Text goButtonLabel;
        [SerializeField] private LocationFlowController flow;

        public void Refresh(RunState state)
        {
            nodeSequenceText.text = string.Join("  ->  ", state.Nodes.Select(n =>
                (n.Cleared ? "[x] " : n == state.CurrentNode ? "[*] " : "[ ] ") + n.Type));

            var current = state.CurrentNode;
            currentNodeText.text = $"Current node: {current.Type}";
            goButtonLabel.text = current.Type == NodeType.PvE ? "Fight!" : "Rest at Camp";
            goButton.interactable = !current.Cleared;
        }

        public void OnGoClicked()
        {
            flow.OnGoClicked();
        }
    }
}
