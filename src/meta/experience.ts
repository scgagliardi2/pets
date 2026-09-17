/**
 * EXP, evolution and catch-up — the one place a mon's stats change outside a battle.
 *
 * Ported from Unity's `ExperienceResolver.cs` (ADR 0008, retuned by ADR 0009).
 *
 * **EXP is a plain count of wins**, and each point is +1 Attack *or* +1 Health. There is no
 * level, no curve and no rising cost: a species' tier says how strong it starts, and EXP says how
 * far it has come since.
 *
 * **EXP is lifetime and never resets.** The nth evolution lands the moment it reaches
 * `n * EXP_PER_EVOLUTION`. One running total means a mon's whole history is a single number, and
 * stats are rebuilt from it every time rather than accumulated.
 *
 * **Nobody in the line-up falls hopelessly behind.** Catch-up keeps every fighting mon within a
 * couple of points of the run's most-experienced, so a mon caught late and put straight to work
 * is still worth using. It levels experience, not power: a caught Caterpie gets the same EXP as
 * the Charizard beside it and is still a Caterpie, so tiers keep meaning something.
 *
 * **The Box is not caught up, because a mon that didn't fight doesn't grow.** Catch-up used to
 * reach storage too, which made benching free and the party choice meaningless. A Box mon keeps
 * what it had; the moment it joins the line-up, the next grant brings it back within the gap, so
 * nothing is ever ruined by being left there.
 */

import { evolutionOf, speciesOf, type Species } from '../content/index.js';
import { EXP_PER_EVOLUTION } from '../content/statGrowth.js';
import type { PokemonInstance } from '../content/factory.js';
import { speciesOfInstance } from '../content/factory.js';
import type { RunState } from './runState.js';

export { EXP_PER_EVOLUTION };

/** How far behind the run's most-experienced mon another may fall — about two fights. */
export const CATCH_UP_EXP_GAP = 2;

/** A bound on the evolution loop, so a malformed chain that cycles can't hang the game. */
const MAX_EVOLUTIONS_PER_GRANT = 8;

/** One evolution that just happened, so a caller can tell the player about it. */
export interface Evolution {
  readonly instanceId: string;
  readonly from: Species;
  readonly to: Species;
}

export interface GrowthReport {
  /** instanceId -> points gained in this grant. */
  readonly gained: Record<string, number>;
  readonly evolutions: Evolution[];
}

const emptyReport = (): GrowthReport => ({ gained: {}, evolutions: [] });

/** EXP this mon has earned since it last evolved. A progress readout, not a stat input. */
export const expSinceEvolution = (mon: PokemonInstance): number =>
  Math.max(0, mon.exp - EXP_PER_EVOLUTION * Math.max(0, mon.timesEvolved));

/** The lifetime EXP at which this mon's next evolution lands. */
export const expForNextEvolution = (mon: PokemonInstance): number =>
  EXP_PER_EVOLUTION * (Math.max(0, mon.timesEvolved) + 1);

/**
 * Raises one mon to a lifetime EXP total, evolving it as many times as that crosses.
 *
 * A mon at the end of its chain still banks the EXP — the points keep buying Attack or Health —
 * it just has nothing left to become, so `timesEvolved` stops rising and it stops collecting the
 * flat evolution bonus.
 */
export function raiseToExp(
  mon: PokemonInstance,
  targetExp: number,
): { mon: PokemonInstance; gained: number; evolutions: Evolution[] } {
  if (targetExp <= mon.exp) return { mon, gained: 0, evolutions: [] };

  const gained = targetExp - mon.exp;
  let next: PokemonInstance = { ...mon, exp: targetExp };
  const evolutions: Evolution[] = [];

  for (let guard = 0; guard < MAX_EVOLUTIONS_PER_GRANT; guard++) {
    if (next.exp < expForNextEvolution(next)) break;

    const current = speciesOf(next.speciesId);
    if (current === null) break;
    const into = evolutionOf(current);
    if (into === null) break;

    next = { ...next, speciesId: into.id, timesEvolved: next.timesEvolved + 1 };
    evolutions.push({ instanceId: next.instanceId, from: current, to: into });
  }

  return { mon: next, gained, evolutions };
}

/** The EXP floor catch-up raises the line-up to. */
export function catchUpFloor(run: RunState): number {
  let highest = 0;
  for (const mon of [...run.lineUp, ...run.box]) highest = Math.max(highest, mon.exp);
  return Math.max(0, highest - CATCH_UP_EXP_GAP);
}

/**
 * Grants EXP to every mon in the line-up, then applies the catch-up floor.
 *
 * Order matters: the grant happens first, so the mon that earned it is ahead, and catch-up then
 * pulls the stragglers toward the new high-water mark rather than the old one.
 */
export function grantWinExp(run: RunState, points: number): { run: RunState; report: GrowthReport } {
  if (points <= 0 && run.lineUp.length === 0) return { run, report: emptyReport() };

  const gained: Record<string, number> = {};
  const evolutions: Evolution[] = [];

  let lineUp = run.lineUp.map((mon) => {
    const result = raiseToExp(mon, mon.exp + Math.max(0, points));
    if (result.gained > 0) gained[mon.instanceId] = result.gained;
    evolutions.push(...result.evolutions);
    return result.mon;
  });

  const floor = catchUpFloor({ ...run, lineUp });
  lineUp = lineUp.map((mon) => {
    const result = raiseToExp(mon, Math.max(mon.exp, floor));
    if (result.gained > 0) {
      gained[mon.instanceId] = (gained[mon.instanceId] ?? 0) + result.gained;
    }
    evolutions.push(...result.evolutions);
    return result.mon;
  });

  return { run: { ...run, lineUp }, report: { gained, evolutions } };
}

/** What a mon is now, for a results screen that wants to name it. */
export const nameOf = (mon: PokemonInstance): string =>
  mon.nickname ?? speciesOfInstance(mon).name;
