using System;
using UnityEngine;
using UnityEngine.UI;

namespace Pets.Gameplay
{
    /// <summary>The stand-in resolution for the two node types that have no mechanics behind them
    /// yet — Event ("Encounter") and PvP ("Mystery Trainer"), design doc §5.1 and §16. It says so
    /// in as many words and moves on, rather than letting the player walk onto a node and have
    /// nothing at all happen.
    ///
    /// Both the title and the body are set by the host (NodeResolutionController) per node type, so
    /// one prefab covers both; <see cref="OnContinue"/> is assigned at runtime for the same reason
    /// CampPanelController's is. Replace this with the real Event branch (Phase 2) and the real PvP
    /// node (Phase 3) rather than growing it into a general dialogue system.</summary>
    public sealed class NodeEventOverlayController : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text messageText;

        public Action OnContinue;

        public void Show(string title, string message)
        {
            titleText.text = title;
            messageText.text = message;
        }

        public void OnContinueClicked()
        {
            OnContinue?.Invoke();
        }
    }
}
