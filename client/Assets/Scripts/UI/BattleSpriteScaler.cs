using UnityEngine;
using UnityEngine.UI;

namespace Pets.UI
{
    /// <summary>Draws a battle sprite at a fixed multiple of its own pixel size, rather than
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
    public static class BattleSpriteScaler
    {
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
