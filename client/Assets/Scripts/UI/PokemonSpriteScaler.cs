using UnityEngine;
using UnityEngine.UI;

namespace Pets.UI
{
    /// <summary>Draws a Pokémon sprite at a size derived from its own pixels, rather than
    /// stretching it to fill a fixed box.
    ///
    /// The distinction matters because the sprite set is drawn at true relative scale: Ralts is
    /// 23x39 pixels and Lugia is 153x94, about four times the linear size, because Lugia *is* about
    /// four times the size. Fitting each one to a fixed slot (`Image.preserveAspect` against a fixed
    /// rect, which is what the field slots did) throws that away and draws every species at the same
    /// on-screen size — a Caterpie looming as large as a Rayquaza. Multiplying each sprite's own
    /// pixel size by one shared factor keeps the proportions the artwork already encodes.
    ///
    /// Two consequences worth stating, because both are deliberate:
    ///
    /// - **The slot no longer bounds the sprite.** A large mon overflows the rect it's anchored to.
    ///   That's the point — the rect stops being a box the art is squeezed into and becomes just the
    ///   spot on the ground where the mon stands. <see cref="AnchorToGround"/> is what re-points it.
    /// - **Scales should be whole numbers.** These are pixel art; a 1.5x scale renders some source
    ///   pixels two screen-pixels wide and some three, which reads as a wobble along every edge that
    ///   Point filtering then makes crisply visible. Integers keep every pixel the same size.</summary>
    public static class PokemonSpriteScaler
    {
        /// <summary>The pixel height a sprite needs to be to fill a slot completely, used by
        /// <see cref="RelativeSize"/>. Set just above the tallest sprite in the set (117), so the
        /// largest species almost fills its box and everything else is drawn in proportion beneath
        /// it — and so adding a slightly taller sprite later doesn't immediately overflow.</summary>
        public const float ReferenceHeight = 120f;

        /// <summary>The size to draw <paramref name="sprite"/> at inside a slot
        /// <paramref name="slotHeight"/> tall, keeping every species in proportion to every other.
        ///
        /// For the screens that show Pokémon in small fixed boxes — the character select grid, the
        /// Pokédex, roster cards — where a whole-number scale like the battlefield's isn't an
        /// option: a 153px Lugia has to come down to fit an 80px card whatever else happens. The
        /// scale is derived from the slot rather than the sprite, so it's the same for every
        /// species in that slot, which is the property that matters. Absolute size still differs
        /// between screens, because their boxes differ.</summary>
        public static Vector2 RelativeSize(Sprite sprite, float slotHeight)
        {
            if (sprite == null)
            {
                return Vector2.zero;
            }
            return sprite.rect.size * (slotHeight / ReferenceHeight);
        }

        /// <summary>Re-points a sprite's RectTransform so its bottom-centre — where a mon's feet
        /// are — stays exactly where the bottom-centre of its original slot rect was, and so
        /// resizing afterwards grows the sprite upward and out to the sides rather than away from
        /// one corner.
        ///
        /// Called once per slot at startup. Doing it in code rather than in BattleSceneBuilder is
        /// what lets this work on a Battle scene that hasn't been regenerated: the builder's rects
        /// are read as input and converted, instead of having to already be authored this way.</summary>
        public static void AnchorToGround(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            // Both PlaceTop and PlaceBottom leave anchorMin == anchorMax, so the rect is positioned
            // relative to a single anchor point and this arithmetic holds. Where the bottom-centre
            // sits, measured from that anchor, depends on the current pivot.
            var size = rect.sizeDelta;
            var pivot = rect.pivot;
            var bottomCentre = rect.anchoredPosition
                + new Vector2((0.5f - pivot.x) * size.x, (0f - pivot.y) * size.y);

            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = bottomCentre;
        }

        /// <summary>Sizes the image to its sprite's own pixel dimensions times <paramref name="scale"/>.
        /// A null sprite leaves the rect alone rather than collapsing it to nothing, so a slot
        /// waiting on content doesn't flicker to zero size.</summary>
        public static void ApplyScale(Image image, float scale)
        {
            if (image == null || image.sprite == null)
            {
                return;
            }
            var native = image.sprite.rect.size;
            image.rectTransform.sizeDelta = native * scale;
        }
    }
}
