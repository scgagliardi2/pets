using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Pets.UI
{
    /// <summary>Reusable sprite-backed button widget — see Assets/Prefabs/UI/Button.prefab (built
    /// by Pets.EditorTools.UiPrefabBuilder). Wraps the 9-sliced button art (Theme.ButtonSprite) so
    /// every place that wants the "Monster Trails" look instantiates one component instead of
    /// re-deriving the Image/Button/Text setup by hand. Style is editable directly in the Inspector
    /// (or the prefab itself) and re-applies live via OnValidate.
    ///
    /// Apply() owns every visual the style implies — sprite, slicing, tint ramp, label colour and
    /// shadow — rather than leaving some of it to whoever builds the scene, because the previous
    /// split (sprite here, Selectable.colors at each call site) was how buttons ended up looking
    /// different depending on which code path created them.</summary>
    [RequireComponent(typeof(Button))]
    [ExecuteAlways]
    public sealed class UiButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        // How far the label sinks on press. The art has a baked 1px top highlight / bottom shadow,
        // so nudging the label alone reads as the whole button depressing.
        private const float PressOffset = 2f;

        [SerializeField] private Image background;
        [SerializeField] private Text label;
        [SerializeField] private Shadow labelShadow;
        [SerializeField] private Theme.ButtonStyle style = Theme.ButtonStyle.Primary;

        private Button cachedButton;

        public Button Button => cachedButton != null ? cachedButton : cachedButton = GetComponent<Button>();
        public Text Label => label;

        public string Text
        {
            get => label != null ? label.text : string.Empty;
            set
            {
                if (label != null)
                {
                    label.text = value;
                }
            }
        }

        public Theme.ButtonStyle Style
        {
            get => style;
            set
            {
                style = value;
                Apply();
            }
        }

        private void Awake() => Apply();

        private void OnEnable() => Apply();

        private void OnValidate() => Apply();

        private void Apply()
        {
            if (background == null || label == null)
            {
                return;
            }

            background.sprite = Theme.ButtonSprite(style);
            background.type = Image.Type.Sliced;
            background.fillCenter = true;
            // The border size baked into the sprite import (see UiSpriteImportProcessor) is already
            // the thickness we want; a multiplier other than 1 would rescale it per call site,
            // which is exactly the inconsistency this component exists to prevent.
            background.pixelsPerUnitMultiplier = 1f;
            // White: the art carries its own colour and Selectable.colors shades it. Tinting here
            // as well would multiply twice and darken the bevel.
            background.color = Color.white;

            var button = Button;
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Theme.ButtonTintNormal;
            colors.highlightedColor = Theme.ButtonTintHighlighted;
            colors.pressedColor = Theme.ButtonTintPressed;
            colors.selectedColor = Theme.ButtonTintSelected;
            colors.disabledColor = Theme.ButtonTintDisabled;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            label.color = Theme.ButtonText(style);
            if (labelShadow != null)
            {
                // A hard 2px offset rather than a soft blur, to match the pixel art. Light labels
                // get a dark drop shadow; the dark label on the slate Secondary art would only be
                // muddied by one, so it gets a light lift instead.
                bool lightLabel = style != Theme.ButtonStyle.Secondary && style != Theme.ButtonStyle.Disabled;
                labelShadow.effectColor = lightLabel
                    ? new Color(0f, 0f, 0f, 0.45f)
                    : new Color(1f, 1f, 1f, 0.35f);
                labelShadow.effectDistance = new Vector2(0f, lightLabel ? -2f : 1f);
                labelShadow.useGraphicAlpha = true;
            }
        }

        // Moves the label rather than this transform: shifting the Button's own RectTransform
        // would fight the LayoutGroups these buttons usually sit inside, which snap it straight
        // back on the next layout pass.
        private void SetPressed(bool pressed)
        {
            if (label == null)
            {
                return;
            }
            var rect = label.rectTransform;
            var position = rect.anchoredPosition;
            position.y = pressed ? -PressOffset : 0f;
            rect.anchoredPosition = position;
        }

        // These live alongside Button's own handlers on this same GameObject. ExecuteEvents runs
        // every component on the target that implements the interface, so Button still gets its
        // pointer-down and drives the ColorTint — putting these on the label child instead would
        // have made the label the handler found first and swallowed that. IPointerClickHandler is
        // deliberately not implemented here, so clicks reach Button and fire onClick exactly once.
        public void OnPointerDown(PointerEventData eventData) => SetPressed(Button.IsInteractable());

        public void OnPointerUp(PointerEventData eventData) => SetPressed(false);

        private void OnDisable() => SetPressed(false);
    }
}
