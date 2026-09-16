using UnityEngine;
using UnityEngine.UI;
using Pets.Simulation;

namespace Pets.UI
{
    /// <summary>The bubble around a mon that is carrying a Shield, with the amount it will absorb
    /// written on its upper-left rim.
    ///
    /// A shield is the one defensive effect on the field with nothing to show for itself: Attack,
    /// Health and Speed move the numbers in the stat box, a status is written on the mon, but an
    /// absorb pool changes nothing that is drawn — so the first the player knows of it is a hit
    /// that did less than the HP bar said it should. The bubble is that pool made visible, and the
    /// number on it is exactly <see cref="BattleCombatant.Shield"/>, so it drains hit by hit and
    /// pops when the pool is spent.
    ///
    /// Read off the combatant rather than from where the shield came, like the badges beside it:
    /// the Water team synergy (Shell Guard, Simulation/TeamSynergy) puts one up on the front of the
    /// line-up as the fight opens, and a Shield passive — Squirtle's Shell Guard, Geodude's Stone
    /// Guard — puts one up mid-fight when its charge meter fills. Both are the same pool, and both
    /// should look the same on screen.
    ///
    /// Attached at runtime to a field sprite (BattleScreenController), like the lunge and the badge
    /// row, so this needs no rebuild of Battle.unity. Being a child of the sprite means it draws in
    /// front of the mon and travels with it — a lunging mon carries its bubble into the blow.</summary>
    public sealed class ShieldBubbleView : MonoBehaviour
    {
        /// <summary>How much bigger than the mon the bubble is drawn. Enough clearance to read as
        /// something around the mon rather than a ring painted on it.</summary>
        public const float SizeMultiplier = 1.32f;

        /// <summary>The narrowest the bubble is allowed to get, as a fraction of its other axis.
        /// The sprite set is drawn at true relative scale (PokemonSpriteScaler), so bounding boxes
        /// run from a near-square Ralts to a 153x94 Lugia; wrapping each axis on its own would
        /// squash the bubble into a lens on the wide ones, and a circle around the longest axis
        /// would leave a Lugia sitting in a mostly empty sphere.</summary>
        private const float MinAxisRatio = 0.72f;

        /// <summary>Never smaller than this, so the bubble on a tiny mon is still a bubble.</summary>
        private const float MinDiameter = 56f;

        /// <summary>Where on the bubble the amount sits, in fractions of its rect: up and to the
        /// left, straddling the rim — clear of the mon's head, away from the badge row floating
        /// above it, and opposite the specular arc the sprite carries on its upper right.</summary>
        private static readonly Vector2 ChipAnchor = new Vector2(0.17f, 0.83f);

        /// <summary>The amount chip's size, in canvas units. Wide enough for two digits — a shield
        /// stacked past 99 would be a balance problem long before it was a layout one.</summary>
        private static readonly Vector2 ChipSize = new Vector2(38f, 30f);

        private RectTransform owner;
        private RectTransform rect;
        private Image bubble;
        private Text label;

        /// <summary>The amount currently written on the bubble; 0 when there's no bubble up. What a
        /// test reads instead of digging for the Text.</summary>
        public int Shown { get; private set; }

        /// <summary>True while the bubble is on screen.</summary>
        public bool IsShowing => gameObject.activeSelf;

        /// <summary>Wraps <paramref name="sprite"/> in a bubble, hidden until something has a shield.
        /// The sprite's rect is the mon's own pixels anchored to the spot it stands on
        /// (PokemonSpriteScaler), so sizing off that rect fits the bubble to the mon rather than to
        /// the slot.</summary>
        public static ShieldBubbleView Attach(Image sprite)
        {
            if (sprite == null)
            {
                return null;
            }

            var go = new GameObject("ShieldBubble", typeof(RectTransform));
            go.transform.SetParent(sprite.transform, false);

            var view = go.AddComponent<ShieldBubbleView>();
            view.owner = sprite.rectTransform;
            view.rect = go.GetComponent<RectTransform>();
            // Centred on the mon whatever the sprite's pivot is: anchors are fractions of the
            // parent's rect, which the ground pivot moves the sprite within but doesn't reshape.
            view.rect.anchorMin = view.rect.anchorMax = new Vector2(0.5f, 0.5f);
            view.rect.pivot = new Vector2(0.5f, 0.5f);
            view.rect.anchoredPosition = Vector2.zero;

            view.bubble = go.AddComponent<Image>();
            view.bubble.sprite = Theme.ShieldBubbleSprite;
            view.bubble.type = Image.Type.Simple;
            view.bubble.preserveAspect = false;
            // The enemy Lead's sprite is the catch tray's drop target (BattleScreenController) —
            // a bubble that took the raycast would swallow every ball thrown at a shielded mon.
            view.bubble.raycastTarget = false;

            view.label = BuildAmountChip(go.transform);
            go.SetActive(false);
            return view;
        }

        /// <summary>Redraws for one mon: a bubble at its current size carrying its current Shield,
        /// or nothing at all for an empty slot, a mon with no shield left, or one that has fainted —
        /// a mon still holding an unspent pool as it drops would otherwise sink out of the field
        /// inside a bubble that never popped.</summary>
        public void Show(BattleCombatant mon)
        {
            int shield = mon != null && mon.IsAlive ? mon.Shield : 0;
            Shown = Mathf.Max(0, shield);
            if (Shown <= 0)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            label.text = Shown.ToString();
            Resize();
        }

        /// <summary>Fits the bubble to whatever the sprite is currently drawn at — done on every
        /// redraw rather than once, because a promotion puts a different species in the slot and
        /// sizes its sprite to its own pixels.</summary>
        private void Resize()
        {
            var size = owner != null ? owner.rect.size : Vector2.zero;
            float width = Mathf.Max(MinDiameter, size.x * SizeMultiplier);
            float height = Mathf.Max(MinDiameter, size.y * SizeMultiplier);
            float floor = Mathf.Max(width, height) * MinAxisRatio;
            rect.sizeDelta = new Vector2(Mathf.Max(width, floor), Mathf.Max(height, floor));
        }

        /// <summary>The amount, on a small dark chip pinned to the bubble's rim. A chip rather than
        /// bare text: the number has to be read against the bubble's bright rim, the mon inside it
        /// and whatever the field is doing behind both, and an outlined glyph in this font is thin
        /// enough to lose against any of the three.</summary>
        private static Text BuildAmountChip(Transform parent)
        {
            var chip = new GameObject("ShieldAmount", typeof(RectTransform));
            chip.transform.SetParent(parent, false);

            var plate = chip.AddComponent<Image>();
            // The dark 9-sliced frame the party slots and the playback pill use, so the chip reads
            // as part of the battle screen's chrome rather than as a fifth new widget.
            plate.sprite = Theme.SlotDarkSprite;
            plate.type = Image.Type.Sliced;
            plate.raycastTarget = false;

            var chipRect = (RectTransform)chip.transform;
            chipRect.anchorMin = chipRect.anchorMax = ChipAnchor;
            chipRect.pivot = new Vector2(0.5f, 0.5f);
            chipRect.sizeDelta = ChipSize;
            chipRect.anchoredPosition = Vector2.zero;

            var go = new GameObject("Amount", typeof(RectTransform));
            go.transform.SetParent(chip.transform, false);

            var text = go.AddComponent<Text>();
            text.font = Theme.GameFont;
            text.fontSize = Theme.FontSizeHeading;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Theme.TextLight;
            text.raycastTarget = false;
            // Overflow both ways: a two-digit shield shouldn't start eating its own characters just
            // because the chip is only as big as the rim can carry.
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return text;
        }
    }
}
