/**
 * The run: everything that persists between fights.
 *
 * Pure, like `/src/sim` — no React, no storage, no clock. Every operation takes a run and returns
 * a new one, so a run can be snapshotted, replayed and diffed, and the UI layer can hold it in
 * whatever store it likes.
 *
 * **Damage does not carry between fights.** A mon's HP is restored the moment a battle ends, and
 * a mon that fainted is back in the line-up for the next fight. Attrition is not the pressure
 * here: Morale is. That makes this Super Auto Pets' model rather than Slay the Spire's, and it
 * means a run is decided by what team you build, not by how much health you nursed through a
 * Location. `PokemonInstance.currentHP` therefore stays null outside a battle.
 */

import type { PokemonInstance } from '../content/factory.js';
import { combine, type Fusion } from './fusion.js';
import { MAX_PARTY_SIZE, STARTING_MONEY, STARTING_MORALE } from './progression.js';
import { spendBall, STARTING_BALLS, type BallInventory, type BallTier } from './balls.js';

export type NodeType = 'Wild' | 'Gym' | 'Center';

/** One node on a Location's path. */
export interface MapNode {
  readonly id: string;
  readonly type: NodeType;
  /** How deep into the Location this node sits; 1 is the first. Feeds encounter scaling. */
  readonly layer: number;
  readonly label: string;
}

export interface RunState {
  readonly seed: number;
  /** The team that fights, in order. Position 0 is the Lead, 1 the Support, the rest dormant. */
  readonly lineUp: readonly PokemonInstance[];
  /** Caught but not carried. Box mons earn no EXP — a mon that didn't fight doesn't grow. */
  readonly box: readonly PokemonInstance[];
  /** Lives. At zero the run is over. */
  readonly morale: number;
  readonly money: number;
  readonly badges: number;
  /** Node ids already resolved, in order. */
  readonly visited: readonly string[];
  /** Where the player is now, or null before the Location starts. */
  readonly currentNodeId: string | null;
  readonly balls: BallInventory;
}

export const createRun = (seed: number, starters: PokemonInstance[]): RunState => ({
  seed,
  lineUp: starters,
  box: [],
  morale: STARTING_MORALE,
  money: STARTING_MONEY,
  badges: 0,
  visited: [],
  currentNodeId: null,
  balls: STARTING_BALLS,
});

export const isRunOver = (run: RunState): boolean => run.morale <= 0;

export const useBall = (run: RunState, tier: BallTier): RunState => ({
  ...run,
  balls: spendBall(run.balls, tier),
});

export const isRunWon = (run: RunState, badgesToWin: number): boolean => run.badges >= badgesToWin;

/** Everything the player owns, line-up first. */
export const allMons = (run: RunState): PokemonInstance[] => [...run.lineUp, ...run.box];

// --- roster -------------------------------------------------------------------------------------

/** Moves a mon from the Box into the line-up. No-op if the line-up is full or it isn't there. */
export function promoteFromBox(run: RunState, instanceId: string): RunState {
  if (run.lineUp.length >= MAX_PARTY_SIZE) return run;
  const mon = run.box.find((m) => m.instanceId === instanceId);
  if (mon === undefined) return run;

  return {
    ...run,
    lineUp: [...run.lineUp, mon],
    box: run.box.filter((m) => m.instanceId !== instanceId),
  };
}

/** Moves a mon out of the line-up into the Box. Refuses to empty the line-up entirely. */
export function benchToBox(run: RunState, instanceId: string): RunState {
  if (run.lineUp.length <= 1) return run;
  const mon = run.lineUp.find((m) => m.instanceId === instanceId);
  if (mon === undefined) return run;

  return {
    ...run,
    lineUp: run.lineUp.filter((m) => m.instanceId !== instanceId),
    box: [...run.box, mon],
  };
}

/**
 * Moves a mon to a new index in the line-up.
 *
 * Order is the whole of formation: index 0 fights, index 1 is the Support that a passive can
 * target, and everything past that is dormant until someone in front of it falls.
 */
