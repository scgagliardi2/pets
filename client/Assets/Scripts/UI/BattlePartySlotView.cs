using UnityEngine;
using UnityEngine.UI;

namespace Pets.UI
{
    public enum PartySlotRole
    {
        Empty,
        Reserve,
        Lead,
        Support,
        Fainted
    }

    /// <summary>One slot in the battle screen's party strip — see
    /// Assets/Prefabs/UI/BattlePartySlot.prefab (built by Pets.EditorTools.UiPrefabBuilder): a
    /// portrait, the mon's name and a slim HP bar in a dark frame. The frame turns gold for the Lead
    /// and blue for the Support (design doc §7: only those two are active); a fainted mon's slot is
    /// dimmed. Its HP bar drains over time the same way the stat boxes' do.</summary>
    public sealed class BattlePartySlotView : MonoBehaviour
    {
        public static readonly Vector2 Size = new Vector2(146f, 150f);

        private const float FaintedAlpha = 0.45f;
        private const float EmptyAlpha = 0.35f;

        [SerializeField] private Image frame;
        [SerializeField] private Image portrait;
        [SerializeField] private Text nameText;
        [SerializeField] private GameObject track;
        [SerializeField] private Image fill;
        [SerializeField] private CanvasGroup group;

        private float fraction = 1f;
        private ValueTween tween;

        public Image Frame => frame;
        public Image Portrait => portrait;
        public Text NameText => nameText;
        public Image Fill => fill;
        public CanvasGroup Group => group;

        public PartySlotRole Role { get; private set; } = PartySlotRole.Empty;

        /// <summary>The value the bar is at or moving to.</summary>
        public float HealthFraction => fraction;

        public bool IsAnimating => tween.IsRunning;

        public void SetMon(Sprite portraitSprite, string displayName)
        {
            portrait.enabled = true;
            portrait.sprite = portraitSprite;
            SizePortrait();
            nameText.text = displayName;
            track.SetActive(true);
        }

        /// <summary>Sizes the portrait from the sprite's own pixels so the party strip shows species
        /// in proportion to each other, the way the battlefield and the card screens now do, rather
        /// than blowing every mon up to fill the same 86-unit row.
        ///
        /// The row is authored pinned to the *top* of the slot (UiPrefabBuilder.PinRow), which is
        /// the wrong way round once heights vary: a small mon would hang from the top of its frame
        /// with a gap beneath it. Re-pinned here to the row's bottom edge, so shorter sprites sit
        /// down on a shared baseline and the strip reads as a line-up rather than a set of dangling
        /// portraits. Done once, guarded by <see cref="portraitBaselined"/>, because it reads the
        /// authored rect to find that baseline and must not then read back its own result.</summary>
        private void SizePortrait()
        {
            if (portrait == null)
            {
                return;
            }

            var rect = portrait.rectTransform;
            if (!portraitBaselined)
            {
                float rowHeight = rect.rect.height;
                // offsetMin.y is the row's bottom edge measured down from the slot's top, which is
                // the baseline to stand the mons on.
                float bottom = rect.offsetMin.y;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, bottom);
                portraitRowHeight = rowHeight;
                portraitBaselined = true;
            }

            rect.sizeDelta = PokemonSpriteScaler.RelativeSize(portrait.sprite, portraitRowHeight);
        }

        private bool portraitBaselined;
        private float portraitRowHeight;

        public void SetRole(PartySlotRole role)
        {
            Role = role;
            switch (role)
            {
                case PartySlotRole.Lead:
                    frame.sprite = Theme.SlotGoldSprite;
                    break;
                case PartySlotRole.Support:
                    frame.sprite = Theme.SlotBlueSprite;
                    break;
                default:
                    frame.sprite = Theme.SlotDarkSprite;
                    break;
            }

            if (role == PartySlotRole.Empty)
            {
                portrait.enabled = false;
                nameText.text = string.Empty;
                track.SetActive(false);
            }

            group.alpha = role == PartySlotRole.Fainted ? FaintedAlpha
                : role == PartySlotRole.Empty ? EmptyAlpha
                : 1f;
        }

        public void SetHealthFraction(float value)
        {
            fraction = Mathf.Clamp01(value);
            tween = default;
            ApplyFill(fraction);
        }

        public void AnimateHealthFraction(float value, float seconds)
        {
            float from = tween.IsRunning ? tween.Value : fraction;
            fraction = Mathf.Clamp01(value);
            tween = new ValueTween(from, fraction, seconds);
            ApplyFill(tween.Value);
        }

        public void Advance(float seconds)
        {
            if (!tween.IsRunning)
            {
                return;
            }
            tween.Advance(seconds);
            ApplyFill(tween.Value);
        }

        private void Update()
        {
            if (tween.IsRunning)
            {
                Advance(Time.deltaTime);
            }
        }

        private void ApplyFill(float shown)
        {
            if (fill == null)
            {
                return;
            }
            StatBarView.SizeFill(fill, shown);
            fill.sprite = Theme.HealthFillSprite(shown);
        }
    }
}
