using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Pets.UI;
using static Pets.EditorTools.SceneBuilderUtils;

namespace Pets.EditorTools
{
    /// <summary>The shape the Map's node overlays share — a title, a body, and one Continue button
    /// inside a modal dialog (see SceneBuilderUtils.CreateModal). Kept in one place because the two
    /// prefabs that use it (Pokémon Center, and the Event/PvP stub) have to read as the same kind of
    /// interruption to the map; what each one *does* is its own controller's business, which is why
    /// they're still separate prefabs and separate builders.
    ///
    /// Contents are anchored by hand rather than laid out by a group, since both overlays write
    /// their text at runtime — see CreateModal.</summary>
    public static class NodeOverlayLayout
    {
        public static readonly Vector2 DialogSize = new Vector2(620f, 300f);

        private const float Padding = 28f;
        private const float TitleHeight = 52f;
        private static readonly Vector2 ButtonSize = new Vector2(220f, 64f);

        public static Text AddTitle(RectTransform dialog, string title)
        {
            var text = CreatePlainText(dialog, "TitleText", title, Theme.FontSizeTitle, TextAnchor.MiddleCenter, Theme.TextDark);
            text.fontStyle = FontStyle.Bold;

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(Padding, -(Padding + TitleHeight));
            rect.offsetMax = new Vector2(-Padding, -Padding);
            return text;
        }

        /// <summary>The body, filling what's left between the title and the button. Starts empty:
        /// every one of these is written by its controller when the node is resolved.</summary>
        public static Text AddMessage(RectTransform dialog)
        {
            var text = CreatePlainText(dialog, "MessageText", string.Empty, Theme.FontSizeHeading, TextAnchor.MiddleCenter, Theme.TextDark);

            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(Padding, Padding + ButtonSize.y + 16f);
            rect.offsetMax = new Vector2(-Padding, -(Padding + TitleHeight + 8f));
            return text;
        }

        public static Button AddContinueButton(RectTransform dialog)
        {
            var button = CreateButton(dialog, "ContinueButton", "Continue", Theme.ButtonStyle.Primary, useSprite: true);

            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = ButtonSize;
            rect.anchoredPosition = new Vector2(0f, Padding);
            return button;
        }

        public static void SaveAsPrefab(RectTransform root, string prefabPath)
        {
            EnsureFolder();
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, prefabPath);
            Object.DestroyImmediate(root.gameObject);
            AssetDatabase.SaveAssets();
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
