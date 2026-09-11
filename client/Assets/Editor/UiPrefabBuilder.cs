using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Pets.UI;
using static Pets.EditorTools.SceneBuilderUtils;

namespace Pets.EditorTools
{
    /// <summary>Builds the reusable sprite-backed UI prefabs (Button, TextBox) that back
    /// UiButton/UiTextBox — generated from code like the rest of this project's assets (see
    /// SceneBuilderUtils), but saved as real .prefab assets under Assets/Prefabs/UI so they can be
    /// opened in Prefab Mode and tuned directly (size, font, colours) without touching C# or
    /// rebuilding a scene. Re-run via Pets &gt; Build UI Prefabs any time UiButton/UiTextBox's
    /// serialized fields change shape; this always rebuilds both prefabs from scratch, so hand
    /// edits made directly in Prefab Mode survive a rebuild only if also reflected here.</summary>
    public static class UiPrefabBuilder
    {
        private const string FolderPath = "Assets/Prefabs/UI";
        public const string ButtonPrefabPath = FolderPath + "/Button.prefab";
        public const string TextBoxPrefabPath = FolderPath + "/TextBox.prefab";

        // The sprite art draws a 10-unit border (5px source pixels at 2 units each — see
        // UiSpriteImportProcessor). Default heights leave a comfortable flat centre inside that,
        // and the label insets clear it so text never sits on the bevel.
        private static readonly Vector2 ButtonSize = new Vector2(220f, 64f);
        private static readonly Vector2 TextBoxSize = new Vector2(300f, 72f);
        private const float LabelInsetX = 18f;
        private const float LabelInsetY = 8f;

        [MenuItem("Pets/Build UI Prefabs")]
        public static void Build()
        {
            EnsureFolder();
            BuildButtonPrefab();
            BuildTextBoxPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log($"UI prefabs rebuilt under {FolderPath}");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }
            if (!AssetDatabase.IsValidFolder(FolderPath))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
            }
        }

        private static void BuildButtonPrefab()
        {
            var go = new GameObject("Button", typeof(RectTransform));
            go.GetComponent<RectTransform>().sizeDelta = ButtonSize;

            var image = go.AddComponent<Image>();
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var text = CreateLabel(go.transform, "Button", Theme.FontSizeBody + 2);
            text.fontStyle = FontStyle.Bold;
            var shadow = text.gameObject.AddComponent<Shadow>();

            // UiButton.Apply() sets sprite, slicing, tint ramp, label colour and shadow from the
            // style, so nothing above needs to guess at those — it only has to exist to be wired.
            var uiButton = go.AddComponent<UiButton>();
            SetField(uiButton, "background", image);
            SetField(uiButton, "label", text);
            SetField(uiButton, "labelShadow", shadow);
            SetField(uiButton, "style", Theme.ButtonStyle.Primary);

            SavePrefab(go, ButtonPrefabPath);
        }

        private static void BuildTextBoxPrefab()
        {
            var go = new GameObject("TextBox", typeof(RectTransform));
            go.GetComponent<RectTransform>().sizeDelta = TextBoxSize;

            var image = go.AddComponent<Image>();
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            var text = CreateLabel(go.transform, "Text Box", Theme.FontSizeBody);

            var uiTextBox = go.AddComponent<UiTextBox>();
            SetField(uiTextBox, "background", image);
            SetField(uiTextBox, "label", text);

            SavePrefab(go, TextBoxPrefabPath);
        }

        private static Text CreateLabel(Transform parent, string content, int fontSize)
        {
            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(parent, false);
            var text = textGO.AddComponent<Text>();
            text.font = Theme.GameFont;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = content;
            // Let the pointer through to the Button on the parent; a raycast-target label is also
            // what would make a press handler on the label shadow the Button's own (see UiButton).
            text.raycastTarget = false;

            var rect = textGO.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(LabelInsetX, LabelInsetY);
            rect.offsetMax = new Vector2(-LabelInsetX, -LabelInsetY);
            return text;
        }

        private static void SavePrefab(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
    }
}
