using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Pets.Simulation;

namespace Pets.UI
{
    /// <summary>The badges floating over one active mon on the battlefield: what is currently on it
    /// that its stat box doesn't already show — a shield, blocked damage, lifesteal, a status ward,
    /// the status it is suffering, and charge it starts the fight owing.
    ///
    /// Most of these arrive from a team type synergy as the fight opens (Simulation/TeamSynergy,
    /// ADR 0015), which is otherwise invisible: Attack, Health and Speed bonuses show up in the stat
    /// box because they change the numbers there, but a shield, flat damage reduction and lifesteal
    /// change nothing the box draws. A passive that shields or poisons puts the same badges up, since
    /// the badge is read off the combatant's state rather than from where that state came.
    ///
    /// Each badge borrows the type badge of the type whose synergy grants it — Water for a shield,
    /// Steel for damage reduction, Grass for lifesteal, Fairy for a ward, Ice for charge owed — so
    /// the icon over a mon matches the chip in the synergy row that caused it. A status uses the type
    /// that inflicts it (Poison, Fire, Electric, Psychic) and is drawn in the danger colour, which is
    /// what tells a bad badge from a good one at a glance.
    ///
    /// Attached at runtime to a field sprite (BattleScreenController), like the lunge beside it, and
    /// refreshed on every redraw. Badges are built once and hidden rather than destroyed, because a
    /// redraw happens several times a Step.</summary>
    public sealed class EffectBadgeRowView : MonoBehaviour
    {
        public const float BadgeSize = 26f;
        public const float BadgeGap = 4f;
        private const float LabelHeight = 16f;

        /// <summary>How far above the mon's head the row floats.</summary>
        private const float LiftAboveSprite = 6f;

        private sealed class Badge
        {
            public GameObject Root;
            public TypeIconView Icon;
            public Text Label;
        }

        private readonly List<Badge> pool = new List<Badge>();
        private GameObject typeIconPrefab;
        private RectTransform rect;

        /// <summary>The badges currently up, in order — what a test reads instead of digging through
        /// child objects.</summary>
        public IReadOnlyList<string> Shown => shown;

        private readonly List<string> shown = new List<string>();

        /// <summary>Hangs a row over <paramref name="sprite"/>. The sprite's rect is pivoted at the
        /// spot on the ground the mon stands on (PokemonSpriteScaler.AnchorToGround) and resized to
        /// the mon's own pixels, so anchoring to its top edge puts the badges over the head of a
        /// Lugia and a Ralts alike.</summary>
        public static EffectBadgeRowView Attach(Image sprite, GameObject typeIconPrefab)
        {
            if (sprite == null)
            {
                return null;
            }

            var go = new GameObject("EffectBadges", typeof(RectTransform));
            go.transform.SetParent(sprite.transform, false);
            var view = go.AddComponent<EffectBadgeRowView>();
            view.typeIconPrefab = typeIconPrefab;
            view.rect = go.GetComponent<RectTransform>();
            view.rect.anchorMin = new Vector2(0.5f, 1f);
            view.rect.anchorMax = new Vector2(0.5f, 1f);
            view.rect.pivot = new Vector2(0.5f, 0f);
            view.rect.sizeDelta = new Vector2(0f, BadgeSize + LabelHeight);
            view.rect.anchoredPosition = new Vector2(0f, LiftAboveSprite);
            return view;
        }

