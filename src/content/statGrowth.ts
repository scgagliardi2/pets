/**
 * What a mon's stats are, given the species it started as, the stats the player has poured into
 * it, and the evolutions behind it.
 *
 * ## A point of EXP buys one stat, and the player picks which
 *
 * Earlier versions drew the stat automatically from a hash of the mon's id, weighted by the
 * species' real Health share. That is gone: a point now goes wherever the player sends it, which
 * turns EXP from a thing that happens to you into a decision about what a mon is for. The same
 * Caterpie can become a wall or a glass cannon.
 *
 * `healthGrowthPercent` survives on `Species` as a record of the old weighting and is no longer
 * read by anything here. It would be the natural default if an auto-allocate button ever appears.
 *
 * ## Stats stay derived, never stored
 *
 * A mon records only how many points went where. Its stat line is rebuilt from that on every
 * read, so nothing can drift out of sync and a mon's whole history is four small numbers.
 *
 * ## Growth is measured from the base form
 *
 * A mon that evolves keeps growing from the species it started as and gains a flat bonus on top;
 * the species it became contributes nothing. A Charmeleon that was caught and one that was raised
 * are therefore the same mon.
 */

import type { Stats } from '../sim/index.js';
import { BASE_SPEED, baseFormOf, baseStatsOf, type Species } from './index.js';

/** The four things a point of EXP can buy. */
export const GROWABLE_STATS = ['attack', 'health', 'special', 'speed'] as const;
export type GrowableStat = (typeof GROWABLE_STATS)[number];

/** How many points a mon has put into each stat. */
export type Allocation = Readonly<Record<GrowableStat, number>>;

export const emptyAllocation = (): Allocation => ({
  attack: 0,
  health: 0,
  special: 0,
  speed: 0,
});

export const totalAllocated = (a: Allocation): number =>
  GROWABLE_STATS.reduce((sum, k) => sum + Math.max(0, a[k]), 0);

export const allocate = (a: Allocation, stat: GrowableStat, points = 1): Allocation => ({
  ...a,
  [stat]: Math.max(0, a[stat] + points),
});

/**
 * What one point buys, per stat.
 *
 * Speed is the odd one out and deliberately the weakest per point. It is the only stat that
 * changes how *often* a mon acts rather than how hard: at the charge threshold of three, going
 * from Speed 1 to Speed 2 halves the wait for every ability the mon will ever fire. A point of
 * Attack is worth one point of Attack; a point of Speed can be worth doubling a mon's output. The
 * cap is what stops "always pick Speed" being the only line of play.
 */
export const GAIN_PER_POINT: Readonly<Record<GrowableStat, number>> = {
  attack: 1,
  health: 1,
  special: 1,
  speed: 1,
};

/**
 * Speed a mon may reach through growth.
 *
 * 100 is the calibration point of the charge scale — three ability activations per attack — so it
 * is the natural ceiling rather than an arbitrary one.
 */
export const MAX_GROWN_SPEED = 100;

/**
 * Every mon's Health, multiplied.
 *
 * Applied to the *derived* value rather than baked into `species.json`, so the generated roster
 * still validates against the Unity assets number for number and the tier budgets still hold.
 * Scaling the whole derived figure rather than only the base keeps the Attack-to-Health ratio
 * constant for a mon's whole life, which is what actually lengthens fights.
 */
export const HEALTH_MULTIPLIER = 3;

/** Attack a point of EXP is worth. */
export const ATTACK_PER_EXP = 1;
/** Health a point of EXP is worth, when the draw picks Health. */
export const HEALTH_PER_EXP = 1;
/** Attack and Health an evolution adds, flat, whatever it evolved into. */
export const ATTACK_PER_EVOLUTION = 3;
export const HEALTH_PER_EVOLUTION = 3;
/** EXP an evolution costs, against roughly four points a Location. */
export const EXP_PER_EVOLUTION = 12;

/**
 * A mon's stats. `baseForm` is the species it *started* as — the root of its chain, not what it
 * is now. Use `statsFor` when you have the current species and would rather not resolve that.
 */
export function statsFromAllocation(
  baseForm: Species,
  allocation: Allocation,
  timesEvolved: number,
): Stats {
  const evolutions = Math.max(0, timesEvolved);
  const base = baseStatsOf(baseForm);
  const put = (stat: GrowableStat): number => Math.max(0, allocation[stat]) * GAIN_PER_POINT[stat];

  const grownAttack = base.attack + put('attack') + ATTACK_PER_EVOLUTION * evolutions;
  const grownHealth = base.health + put('health') + HEALTH_PER_EVOLUTION * evolutions;

  // Special rises only when Special is chosen. An earlier version had it ride the Attack line so
  // an un-invested mon's ability kept pace — but that meant picking Attack silently raised two
  // stats, which makes the choice a lie. If a mon's ability is falling behind, the answer is to
  // spend a point on it.
  return {
    attack: grownAttack,
    health: grownHealth * HEALTH_MULTIPLIER,
    speed: Math.min(MAX_GROWN_SPEED, BASE_SPEED + put('speed')),
    special: Math.max(1, base.special + put('special')),
  };
}

/** Stats for a mon currently of `species`, resolving its base form for you. */
export function statsFor(
  species: Species,
  allocation: Allocation,
  timesEvolved: number,
): Stats {
  return statsFromAllocation(baseFormOf(species), allocation, timesEvolved);
}
