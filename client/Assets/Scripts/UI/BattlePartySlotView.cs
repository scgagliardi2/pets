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
            nameText.text = displayName;
            track.SetActive(true);
        }

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