        /// <summary>Redraws for one mon, or clears the row when there's nobody in the slot.</summary>
        public void Show(BattleCombatant mon)
        {
            shown.Clear();
            if (mon != null)
            {
                if (mon.Shield > 0)
                {
                    Add(PokemonType.Water, mon.Shield.ToString(), positive: true, key: "Shield");
                }
                if (mon.DamageReductionFlat > 0)
                {
                    Add(PokemonType.Steel, mon.DamageReductionFlat.ToString(), positive: true, key: "Armour");
                }
                if (mon.LifestealPercent > 0f)
                {
                    Add(PokemonType.Grass, $"{Mathf.RoundToInt(mon.LifestealPercent * 100f)}%", positive: true, key: "Lifesteal");
                }
                if (mon.StatusWards > 0)
                {
                    Add(PokemonType.Fairy, mon.StatusWards.ToString(), positive: true, key: "Ward");
                }
                if (mon.Status.HasValue)
                {
                    Add(TypeForStatus(mon.Status.Value), string.Empty, positive: false, key: mon.Status.Value.ToString());
                }
                // Charge owed to Permafrost or Sand Tomb: the mon accrues as normal but starts in
                // debt, so until it climbs back to zero its passive is further off than its Speed
                // suggests. Nothing else on screen would say why.
                if (mon.Charge < 0)
                {
                    Add(PokemonType.Ice, mon.Charge.ToString(), positive: false, key: "ChargeDebt");
                }
            }

            for (int i = 0; i < pool.Count; i++)
            {
                pool[i].Root.SetActive(i < shown.Count);
            }
            Layout();
        }

        private static PokemonType TypeForStatus(StatusType status)
        {
            switch (status)
            {
                case StatusType.Poisoned: return PokemonType.Poison;
                case StatusType.Burned: return PokemonType.Fire;
                case StatusType.Paralyzed: return PokemonType.Electric;
                default: return PokemonType.Psychic;
            }
        }

        private void Add(PokemonType type, string label, bool positive, string key)
        {
            var badge = At(shown.Count);
            if (badge.Icon != null)
            {
                badge.Icon.Type = type;
            }
            badge.Label.text = label;
            badge.Label.color = positive ? Theme.TextLight : Theme.Danger;
            badge.Root.name = $"Effect_{key}";
            badge.Root.SetActive(true);
            shown.Add(key);
        }

        private Badge At(int index)
        {
            while (pool.Count <= index)
            {
                pool.Add(Build());
            }
            return pool[index];
        }

        private Badge Build()
        {
            var root = new GameObject("Effect", typeof(RectTransform));
            root.transform.SetParent(rect, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0f, 0f);
            rootRect.pivot = new Vector2(0f, 0f);
            rootRect.sizeDelta = new Vector2(BadgeSize, BadgeSize + LabelHeight);

            TypeIconView icon = null;
            if (typeIconPrefab != null)
            {
                var instance = Object.Instantiate(typeIconPrefab, root.transform, false);
                instance.name = "Icon";
                icon = instance.GetComponent<TypeIconView>();
                var iconRect = (RectTransform)instance.transform;
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1f);
                iconRect.pivot = new Vector2(0.5f, 1f);
                iconRect.sizeDelta = new Vector2(BadgeSize, BadgeSize);
                iconRect.anchoredPosition = Vector2.zero;
            }

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(root.transform, false);
            var label = labelGo.AddComponent<Text>();
            label.font = Theme.GameFont;
            label.fontSize = Theme.FontSizeSmall;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.UpperCenter;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = labelGo.AddComponent<Outline>();
            outline.effectColor = Theme.ChromeBg;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.sizeDelta = new Vector2(0f, LabelHeight);
            labelRect.anchoredPosition = Vector2.zero;

            return new Badge { Root = root, Icon = icon, Label = label };
        }

        /// <summary>Centres however many badges are up, so a mon with one badge wears it over its
        /// head rather than off to one side.</summary>
        private void Layout()
        {
            float width = shown.Count * BadgeSize + Mathf.Max(0, shown.Count - 1) * BadgeGap;
            rect.sizeDelta = new Vector2(width, BadgeSize + LabelHeight);
            for (int i = 0; i < shown.Count; i++)
            {
                var badgeRect = (RectTransform)pool[i].Root.transform;
                badgeRect.anchoredPosition = new Vector2(i * (BadgeSize + BadgeGap), 0f);
            }
        }
    }
}
