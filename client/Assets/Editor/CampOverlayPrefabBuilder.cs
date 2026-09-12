using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using Pets.Gameplay;
using Pets.UI;
using static Pets.EditorTools.SceneBuilderUtils;

namespace Pets.EditorTools
{
    /// <summary>Builds the Camp overlay (design doc §5.1) as a real, reusable .prefab instead of
    /// baking it into Game.unity — the first screen migrated under the "author screens as
    /// prefabs" plan (see the Unity-architecture review this follows). Two things this buys over
    /// the old inline-panel approach the retired Forest hub scene used:
    ///
    ///  1. The panel's VerticalLayoutGroup is saved *alive* (root is passed as keepLive to
    ///     ForceLayoutRebuild below, purely so the panel already looks right to anyone opening it
    ///     in Prefab Mode without pressing Play — PrefabUtility.SaveAsPrefabAsset reverts driven
    ///     RectTransform values back to their pre-layout snapshot on save, same as
    ///     EditorSceneManager.SaveScene does for a baked-and-destroyed scene panel; confirmed
    ///     empirically, not assumed). What a live component actually buys is that it recomputes
    ///     on its own the moment this panel is activated — LocationHubController.ShowCampOverlay()
    ///     does exactly that — so the stale bake plays no part once the game is actually running.
    ///     ForestScenePlayModeTests' CampOverlay test activates the panel the same way gameplay
    ///     does and then asserts ResultText/ContinueButton come out with a real (nonzero) height,
    ///     catching an inactive-hierarchy collapse that a check that skipped activation missed.
    ///     A live LayoutGroup also means this panel could grow a second dynamic row later (e.g. a
    ///     per-mon EXP breakdown) without needing a keepLive escape hatch.
    ///  2. The prefab is fully wired — CampPanelController's resultText field and the Continue
    ///     button's onClick persistent listener are both saved on the asset itself, because both
    ///     targets live inside the same prefab. (`flow` is the one field the scene still has to
    ///     set after instantiating, since it points at a scene-only object — LocationFlowController
    ///     isn't part of this prefab and shouldn't be: Camp doesn't own run-flow orchestration.)
    ///     a scene builder now just instantiates this prefab and sets that one field, instead of
    ///     re-deriving the whole hierarchy + wiring inline.
    ///
    /// Re-run via Pets &gt; Build Camp Overlay Prefab any time CampPanelController's serialized
    /// fields change shape; this always rebuilds the prefab from scratch, so hand edits made
    /// directly in Prefab Mode survive a rebuild only if also reflected here (same contract as
    /// UiPrefabBuilder).</summary>
    public static class CampOverlayPrefabBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/UI/CampOverlay.prefab";

        [MenuItem("Pets/Build Camp Overlay Prefab")]
        public static void Build()
        {
            var root = CreatePanel(null, "CampOverlay", Theme.PanelBg, Vector2.zero, Vector2.one);
            AddVerticalLayout(root);
            AddPanelHeader(root, "Camp");

            var resultText = CreateText(root, "ResultText", string.Empty, Theme.FontSizeHeading, TextAnchor.MiddleCenter, 80);

            var continueButton = CreateButton(root, "ContinueButton", "Continue", Theme.ButtonStyle.Primary, useSprite: true);

            var controller = root.gameObject.AddComponent<CampPanelController>();
            SetField(controller, "resultText", resultText);
            // `flow` is deliberately left unset here — see the class doc comment. The scene
            // builder assigns it after instantiating, once LocationFlowController exists.

            UnityEventTools.AddVoidPersistentListener(continueButton.onClick, controller.OnContinueClicked);

            // Lays out Header/ResultText/ContinueButton once against the panel's actual saved
            // size, same as a scene build would — but root is passed as keepLive so the
            // VerticalLayoutGroup survives the save (see the class doc comment).
            ForceLayoutRebuild(root, root);

            EnsureFolder();
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
            Object.DestroyImmediate(root.gameObject);
            AssetDatabase.SaveAssets();
            Debug.Log($"Camp overlay prefab rebuilt at {PrefabPath}");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
            }
        }
    }
}
