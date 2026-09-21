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

import { baseFormOf, evolutionOf, speciesOf, type Species } from '../content/index.js';
import { EXP_PER_EVOLUTION } from '../content/statGrowth.js';
import type { PokemonInstance } from '../content/factory.js';
import { speciesOfInstance, unspentPoints } from '../content/factory.js';
import { itemById } from '../content/items.js';
import { allocate, type GrowableStat } from '../content/statGrowth.js';
import { allMons, type RunState } from './runState.js';

export { EXP_PER_EVOLUTION };

/** How far behind the run's most-experienced mon another may fall — about two fights. */
export const CATCH_UP_EXP_GAP = 2;

/**
 * Stat points a mon receives each time it evolves.
 *
 * Evolution is now the *only* routine source of points the player assigns by hand. Battles award
 * their two stats directly, so the assignment screen appears at a milestone rather than after
 * every fight — a decision worth stopping for, instead of constant upkeep.
 */
export const STAT_POINTS_PER_EVOLUTION = 10;

/**
 * EXP one combine is worth, toward evolving.
 *
 * Four, so three sacrifices evolve a base-form mon: four Charmanders combine into one Charmeleon.
 * A flat amount rather than the sacrifice's own progress, so a pile of low-tier catches can't be
 * cashed in as a shortcut past the tier curve.
 */
export const EXP_PER_COMBINE = 4;

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
 * The points arrive *unspent*: `exp` rises but `allocation` does not, so the mon carries a
 * pending choice until the player makes it. Evolution still keys off lifetime EXP, so a mon
 * evolves on schedule whether or not its owner has got round to spending anything.
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

    next = {
      ...next,
      speciesId: into.id,
      timesEvolved: next.timesEvolved + 1,
      // Evolving is what hands the player stat points to assign.
      statPoints: next.statPoints + STAT_POINTS_PER_EVOLUTION,
    };
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
export function grantWinExp(
  run: RunState,
  points: number,
  /**
   * Who fought. Omitted means the whole line-up — which is right for an encounter that trains
   * everyone, and wrong for a battle, where a mon that never left the back of the train should
   * not be paid for it.
   */
  participants?: readonly string[],
): { run: RunState; report: GrowthReport } {
  if (points <= 0 && run.lineUp.length === 0) return { run, report: emptyReport() };

  const fought = participants === undefined ? null : new Set(participants);
  const gained: Record<string, number> = {};
  const evolutions: Evolution[] = [];

  let lineUp = run.lineUp.map((mon) => {
    if (fought !== null && !fought.has(mon.instanceId)) return mon;
    const result = raiseToExp(mon, mon.exp + Math.max(0, points) + itemExpBonus(mon));
    if (result.gained > 0) gained[mon.instanceId] = result.gained;
    evolutions.push(...result.evolutions);
    return result.mon;
  });

  const floor = catchUpFloor({ ...run, lineUp });
  lineUp = lineUp.map((mon) => {
    // Gated by the same list as the grant. Catching up a mon that never left the back of the
    // train is the same mistake as catching up the Box: it makes sitting a fight out free, and
    // the line-up order stops being a decision.
    if (fought !== null && !fought.has(mon.instanceId)) return mon;
    const result = raiseToExp(mon, Math.max(mon.exp, floor));
    if (result.gained > 0) {
      gained[mon.instanceId] = (gained[mon.instanceId] ?? 0) + result.gained;
    }
    evolutions.push(...result.evolutions);
    return result.mon;
  });

  return { run: { ...run, lineUp }, report: { gained, evolutions } };
}

/** Spends one pending stat point on a stat. No-op if the mon has nothing pending. */
export function spendPoint(
  run: RunState,
  instanceId: string,
  stat: GrowableStat,
): RunState {
  const apply = (mon: PokemonInstance): PokemonInstance =>
    mon.instanceId === instanceId && unspentPoints(mon) > 0
      ? {
          ...mon,
          // Both sides move: the point leaves the pool and lands in the allocation. When unspent
          // was derived as `exp - totalAllocated` the decrement was implicit; now that stat
          // points are their own counter it has to be explicit, or spending is free.
          statPoints: mon.statPoints - 1,
          allocation: allocate(mon.allocation, stat),
        }
      : mon;

  return { ...run, lineUp: run.lineUp.map(apply), box: run.box.map(apply) };
}

/** Every mon with points waiting to be spent, line-up first. */
export const monsAwaitingChoice = (run: RunState): PokemonInstance[] =>
  [...run.lineUp, ...run.box].filter((m) => unspentPoints(m) > 0);

