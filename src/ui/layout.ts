/**
 * Battlefield layout — the single source of truth.
 *
 * §6.7 of the rebuild reference records two multi-round bugs in the Unity build, and both are
 * avoided here by construction rather than by care:
 *
 * **Mixed anchoring.** The canvas scaled to match width, so its height moved with the window's
 * aspect ratio. Sprites anchored to the bottom edge and their stat readouts anchored to the top
 * drifted about 100px apart on a non-16:9 window. The fix is that there is exactly one coordinate
 * space: a fixed 1280x720 field, scaled as a whole to fit whatever the viewport is. Everything on
 * the field is positioned in *field* pixels against that box, never against a viewport edge.
 *
 * **Layout baked in two places.** Positions lived both in a scene file and in code and silently
 * disagreed; two rounds of position fixes shipped without appearing on screen, because the baked
 * copy won. So every position on the field comes from this module. No component hard-codes a
 * coordinate, and no stylesheet positions a field element.
 *
 * Nothing here imports React, so tests can assert against the same numbers the renderer draws.
 */

/** The field's intrinsic size. Everything below is in these pixels. */
export const FIELD = { width: 1280, height: 720 } as const;

/**
 * Whole-number sprite scale factors. Your side is drawn larger than the foe's and the size
 * difference reads as depth.
 *
 * Whole numbers are not fussiness: at 1.5x some source pixels render two screen-pixels wide and
 * others three, which looks like a wobble crawling along every edge of the sprite.
 */
export const SPRITE_SCALE = { own: 3, foe: 2 } as const;

/** A slot on the field. `x`/`y` is where the mon's **feet** land, not its centre or a corner. */
export interface Slot {
  /** Feet anchor, in field pixels. */
  x: number;
  y: number;
  /** Which way the stat readout sits relative to the mon. */
  readout: 'right' | 'below';
  scale: number;
  facing: 'front' | 'back';
  /** Draw order; the Support sits further back. */
  z: number;
}

/**
 * The four field positions.
 *
 * Your pair is lower-left with **back** sprites; the foe's is upper-right with **front** sprites.
 * Your stats sit to the right of the Lead and beneath the Support, the foe's beneath both — the
 * readout goes wherever there is empty field next to that mon, which differs by slot.
 *
 * Supports are set back and up from their Lead so all four mons are legible at once.
 */
export const SLOTS: Readonly<Record<'ownLead' | 'ownSupport' | 'foeLead' | 'foeSupport', Slot>> = {
  ownLead: { x: 360, y: 632, readout: 'right', scale: SPRITE_SCALE.own, facing: 'back', z: 30 },
  ownSupport: { x: 168, y: 498, readout: 'below', scale: SPRITE_SCALE.own, facing: 'back', z: 20 },
  foeLead: { x: 870, y: 392, readout: 'below', scale: SPRITE_SCALE.foe, facing: 'front', z: 30 },
  foeSupport: { x: 1046, y: 320, readout: 'below', scale: SPRITE_SCALE.foe, facing: 'front', z: 20 },
};

/**
 * The most of the field one sprite may occupy, as a fraction of each axis.
 *
 * Gen 5 sprites are drawn at true relative scale and run from 23x39 to 153x94, so a flat 3x puts
 * the largest of them at 459x282 — over a third of the field's width, spilling off the left edge
 * from the Support slot and swallowing its own Lead. A flat 2x, meanwhile, wastes the field on the
 * hundred-odd species that are small.
 *
 * So the slot's scale is a *ceiling*, stepped down in whole numbers until the sprite fits. Whole
 * numbers are preserved, which is the part that matters for pixel art; what's given up is that a
 * Lugia is no longer exactly four times a Ralts on screen. It is still clearly far bigger, and
 * that reads as intended — the alternative is a legendary drawn a fifth off-screen.
 */
export const MAX_SPRITE_FRACTION = { width: 0.26, height: 0.42 } as const;

/**
 * The scale a sprite is actually drawn at: the slot's scale, stepped down by whole numbers until
 * it fits the field. Never below 1.
 */
export function effectiveScale(slot: Slot, natural: { width: number; height: number }): number {
  const maxW = FIELD.width * MAX_SPRITE_FRACTION.width;
  const maxH = FIELD.height * MAX_SPRITE_FRACTION.height;

  for (let scale = slot.scale; scale > 1; scale--) {
    if (natural.width * scale <= maxW && natural.height * scale <= maxH) return scale;
  }
  return 1;
}

