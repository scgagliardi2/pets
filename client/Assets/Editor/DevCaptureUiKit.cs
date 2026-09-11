using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Pets.EditorTools
{
    /// <summary>Temporary dev helper that rebuilds the UI prefabs + Game scene and renders the
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
            ForestSceneBuilder.Build();
            Capture(GetArg("-captureOutput") ?? "ui-kit.png");
        }

        [MenuItem("Pets/Dev/Capture Game Scene")]
        public static void CaptureGameScene()
        {
            EditorSceneManager.OpenScene(ForestSceneBuilder.ScenePath);
            Capture(GetArg("-captureOutput") ?? "ui-kit.png");
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
