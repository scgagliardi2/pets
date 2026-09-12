using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using Pets.Gameplay;
using Pets.UI;
using static Pets.EditorTools.SceneBuilderUtils;

namespace Pets.EditorTools
{
    /// <summary>Builds the Pokémon Center overlay (NodeType.Camp's resolution, design doc §5.1) as a
    /// reusable .prefab: a dimmed modal backdrop with a centred dialog the Map scene drops over
    /// itself when the player walks onto that node.
    ///
    /// Authoring it as a prefab rather than inline in the scene means the panel lives in one
    /// editable asset, fully wired — CampPanelController's resultText field and the Continue
    /// button's onClick persistent listener are both saved on the asset, because both targets live
    /// inside it. CampPanelController.OnContinue is the one thing the host sets after instantiating,
    /// since it's a runtime callback onto a scene-only object: the Camp doesn't own run-flow
    /// orchestration, NodeResolutionController does.
    ///
    /// Re-run via Pets &gt; Build Camp Overlay Prefab any time CampPanelController's serialized
    /// fields change shape (or Pets &gt; Build All Scenes, which rebuilds this and then the Map scene
    /// that instantiates it). This always rebuilds from scratch, so hand edits made directly in
    /// Prefab Mode survive a rebuild only if also reflected here — same contract as
    /// UiPrefabBuilder.</summary>
    public static class CampOverlayPrefabBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/UI/CampOverlay.prefab";

        [MenuItem("Pets/Build Camp Overlay Prefab")]
        public static void Build()
        {
            var (root, dialog) = CreateModal(null, "CampOverlay", NodeOverlayLayout.DialogSize);

            NodeOverlayLayout.AddTitle(dialog, "Pokémon Center");
            var resultText = NodeOverlayLayout.AddMessage(dialog);
            var continueButton = NodeOverlayLayout.AddContinueButton(dialog);

            var controller = root.gameObject.AddComponent<CampPanelController>();
            SetField(controller, "resultText", resultText);
            // OnContinue is deliberately left unset — see the class doc comment.

            UnityEventTools.AddVoidPersistentListener(continueButton.onClick, controller.OnContinueClicked);

            NodeOverlayLayout.SaveAsPrefab(root, PrefabPath);
            Debug.Log($"Camp overlay prefab rebuilt at {PrefabPath}");
        }
    }
}
