using UnityEngine;
using UnityEngine.UI;
using Pets.Simulation;

namespace Pets.UI
{
    /// <summary>A mon's numbers on the battlefield: attack, current HP, any shield, and its status
    /// condition — floated beside the sprite instead of gathered into a stat box.
    ///
    /// Replaces BattleStatsBoxView on the field. The name and type badges it used to carry move to
    /// the party strip and the enemy preview, where there's room to read them; what's left here is
    /// only what changes moment to moment, so the board can be read at a glance mid-Step.
    ///
    /// Layout follows the mockup: attack and HP side by side, the status icon to the right of them
    /// (one at a time, since a mon can only carry one — battle-sim-spec.md §5), and the shield
    /// above the HP, where it reads as sitting on top of the health it absorbs for.
    ///
    /// **Note the HP is a number, not a bar.** That's the mockup's call and it's what's built here,
    /// but it does mean you can't see at a glance that something is on 12 of 60 rather than 12 of
    /// 14 — the proportion a bar gave you for free. The health bar prefab is untouched and still
    /// used elsewhere, so putting a slim one back under these numbers later is a small change.</summary>
    public sealed class BattleMonReadoutView : MonoBehaviour
    {
        [SerializeField] private Text attackText;
        [SerializeField] private Text healthText;
        [SerializeField] private Text shieldText;
        [SerializeField] private Image statusIcon;
        [SerializeField] private CanvasGroup group;

        public Text AttackText => attackText;
        public Text HealthText => healthText;
        public Text ShieldText => shieldText;
        public Image StatusIcon => statusIcon;

        /// <summary>Draws a mon's current numbers. Called every time the board is redrawn, so it
        /// has to be cheap and has to cope with every combination — a shielded, poisoned mon and a
        /// plain one go through the same path.</summary>
        public void Show(int attack, int currentHP, int shield, StatusType? status, Sprite statusSprite)
        {
            SetActive(true);
            attackText.text = attack.ToString();
            healthText.text = Mathf.Max(0, currentHP).ToString();

            // Zero shield hides the label rather than printing "0", which would read as a stat the
            // mon has rather than one it doesn't.
            bool shielded = shield > 0;
            shieldText.gameObject.SetActive(shielded);
            if (shielded)
            {
                shieldText.text = shield.ToString();
            }

            bool hasStatus = status.HasValue && statusSprite != null;
            statusIcon.gameObject.SetActive(hasStatus);
            if (hasStatus)
            {
                statusIcon.sprite = statusSprite;
            }
        }

        /// <summary>Hides everything — for a slot with no mon in it, a side down to its Lead.</summary>
        public void Clear() => SetActive(false);

        private void SetActive(bool on)
        {
            if (group != null)
            {
                group.alpha = on ? 1f : 0f;
            }
            else
            {
                gameObject.SetActive(on);
            }
        }
    }
}
