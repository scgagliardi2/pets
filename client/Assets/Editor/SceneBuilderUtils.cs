using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Pets.EditorTools
{
    /// <summary>Shared uGUI-construction helpers for the code-generated scene builders (see
    /// ForestSceneBuilder, CharacterSelectSceneBuilder, RegionMapSceneBuilder) — these scenes are
    /// built from code rather than hand-edited so their structure stays in lockstep with the
    /// Gameplay controllers' serialized fields; re-run the relevant Pets &gt; Build ... menu item
    /// after changing a controller's fields.</summary>
    public static class SceneBuilderUtils
    {
        public static readonly Color ButtonBg = new Color(0.7f, 0.8f, 0.9f);

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
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.black;
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
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = color;
            text.text = content;
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = ButtonBg;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 44;
            layoutElement.flexibleWidth = 1;

            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var text = textGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
            text.text = label;
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return button;
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

        public static void AddHorizontalLayout(RectTransform panel, bool expandHeight)
        {
            var layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
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
