using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Pets.EditorTools
{
    /// <summary>Exports the game as an Xcode project for installing on a test iPhone. Player
    /// settings are set here rather than in the Inspector for the same reason scenes are built from
    /// code: the build is reproducible from a clean checkout. Signing is left to Xcode (sign in with
    /// an Apple ID, pick the team) unless APPLE_TEAM_ID is set, in which case automatic signing is
    /// configured up front. For device testing only — see PLAN.md §9 on publishing.</summary>
    public static class IosBuild
    {
        public const string BundleId = "com.samgagliardi.pets";
        public const string OutputPath = "Builds/iOS";

        [MenuItem("Pets/Build iOS Xcode Project")]
        public static void Build()
        {
            SceneCatalog.EnsureBuildScenes();

            PlayerSettings.companyName = "Sam Gagliardi";
            PlayerSettings.productName = "Pets";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);

            // Every screen is laid out at 960x720, so portrait would squash it.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            var teamId = Environment.GetEnvironmentVariable("APPLE_TEAM_ID");
            if (!string.IsNullOrEmpty(teamId))
            {
                PlayerSettings.iOS.appleDeveloperTeamID = teamId;
                PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = SceneCatalog.AllScenePaths,
                locationPathName = OutputPath,
                target = BuildTarget.iOS,
                options = BuildOptions.None,
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"iOS build {report.summary.result}: {report.summary.totalErrors} errors");
            Debug.Log($"iOS Xcode project written to {OutputPath}.");
        }
    }
}
