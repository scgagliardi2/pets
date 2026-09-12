using UnityEngine;
using UnityEngine.UI;
using Pets.Simulation;

namespace Pets.UI
{
    /// <summary>One active mon's stat box on the battle screen — see
    /// Assets/Prefabs/UI/BattleStatsBox.prefab (built by Pets.EditorTools.UiPrefabBuilder): type
    /// badges and name, a sword with the attack value, then the HealthBar and SpeedBar prefabs nested
    /// inside it. The battle screen holds four, one per Lead/Support slot, and re-points them as mons
    /// faint and step up; the sprite on the field is drawn separately, not in here.</summary>
    public sealed class BattleStatsBoxView : MonoBehaviour
    {
        public static readonly Vector2 Size = new Vector2(320f, 104f);

        // Layout constants shared with UiPrefabBuilder, which places the pieces; the view only needs
        // them to slide the name over when the second type badge is hidden.
        public const float Inset = 14f;
        public const float RowTop = 10f;
        public const float RowHeight = 32f;
        public const float IconSize = 28f;
        public const float IconGap = 4f;
        public const float NameGap = 8f;
        public const float AttackWidth = 40f;
        public const float SwordSize = 24f;

        /// <summary>A box with no mon in it — a side that's down to its Lead.</summary>
        private const float EmptyAlpha = 0.6f;

        [SerializeField] private TypeIconView firstType;
        [SerializeField] private TypeIconView secondType;
        [SerializeField] private Text nameText;
        [SerializeField] private Image swordIcon;
        [SerializeField] private Text attackText;
        [SerializeField] private HealthBarView healthBar;
        [SerializeField] private StatBarView speedBar;
        [SerializeField] private CanvasGroup group;

        public TypeIconView FirstType => firstType;
        public TypeIconView SecondType => secondType;
        public Text NameText => nameText;
        public Text AttackText => attackText;
        public HealthBarView HealthBar => healthBar;
        public StatBarView SpeedBar => speedBar;
        public CanvasGroup Group => group;

        public bool IsEmpty { get; private set; }

        /// <summary>Points the box at a mon. Health is left to the caller (<see cref="HealthBar"/>),
        /// since whether it jumps or drains depends on why the box is being redrawn.</summary>
        public void Show(string displayName, PokemonType type1, bool hasSecondType, PokemonType type2,
            int attack, int speed, int speedMax)
        {
            IsEmpty = false;
            SetContentActive(true);

            firstType.Type = type1;
            secondType.gameObject.SetActive(hasSecondType);
            if (hasSecondType)
            {
                secondType.Type = type2;
            }

            nameText.text = displayName;
            float nameLeft = Inset + IconSize + (hasSecondType ? IconGap + IconSize : 0f) + NameGap;
            nameText.rectTransform.offsetMin = new Vector2(nameLeft, nameText.rectTransform.offsetMin.y);

            attackText.text = attack.ToString();
            speedBar.SetValue(speed, speedMax);
            group.alpha = 1f;
        }

        /// <summary>No mon in this slot: the frame stays with a label, so the formation still reads
        /// as Lead + Support.</summary>
        public void SetEmpty(string label)
        {
            IsEmpty = true;
            SetContentActive(false);
            nameText.text = label;
            nameText.rectTransform.offsetMin = new Vector2(Inset, nameText.rectTransform.offsetMin.y);
            group.alpha = EmptyAlpha;
        }

        private void SetContentActive(bool active)
        {
            firstType.gameObject.SetActive(active);
            secondType.gameObject.SetActive(active);
            swordIcon.gameObject.SetActive(active);
            attackText.gameObject.SetActive(active);
            healthBar.gameObject.SetActive(active);
            speedBar.gameObject.SetActive(active);
        }
    }
}
