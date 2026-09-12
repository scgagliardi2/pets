using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Pets.UI;

namespace Pets.Tests
{
    /// <summary>Covers the SpeedBar prefab + StatBarView contract: wired, fill scaled against the
    /// ceiling, value-only readout, and the out-of-range edges.</summary>
    public sealed class StatBarViewTests
    {
        private const string SpeedBarPrefabPath = "Assets/Prefabs/UI/SpeedBar.prefab";

        private GameObject instance;
        private StatBarView bar;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpeedBarPrefabPath);
            Assert.IsNotNull(prefab, $"SpeedBar prefab missing at {SpeedBarPrefabPath} - run Pets > Build UI Prefabs.");
            instance = Object.Instantiate(prefab);
            bar = instance.GetComponent<StatBarView>();
            Assert.IsNotNull(bar, "SpeedBar prefab has no StatBarView component.");
        }

        [TearDown]
        public void TearDown()
        {
            if (instance != null)
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void PrefabIsWiredWithLoadedArtAndBlocksNoClicks()
        {
            Assert.IsNotNull(bar.Fill, "fill is not wired");
            Assert.IsNotNull(bar.ValueLabel, "valueLabel is not wired");
            Assert.AreSame(Theme.SpeedFillSprite, bar.Fill.sprite);
            foreach (var image in instance.GetComponentsInChildren<Image>(true))
            {
                Assert.IsNotNull(image.sprite, $"{image.name} has no sprite");
            }
            foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true))
            {
                Assert.IsFalse(graphic.raycastTarget, $"{graphic.name} is a raycast target");
            }
        }

        [Test]
        public void Value_FillsItsFractionOfTheCeiling_AndReadsJustTheValue()
        {
            bar.SetValue(50, 200);

            Assert.AreEqual(0.25f, bar.Fill.rectTransform.anchorMax.x, 0.0001f);
            Assert.IsTrue(bar.Fill.enabled);
            Assert.AreEqual("50", bar.ValueLabel.text);
        }

        [Test]
        public void ValueAboveTheCeiling_PinsAFullBar()
        {
            bar.SetValue(250, 200);

            Assert.AreEqual(1f, bar.Fill.rectTransform.anchorMax.x, 0.0001f);
        }

        [Test]
        public void ZeroValueOrMax_ShowsOnlyTheTrack()
        {
            bar.SetValue(0, 200);
            Assert.IsFalse(bar.Fill.enabled);

            bar.SetValue(10, 0);
            Assert.IsFalse(bar.Fill.enabled);
        }
    }
}
