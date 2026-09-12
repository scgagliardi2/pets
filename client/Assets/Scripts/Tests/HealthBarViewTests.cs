using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Pets.UI;

namespace Pets.Tests
{
    /// <summary>Covers the HealthBar prefab + HealthBarView contract: that the prefab is wired, that
    /// the fill tracks current/max (including the edges a battle will actually hit — zero, overheal,
    /// negative), and that the colour bands switch where Theme says they do.</summary>
    public sealed class HealthBarViewTests
    {
        private const string HealthBarPrefabPath = "Assets/Prefabs/UI/HealthBar.prefab";

        private GameObject instance;
        private HealthBarView bar;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HealthBarPrefabPath);
            Assert.IsNotNull(prefab, $"HealthBar prefab missing at {HealthBarPrefabPath} - run Pets > Build UI Prefabs.");
            instance = Object.Instantiate(prefab);
            bar = instance.GetComponent<HealthBarView>();
            Assert.IsNotNull(bar, "HealthBar prefab has no HealthBarView component.");
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
        public void PrefabIsWiredWithLoadedArt()
        {
            Assert.IsNotNull(bar.Fill, "fill is not wired");
            Assert.IsNotNull(bar.ValueLabel, "valueLabel is not wired");
            foreach (var image in instance.GetComponentsInChildren<Image>(true))
            {
                Assert.IsNotNull(image.sprite, $"{image.name} has no sprite");
                Assert.AreEqual(Image.Type.Sliced, image.type, $"{image.name} should be 9-sliced");
                Assert.AreNotEqual(Vector4.zero, image.sprite.border, $"{image.name}'s sprite has no 9-slice border");
            }
        }

        /// <summary>The bar sits on cards that are Buttons; a raycast-target child would swallow the
        /// click meant for the card.</summary>
        [Test]
        public void NothingInTheBarBlocksClicks()
        {
            foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true))
            {
                Assert.IsFalse(graphic.raycastTarget, $"{graphic.name} is a raycast target");
            }
        }

        [Test]
        public void FullHealth_FillsTheTrackInGreen()
        {
            bar.SetHealth(45, 45);

            Assert.AreEqual(1f, bar.Fill.rectTransform.anchorMax.x, 0.0001f);
            Assert.IsTrue(bar.Fill.enabled);
            Assert.AreSame(Theme.HealthGreenSprite, bar.Fill.sprite);
            Assert.AreEqual("45/45", bar.ValueLabel.text);
        }

        [Test]
        public void PartialHealth_ScalesTheFillToTheFraction()
        {
            bar.SetHealth(30, 120);

            Assert.AreEqual(0.25f, bar.Fraction, 0.0001f);
            Assert.AreEqual(0.25f, bar.Fill.rectTransform.anchorMax.x, 0.0001f);
            Assert.AreEqual(0f, bar.Fill.rectTransform.anchorMin.x);
            Assert.AreEqual("30/120", bar.ValueLabel.text);
        }

        [Test]
        public void FillColour_ChangesAtHalfAndAtAFifth()
        {
            bar.SetHealth(51, 100);
            Assert.AreSame(Theme.HealthGreenSprite, bar.Fill.sprite, "above half should be green");

            bar.SetHealth(50, 100);
            Assert.AreSame(Theme.HealthYellowSprite, bar.Fill.sprite, "exactly half should be yellow");

            bar.SetHealth(21, 100);
            Assert.AreSame(Theme.HealthYellowSprite, bar.Fill.sprite, "just above a fifth should still be yellow");

            bar.SetHealth(20, 100);
            Assert.AreSame(Theme.HealthRedSprite, bar.Fill.sprite, "a fifth and below should be red");

            Assert.AreNotSame(Theme.HealthGreenSprite, Theme.HealthYellowSprite);
            Assert.AreNotSame(Theme.HealthYellowSprite, Theme.HealthRedSprite);
        }

        [Test]
        public void Fainted_HidesTheFillAndReadsZero()
        {
            bar.SetHealth(-7, 50);

            Assert.AreEqual(0f, bar.Fraction);
            Assert.IsFalse(bar.Fill.enabled, "an empty bar should show only the track");
            Assert.AreEqual("0/50", bar.ValueLabel.text, "damage past zero shouldn't show a negative HP");
        }

        [Test]
        public void Overheal_ClampsToAFullBar()
        {
            bar.SetHealth(80, 50);

            Assert.AreEqual(1f, bar.Fill.rectTransform.anchorMax.x, 0.0001f);
        }

        [Test]
        public void ZeroMax_IsEmptyRatherThanDividingByZero()
        {
            bar.SetHealth(0, 0);

            Assert.AreEqual(0f, bar.Fraction);
            Assert.IsFalse(bar.Fill.enabled);
        }
    }
}
