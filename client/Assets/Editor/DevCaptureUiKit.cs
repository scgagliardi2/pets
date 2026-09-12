using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Pets.EditorTools
{
    /// <summary>Temporary dev helper that rebuilds the UI prefabs + Home scene and renders the
    /// result straight to a PNG, so sprite/9-slice work can be checked without opening the Editor
    /// by hand. Same throwaway status as DevDiagnostics/DevOpenScene — safe to delete.
    ///
    /// Capture works by flipping the Canvas to ScreenSpaceCamera against an off-screen
    /// RenderTexture: a ScreenSpaceOverlay canvas draws straight to the backbuffer and can't be
    /// read back. The CanvasScaler is pinned to ConstantPixelSize at the same time, so the canvas
    /// lays out at exactly its 960x720 reference resolution instead of at whatever
    /// Screen.width/height happens to be in batchmode.</summary>
    public static class DevCaptureUiKit
    {
        private const int Supersample = 2;

        [MenuItem("Pets/Dev/Rebuild And Capture UI Kit")]
        public static void RebuildAndCapture()
        {
            UiPrefabBuilder.Build();
            HomeSceneBuilder.Build();
            Capture(GetArg("-captureOutput") ?? "ui-kit.png");
        }

        [MenuItem("Pets/Dev/Capture Home Scene")]
        public static void CaptureHomeScene()
        {
            EditorSceneManager.OpenScene(HomeSceneBuilder.ScenePath);
            Capture(GetArg("-captureOutput") ?? "ui-kit.png");
        }

        private static int playModeFrameCount;

        /// <summary>Unlike CaptureHomeScene, Character Select's species grid is populated at
        /// runtime (CharacterSelectController.Start -&gt; RefreshGrid), not baked into the saved
        /// scene — capturing it without opening Play mode would show an empty grid. Enters Play
        /// mode, waits a few frames for Start() to run, captures, then exits Play mode and the
        /// batch itself (this can't be combined with a plain -quit on the command line, since Play
        /// mode entry is asynchronous relative to -executeMethod returning).</summary>
        [MenuItem("Pets/Dev/Capture Character Select Scene (Playing)")]
        public static void CaptureCharacterSelectScenePlaying()
        {
            EditorSceneManager.OpenScene(CharacterSelectSceneBuilder.ScenePath);
            playModeFrameCount = 0;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                EditorApplication.update += WaitThenCapture;
            }
        }

        private static void WaitThenCapture()
        {
            playModeFrameCount++;
            if (playModeFrameCount < 10)
            {
                return;
            }
            EditorApplication.update -= WaitThenCapture;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Capture(GetArg("-captureOutput") ?? "ui-kit.png");
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(0);
        }

        private static string GetArg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        private static void Capture(string outputPath)
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Capture] No Canvas in the open scene.");
                EditorApplication.Exit(1);
                return;
            }

            var scaler = canvas.GetComponent<CanvasScaler>();
            var reference = scaler != null ? scaler.referenceResolution : new Vector2(960f, 720f);
            int width = Mathf.RoundToInt(reference.x) * Supersample;
            int height = Mathf.RoundToInt(reference.y) * Supersample;

            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = Supersample;
            }

            var cameraGO = new GameObject("CaptureCamera");
            var camera = cameraGO.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Pets.UI.Theme.ScreenBg;
            camera.orthographic = true;

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1,
                filterMode = FilterMode.Point,
            };
            camera.targetTexture = rt;

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;

            Canvas.ForceUpdateCanvases();
            camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;

            var full = Path.IsPathRooted(outputPath) ? outputPath : Path.Combine(Directory.GetCurrentDirectory(), outputPath);
            File.WriteAllBytes(full, texture.EncodeToPNG());
            Debug.Log($"[Capture] Wrote {full} ({width}x{height})");

            camera.targetTexture = null;
            Object.DestroyImmediate(cameraGO);
            Object.DestroyImmediate(texture);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