/**
 * Applies a battle node's stat rewards directly to everyone who fought.
 *
 * Straight into `allocation`, not into `statPoints`: the node already told the player which two
 * stats it pays, so making them click the same two buttons afterwards would be upkeep without a
 * decision. The decision was choosing the route.
 */
export function applyStatRewards(
  run: RunState,
  stats: readonly GrowableStat[],
  participants: readonly string[],
): RunState {
  if (stats.length === 0 || participants.length === 0) return run;
  const fought = new Set(participants);

  const apply = (mon: PokemonInstance): PokemonInstance => {
    if (!fought.has(mon.instanceId)) return mon;
    let allocation = mon.allocation;
    for (const stat of stats) allocation = allocate(allocation, stat);
    return { ...mon, allocation };
  };

  return { ...run, lineUp: run.lineUp.map(apply), box: run.box.map(apply) };
}

/**
 * Extra EXP a mon earns from its held item, on top of the win's own.
 *
 * Per-mon rather than run-wide, unlike the Scholar buff: the item is held by one Pokémon and only
 * that Pokémon benefits, which is the whole reason to choose who carries it.
 */
export function itemExpBonus(mon: PokemonInstance): number {
  const item = itemById(mon.heldItemId);
  return item !== null && item.kind === 'exp' ? (item.amount ?? 0) : 0;
}

/**
 * Applies Power-item training to everyone who fought.
 *
 * Straight into the allocation, like a node's stat rewards — the item is the decision, and making
 * the player confirm it afterwards would be upkeep. Only participants, for the same reason EXP is
 * only for participants: an item cannot train a mon that never left the back of the train.
 */
export function applyItemTraining(
  run: RunState,
  participants: readonly string[],
): RunState {
  const fought = new Set(participants);

  const apply = (mon: PokemonInstance): PokemonInstance => {
    if (!fought.has(mon.instanceId)) return mon;
    const item = itemById(mon.heldItemId);
    if (item === null || item.kind !== 'training' || item.stat === undefined) return mon;
    return { ...mon, allocation: allocate(mon.allocation, item.stat, item.amount ?? 1) };
  };

  return { ...run, lineUp: run.lineUp.map(apply), box: run.box.map(apply) };
}

/** What a mon is now, for a results screen that wants to name it. */
export const nameOf = (mon: PokemonInstance): string =>
  mon.nickname ?? speciesOfInstance(mon).name;

/**
 * Merges two Pokémon in the same evolution line into one.
 *
 * This is a rare-candy, not a pooling of two histories: `consumeId` contributes a flat
 * `EXP_PER_COMBINE` toward `keepId`'s evolution — a third of the way there — rather than its own
 * progress. Four base-form mons therefore combine into one evolved mon. Summing both totals
 * instead would let a player grind a pile of low-tier catches and cash them in as a shortcut past
 * the tier curve; a flat lump keeps combining worth the same regardless of what was sacrificed.
 *
 * Routed through `raiseToExp` rather than a bare `exp +=`, so a combine that crosses a threshold
 * evolves the kept mon exactly as a battle win would — no separate evolution path to keep in
 * sync with this one.
 *
 * Refuses two different evolution lines: a Charmander and a Squirtle are not the same premise at
 * different points, and there is no single species for the result to become. `consumeId` is
 * removed from wherever it was, line-up or Box; `keepId` stays exactly where it was.
 */
export function combineMons(run: RunState, keepId: string, consumeId: string): RunState {
  if (keepId === consumeId) return run;

  const all = allMons(run);
  const keep = all.find((m) => m.instanceId === keepId);
  const consume = all.find((m) => m.instanceId === consumeId);
  if (keep === undefined || consume === undefined) return run;

  const keepSpecies = speciesOf(keep.speciesId);
  const consumeSpecies = speciesOf(consume.speciesId);
  if (keepSpecies === null || consumeSpecies === null) return run;
  if (baseFormOf(keepSpecies).id !== baseFormOf(consumeSpecies).id) return run;

  const { mon: grown } = raiseToExp(keep, keep.exp + EXP_PER_COMBINE);

  const apply = (list: readonly PokemonInstance[]): PokemonInstance[] =>
    list.filter((m) => m.instanceId !== consumeId).map((m) => (m.instanceId === keepId ? grown : m));

  return { ...run, lineUp: apply(run.lineUp), box: apply(run.box) };
}
