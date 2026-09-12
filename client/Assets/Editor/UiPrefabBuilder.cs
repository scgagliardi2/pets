using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Pets.UI;
using static Pets.EditorTools.SceneBuilderUtils;

namespace Pets.EditorTools
{
    /// <summary>Builds the reusable sprite-backed UI prefabs (Button, TextBox, HealthBar, SpeedBar)
    /// that back UiButton/UiTextBox/HealthBarView/StatBarView — generated from code like the rest of
    /// this project's assets (see
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
        public const string HealthBarPrefabPath = FolderPath + "/HealthBar.prefab";
        public const string SpeedBarPrefabPath = FolderPath + "/SpeedBar.prefab";

        // The sprite art draws a 10-unit border (5px source pixels at 2 units each — see
        // UiSpriteImportProcessor). Default heights leave a comfortable flat centre inside that,
        // and the label insets clear it so text never sits on the bevel.
        private static readonly Vector2 ButtonSize = new Vector2(220f, 64f);
        private static readonly Vector2 TextBoxSize = new Vector2(300f, 72f);
        private const float LabelInsetX = 18f;
        private const float LabelInsetY = 8f;

        // Stat bars, sized for the ~188 units of width inside a Character Select card: a label pill,
        // the track, and a "value" readout wide enough for three-digit stats. Every stat bar shares
        // these numbers so stacked bars line their tracks up. The bar sprites are drawn at
        // pixelsPerUnitMultiplier 2 — one source pixel per canvas unit, so a 5px border is 5 units —
        // which leaves the 16-unit track a clean 2px outline+bevel around a 12-unit fill (exactly
        // the fill sprite's two borders plus a 2-unit flat centre).
        private static readonly Vector2 StatBarSize = new Vector2(188f, 18f);
        private const float StatBarLabelWidth = 30f;
        private const float StatBarValueWidth = 48f;
        private const float StatBarGap = 4f;
        private const float StatBarTrackHeight = 16f;
        private const float StatBarTrackInset = 2f;
        private const float StatBarPixelScale = 2f;
        private const int StatBarFontSize = 14;

        [MenuItem("Pets/Build UI Prefabs")]
        public static void Build()
        {
            EnsureFolder();
            BuildButtonPrefab();
            BuildTextBoxPrefab();
            BuildHealthBarPrefab();
            BuildSpeedBarPrefab();
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

        private static void BuildHealthBarPrefab()
        {
            var (go, fill, value) = BuildStatBar("HealthBar", "HP", Theme.HealthLabelSprite, Theme.HealthGreenSprite, "1/1");

            var view = go.AddComponent<HealthBarView>();
            SetField(view, "fill", fill);
            SetField(view, "valueLabel", value);
            view.SetHealth(1, 1);

            SavePrefab(go, HealthBarPrefabPath);
        }

        private static void BuildSpeedBarPrefab()
        {
            var (go, fill, value) = BuildStatBar("SpeedBar", "SPD", Theme.SpeedLabelSprite, Theme.SpeedFillSprite, "1");

            var view = go.AddComponent<StatBarView>();
            SetField(view, "fill", fill);
            SetField(view, "valueLabel", value);
            view.SetValue(1, 1);

            SavePrefab(go, SpeedBarPrefabPath);
        }

        /// <summary>The pill/track/readout body every stat bar shares; the caller adds the view.
        /// Laid out with anchors rather than a HorizontalLayoutGroup: bars sit on every card in a
        /// grid that grows toward the full roster, and three fixed-width pieces don't need a layout
        /// pass per card to find their positions.</summary>
        private static (GameObject Root, Image Fill, Text Value) BuildStatBar(
            string name, string labelText, Sprite labelSprite, Sprite fillSprite, string valuePreview)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.GetComponent<RectTransform>().sizeDelta = StatBarSize;
            // Lets a card's VerticalLayoutGroup give the bar its height without the caller knowing it.
            go.AddComponent<LayoutElement>().preferredHeight = StatBarSize.y;

            var label = CreateBarImage(go.transform, "Label", labelSprite);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.sizeDelta = new Vector2(StatBarLabelWidth, 0f);
            labelRect.anchoredPosition = Vector2.zero;
            Stretch(CreateBarText(label.transform, "Text", labelText, TextAnchor.MiddleCenter, Theme.TextLight).rectTransform, 0f);

            var track = CreateBarImage(go.transform, "Track", Theme.BarTrackSprite);
            var trackRect = track.rectTransform;
            trackRect.anchorMin = new Vector2(0f, 0.5f);
            trackRect.anchorMax = new Vector2(1f, 0.5f);
            trackRect.offsetMin = new Vector2(StatBarLabelWidth + StatBarGap, -StatBarTrackHeight / 2f);
            trackRect.offsetMax = new Vector2(-(StatBarValueWidth + StatBarGap), StatBarTrackHeight / 2f);

            // The fill's anchors are the view's to move, so the inset lives on a parent instead —
            // otherwise a fraction of 0.02 minus a fixed inset would go negative-width.
            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(track.transform, false);
            Stretch(fillArea.GetComponent<RectTransform>(), StatBarTrackInset);
            var fill = CreateBarImage(fillArea.transform, "Fill", fillSprite);

            // Left-aligned within its fixed width, so a short readout hugs the track and the unused
            // width falls at the outer edge instead of opening a gap after the bar.
            var value = CreateBarText(go.transform, "Value", valuePreview, TextAnchor.MiddleLeft, Theme.TextDark);
            var valueRect = value.rectTransform;
            valueRect.anchorMin = new Vector2(1f, 0f);
            valueRect.anchorMax = new Vector2(1f, 1f);
            valueRect.pivot = new Vector2(1f, 0.5f);
            valueRect.sizeDelta = new Vector2(StatBarValueWidth, 0f);
            valueRect.anchoredPosition = Vector2.zero;

            return (go, fill, value);
        }

        private static Image CreateBarImage(Transform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = StatBarPixelScale;
            // Decoration on a card that is itself a Button — must not eat the card's clicks.
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateBarText(Transform parent, string name, string content, TextAnchor alignment, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Theme.GameFont;
            text.fontSize = StatBarFontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
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