export function reorderLineUp(run: RunState, instanceId: string, toIndex: number): RunState {
  const from = run.lineUp.findIndex((m) => m.instanceId === instanceId);
  if (from < 0) return run;

  const next = [...run.lineUp];
  const [mon] = next.splice(from, 1);
  next.splice(Math.max(0, Math.min(next.length, toIndex)), 0, mon!);
  return { ...run, lineUp: next };
}

/** A mon the run owns, wherever it is kept. */
export const findMon = (run: RunState, instanceId: string): PokemonInstance | null =>
  allMons(run).find((m) => m.instanceId === instanceId) ?? null;

/**
 * Folds two mons of a family into one, wherever the two are kept.
 *
 * **The survivor lands in the line-up if either half was fighting**, in the earlier of their two
 * slots. Merging is meant to be a way to turn two bodies into one better one, not a way to
 * accidentally bench it — and the alternative rule, "it lands where the one you dropped onto
 * was", can empty a line-up of one by dragging its last mon into the Box.
 *
 * Returns null when the two can't merge, so a stale click or a hand-made id changes nothing.
 */
export function combineMons(
  run: RunState,
  aId: string,
  bId: string,
): { run: RunState; fusion: Fusion } | null {
  const a = findMon(run, aId);
  const b = findMon(run, bId);
  if (a === null || b === null) return null;

  const fusion = combine(a, b);
  if (fusion === null) return null;

  const indexIn = (list: readonly PokemonInstance[], id: string): number =>
    list.findIndex((m) => m.instanceId === id);
  const positions = (list: readonly PokemonInstance[]): number[] =>
    [indexIn(list, aId), indexIn(list, bId)].filter((i) => i >= 0);

  const lineUpSlots = positions(run.lineUp);
  const boxSlots = positions(run.box);
  const isOther = (m: PokemonInstance): boolean =>
    m.instanceId !== aId && m.instanceId !== bId;

  const lineUp = run.lineUp.filter(isOther);
  const box = run.box.filter(isOther);

  if (lineUpSlots.length > 0) {
    lineUp.splice(Math.min(Math.min(...lineUpSlots), lineUp.length), 0, fusion.mon);
  } else {
    box.splice(Math.min(Math.min(...boxSlots), box.length), 0, fusion.mon);
  }

  return { run: { ...run, lineUp, box }, fusion };
}

/** A caught mon joins the Box, or the line-up if there's room and the caller asks. */
export function addCaught(run: RunState, mon: PokemonInstance, toLineUp = false): RunState {
  if (toLineUp && run.lineUp.length < MAX_PARTY_SIZE) {
    return { ...run, lineUp: [...run.lineUp, mon] };
  }
  return { ...run, box: [...run.box, mon] };
}

// --- progress -----------------------------------------------------------------------------------

export const spendMorale = (run: RunState, amount = 1): RunState => ({
  ...run,
  morale: Math.max(0, run.morale - Math.max(0, amount)),
});

export const addMoney = (run: RunState, amount: number): RunState => ({
  ...run,
  money: Math.max(0, run.money + amount),
});

export const awardBadge = (run: RunState): RunState => ({ ...run, badges: run.badges + 1 });

export const enterNode = (run: RunState, nodeId: string): RunState => ({
  ...run,
  currentNodeId: nodeId,
});

/** Marks the current node resolved and steps off it. */
export function completeNode(run: RunState, nodeId: string): RunState {
  if (run.visited.includes(nodeId)) return { ...run, currentNodeId: null };
  return { ...run, visited: [...run.visited, nodeId], currentNodeId: null };
}

/**
 * Restores everything after a battle.
 *
 * This is where "damage resets" is actually enforced: HP goes back to null, which means derived
 * from the mon's Health stat, and a fainted mon is simply back. Nothing about a battle survives
 * it except EXP and anything caught.
 */
export const healAll = (run: RunState): RunState => ({
  ...run,
  lineUp: run.lineUp.map((m) => ({ ...m, currentHP: null })),
  box: run.box.map((m) => ({ ...m, currentHP: null })),
});
