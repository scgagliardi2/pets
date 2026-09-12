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
        public const string BattleStatsBoxPrefabPath = FolderPath + "/BattleStatsBox.prefab";
        public const string BattlePartySlotPrefabPath = FolderPath + "/BattlePartySlot.prefab";

        // Battle stats box: the bars nest at this height, a notch taller than on a card.
        private const float StatsBoxBarHeight = 20f;
        private const float StatsBoxHealthTop = 46f;
        private const float StatsBoxSpeedTop = 72f;
        private const int StatsBoxFontSize = 22;

        // Party slot (146x150): portrait, name, then a slim HP track — 16 tall like the stat bars'
        // track, so the same 5-unit-bordered art keeps whole-pixel chamfers.
        private const float SlotPortraitTop = 10f;
        private const float SlotPortraitHeight = 86f;
        private const float SlotNameTop = 98f;
        private const float SlotNameHeight = 24f;
        private const float SlotTrackTop = 122f;
        private const float SlotTrackInsetX = 16f;
        private const int SlotNameFontSize = 18;

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
            // After the bars and the type icon, which these two nest.
            BuildBattleStatsBoxPrefab();
            BuildBattlePartySlotPrefab();
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

        /// <summary>The battle screen's per-mon stat box (Pets.UI.BattleStatsBoxView). Nests the
        /// TypeIcon, HealthBar and SpeedBar prefabs rather than rebuilding them, so a change to a bar
        /// reaches the box on the next prefab build.</summary>
        private static void BuildBattleStatsBoxPrefab()
        {
            var go = new GameObject("BattleStatsBox", typeof(RectTransform));
            go.GetComponent<RectTransform>().sizeDelta = BattleStatsBoxView.Size;
            var background = go.AddComponent<Image>();
            background.sprite = Theme.TextBoxSprite;
            background.type = Image.Type.Sliced;
            background.raycastTarget = false;
            var group = go.AddComponent<CanvasGroup>();

            float inset = BattleStatsBoxView.Inset;
            float iconTop = BattleStatsBoxView.RowTop + (BattleStatsBoxView.RowHeight - BattleStatsBoxView.IconSize) / 2f;
            var firstType = InstantiateNested<TypeIconView>(TypeIconPrefabBuilder.PrefabPath, go.transform, "FirstType");
            PlaceTopLeft((RectTransform)firstType.transform, inset, iconTop, BattleStatsBoxView.IconSize, BattleStatsBoxView.IconSize);
            var secondType = InstantiateNested<TypeIconView>(TypeIconPrefabBuilder.PrefabPath, go.transform, "SecondType");
            PlaceTopLeft((RectTransform)secondType.transform, inset + BattleStatsBoxView.IconSize + BattleStatsBoxView.IconGap,
                iconTop, BattleStatsBoxView.IconSize, BattleStatsBoxView.IconSize);

            var name = CreateBoxText(go.transform, "Name", "Pokemon", StatsBoxFontSize, TextAnchor.MiddleLeft);
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = Theme.FontSizeSmall;
            name.resizeTextMaxSize = StatsBoxFontSize;
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            var nameRect = name.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0f, 1f);
            float attackBlock = inset + BattleStatsBoxView.AttackWidth + BattleStatsBoxView.IconGap + BattleStatsBoxView.SwordSize + BattleStatsBoxView.NameGap;
            nameRect.offsetMin = new Vector2(inset, -(BattleStatsBoxView.RowTop + BattleStatsBoxView.RowHeight));
            nameRect.offsetMax = new Vector2(-attackBlock, -BattleStatsBoxView.RowTop);

            var attack = CreateBoxText(go.transform, "Attack", "10", StatsBoxFontSize, TextAnchor.MiddleLeft);
            var attackRect = attack.rectTransform;
            attackRect.anchorMin = attackRect.anchorMax = Vector2.one;
            attackRect.pivot = Vector2.one;
            attackRect.sizeDelta = new Vector2(BattleStatsBoxView.AttackWidth, BattleStatsBoxView.RowHeight);
            attackRect.anchoredPosition = new Vector2(-inset, -BattleStatsBoxView.RowTop);

            var swordGo = new GameObject("Sword", typeof(RectTransform));
            swordGo.transform.SetParent(go.transform, false);
            var sword = swordGo.AddComponent<Image>();
            sword.sprite = Theme.AttackIconSprite;
            sword.raycastTarget = false;
            var swordRect = sword.rectTransform;
            swordRect.anchorMin = swordRect.anchorMax = Vector2.one;
            swordRect.pivot = Vector2.one;
            swordRect.sizeDelta = new Vector2(BattleStatsBoxView.SwordSize, BattleStatsBoxView.SwordSize);
            swordRect.anchoredPosition = new Vector2(
                -(inset + BattleStatsBoxView.AttackWidth + BattleStatsBoxView.IconGap),
                -(BattleStatsBoxView.RowTop + (BattleStatsBoxView.RowHeight - BattleStatsBoxView.SwordSize) / 2f));

            var health = InstantiateNested<HealthBarView>(HealthBarPrefabPath, go.transform, "HealthBar");
            PinRow((RectTransform)health.transform, StatsBoxHealthTop, StatsBoxBarHeight, inset);
            var speed = InstantiateNested<StatBarView>(SpeedBarPrefabPath, go.transform, "SpeedBar");
            PinRow((RectTransform)speed.transform, StatsBoxSpeedTop, StatsBoxBarHeight, inset);

            var view = go.AddComponent<BattleStatsBoxView>();
            SetField(view, "firstType", firstType);
            SetField(view, "secondType", secondType);
            SetField(view, "nameText", name);
            SetField(view, "swordIcon", sword);
            SetField(view, "attackText", attack);
            SetField(view, "healthBar", health);
            SetField(view, "speedBar", speed);
            SetField(view, "group", group);
            view.Show("Pokemon", Pets.Simulation.PokemonType.Normal, false, Pets.Simulation.PokemonType.Normal,
                10, 50, Pets.Data.PokemonSpeciesDefinitionAsset.MaxBaseSpeed);
            health.SetHealth(40, 40);

            SavePrefab(go, BattleStatsBoxPrefabPath);
        }

        /// <summary>One party-strip slot on the battle screen (Pets.UI.BattlePartySlotView).</summary>
        private static void BuildBattlePartySlotPrefab()
        {
            var go = new GameObject("BattlePartySlot", typeof(RectTransform));
            go.GetComponent<RectTransform>().sizeDelta = BattlePartySlotView.Size;
            var frame = go.AddComponent<Image>();
            frame.sprite = Theme.SlotDarkSprite;
            frame.type = Image.Type.Sliced;
            frame.raycastTarget = false;
            var group = go.AddComponent<CanvasGroup>();

            var portraitGo = new GameObject("Portrait", typeof(RectTransform));
            portraitGo.transform.SetParent(go.transform, false);
            var portrait = portraitGo.AddComponent<Image>();
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            PinRow(portrait.rectTransform, SlotPortraitTop, SlotPortraitHeight, 12f);

            var name = CreateBoxText(go.transform, "Name", "Pokemon", SlotNameFontSize, TextAnchor.MiddleCenter);
            name.color = Theme.TextLight;
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 12;
            name.resizeTextMaxSize = SlotNameFontSize;
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            PinRow(name.rectTransform, SlotNameTop, SlotNameHeight, 8f);

            var track = CreateBarImage(go.transform, "Track", Theme.BarTrackSprite);
            PinRow(track.rectTransform, SlotTrackTop, StatBarTrackHeight, SlotTrackInsetX);
            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(track.transform, false);
            Stretch(fillArea.GetComponent<RectTransform>(), StatBarTrackInset);
            var fill = CreateBarImage(fillArea.transform, "Fill", Theme.HealthGreenSprite);

            var view = go.AddComponent<BattlePartySlotView>();
            SetField(view, "frame", frame);
            SetField(view, "portrait", portrait);
            SetField(view, "nameText", name);
            SetField(view, "track", track.gameObject);
            SetField(view, "fill", fill);
            SetField(view, "group", group);
            view.SetRole(PartySlotRole.Reserve);
            view.SetHealthFraction(1f);

            SavePrefab(go, BattlePartySlotPrefabPath);
        }

        private static T InstantiateNested<T>(string prefabPath, Transform parent, string name) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                throw new System.InvalidOperationException($"Prefab build failed: {prefabPath} doesn't exist yet.");
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            return instance.GetComponent<T>();
        }

        private static void PlaceTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        /// <summary>Stretches a rect across its parent's width less <paramref name="insetX"/> each
        /// side, <paramref name="top"/> units down with a fixed height.</summary>
        private static void PinRow(RectTransform rect, float top, float height, float insetX)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(insetX, -(top + height));
            rect.offsetMax = new Vector2(-insetX, -top);
        }

        private static Text CreateBoxText(Transform parent, string name, string content, int fontSize, TextAnchor alignment)
        {
            var text = CreateBarText(parent, name, content, alignment, Theme.TextDark);
            text.fontSize = fontSize;
            return text;
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
