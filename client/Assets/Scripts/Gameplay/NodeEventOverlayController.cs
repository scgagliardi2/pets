using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Pets.Meta;
using Pets.UI;

namespace Pets.Gameplay
{
    /// <summary>The modal the map resolves in place: an Event node's encounter (Meta/RoadEvents —
    /// a title, a scene and up to <see cref="MaxChoices"/> choices, then what the choice did with a
    /// Continue under it), and the PvP stub ("Mystery Trainer"), which is still a title, a line saying
    /// it isn't built, and Continue.
    ///
    /// Choice buttons and Continue share the bottom of the dialog and are never shown together. Both
    /// callbacks are assigned at runtime by the host (NodeResolutionController), since it's a
    /// scene-only object this prefab shouldn't know about.</summary>
    public sealed class NodeEventOverlayController : MonoBehaviour
    {
        public const int MaxChoices = 3;

        [SerializeField] private Text titleText;
        [SerializeField] private Text messageText;
        [SerializeField] private Button continueButton;
        [SerializeField] private UiButton[] choiceButtons;

        public Action OnContinue;
        public Action<int> OnChoice;

        public Text TitleText => titleText;
        public Text MessageText => messageText;

        /// <summary>A message with only Continue under it.</summary>
        public void Show(string title, string message)
        {
            titleText.text = title;
            messageText.text = message;
            ShowChoiceButtons(null);
            continueButton.gameObject.SetActive(true);
        }

        /// <summary>An encounter waiting on a choice. Unavailable choices are drawn but dead.</summary>
        public void ShowChoices(string title, string message, IReadOnlyList<RoadEventChoice> choices)
        {
            titleText.text = title;
            messageText.text = message;
            ShowChoiceButtons(choices);
            continueButton.gameObject.SetActive(false);
        }

        public void OnContinueClicked()
        {
            OnContinue?.Invoke();
        }

        /// <summary>A choice button, wired with its *slot* by NodeEventOverlayPrefabBuilder; raises
        /// <see cref="OnChoice"/> with the choice that slot is showing.</summary>
        public void OnChoiceClicked(int slot)
        {
            int choice = slot - (choiceButtons.Length - shownChoices);
            if (choice >= 0 && choice < shownChoices)
            {
                OnChoice?.Invoke(choice);
            }
        }

        /// <summary>The button slot choice <paramref name="choice"/> of <paramref name="choiceCount"/>
        /// is drawn in. Choices are bottom-aligned: the last one sits where Continue does, so a
        /// two-choice encounter doesn't leave a gap under its buttons.</summary>
        public static int SlotForChoice(int choice, int choiceCount) => MaxChoices - choiceCount + choice;

        private int shownChoices;

        private void ShowChoiceButtons(IReadOnlyList<RoadEventChoice> choices)
        {
            int count = Mathf.Min(choices?.Count ?? 0, choiceButtons.Length);
            shownChoices = count;
            int offset = choiceButtons.Length - count;
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                int choice = i - offset;
                bool shown = choice >= 0 && choice < count;
                choiceButtons[i].gameObject.SetActive(shown);
                if (shown)
                {
                    choiceButtons[i].Text = choices[choice].Label;
                    choiceButtons[i].Button.interactable = choices[choice].Available;
                }
            }
        }
    }
}
