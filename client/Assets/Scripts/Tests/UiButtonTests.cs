using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Pets.UI;

namespace Pets.Tests
{
    /// <summary>Covers the Button prefab + UiButton contract. These assert the things that are
    /// invisible in a screenshot and that previously drifted per call site — the slicing setup,
    /// the per-style sprite, the interaction tint ramp, and the press nudge — rather than anything
    /// about how the art looks.</summary>
    public sealed class UiButtonTests
    {
        private const string ButtonPrefabPath = "Assets/Prefabs/UI/Button.prefab";

        private GameObject instance;
        private UiButton uiButton;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);
            Assert.IsNotNull(prefab, $"Button prefab missing at {ButtonPrefabPath} - run Pets > Build UI Prefabs.");
            instance = Object.Instantiate(prefab);
            uiButton = instance.GetComponent<UiButton>();
            Assert.IsNotNull(uiButton, "Button prefab has no UiButton component.");
        }

        [TearDown]
        public void TearDown()
        {
            if (instance != null)
            {
                Object.DestroyImmediate(instance);
            }
        }

        private Image Background => instance.GetComponent<Image>();

        [Test]
        public void BackgroundIsSlicedAtUnmodifiedBorderScale()
        {
            uiButton.Style = Theme.ButtonStyle.Primary;

            Assert.AreEqual(Image.Type.Sliced, Background.type);
            Assert.IsTrue(Background.fillCenter);
            // A multiplier other than 1 rescales the 9-slice border per call site, which is the
            // inconsistency UiButton exists to prevent.
            Assert.AreEqual(1f, Background.pixelsPerUnitMultiplier);
            // The art carries its own colour; tinting here as well would multiply twice.
            Assert.AreEqual(Color.white, Background.color);
        }

        [Test]
        public void SpriteHasNineSliceBorderOnEverySide()
        {
            foreach (Theme.ButtonStyle style in System.Enum.GetValues(typeof(Theme.ButtonStyle)))
            {
                uiButton.Style = style;
                var border = Background.sprite.border;
                Assert.IsTrue(border.x > 0 && border.y > 0 && border.z > 0 && border.w > 0,
                    $"{style} sprite '{Background.sprite.name}' has border {border} - a zero side makes " +
                    "Image.Type.Sliced stretch the corner art instead of holding it fixed.");
            }
        }

        [Test]
        public void EachStyleGetsItsOwnPaletteSprite()
        {
            uiButton.Style = Theme.ButtonStyle.Primary;
            Assert.AreEqual(Theme.ButtonBlueSprite, Background.sprite);

            uiButton.Style = Theme.ButtonStyle.Confirm;
            Assert.AreEqual(Theme.ButtonGreenSprite, Background.sprite);

            uiButton.Style = Theme.ButtonStyle.Danger;
            Assert.AreEqual(Theme.ButtonRedSprite, Background.sprite);

            // Secondary/Disabled share the slate art and are separated by the tint, not by a grey
            // wash over the blue art.
            uiButton.Style = Theme.ButtonStyle.Secondary;
            Assert.AreEqual(Theme.ButtonGraySprite, Background.sprite);

            uiButton.Style = Theme.ButtonStyle.Disabled;
            Assert.AreEqual(Theme.ButtonGraySprite, Background.sprite);
        }

        [Test]
        public void AppliesTheSharedTintRamp()
        {
            uiButton.Style = Theme.ButtonStyle.Primary;
            var colors = uiButton.Button.colors;

            Assert.AreEqual(Selectable.Transition.ColorTint, uiButton.Button.transition);
            Assert.AreEqual(Theme.ButtonTintNormal, colors.normalColor);
            Assert.AreEqual(Theme.ButtonTintHighlighted, colors.highlightedColor);
            Assert.AreEqual(Theme.ButtonTintPressed, colors.pressedColor);
            Assert.AreEqual(Theme.ButtonTintDisabled, colors.disabledColor);
            Assert.AreEqual(Background, uiButton.Button.targetGraphic);
        }

        [Test]
        public void PressNudgesLabelDownAndReleaseRestoresIt()
        {
            var label = uiButton.Label.rectTransform;
            Assert.AreEqual(0f, label.anchoredPosition.y);

            uiButton.OnPointerDown(new PointerEventData(EventSystem.current));
            Assert.Less(label.anchoredPosition.y, 0f, "Pressing should sink the label.");

            uiButton.OnPointerUp(new PointerEventData(EventSystem.current));
            Assert.AreEqual(0f, label.anchoredPosition.y, "Releasing should restore the label.");
        }

        [Test]
        public void NonInteractableButtonDoesNotNudge()
        {
            uiButton.Button.interactable = false;
            var label = uiButton.Label.rectTransform;

            uiButton.OnPointerDown(new PointerEventData(EventSystem.current));

            Assert.AreEqual(0f, label.anchoredPosition.y);
        }

        [Test]
        public void DoesNotHandleClicksItself()
        {
            // UiButton sits on the same GameObject as Button, where ExecuteEvents runs every
            // component implementing the handler. Implementing IPointerClickHandler here as well
            // would route the same click to both and fire onClick twice.
            Assert.IsFalse(uiButton is IPointerClickHandler);
        }

        [Test]
        public void LabelDoesNotBlockPointerEventsFromTheButton()
        {
            // A raycast-target label would be found before the Button underneath it and would
            // swallow the press.
            Assert.IsFalse(uiButton.Label.raycastTarget);
        }
    }
}
