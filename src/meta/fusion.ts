/**
 * Fusion: two mons of one family folded into a single stronger one, while editing the team.
 *
 * **What may combine is a family, not a species and not an elemental type.** Any two mons whose
 * chains share a base form qualify — a Charmander and a Charmeleon are the same mon at different
 * points of its life, so merging them is the one merge that needs no explanation. A shared
 * elemental type would let a Charmander eat a Growlithe, and "the highest evolution survives"
 * stops meaning anything when the two aren't on the same chain.
 *
 * **The survivor is the one furthest along the chain**, because that is the form the player has
 * already worked toward; the other is absorbed. What it leaves behind is a flat stat bonus and a
 * point of EXP:
 *
 * - Attack and Health become **the higher of the two, plus one**.
 * - The survivor gains **one EXP**, which can tip it over an evolution — so a fusion sometimes
 *   evolves a mon on the spot, and says so.
 *
 * The bonus is stored on the mon (`bonusAttack` / `bonusHealth`) rather than derived, which makes
 * this the single exception to the rule that a stat line is rebuilt from scratch. It has to be:
 * the mon it came from no longer exists, so there is nothing left to derive it from. Everything
 * else still works the way it did — EXP earned after a fusion keeps growing the survivor
 * normally, and the bonus is simply added last.
 *
 * Pure, like the rest of `/src/meta`: a fusion is computed here and committed by the caller, so
 * the same function serves the preview the player hovers and the move they commit to.
 */

import { baseFormOf, type Species } from '../content/index.js';
import {
  derivedStatsOf,
  speciesOfInstance,
  statsOf,
  type PokemonInstance,
} from '../content/factory.js';
import { raiseToExp, type Evolution } from './experience.js';

/** Attack and Health a fusion adds on top of the better of the two mons. */
export const FUSION_STAT_BONUS = 1;
/** EXP a fusion is worth to the survivor. */
export const FUSION_EXP_BONUS = 1;

/** The base form a mon descends from — its family, and the only thing fusion matches on. */
export const familyOf = (mon: PokemonInstance): Species => baseFormOf(speciesOfInstance(mon));

/** Whether these two mons may be folded together: same family, and not the same mon twice. */
export function canCombine(a: PokemonInstance, b: PokemonInstance): boolean {
  if (a.instanceId === b.instanceId) return false;
  return familyOf(a).id === familyOf(b).id;
}

/** Everything in `pool` that `mon` could be combined with. */
export function partnersFor(
  mon: PokemonInstance,
  pool: readonly PokemonInstance[],
): PokemonInstance[] {
  return pool.filter((other) => canCombine(mon, other));
}

/** How far along its chain a mon is now — a caught Charmeleon counts as evolved once. */
const stageOf = (mon: PokemonInstance): number =>
  Math.max(speciesOfInstance(mon).evolutionStage, mon.timesEvolved);

/**
 * Which of the two survives.
 *
 * Stage first, since that is what "highest evolution" means, then the longer history, then the
 * bigger mon. `a` wins an outright tie, so a caller passing the mon the player aimed at first
 * gets the answer the player expects.
 */
function keeperOf(a: PokemonInstance, b: PokemonInstance): [PokemonInstance, PokemonInstance] {
  const rank = (mon: PokemonInstance): number[] => {
    const stats = statsOf(mon);
    return [stageOf(mon), mon.exp, stats.attack + stats.health];
  };
  const [ra, rb] = [rank(a), rank(b)];
  for (let i = 0; i < ra.length; i++) {
    if (ra[i]! !== rb[i]!) return ra[i]! > rb[i]! ? [a, b] : [b, a];
  }
  return [a, b];
}

export interface Fusion {
  /** The mon that comes out, carrying the survivor's instance id and stat history. */
  readonly mon: PokemonInstance;
  /** The instance id that is consumed and must be removed from the roster. */
  readonly absorbedId: string;
  /** Any evolution the fusion's point of EXP happened to trigger. */
  readonly evolutions: Evolution[];
}

/**
 * Folds `b` into `a`, or returns null if they aren't of a family.
 *
 * Nothing is removed here — the caller owns the roster, and gets told which id to drop.
 */
export function combine(a: PokemonInstance, b: PokemonInstance): Fusion | null {
  if (!canCombine(a, b)) return null;

  const [keeper, absorbed] = keeperOf(a, b);
  const best = {
    attack: Math.max(statsOf(keeper).attack, statsOf(absorbed).attack) + FUSION_STAT_BONUS,
    health: Math.max(statsOf(keeper).health, statsOf(absorbed).health) + FUSION_STAT_BONUS,
  };

  // The EXP lands before the bonus is worked out, so a fusion that evolves the survivor keeps the
  // evolution's own +3/+3 rather than having it quietly absorbed into a smaller bonus.
  const raised = raiseToExp(keeper, Math.max(keeper.exp, absorbed.exp) + FUSION_EXP_BONUS);
  const derived = derivedStatsOf(raised.mon);

  return {
    mon: {
      ...raised.mon,
      // Never negative: an evolution can carry the mon past the target on its own, and when it
      // does the mon keeps the better line rather than being trimmed back to the promise.
      bonusAttack: Math.max(0, best.attack - derived.attack),
      bonusHealth: Math.max(0, best.health - derived.health),
      timesFused: (keeper.timesFused ?? 0) + (absorbed.timesFused ?? 0) + 1,
      currentHP: null,
    },
    absorbedId: absorbed.instanceId,
    evolutions: raised.evolutions,
  };
}
