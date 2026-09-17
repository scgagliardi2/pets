/**
 * Sprite resolution.
 *
 * The Unity project's `animated_sprites/` folder holds PokeAPI's Gen 5 (Black/White) animated
 * battle sprites — 185 front and 185 back GIFs, fetched by `tools/fetch_animated_sprites.py`.
 * Unity had no native animated-GIF support and only ever showed the first frame. A browser
 * animates them for free.
 *
 * Two sources, because they suit different stages:
 *
 * - **remote** (the default) points at PokeAPI's GitHub-hosted sprite files directly. Nothing to
 *   copy, nothing to build, works the moment you open a page. Good for getting the battle screen
 *   standing up.
 * - **local** points at `/sprites/...` under this project's `public/`. Copy the folder across from
 *   the Unity repo when you want offline work, deterministic loading, or control over caching.
 *
 * Front for the foe and everywhere outside battle; back for your own mons on the field.
 *
 * ## Sizing
 *
 * Sprites are drawn at **true relative scale** — Ralts is 23x39, Lugia 153x94, because Lugia is
 * about four times the size. Fitting each to a fixed box throws that away and draws a Caterpie as
 * large as a Rayquaza. See `spriteScale` below and REACT_REBUILD_REFERENCE.md §6.6.
 */

import type { Species } from './index.js';

export type SpriteFacing = 'front' | 'back';
export type SpriteSource = 'remote' | 'local';

/** PokeAPI's Gen 5 animated sprites, served from the sprite repo's raw GitHub host. */
const REMOTE_BASE =
  'https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/versions/generation-v/black-white/animated';

/** Where a copied `animated_sprites/` folder lives under `public/`. */
const LOCAL_BASE = '/sprites';

export interface SpriteOptions {
  facing?: SpriteFacing;
  source?: SpriteSource;
}

/**
 * The sprite URL for a species.
 *
 * Remote URLs are keyed by dex id, which is how PokeAPI stores them. Local ones are keyed by
 * lowercased name, which is how the Unity fetch script wrote them — so the two aren't
 * interchangeable paths, only interchangeable results.
 */
export function spriteUrl(species: Species, options: SpriteOptions = {}): string {
  const facing = options.facing ?? 'front';
  const source = options.source ?? 'remote';

  if (source === 'local') {
    return `${LOCAL_BASE}/${facing}/${species.name.toLowerCase()}.gif`;
  }
  return facing === 'back'
    ? `${REMOTE_BASE}/back/${species.id}.gif`
    : `${REMOTE_BASE}/${species.id}.gif`;
}

/**
 * Scale factors for the battlefield.
 *
 * Your side is drawn larger than the foe's; the size difference reads as depth. **Whole numbers
 * only** — a 1.5x scale renders some source pixels two screen-pixels wide and others three, which
 * looks like a wobble along every edge.
 */
export const BATTLEFIELD_SCALE = { own: 3, foe: 2 } as const;

/**
 * A shared scale factor for a fixed box (a roster card, a Pokédex grid).
 *
 * Whole numbers aren't available at small box sizes, so derive **one** factor from the largest
 * sprite in the set and share it across every species — proportions survive even though the
 * factor is fractional. Deriving a factor per sprite is the mistake this exists to prevent.
 */
export function sharedBoxScale(
  box: { width: number; height: number },
  largest: { width: number; height: number },
): number {
  return Math.min(box.width / largest.width, box.height / largest.height);
}

/**
 * CSS for pixel art. Sprites must be nearest-neighbour scaled, and anchored by their **feet** so
 * mons of different heights stand on the same ground line — not by their centre or a box corner.
 */
export const PIXEL_ART_STYLE = {
  imageRendering: 'pixelated',
  objectFit: 'contain',
  objectPosition: 'bottom',
} as const;
