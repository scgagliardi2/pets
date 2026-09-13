using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
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
        public static void CaptureCharacterSelectScenePlaying() =>
            CapturePlaying(CharacterSelectSceneBuilder.ScenePath);

        /// <summary>The Pokédex builds its 183 cards in PokedexController.Start, so like
        /// Character Select there's nothing to look at outside Play mode.</summary>
        [MenuItem("Pets/Dev/Capture Pokedex Scene (Playing)")]
        public static void CapturePokedexScenePlaying() =>
            CapturePlaying(PokedexSceneBuilder.ScenePath);

        /// <summary>The Location Map's nodes, edges, captions and player token are all built in
        /// LocationMapController.Build at runtime, so like Character Select it only shows anything
        /// worth looking at in Play mode.</summary>
        [MenuItem("Pets/Dev/Capture Location Map Scene (Playing)")]
        public static void CaptureLocationMapScenePlaying() =>
            CapturePlaying(LocationMapSceneBuilder.ScenePath);

        /// <summary>The Region Hub fills its three cards in Start. Seeded two badges in, so the
        /// header, the level preview and the Badges readout all show something past the defaults.</summary>
        [MenuItem("Pets/Dev/Capture Region Hub Scene (Playing)")]
        public static void CaptureRegionHubScenePlaying()
        {
            var library = AssetDatabase.LoadAssetAtPath<Pets.Data.PokemonSpeciesLibrary>("Assets/Content/PokemonSpeciesLibrary.asset");
            var run = new Pets.Meta.RunState { RunSeed = 12345 };
            run.CompletedLocations.Add(Pets.Meta.LocationType.Forest);
            run.CompletedLocations.Add(Pets.Meta.LocationType.Cave);
            run.LineUp.Add(Pets.Meta.ExperienceResolver.CreateAtExp(library.AllSpecies[0], "capture-0", 7, library));
            Pets.Gameplay.ActiveRun.Begin(run, library);
            CapturePlaying(RegionHubSceneBuilder.ScenePath);
        }

        /// <summary>The Battle screen redirects to Character Select without a party, so a
        /// three-mon run from the library's first species is seeded first — before Play mode is
        /// entered, since Start runs before the EnteredPlayMode callback would get a chance.</summary>
        [MenuItem("Pets/Dev/Capture Battle Scene (Playing)")]
        public static void CaptureBattleScenePlaying()
        {
            var library = AssetDatabase.LoadAssetAtPath<Pets.Data.PokemonSpeciesLibrary>("Assets/Content/PokemonSpeciesLibrary.asset");
            var run = new Pets.Meta.RunState();
            for (int i = 0; i < 3 && i < library.AllSpecies.Count; i++)
            {
                run.LineUp.Add(Pets.Data.PokemonInstanceFactory.Create(library.AllSpecies[i], $"capture-{i}"));
            }
            Pets.Gameplay.ActiveRun.Begin(run, library);
            CapturePlaying(BattleSceneBuilder.ScenePath);
        }

        /// <summary>The result panel of a *won node fight* — the one state of the Battle screen
        /// that reports what the run just earned, and the only way to look at the rewards list
        /// (BattleScreenController's rewardText) without walking a map by hand. A four-mon party
        /// against one weak foe, skipped straight to the end.</summary>
        [MenuItem("Pets/Dev/Capture Battle Result Scene (Playing)")]
        public static void CaptureBattleResultScenePlaying()
        {
            var library = AssetDatabase.LoadAssetAtPath<Pets.Data.PokemonSpeciesLibrary>("Assets/Content/PokemonSpeciesLibrary.asset");
            var run = new Pets.Meta.RunState { RunSeed = 12345 };
            for (int i = 0; i < 4 && i < library.AllSpecies.Count; i++)
            {
                run.LineUp.Add(Pets.Meta.ExperienceResolver.CreateAtExp(library.AllSpecies[i], $"capture-{i}", 3, library));
            }
            Pets.Gameplay.ActiveRun.Begin(run, library);

            // A single 0-EXP foe, so the fight is a win however the party's stats fell out.
            var foe = Pets.Meta.ExperienceResolver.CreateAtExp(library.AllSpecies[0], "capture-foe", 0, library);
            Pets.Gameplay.PendingBattle.Set(
                new System.Collections.Generic.List<Pets.Simulation.PokemonInstance> { foe },
                "capture-node", isGym: false, seed: 12345);

            CapturePlaying(BattleSceneBuilder.ScenePath, SkipToBattleResult);
        }

        private static void SkipToBattleResult()
        {
            var controller = Object.FindFirstObjectByType<Pets.Gameplay.BattleScreenController>();
            if (controller == null)
            {
                Debug.LogError("[Capture] Battle scene has no BattleScreenController.");
                return;
            }
            controller.OnSkipClicked();
        }

        /// <summary>The faint, mid-drop. A single weak foe that dies to the first Step, stepped by
        /// hand and shot partway through the faint beat — the one frame that shows whether the
        /// sprite is actually falling and fading (Pets.UI.FaintAnimationView) rather than just
        /// vanishing.</summary>
        [MenuItem("Pets/Dev/Capture Battle Faint (Playing)")]
        public static void CaptureBattleFaintPlaying()
        {
            var library = AssetDatabase.LoadAssetAtPath<Pets.Data.PokemonSpeciesLibrary>("Assets/Content/PokemonSpeciesLibrary.asset");
            var run = new Pets.Meta.RunState { RunSeed = 7 };
            for (int i = 0; i < 2 && i < library.AllSpecies.Count; i++)
            {
                run.LineUp.Add(Pets.Meta.ExperienceResolver.CreateAtExp(library.AllSpecies[i], $"capture-{i}", 6, library));
            }
            Pets.Gameplay.ActiveRun.Begin(run, library);

            var foe = Pets.Meta.ExperienceResolver.CreateAtExp(library.AllSpecies[3], "capture-foe", 0, library);
            foe.CurrentStats = new Pets.Simulation.Stats { Attack = 1, Health = 1, Speed = 1 };
            foe.CurrentHP = 1;
            Pets.Gameplay.PendingBattle.Set(
                new System.Collections.Generic.List<Pets.Simulation.PokemonInstance> { foe },
                "capture-node", isGym: false, seed: 7);

            // Stepped rather than skipped: Skip jumps to the end and the faint is never drawn.
            CapturePlaying(BattleSceneBuilder.ScenePath,
                () => Object.FindFirstObjectByType<Pets.Gameplay.BattleScreenController>().OnStepClicked(),
                captureAfterSeconds: DelayArg(2.35f));
        }

        /// <summary>The evolution scene, mid-flicker. A mon one point short of its twelfth, so the
        /// win's single point of EXP evolves it (see EvolutionOverlayController).</summary>
        [MenuItem("Pets/Dev/Capture Battle Evolution (Playing)")]
        public static void CaptureBattleEvolutionPlaying()
        {
            var library = AssetDatabase.LoadAssetAtPath<Pets.Data.PokemonSpeciesLibrary>("Assets/Content/PokemonSpeciesLibrary.asset");
            var evolving = library.AllSpecies.FirstOrDefault(s => s != null && s.EvolvesInto != null);
            if (evolving == null)
            {
                Debug.LogError("[Capture] No species in the library has an evolution to show.");
                return;
            }

            var run = new Pets.Meta.RunState { RunSeed = 11 };
            var mon = Pets.Meta.ExperienceResolver.CreateAtExp(evolving, "capture-0",
                Pets.Meta.ExperienceResolver.ExpPerEvolution - 1, library);
            run.LineUp.Add(mon);
            Pets.Gameplay.ActiveRun.Begin(run, library);

            var foe = Pets.Meta.ExperienceResolver.CreateAtExp(library.AllSpecies[0], "capture-foe", 0, library);
            foe.CurrentStats = new Pets.Simulation.Stats { Attack = 0, Health = 1, Speed = 1 };
            foe.CurrentHP = 1;
            Pets.Gameplay.PendingBattle.Set(
                new System.Collections.Generic.List<Pets.Simulation.PokemonInstance> { foe },
                "capture-node", isGym: false, seed: 11);

            // -captureDelay overrides how far into the sequence the shot lands, so the intro, the
            // flicker and the reveal can each be looked at without editing this file.
            CapturePlaying(BattleSceneBuilder.ScenePath,
                () => Object.FindFirstObjectByType<Pets.Gameplay.BattleScreenController>().OnSkipClicked(),
                captureAfterSeconds: DelayArg(1.6f));
        }

        /// <summary>Run once in Play mode, a few frames before the shot is taken — for a screen
        /// whose interesting state is behind a gesture rather than in its resting layout.</summary>
        private static System.Action afterStart;

        /// <summary>Wall-clock seconds to let run between <see cref="afterStart"/> and the shot, for
        /// a screen whose interesting state is a moment *inside* an animation rather than a layout
        /// that settles. Zero (the default) keeps the original behaviour: settle the canvas and
        /// shoot immediately.</summary>
        private static float captureDelay;
        private static double captureAt;

        /// <summary>The Team screen with the duplicate question open — the one piece of this
        /// screen that isn't visible in its resting state. Seeds a party whose first two mons are
        /// the same species (which is what makes the gesture ambiguous, see TeamPanelController),
        /// then performs the drop that raises it.</summary>
        [MenuItem("Pets/Dev/Capture Team Scene Combine Dialog (Playing)")]
        public static void CaptureTeamCombineDialogPlaying()
        {
            var library = AssetDatabase.LoadAssetAtPath<Pets.Data.PokemonSpeciesLibrary>("Assets/Content/PokemonSpeciesLibrary.asset");
            var run = new Pets.Meta.RunState();
            var duplicate = library.AllSpecies[0];
            run.LineUp.Add(Pets.Data.PokemonInstanceFactory.Create(duplicate, "capture-0"));
            run.LineUp.Add(Pets.Data.PokemonInstanceFactory.Create(duplicate, "capture-1"));
            run.LineUp.Add(Pets.Data.PokemonInstanceFactory.Create(library.AllSpecies[3], "capture-2"));
            // A little EXP on the Lead so the card's growth readout shows something other than zero.
            Pets.Meta.ExperienceResolver.GrantExp(run.LineUp[0], 2, library);
            Pets.Gameplay.ActiveRun.Begin(run, library);

            CapturePlaying(TeamSceneBuilder.ScenePath, DropDuplicateOntoLead);
        }

        /// <summary>Drives the real drag handlers rather than reaching into the controller: the
        /// thing worth looking at is what the gesture produces, and this is the same path the
        /// PlayMode tests use.</summary>
        private static void DropDuplicateOntoLead()
        {
            var source = GameObject.Find("PartySlot1")?.GetComponent<Pets.Gameplay.TeamSlotView>();
            var target = GameObject.Find("PartySlot0")?.GetComponent<Pets.Gameplay.TeamSlotView>();
            if (source == null || target == null)
            {
                Debug.LogError("[Capture] Team scene has no PartySlot0/PartySlot1 to drag between.");
                return;
            }

            var eventData = new PointerEventData(EventSystem.current) { pointerDrag = source.gameObject };
            source.OnBeginDrag(eventData);
            target.OnDrop(eventData);
            source.OnEndDrag(eventData);
        }

        private static void CapturePlaying(string scenePath, System.Action onStarted = null, float captureAfterSeconds = 0f)
        {
            EditorSceneManager.OpenScene(scenePath);
            playModeFrameCount = 0;
            afterStart = onStarted;
            captureDelay = captureAfterSeconds;
            captureAt = 0d;
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
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            if (afterStart != null)
            {
                afterStart();
                afterStart = null;
                // Canvas.ForceUpdateCanvases rather than another frame wait: the gesture above only
                // toggles objects active and writes text, and the layout has to settle before the
                // RenderTexture is read back.
                Canvas.ForceUpdateCanvases();
                if (captureDelay > 0f)
                {
                    // Keep the update callback attached and come back when the animation has run
                    // far enough to be worth looking at.
                    captureAt = EditorApplication.timeSinceStartup + captureDelay;
                    return;
                }
            }
            if (captureAt > 0d && EditorApplication.timeSinceStartup < captureAt)
            {
                return;
            }

            EditorApplication.update -= WaitThenCapture;
            Capture(GetArg("-captureOutput") ?? "ui-kit.png");
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(0);
        }

        /// <summary>-captureDelay from the command line, or <paramref name="fallback"/>.</summary>
        private static float DelayArg(float fallback)
        {
            string raw = GetArg("-captureDelay");
            return !string.IsNullOrEmpty(raw) && float.TryParse(raw, out float seconds) ? seconds : fallback;
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
            // By CanvasScaler rather than FindFirstObjectByType<Canvas>: in Play mode
            // Pets.Gameplay.ScreenFade adds a full-screen overlay canvas of its own, which has no
            // scaler and would otherwise be a coin-flip to capture instead of the screen — as a
            // black frame, since that's what it draws.
            var scaler = Object.FindFirstObjectByType<CanvasScaler>();
            var canvas = scaler != null ? scaler.GetComponent<Canvas>() : null;
            if (canvas == null)
            {
                Debug.LogError("[Capture] No Canvas with a CanvasScaler in the open scene.");
                EditorApplication.Exit(1);
                return;
            }

            var reference = scaler.referenceResolution;
            int width = Mathf.RoundToInt(reference.x) * Supersample;
            int height = Mathf.RoundToInt(reference.y) * Supersample;

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = Supersample;

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
