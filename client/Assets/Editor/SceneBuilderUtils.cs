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

        public static Button CreateButton(Transform parent, string name, string label, Theme.ButtonStyle style = Theme.ButtonStyle.Primary)
        {
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

        public static void AddHorizontalLayout(RectTransform panel, bool expandHeight, RectOffset padding = null)
        {
            var layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = expandHeight;
            layout.childControlWidth = false;
            layout.childControlHeight = expandHeight;
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
