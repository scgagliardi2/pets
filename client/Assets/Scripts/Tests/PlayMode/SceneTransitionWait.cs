using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pets.Tests
{
    /// <summary>Waiting helpers for the PlayMode suites, for the tests that drive a real
    /// navigation rather than asserting on a button's wiring.
    ///
    /// Scene changes go through Pets.Gameplay.ScreenFade, which fades out and then loads
    /// asynchronously, so the next scene isn't up on the following frame the way a synchronous
    /// LoadScene left it. Polling rather than yielding a fixed number of frames, so these tests
    /// don't quietly turn into races if the fade duration changes.</summary>
    public static class SceneTransitionWait
    {
        /// <summary>Generous enough to cover the fade plus a real scene load on a cold batchmode
        /// run, small enough that a genuinely broken transition still fails the test rather than
        /// hanging it.</summary>
        private const int MaxFrames = 600;

        public static IEnumerator Until(Func<bool> condition, string message)
        {
            for (int frame = 0; frame < MaxFrames; frame++)
            {
                if (condition())
                {
                    yield break;
                }
                yield return null;
            }
            Assert.Fail($"{message} (timed out after {MaxFrames} frames)");
        }

        public static IEnumerator UntilActiveScene(string sceneName) =>
            Until(() => SceneManager.GetActiveScene().name == sceneName,
                $"expected the '{sceneName}' scene to become active");

        public static IEnumerator UntilExists<T>(string message) where T : Component =>
            Until(() => UnityEngine.Object.FindFirstObjectByType<T>() != null, message);
    }
}
