using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using Pets.Gameplay;
using static Pets.EditorTools.SceneBuilderUtils;

namespace Pets.EditorTools
{
    /// <summary>Builds the overlay the map resolves in place (see NodeEventOverlayController): a modal
    /// dialog with a title, a body, a stack of choice buttons for an Event node's encounter, and the
    /// Continue button that follows a choice (and is all the PvP stub has). Wired to its own
    /// controller on the asset, with only the runtime OnContinue/OnChoice callbacks left for the host
    /// scene to set.
    ///
    /// Its title and choices are written at runtime rather than baked, because one prefab covers every
    /// encounter and the PvP stub.
    ///
    /// Re-run via Pets &gt; Build Node Event Overlay Prefab after changing the controller's
    /// serialized fields, or Pets &gt; Build All Scenes to rebuild the Map scene with it.</summary>
    public static class NodeEventOverlayPrefabBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/UI/NodeEventOverlay.prefab";

        [MenuItem("Pets/Build Node Event Overlay Prefab")]
        public static void Build()
        {
            var (root, dialog) = CreateModal(null, "NodeEventOverlay", NodeOverlayLayout.DialogSize);

            var title = NodeOverlayLayout.AddTitle(dialog, "Encounter");
            var message = NodeOverlayLayout.AddMessage(dialog);
            var choices = NodeOverlayLayout.AddChoiceButtons(dialog, NodeEventOverlayController.MaxChoices);
            var continueButton = NodeOverlayLayout.AddContinueButton(dialog);

            var controller = root.gameObject.AddComponent<NodeEventOverlayController>();
            SetField(controller, "titleText", title);
            SetField(controller, "messageText", message);
            SetField(controller, "continueButton", continueButton);
            SetField(controller, "choiceButtons", choices);
            // OnContinue/OnChoice are assigned by the host after instantiating — they point at a scene object.

            UnityEventTools.AddVoidPersistentListener(continueButton.onClick, controller.OnContinueClicked);
            for (int i = 0; i < choices.Length; i++)
            {
                UnityEventTools.AddIntPersistentListener(choices[i].Button.onClick, controller.OnChoiceClicked, i);
            }

            NodeOverlayLayout.SaveAsPrefab(root, PrefabPath);
            Debug.Log($"Node event overlay prefab rebuilt at {PrefabPath}");
        }
    }
}
