using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Pets.UI;

namespace Pets.EditorTools
{
    /// <summary>Shared uGUI-construction helpers for the code-generated scene builders (see
    /// ForestSceneBuilder, CharacterSelectSceneBuilder, RegionMapSceneBuilder) — these scenes are
    /// built from code rather than hand-edited so their structure stays in lockstep with the
    /// Gameplay controllers' serialized fields; re-run the relevant Pets &gt; Build ... menu item
    /// after changing a controller's fields. Colors/typography come from Pets.UI.Theme, which is
    /// the flat-color approximation of the "Monster Trails" style guide's palette — see PLAN.md
    /// for the real sprite/icon work this doesn't attempt to replace.</summary>
    public static class SceneBuilderUtils
    {
        public static void CreateMainCamera(Color? backgroundColor = null)
        {
            // Screen Space - Overlay UI doesn't need a camera to render, but the Game view shows
            // a "No cameras rendering" placeholder without one.
            var go = new GameObject("Main Camera", typeof(Camera));
            go.tag = "MainCamera";
            var camera = go.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = backgroundColor ?? new Color(0.1f, 0.1f, 0.1f);
            camera.orthographic = true;
        }

        public static void CreateEventSystem()
        {
            // The project's Active Input Handling is set to the new Input System package only
            // (activeInputHandler: 1 in ProjectSettings) — the legacy StandaloneInputModule throws
            // at runtime under that setting, so UI needs InputSystemUIInputModule instead.
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        public static RectTransform CreateCanvas(Vector2? referenceResolution = null)
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution ?? new Vector2(960, 720);
            go.AddComponent<GraphicRaycaster>();
            return go.GetComponent<RectTransform>();
        }

        public static RectTransform CreatePanel(Transform parent, string name, Color background, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            if (background.a > 0f)
            {
                var image = go.AddComponent<Image>();
                image.color = background;
            }
            return rect;
        }