/** Slot for a side and a role. Anything past the Support is dormant and not on the field. */
export function slotFor(side: 'A' | 'B', index: number): Slot | null {
  if (side === 'A') return index === 0 ? SLOTS.ownLead : index === 1 ? SLOTS.ownSupport : null;
  return index === 0 ? SLOTS.foeLead : index === 1 ? SLOTS.foeSupport : null;
}

/** The charge arc, drawn over a mon's head. */
export const CHARGE_ARC = {
  /** Radius in field pixels. Wide and shallow, so the arc reads as a bar bent over the mon. */
  radius: 120,
  thickness: 14,
  /** How far above the sprite's top edge the arc's lowest point sits. */
  liftAboveHead: 30,
  /**
   * Degrees the arc spans, centred on straight up. Wide and shallow: at this radius the arc is
   * about 130px across with only ~19px of sag, which reads as a bar bent over the mon rather
   * than as a ring around it.
   */
  sweepDegrees: 66,
} as const;

/**
 * The numbers block that sits on the grass beside a mon: the attack burst, the HP figure, and the
 * HP bar under them.
 *
 * Sized here rather than in the stylesheet because the block is *positioned* from its own size —
 * the 'below' readout centres it on the sprite, and the 'right' readout lifts it so its bottom
 * lands near the mon's feet. With the size in CSS and the offsets in the component those two
 * drift apart the moment either changes, which is §6.7's "layout baked in two places" again.
 *
 * The bar is deliberately chunky. It is read from across a 1280-wide field while four of them
 * move at once, and at five pixels tall the difference between "hurt" and "nearly dead" was a
 * colour change nobody caught in peripheral vision.
 */
export const FIGURES = {
  /** The attack burst's box, which sets the height of the top row. */
  rowHeight: 46,
  gap: 4,
  bar: { width: 148, height: 14 },
} as const;

/** How far above the sprite's feet a 'right' readout is lifted, so the block clears the ground. */
export const figuresLift = (): number =>
  FIGURES.rowHeight + FIGURES.gap + FIGURES.bar.height + 8;

/** The shield bubble drawn around a mon. */
export const SHIELD_BUBBLE = {
  /** Padding beyond the sprite's own box. */
  pad: 14,
  strokeWidth: 3,
} as const;

/** Where the non-field chrome sits, also in field pixels. */
export const CHROME = {
  /** Trait counters: a type icon and the count, one cluster per side in the top corners. */
  ownTraits: { x: 20, y: 16 },
  foeTraits: { x: FIELD.width - 20, y: 16 },
  /** The foe's line-up, across the top centre. */
  foeRoster: { x: FIELD.width / 2, y: 14 },
  /** Playback controls, stacked down the right edge. */
  controlStack: { x: FIELD.width - 30, y: 196, gap: 12, size: 52 },
  caption: { x: 0, y: FIELD.height - 46, width: FIELD.width },
  stepBadge: { x: 20, y: FIELD.height - 40 },
} as const;

/** Ground line, for the horizon in the painted background. */
export const HORIZON_Y = 396;

/**
 * Nominal sprite box, used to place readouts and arcs before the real image loads.
 *
 * Gen 5 sprites run from about 23x39 to 153x94, so a fixed box would be wrong for every one of
 * them — but something has to be laid out on the first frame. The real dimensions replace this as
 * soon as the image reports them.
 */
export const NOMINAL_SPRITE: { width: number; height: number } = { width: 68, height: 62 };

/** Top-left corner of a sprite drawn at a slot, given its true pixel size. */
export function spriteBox(
  slot: Slot,
  natural: { width: number; height: number } = NOMINAL_SPRITE,
): { left: number; top: number; width: number; height: number; scale: number } {
  const scale = effectiveScale(slot, natural);
  const width = natural.width * scale;
  const height = natural.height * scale;
  return {
    // Horizontally centred on the anchor, and sitting *on* it vertically: feet-anchored, so mons
    // of different heights share a ground line instead of floating at a shared centre.
    left: slot.x - width / 2,
    top: slot.y - height,
    width,
    height,
    scale,
  };
}
