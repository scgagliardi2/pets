/**
 * Catching: the odds, the roll, and what a successful throw produces.
 *
 * The odds are pure arithmetic over a ball tier and the target's current condition — no RNG, no
 * battle state, no React — so they can be tested directly and, separately, shown to the player
 * before they commit a ball. Committing a ball to odds you can't see would make the whole
 * weaken-then-throw loop guesswork.
 */

import { createInstance, type PokemonInstance } from '../content/factory.js';
import { speciesOf } from '../content/index.js';
import type { Combatant, Rng, StatusType } from '../sim/index.js';
import { baseChance, expCap, type BallTier } from './balls.js';

/** Added at 0% HP, scaled linearly to nothing at full health. */
export const WEAKENED_BONUS = 0.45;

/**
 * Added flat when the target has any status.
 *
 * Flat and equal across all four on purpose for a first pass. Making sleep better than poison is
 * a balance decision that wants playtesting, not a guess baked into the formula now.
 */
export const STATUS_BONUS = 0.15;

/** No throw is ever certain, however weakened the target — "will it break free" needs a maybe. */
export const MAX_CHANCE = 0.95;

/**
 * Odds as a fraction in [0, MAX_CHANCE].
 *
 * Current HP is the big lever: a target at zero adds the whole weakened bonus, scaling down to
 * nothing at full health. That is what makes "weaken it first" the loop rather than a suggestion.
 */
export function chanceFor(
  tier: BallTier,
  currentHP: number,
  maxHP: number,
  status: StatusType | null,
): number {
  const hpFraction = maxHP > 0 ? Math.max(0, Math.min(1, currentHP / maxHP)) : 0;
  const chance =
    baseChance(tier) + WEAKENED_BONUS * (1 - hpFraction) + (status !== null ? STATUS_BONUS : 0);
  return Math.max(0, Math.min(MAX_CHANCE, chance));
}

/**
 * Odds against a live combatant.
 *
 * Max HP comes from the combatant's own stat line rather than its run-level record, so a
 * battle-only Health buff counts against the catch exactly as it counts in the fight.
 */
export const chanceAgainst = (tier: BallTier, target: Combatant | null): number =>
  target === null ? 0 : chanceFor(tier, target.currentHP, target.currentStats.health, target.status);

/** Whole percent, for a tray. Rounded rather than truncated, so 0.649 reads as 65%. */
export const percentAgainst = (tier: BallTier, target: Combatant | null): number =>
  Math.round(chanceAgainst(tier, target) * 100);

/**
 * Rolls a throw.
 *
 * Takes the battle's own RNG so a fight stays reproducible end to end from its seed — a catch is
 * part of the fight's history, not a side channel with its own entropy. Resolution is one draw in
 * 10,000, finer than any tuning these odds will plausibly get.
 */
export function rollCatch(tier: BallTier, target: Combatant, rng: Rng): boolean {
  const RESOLUTION = 10_000;
  const threshold = Math.round(chanceAgainst(tier, target) * RESOLUTION);
  return rng.nextInt(RESOLUTION) < threshold;
}

/**
 * The run-level mon a successful catch produces.
 *
 * A fresh, full-health instance carrying the lower of the target's own EXP and the ball's cap,
 * but never below the run's catch-up floor — a catch is meant to be usable straight away rather
 * than a project.
 *
 * A new instance id, deliberately: the stat-growth draw is keyed to the id, so reusing the wild
 * mon's would mean a caught mon grew along exactly the line the enemy would have. It should be
 * its own mon from here.
 */
export function caughtInstance(
  speciesId: number,
  tier: BallTier,
  wildExp: number,
  catchUpFloorExp: number,
  instanceId: string,
): PokemonInstance | null {
  const species = speciesOf(speciesId);
  if (species === null) return null;

  // The ball's cap limits what the mon keeps of its own growth — a Poke Ball can still land a
  // strong target, it just yields an under-grown one. The run's catch-up floor then applies, so a
  // catch is usable straight away rather than a project.
  const cap = expCap(tier);
  const kept = cap === null ? Math.max(0, wildExp) : Math.min(Math.max(0, wildExp), cap);

  return createInstance(species, {
    instanceId,
    exp: Math.max(kept, Math.max(0, catchUpFloorExp)),
    currentHP: null,
  });
}

/** What a throw did, for the screen to narrate. */
export type ThrowOutcome = 'NotThrown' | 'Caught' | 'BrokeFree';

export interface ThrowResult {
  outcome: ThrowOutcome;
  tier: BallTier;
  /** The instance id of the mon thrown at, when there was a valid target. */
  targetInstanceId: string | null;
  /** The mon added to the run, on a catch. */
  caught: PokemonInstance | null;
  /** Odds the throw was made at, so the log can say what the gamble was. */
  chance: number;
}

export const notThrown = (tier: BallTier): ThrowResult => ({
  outcome: 'NotThrown',
  tier,
  targetInstanceId: null,
  caught: null,
  chance: 0,
});