        public static Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor anchor, float preferredHeight)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Theme.GameFont;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Theme.TextDark;
            text.text = content;
            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.flexibleWidth = 1;
            return text;
        }

        /// <summary>A plain UI Text with no LayoutElement/font sizing assumptions, for use inside
        /// manually-positioned (non-layout-group) hierarchies like grid cells or map nodes.</summary>
        public static Text CreatePlainText(Transform parent, string name, string content, int fontSize, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Theme.GameFont;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = color;
            text.text = content;
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label, Theme.ButtonStyle style = Theme.ButtonStyle.Primary, bool useSprite = false)
        {
            if (useSprite)
            {
                return CreateSpriteButton(parent, name, label, style);
            }

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = Theme.ButtonBackground(style);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.disabledColor = Theme.ButtonDisabledBg;
            button.colors = colors;
            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 44;
            layoutElement.flexibleWidth = 1;

            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var text = textGO.AddComponent<Text>();
            text.font = Theme.GameFont;
            text.fontSize = 18;
            // Bold by default: Handjet is a thin "grid"/segmented display font and reads as
            // washed-out at button sizes in its Regular weight — the synthetic (faux) bold legacy
            // Text applies for FontStyle.Bold thickens strokes enough to stay legible without
            // importing a second font asset.
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Theme.ButtonText(style);
            text.text = label;
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return button;
        }

        /// <summary>Instantiates the reusable Assets/Prefabs/UI/Button.prefab (Pets.UI.UiButton —
        /// see UiPrefabBuilder) instead of hand-building the sprite/Text hierarchy inline, so the
        /// 9-sliced "Monster Trails" button art lives in one editable asset rather than being
        /// re-derived by every call site. LayoutElement/Button.colors are still applied here
        /// (rather than baked into the prefab) so every existing caller — toolbar rows, anchored
        /// confirm buttons — keeps the exact sizing/layout behavior it had before.</summary>
        private static Button CreateSpriteButton(Transform parent, string name, string label, Theme.ButtonStyle style)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabBuilder.ButtonPrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;

            var uiButton = instance.GetComponent<UiButton>();
            uiButton.Style = style;
            uiButton.Text = label;

            var button = instance.GetComponent<Button>();
            var colors = button.colors;
            colors.disabledColor = Theme.ButtonDisabledBg;
            button.colors = colors;

            var layoutElement = instance.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 44;
            layoutElement.flexibleWidth = 1;

            return button;
        }

        /// <summary>Instantiates the reusable Assets/Prefabs/UI/TextBox.prefab (Pets.UI.UiTextBox
        /// — see UiPrefabBuilder), the non-interactive counterpart to CreateSpriteButton, for
        /// previewing/using the "Monster Trails" TextBox art wherever a plain label needs the
        /// bordered-box treatment instead of a flat panel background.</summary>
        public static Text CreateTextBox(Transform parent, string name, string content, int fontSize = 16)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabBuilder.TextBoxPrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;

            var uiTextBox = instance.GetComponent<UiTextBox>();
            uiTextBox.Text = content;
            var text = uiTextBox.TextComponent;
            text.fontSize = fontSize;

            var layoutElement = instance.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 44;
            layoutElement.flexibleWidth = 1;

            return text;
        }

        /// <summary>A real legacy UI.Dropdown (not a cycle-button stand-in) — mirrors Unity's own
        /// built-in Dropdown prefab structure by hand (Label/Arrow + an inactive Template holding
        /// a masked, scrollable Viewport/Content/Item so long option lists — e.g. all 18 Pokémon
        /// types — scroll instead of overflowing), since this codebase builds every scene from
        /// code rather than shipping prefab assets. The Template gets its own overridden-sorting
        /// Canvas + GraphicRaycaster, exactly like Unity's default prefab, so the open popup
        /// renders above the rest of the screen regardless of where the Dropdown itself sits in
        /// the hierarchy.</summary>
        public static Dropdown CreateDropdown(Transform parent, string name, IReadOnlyList<string> optionLabels)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var bgImage = go.AddComponent<Image>();
            bgImage.color = Theme.ButtonSecondaryBg;
            var dropdown = go.AddComponent<Dropdown>();
            dropdown.targetGraphic = bgImage;
            var rootLayoutElement = go.AddComponent<LayoutElement>();
            rootLayoutElement.preferredWidth = 170;
            rootLayoutElement.preferredHeight = 44;

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(go.transform, false);
            var labelText = labelGO.AddComponent<Text>();
            labelText.font = Theme.GameFont;
            labelText.fontSize = 16;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.color = Theme.TextDark;
            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 2f);
            labelRect.offsetMax = new Vector2(-24f, -2f);
            dropdown.captionText = labelText;

            var arrowGO = new GameObject("Arrow", typeof(RectTransform));
            arrowGO.transform.SetParent(go.transform, false);
            var arrowText = arrowGO.AddComponent<Text>();
            arrowText.font = Theme.GameFont;
            arrowText.fontSize = 14;
            arrowText.fontStyle = FontStyle.Bold;
            arrowText.alignment = TextAnchor.MiddleCenter;
            arrowText.color = Theme.TextDark;
            arrowText.text = "v";
            var arrowRect = arrowGO.GetComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1f, 0f);
            arrowRect.anchorMax = Vector2.one;
            arrowRect.offsetMin = new Vector2(-22f, 0f);
            arrowRect.offsetMax = new Vector2(-6f, 0f);

            var templateGO = new GameObject("Template", typeof(RectTransform));
            templateGO.transform.SetParent(go.transform, false);
            var templateRect = templateGO.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, -2f);
            templateRect.sizeDelta = new Vector2(0f, Mathf.Min(28f * optionLabels.Count, 220f));
            var templateImage = templateGO.AddComponent<Image>();
            templateImage.color = Theme.ButtonSecondaryBg;
            var templateCanvas = templateGO.AddComponent<Canvas>();
            templateCanvas.overrideSorting = true;
            templateCanvas.sortingOrder = 30000;
            templateGO.AddComponent<GraphicRaycaster>();
            var templateScrollRect = templateGO.AddComponent<ScrollRect>();
            templateScrollRect.horizontal = false;
            templateScrollRect.movementType = ScrollRect.MovementType.Clamped;

            var viewportGO = new GameObject("Viewport", typeof(RectTransform));
            viewportGO.transform.SetParent(templateGO.transform, false);
            var viewportRect = viewportGO.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewportGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            // RectMask2D (a plain geometric clip), not the stencil-based legacy Mask: confirmed by
            // screenshot that the popup's own background rendered fine but everything inside it
            // (every option's background/checkmark/text) was completely blank — consistent with a
            // Mask whose stencil write/test isn't taking effect here, most likely because Viewport
            // sits inside Template's own separate override-sorting Canvas (added a few lines up),
            // and nested Canvases each manage their own stencil ID range. RectMask2D sidesteps
            // stencil entirely and is already the proven-working choice for the species grid's own
            // ScrollRect viewport elsewhere in this same file.
            viewportGO.AddComponent<RectMask2D>();
            templateScrollRect.viewport = viewportRect;

            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRect = contentGO.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            // Deliberately sized for exactly ONE item (28), not the full option list: Show()
            // measures the "inset" between the item template's edges and Content's edges from
            // whatever Content's height is *before* Show() runs, then adds itemSize.y * itemCount
            // on top of that inset — so a Content pre-sized to the full list here double-counts,
            // producing a computed height roughly double the real total and pushing every option
            // off-screen (confirmed empirically: 1036 instead of 19*28=532). Matches Unity's own
            // stock Dropdown prefab, where Content likewise starts sized for a single item.
            //
            // Also deliberately no LayoutGroup on Content: Show() positions and resizes each
            // cloned option item itself via direct RectTransform math — a LayoutGroup here fights
            // that instead of complementing it (confirmed empirically too: a height of 2332 with
            // one attached). Both of these together were the actual cause of the dropdown's
            // options rendering with no visible text.
            contentRect.sizeDelta = new Vector2(0f, 28f);
            templateScrollRect.content = contentRect;

            var itemGO = new GameObject("Item", typeof(RectTransform));
            itemGO.transform.SetParent(contentGO.transform, false);
            // Dropdown.Show() only ever touches this rect's Y-axis (height + vertical position)
            // when laying out each cloned option — it expects the item to already be correctly
            // stretched horizontally, exactly like Unity's own stock Dropdown prefab. Left at the
            // raw default (center-anchored, 100x100) this reads as itemSize.y=100 instead of 28,
            // inflating Content's computed height and pushing every option off-screen — this was
            // the actual cause of the dropdown's options rendering with no visible text.
            // Anchors/pivot here deliberately match Unity's own DefaultControls.CreateDropdown
            // exactly (anchorMin/Max Y = 0.5, default 0.5/0.5 pivot) — Show() only ever rewrites
            // anchorMin.y/anchorMax.y (to 0) and sizeDelta.y itself, but reads THIS pivot.y as
            // part of its Y-position formula, so diverging from the stock pivot shifts every
            // option by a constant offset relative to the reference implementation.
            var itemRect = itemGO.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.anchoredPosition = Vector2.zero;
            itemRect.sizeDelta = new Vector2(0f, 28f);
            var itemToggle = itemGO.AddComponent<Toggle>();

            var itemBgGO = new GameObject("Item Background", typeof(RectTransform));
            itemBgGO.transform.SetParent(itemGO.transform, false);
            var itemBgImage = itemBgGO.AddComponent<Image>();
            itemBgImage.color = Theme.ButtonSecondaryBg;
            var itemBgRect = itemBgGO.GetComponent<RectTransform>();
            itemBgRect.anchorMin = Vector2.zero;
            itemBgRect.anchorMax = Vector2.one;
            itemBgRect.offsetMin = Vector2.zero;
            itemBgRect.offsetMax = Vector2.zero;
            itemToggle.targetGraphic = itemBgImage;

            var itemCheckGO = new GameObject("Item Checkmark", typeof(RectTransform));
            itemCheckGO.transform.SetParent(itemGO.transform, false);
            var itemCheckImage = itemCheckGO.AddComponent<Image>();
            itemCheckImage.color = Theme.TabSelectedBg;
            var itemCheckRect = itemCheckGO.GetComponent<RectTransform>();
            itemCheckRect.anchorMin = new Vector2(0f, 0.5f);
            itemCheckRect.anchorMax = new Vector2(0f, 0.5f);
            itemCheckRect.sizeDelta = new Vector2(14f, 14f);
            itemCheckRect.anchoredPosition = new Vector2(12f, 0f);
            itemToggle.graphic = itemCheckImage;

            var itemLabelGO = new GameObject("Item Label", typeof(RectTransform));
            itemLabelGO.transform.SetParent(itemGO.transform, false);
            var itemLabelText = itemLabelGO.AddComponent<Text>();
            itemLabelText.font = Theme.GameFont;
            itemLabelText.fontSize = 15;
            itemLabelText.fontStyle = FontStyle.Bold;
            itemLabelText.alignment = TextAnchor.MiddleLeft;
            itemLabelText.color = Theme.TextDark;
            var itemLabelRect = itemLabelGO.GetComponent<RectTransform>();
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(26f, 1f);
            itemLabelRect.offsetMax = new Vector2(-10f, -1f);
            dropdown.itemText = itemLabelText;

            dropdown.template = templateRect;
            templateGO.SetActive(false);

            dropdown.options.Clear();
            foreach (var label in optionLabels)
            {
                dropdown.options.Add(new Dropdown.OptionData(label));
            }
            dropdown.value = 0;
            dropdown.RefreshShownValue();

            return dropdown;
        }

        /// <summary>Inserts a dark title-bar strip as the first child of a panel that already has
        /// AddVerticalLayout applied, approximating the style guide's "Large Panel (9-slice) with
        /// Panel Title" component (section 1) with a flat color block in place of a real 9-slice
        /// sprite. Deliberately does NOT wrap the panel's existing children in a new hierarchy
        /// level — it just adds one more layout child — so callers (and the PlayMode tests that
        /// Transform.Find into these panels, e.g. "Content/TeamPanel/LineUpText") don't need to
        /// change their child paths.</summary>
        public static void AddPanelHeader(RectTransform panel, string title)
        {
            var header = CreatePanel(panel, "Header", Theme.PanelHeaderBg, Vector2.zero, Vector2.one);
            header.SetAsFirstSibling();
            var headerLayoutElement = header.gameObject.AddComponent<LayoutElement>();
            headerLayoutElement.preferredHeight = 36;
            headerLayoutElement.flexibleWidth = 1;

            var titleText = CreatePlainText(header, "Title", title, Theme.FontSizeHeading, TextAnchor.MiddleLeft, Theme.TextLight);
            titleText.fontStyle = FontStyle.Bold;
            var titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(12f, 0f);
            titleRect.offsetMax = new Vector2(-12f, 0f);
        }

        /// <summary>A dark chrome strip of label/value readouts (guide section 8's "Run Resource
        /// Icons" row — Money, Morale, Badge, etc.) using text labels in place of the guide's
        /// icon set. Returns each item's value Text in the same order as <paramref name="labels"/>
        /// so a controller can update them as run state changes.</summary>
        public static (RectTransform bar, Text[] values) CreateResourceBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, string[] labels)
        {
            var bar = CreatePanel(parent, name, Theme.ChromeBg, anchorMin, anchorMax);
            AddHorizontalLayout(bar, expandHeight: true, padding: new RectOffset(20, 20, 6, 6));
            var layout = bar.gameObject.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 28;

            var values = new Text[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                var itemText = CreatePlainText(bar, $"{labels[i]}Value", labels[i], Theme.FontSizeBody, TextAnchor.MiddleLeft, Theme.TextLight);
                itemText.fontStyle = FontStyle.Bold;
                var le = itemText.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 120;
                le.preferredHeight = 28;
                values[i] = itemText;
            }
            return (bar, values);
        }

        public static void AddVerticalLayout(RectTransform panel, RectOffset padding = null, int spacing = 10)
        {
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = padding ?? new RectOffset(20, 20, 20, 20);
            layout.spacing = spacing;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            // childControlHeight must be true for each child's LayoutElement.preferredHeight
            // (set by CreateText/CreateButton) to actually apply — with it false, the layout
            // group instead uses each child's raw RectTransform size, which defaults to 100x100
            // for a freshly created GameObject regardless of any LayoutElement, causing every
            // child to overlap/overflow into the next several rows.
            layout.childControlHeight = true;
        }

        /// <summary>Builds a standard ScrollRect/Viewport/Content skeleton. The caller is
        /// responsible for setting Content's own anchors/pivot/size afterward, since that differs
        /// by use case (e.g. a GridLayoutGroup's full-width-stretch Content vs. a manually
        /// positioned map's point-anchored Content).</summary>
        public static (ScrollRect scrollRect, RectTransform viewport, RectTransform content) CreateScrollView(
            Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, bool horizontal, bool vertical)
        {
            var root = CreatePanel(parent, name, Color.clear, anchorMin, anchorMax);
            var scrollRect = root.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = horizontal;
            scrollRect.vertical = vertical;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            var viewport = CreatePanel(root, "Viewport", new Color(0f, 0f, 0f, 0.001f), Vector2.zero, Vector2.one);
            viewport.gameObject.AddComponent<RectMask2D>();

            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewport, false);
            var content = contentGO.GetComponent<RectTransform>();

            scrollRect.viewport = viewport;
            scrollRect.content = content;

            return (scrollRect, viewport, content);
        }

        public static void AddHorizontalLayout(RectTransform panel, bool expandHeight, RectOffset padding = null, bool controlWidth = false)
        {
            var layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = expandHeight;
            // Off by default (existing rows like the tab bar rely on each child's own
            // LayoutElement/RectTransform width). When true, LayoutElement.preferredWidth on each
            // child actually drives its rendered width instead of being ignored — needed for
            // controls like a Dropdown where an ambiguous fallback width isn't good enough.
            layout.childControlWidth = controlWidth;
            layout.childControlHeight = expandHeight;
        }

        /// <summary>Synchronously runs every LayoutGroup under <paramref name="root"/> right now,
        /// instead of leaving it to Unity's normal deferred layout pass (which only flushes on an
        /// actual Canvas update — i.e. during Play, never during a headless batchmode Editor
        /// script). Without this, every row built via AddHorizontalLayout / AddVerticalLayout gets
        /// saved to the .unity file with each child still at its raw, never-laid-out RectTransform
        /// default (center-anchored, 100x100) — they render stacked exactly on top of each other
        /// instead of arranged in the row/column the layout group was supposed to produce.
        ///
        /// Two things this has to work around, confirmed empirically (see the "Diag Layout" throwaway
        /// investigation this replaced):
        ///  1. Plain `LayoutRebuilder.ForceRebuildLayoutImmediate(root)` only recurses into a rect's
        ///     children once the rect it's given already carries a layout controller itself (see its
        ///     PerformLayoutControl's early-return when GetComponents(ILayoutController) is empty) —
        ///     calling it once on a plain Canvas root, which has no LayoutGroup of its own, is a
        ///     silent no-op for every nested row/column beneath it. Fixed by walking the whole
        ///     subtree ourselves and calling ForceRebuildLayoutImmediate on every RectTransform that
        ///     actually has a LayoutGroup, innermost first.
        ///  2. Even once correctly computed, the result does NOT survive EditorSceneManager.SaveScene
        ///     while the LayoutGroup component is still present — LayoutGroup drives the RectTransform
        ///     properties it computes through a DrivenRectTransformTracker, and something in Save's
        ///     internal object lifecycle reverts tracked properties back to their pre-driven snapshot
        ///     (same 100x100 default) before serializing. The values only stick if the driving
        ///     LayoutGroup component is gone by the time the scene is saved — so this bakes the
        ///     layout once and then destroys the LayoutGroup, for any row whose child *set* is fixed
        ///     forever (a tab bar, a static button row). Rows a Gameplay controller populates at
        ///     runtime (species cards, PvE catch buttons, an opened Dropdown's option list) need the
        ///     LayoutGroup to stay alive to arrange whatever gets added later — pass their
        ///     RectTransform in <paramref name="keepLive"/> to rebuild-but-not-destroy them, and any
        ///     row that's inactive at build time (e.g. a Dropdown's popup Template) is skipped
        ///     entirely rather than baked into a wrong, never-updated-again state.</summary>
        public static void ForceLayoutRebuild(RectTransform root, params RectTransform[] keepLive)
        {
            if (!root.gameObject.activeInHierarchy)
            {
                return;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                if (root.GetChild(i) is RectTransform child)
                {
                    ForceLayoutRebuild(child, keepLive);
                }
            }

            var layoutGroup = root.GetComponent(typeof(ILayoutGroup)) as Behaviour;
            if (layoutGroup != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(root);
                if (System.Array.IndexOf(keepLive, root) < 0)
                {
                    Object.DestroyImmediate(layoutGroup);
                }
            }
        }

        public static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                Debug.LogError($"Field '{fieldName}' not found on {target.GetType().Name}");
                return;
            }
            field.SetValue(target, value);
        }

        /// <summary>Ensures the given scene paths are exactly the project's Build Settings scene
        /// list, in order — needed for SceneManager.LoadScene(name) to work at runtime.</summary>
        public static void SetBuildScenes(params string[] scenePaths)
        {
            var scenes = new EditorBuildSettingsScene[scenePaths.Length];
            for (int i = 0; i < scenePaths.Length; i++)
            {
                scenes[i] = new EditorBuildSettingsScene(scenePaths[i], true);
            }
            EditorBuildSettings.scenes = scenes;
        }
    }
}
