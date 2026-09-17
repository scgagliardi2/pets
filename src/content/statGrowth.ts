/**
 * What a mon's stats are, given the species it started as, the EXP it has earned and the
 * evolutions behind it. Ported from Unity's `StatGrowth.cs` (ADR 0008, retuned by ADR 0009).
 *
 * ## The rule that matters
 *
 * **A point of EXP buys +1 Attack *or* +1 Health, never both.**
 *
 * This is load-bearing, not a detail. The first version added +1 to each, so a mon's Attack and
 * Health stayed locked together forever and two even mons always killed each other on the first
 * Step — for the whole run, whatever their EXP, leaving nothing for a passive or a Speed
 * advantage to decide. Splitting the point is what makes fights lengthen as a run progresses: a
 * Metapod banks 86% of its points into Health and pulls away from its own Attack.
 *
 * Note that `docs/battle-sim-spec.md` §12 in the Unity repo still describes the old lockstep
 * behaviour and cites ADR 0008. ADR 0009 supersedes it; this follows 0009.
 *
 * ## The draw is deterministic
 *
 * Not a dice roll. Stats are rebuilt from scratch on every EXP grant, so nothing about a mon's
 * history can be stored — the draw is a pure hash of the mon's instance id and which point of EXP
 * it is. It behaves like luck (a 70% mon really can take Attack three times running, and two
 * Charmanders in the same party grow into different stat lines) while a mon's whole stat line
 * stays rebuildable from its EXP count alone, in any order, forever.
 *
 * That is why this uses its own hash rather than the sim's PRNG: a stream answers "what is the
 * next draw", and this needs to answer "what was the draw for point 7" without replaying.
 *
 * ## Growth is measured from the base form
 *
 * A mon that evolves keeps growing from the species it started as and gains a flat bonus on top;
 * the species it became contributes nothing. A Charmeleon that was caught and one that was raised
 * are therefore the same mon. Speed never changes at all — not with EXP, and, since evolution
 * ignores the new species' stats, not with evolution either.
 */

import type { Stats } from '../sim/index.js';
import { baseFormOf, baseStatsOf, type Species } from './index.js';

/** Attack a point of EXP is worth, when the draw picks Attack. */
export const ATTACK_PER_EXP = 1;
/** Health a point of EXP is worth, when the draw picks Health. */
export const HEALTH_PER_EXP = 1;
/** Attack and Health an evolution adds, flat, whatever it evolved into. */
export const ATTACK_PER_EVOLUTION = 3;
export const HEALTH_PER_EVOLUTION = 3;
/** EXP an evolution costs, against roughly four points a Location. */
export const EXP_PER_EVOLUTION = 12;

/**
 * A number in 0..99 from the mon and which point this is.
 *
 * FNV-1a with a final avalanche. The avalanche matters: the plain hash leaves adjacent point
 * indices correlated, and a mon's growth would come out in visible runs of one stat.
 */
function draw(instanceId: string, pointIndex: number): number {
  let hash = 2166136261 >>> 0;
  for (let i = 0; i < instanceId.length; i++) {
    hash = Math.imul(hash ^ instanceId.charCodeAt(i), 16777619) >>> 0;
  }
  hash = Math.imul(hash ^ pointIndex, 16777619) >>> 0;
  hash ^= hash >>> 15;
  hash = Math.imul(hash, 2246822519) >>> 0;
  hash ^= hash >>> 13;
  // The `>>> 0` is load-bearing, not tidiness. C# does this arithmetic on a uint; JavaScript's
  // `^=` yields a *signed* 32-bit result, so without the coercion `hash` can be negative here and
  // a negative modulo is less than any growth percentage — every draw would pick Health. That is
  // exactly what happened before this line existed: Shedinja gained Health at 0%, and draws came
  // out in runs of 29.
  return (hash >>> 0) % 100;
}

/** Whether a mon's `pointIndex`-th point of EXP (0-based) goes to Health rather than Attack. */
export function gainsHealth(
  healthGrowthPercent: number,
  instanceId: string,
  pointIndex: number,
): boolean {
  return draw(instanceId, pointIndex) < healthGrowthPercent;
}

/** How many of a mon's first `points` EXP went into Health. */
export function healthGainsIn(
  healthGrowthPercent: number,
  instanceId: string,
  points: number,
): number {
  let health = 0;
  for (let i = 0; i < points; i++) {
    if (gainsHealth(healthGrowthPercent, instanceId, i)) health++;
  }
  return health;
}

/**
 * A mon's stats. `baseForm` is the species it *started* as — the root of its chain, not what it
 * is now. Use `statsFor` when you have the current species and would rather not resolve that
 * yourself.
 */
export function statsAtExp(
  baseForm: Species,
  instanceId: string,
  exp: number,
  timesEvolved: number,
): Stats {
  const points = Math.max(0, exp);
  const evolutions = Math.max(0, timesEvolved);
  const base = baseStatsOf(baseForm);

  const health = healthGainsIn(baseForm.healthGrowthPercent, instanceId, points);
  const attack = points - health;

  return {
    attack: base.attack + ATTACK_PER_EXP * attack + ATTACK_PER_EVOLUTION * evolutions,
    health: base.health + HEALTH_PER_EXP * health + HEALTH_PER_EVOLUTION * evolutions,
    speed: base.speed,
  };
}

/** Stats for a mon currently of `species`, resolving its base form for you. */
export function statsFor(
  species: Species,
  instanceId: string,
  exp: number,
  timesEvolved: number,
): Stats {
  return statsAtExp(baseFormOf(species), instanceId, exp, timesEvolved);
}
