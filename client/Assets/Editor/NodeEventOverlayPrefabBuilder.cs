using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using Pets.Gameplay;
using static Pets.EditorTools.SceneBuilderUtils;

namespace Pets.EditorTools
{
    /// <summary>Builds the overlay that stands in for the two node types with no mechanics yet —
    /// Event and PvP (see NodeEventOverlayController). Same shape and the same contract as
    /// CampOverlayPrefabBuilder: a modal dialog wired to its own controller on the asset, with only
    /// the runtime OnContinue callback left for the host scene to set.
    ///
    /// Its title is written at runtime rather than baked, because one prefab covers both node types
    /// ("Encounter" and "Mystery Trainer") — a baked title would need a second prefab to say the
    /// other thing.
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
            var continueButton = NodeOverlayLayout.AddContinueButton(dialog);

            var controller = root.gameObject.AddComponent<NodeEventOverlayController>();
            SetField(controller, "titleText", title);
            SetField(controller, "messageText", message);
            // OnContinue is assigned by the host after instantiating — it points at a scene object.

            UnityEventTools.AddVoidPersistentListener(continueButton.onClick, controller.OnContinueClicked);

            NodeOverlayLayout.SaveAsPrefab(root, PrefabPath);
            Debug.Log($"Node event overlay prefab rebuilt at {PrefabPath}");
        }
    }
}
