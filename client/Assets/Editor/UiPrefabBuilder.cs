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
    /// opened in Prefab Mode and tuned directly (size, border, font, colors) without touching C#
    /// or rebuilding a scene. Re-run via Pets &gt; Build UI Prefabs any time UiButton/UiTextBox's
    /// serialized fields change shape; this always rebuilds both prefabs from scratch, so hand
    /// edits made directly in Prefab Mode (resize, retint, etc.) survive a rebuild only if also
    /// reflected here.</summary>
    public static class UiPrefabBuilder
    {
        private const string FolderPath = "Assets/Prefabs/UI";
        public const string ButtonPrefabPath = FolderPath + "/Button.prefab";
        public const string TextBoxPrefabPath = FolderPath + "/TextBox.prefab";

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
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 64f);

            var image = go.AddComponent<Image>();
            image.sprite = Theme.ButtonSprite(Theme.ButtonStyle.Primary);
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.disabledColor = Theme.ButtonDisabledBg;
            button.colors = colors;

            var text = CreateLabel(go.transform, "Button");
            text.fontStyle = FontStyle.Bold;
            text.color = Theme.TextLight;

            var uiButton = go.AddComponent<UiButton>();
            SetField(uiButton, "background", image);
            SetField(uiButton, "label", text);
            SetField(uiButton, "style", Theme.ButtonStyle.Primary);

            SavePrefab(go, ButtonPrefabPath);
        }

        private static void BuildTextBoxPrefab()
        {
            var go = new GameObject("TextBox", typeof(RectTransform));
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(260f, 64f);

            var image = go.AddComponent<Image>();
            image.sprite = Theme.TextBoxSprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            var text = CreateLabel(go.transform, "Text Box");
            text.color = Theme.TextDark;

            var uiTextBox = go.AddComponent<UiTextBox>();
            SetField(uiTextBox, "background", image);
            SetField(uiTextBox, "label", text);

            SavePrefab(go, TextBoxPrefabPath);
        }

        private static Text CreateLabel(Transform parent, string content)
        {
            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(parent, false);
            var text = textGO.AddComponent<Text>();
            text.font = Theme.GameFont;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = content;
            var rect = textGO.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12f, 4f);
            rect.offsetMax = new Vector2(-12f, -4f);
            return text;
        }

        private static void SavePrefab(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
    }
}
