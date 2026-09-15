using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using Pets.Gameplay;
using Pets.UI;
using static Pets.EditorTools.SceneBuilderUtils;

namespace Pets.EditorTools
{
    /// <summary>Builds the evolution overlay (see EvolutionOverlayController) as a reusable
    /// .prefab: a full-screen dimmed backdrop with the evolving mon large and centred, a white
    /// flash plate over it, and a caption underneath.
    ///
    /// A prefab rather than inline scene objects for the same reason the node overlay is
    /// (NodeEventOverlayPrefabBuilder): the controller's serialized fields and the skip button's
    /// persistent listener all live inside it, so the whole thing is one wired asset and the Battle
    /// scene only has to instantiate it. <see cref="EvolutionOverlayController.OnComplete"/> is the
    /// one thing the host sets afterwards, since it's a runtime callback onto a scene-only object.
    ///
    /// Re-run via Pets &gt; Build Evolution Overlay Prefab after changing that controller's
    /// serialized fields, or Pets &gt; Build All Scenes, which rebuilds this and then the Battle
    /// scene that instantiates it.</summary>
    public static class EvolutionOverlayPrefabBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/UI/EvolutionOverlay.prefab";

        /// <summary>Big — this is the one screen in the game whose whole job is to be looked at, and
        /// a species sprite drawn at field size would make it read as just another battle beat.</summary>
        private static readonly Vector2 SpriteSize = new Vector2(420f, 420f);

        private static readonly Color Backdrop = new Color(0.05f, 0.07f, 0.12f, 0.92f);
        private const float CaptionHeight = 72f;
        private const float CaptionGap = 40f;

        [MenuItem("Pets/Build Evolution Overlay Prefab")]
        public static void Build()
        {
            var root = CreatePanel(null, "EvolutionOverlay", Backdrop, Vector2.zero, Vector2.one);
            StretchTo(root, Vector2.zero, Vector2.one);

            // The backdrop itself is the skip button: the sequence is an animation rather than a
            // choice, so anywhere on screen is the right place to press to get past it. A Button on
            // the full-screen panel also takes the raycast, which is what keeps the battle controls
            // underneath from being clicked mid-evolution.
            var skip = root.gameObject.AddComponent<Button>();
            skip.targetGraphic = root.GetComponent<Image>();
            skip.transition = Selectable.Transition.None;

            var spriteGO = new GameObject("EvolvingSprite", typeof(RectTransform));
            spriteGO.transform.SetParent(root, false);
            var sprite = spriteGO.AddComponent<Image>();
            sprite.preserveAspect = true;
            sprite.raycastTarget = false;
            var spriteRect = sprite.rectTransform;
            spriteRect.anchorMin = spriteRect.anchorMax = spriteRect.pivot = new Vector2(0.5f, 0.5f);
            spriteRect.sizeDelta = SpriteSize;
            spriteRect.anchoredPosition = new Vector2(0f, CaptionHeight * 0.5f);

            // Full screen rather than sized to the sprite. A plate covering just the sprite's rect
            // reads as a white *square* appearing over the mon — the box the art sits in, which is
            // never meant to be visible. Washing the whole screen out is also what the mainline games
            // do at the moment of transformation, and it hides the swap between the two forms
            // completely. Starts off; the controller turns it on for the reveal and fades it.
            var flashGO = new GameObject("Flash", typeof(RectTransform));
            flashGO.transform.SetParent(root, false);
            var flash = flashGO.AddComponent<Image>();
            flash.color = new Color(1f, 1f, 1f, 0f);
            flash.raycastTarget = false;
            flash.enabled = false;
            StretchTo(flash.rectTransform, Vector2.zero, Vector2.one);

            var caption = CreatePlainText(root, "CaptionText", string.Empty, Theme.FontSizeTitle,
                TextAnchor.MiddleCenter, Theme.TextLight);
            caption.fontStyle = FontStyle.Bold;
            caption.raycastTarget = false;
            var captionRect = caption.rectTransform;
            captionRect.anchorMin = new Vector2(0f, 0.5f);
            captionRect.anchorMax = new Vector2(1f, 0.5f);
            captionRect.pivot = new Vector2(0.5f, 1f);
            captionRect.offsetMin = new Vector2(40f, 0f);
            captionRect.offsetMax = new Vector2(-40f, 0f);
            captionRect.sizeDelta = new Vector2(captionRect.sizeDelta.x, CaptionHeight);
            captionRect.anchoredPosition = new Vector2(0f, -(SpriteSize.y * 0.5f) + CaptionHeight * 0.5f - CaptionGap);

            var controller = root.gameObject.AddComponent<EvolutionOverlayController>();
            SetField(controller, "sprite", sprite);
            SetField(controller, "flash", flash);
            SetField(controller, "captionText", caption);
            // OnComplete is deliberately left unset — see the class doc comment.

            UnityEventTools.AddVoidPersistentListener(skip.onClick, controller.OnSkipClicked);

            NodeOverlayLayout.SaveAsPrefab(root, PrefabPath);
            Debug.Log($"Evolution overlay prefab rebuilt at {PrefabPath}");
        }
    }
}
