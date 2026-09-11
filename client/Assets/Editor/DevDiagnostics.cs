using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Pets.EditorTools
{
    /// <summary>Temporary diagnostic to dump sort-button RectTransform/Image state to the log
    /// without needing to click around the Editor by hand. Not part of the game — safe to delete.</summary>
    public static class DevDiagnostics
    {
        [MenuItem("Pets/Dev/Diagnose Sort Buttons")]
        public static void DiagnoseSortButtons()
        {
            EditorSceneManager.OpenScene(CharacterSelectSceneBuilder.ScenePath);
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Pets/Dev/Open Character Select And Select Sort Button")]
        public static void OpenAndSelectSortButton()
        {
            EditorSceneManager.OpenScene(CharacterSelectSceneBuilder.ScenePath);
            EditorApplication.playModeStateChanged += OnPlayModeStateChangedSelect;
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeStateChangedSelect(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChangedSelect;
                EditorApplication.delayCall += () =>
                {
                    var go = GameObject.Find("SortAttackButton");
                    Selection.activeGameObject = go;
                    Debug.Log($"[Diag] Selected {(go == null ? "NULL" : go.name)}");
                    Debug.Log($"[Diag] Screen={Screen.width}x{Screen.height}");
                    var canvas = Object.FindFirstObjectByType<Canvas>();
                    Debug.Log($"[Diag] Canvas scaleFactor={canvas.scaleFactor} renderMode={canvas.renderMode}");
                    Dump();
                    if (go != null)
                    {
                        var rt = go.GetComponent<RectTransform>();
                        var corners = new Vector3[4];
                        rt.GetWorldCorners(corners);
                        Debug.Log($"[Diag] SortAttackButton world corners: {corners[0]} {corners[1]} {corners[2]} {corners[3]}");
                    }
                };
            }
        }

        private static int frameCount;

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                frameCount = 0;
                EditorApplication.update += WaitThenDump;
            }
        }

        private static void WaitThenDump()
        {
            frameCount++;
            if (frameCount < 10)
            {
                return;
            }
            EditorApplication.update -= WaitThenDump;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Dump();
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(0);
        }

        private static void Dump()
        {
            var toolbar = GameObject.Find("ToolbarBar");
            if (toolbar == null)
            {
                Debug.Log("[Diag] ToolbarBar not found");
                return;
            }

            Debug.Log($"[Diag] ToolbarBar active={toolbar.activeInHierarchy} childCount={toolbar.transform.childCount}");
            var toolbarRect = toolbar.GetComponent<RectTransform>();
            Debug.Log($"[Diag] ToolbarBar rect size={toolbarRect.rect.size} anchoredPos={toolbarRect.anchoredPosition}");

            for (int i = 0; i < toolbar.transform.childCount; i++)
            {
                var child = toolbar.transform.GetChild(i);
                var rect = child.GetComponent<RectTransform>();
                var image = child.GetComponent<Image>();
                var le = child.GetComponent<LayoutElement>();
                string imageInfo = image == null ? "none" : $"sprite={(image.sprite == null ? "NULL" : image.sprite.name)} color={image.color} type={image.type} enabled={image.enabled} isActiveAndEnabled={image.isActiveAndEnabled} mainTexture={(image.mainTexture == null ? "NULL" : image.mainTexture.name)} material={(image.material == null ? "NULL" : image.material.name)}";
                string leInfo = le == null ? "none" : $"preferredW={le.preferredWidth} preferredH={le.preferredHeight} flexW={le.flexibleWidth}";
                var cr = child.GetComponent<CanvasRenderer>();
                string crInfo = cr == null ? "none" : $"cull={cr.cull} alpha={cr.GetAlpha()} color={cr.GetColor()}";
                Debug.Log($"[Diag] Child[{i}]='{child.name}' active={child.gameObject.activeSelf} rectSize={rect.rect.size} anchoredPos={rect.anchoredPosition} localScale={child.localScale} image=({imageInfo}) layoutElement=({leInfo}) canvasRenderer=({crInfo})");

                var text = child.GetComponentInChildren<Text>();
                if (text != null)
                {
                    Debug.Log($"[Diag]   Text='{text.text}' fontSize={text.fontSize} color={text.color} enabled={text.enabled} rectSize={text.GetComponent<RectTransform>().rect.size}");
                }
            }
        }
    }
}
