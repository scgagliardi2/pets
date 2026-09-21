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
import { baseFormOf, speciesOf } from '../content/index.js';
import type { GrowableStat } from '../content/statGrowth.js';
import { MAX_PARTY_SIZE, STARTING_MONEY, STARTING_MORALE } from './progression.js';
import { spendBall, STARTING_BALLS, type BallInventory, type BallTier } from './balls.js';

/**
 * `Wild` is a battle against wild Pokémon, which can be caught.
 *
 * `MysteryTrainer` is the hook for the asynchronous multiplayer that comes later: another
 * player's team, fought as an opponent. Their Pokémon are not catchable — they belong to someone
 * — so the reward is money instead.
 */
export type NodeType = 'Wild' | 'MysteryTrainer' | 'Gym' | 'Center' | 'Encounter';

/** One node on a Location's path. */
export interface MapNode {
  readonly id: string;
  readonly type: NodeType;
  /** How deep into the Location this node sits; 1 is the first. Feeds encounter scaling. */
  readonly layer: number;
  readonly label: string;
  /**
   * The two stats every participant gains by winning here, shown on the node before it is taken.
   *
   * Awarded directly rather than as points to assign: the choice is which *route* to walk, made
   * once on the map, instead of the same four buttons after every fight. Only battle nodes carry
   * it — a shop or an encounter has no participants.
   */
  readonly statRewards?: readonly GrowableStat[];
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
  /** Permanent trainer buffs picked on entering each region, oldest first. */
  readonly buffs: readonly string[];
  /** Items not currently held by anyone, as id -> count. */
  readonly bag: Readonly<Record<string, number>>;
  /** Regions already travelled, so the same one is never offered twice. */
  readonly regionsVisited: readonly string[];
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
  buffs: [],
  bag: {},
  regionsVisited: ['forest'],
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

export const addBuff = (run: RunState, buffId: string): RunState =>
  run.buffs.includes(buffId) ? run : { ...run, buffs: [...run.buffs, buffId] };

export const visitRegion = (run: RunState, region: string): RunState =>
  run.regionsVisited.includes(region)
    ? run
    : { ...run, regionsVisited: [...run.regionsVisited, region] };

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
/**
 * Every other owned Pokémon in the same evolution line — what a card's "combine" list draws from,
 * and what a drag-onto-a-card drop checks before acting.
 *
 * Line, not species: a Charmander and a Charmeleon are the same premise at different points, so
 * either can absorb the other. A Charmander and a Squirtle are not.
 */
export function duplicatesOf(run: RunState, instanceId: string): PokemonInstance[] {
  const mon = allMons(run).find((m) => m.instanceId === instanceId);
  if (mon === undefined) return [];
  const species = speciesOf(mon.speciesId);
  if (species === null) return [];
  const rootId = baseFormOf(species).id;

  return allMons(run).filter((m) => {
    if (m.instanceId === instanceId) return false;
    const other = speciesOf(m.speciesId);
    return other !== null && baseFormOf(other).id === rootId;
  });
}

/** Whether two owned Pokémon could be combined: different mons, same evolution line. */
export function canCombine(run: RunState, aId: string, bId: string): boolean {
  if (aId === bId) return false;
  return duplicatesOf(run, aId).some((m) => m.instanceId === bId);
}

/**
 * Swaps two owned Pokémon's positions.
 *
 * Works regardless of where either currently is — line-up or Box — and each lands in exactly the
 * slot the other vacated. That symmetry is what makes "drop one mon onto another" a single,
 * predictable action everywhere: dragging a Box mon onto a line-up card promotes and benches in
 * one move, dragging within the line-up reorders, and dragging within the Box just trades two
 * storage slots (which does nothing functionally, but is harmless and keeps the rule uniform
 * rather than special-cased per screen).
 */
export function swapMons(run: RunState, aId: string, bId: string): RunState {
  if (aId === bId) return run;

  const aLineUp = run.lineUp.findIndex((m) => m.instanceId === aId);
  const aBox = run.box.findIndex((m) => m.instanceId === aId);
  const bLineUp = run.lineUp.findIndex((m) => m.instanceId === bId);
  const bBox = run.box.findIndex((m) => m.instanceId === bId);

  if ((aLineUp < 0 && aBox < 0) || (bLineUp < 0 && bBox < 0)) return run;

  const aMon = aLineUp >= 0 ? run.lineUp[aLineUp]! : run.box[aBox]!;
  const bMon = bLineUp >= 0 ? run.lineUp[bLineUp]! : run.box[bBox]!;

  const lineUp = [...run.lineUp];
  const box = [...run.box];

  if (aLineUp >= 0 && bLineUp >= 0) {
    lineUp[aLineUp] = bMon;
    lineUp[bLineUp] = aMon;
  } else if (aBox >= 0 && bBox >= 0) {
    box[aBox] = bMon;
    box[bBox] = aMon;
  } else if (aLineUp >= 0 && bBox >= 0) {
    lineUp[aLineUp] = bMon;
    box[bBox] = aMon;
  } else {
    lineUp[bLineUp] = aMon;
    box[aBox] = bMon;
  }

  return { ...run, lineUp, box };
}

/**
 * Moves an item onto a mon, from the bag or from another mon.
 *
 * A mon holds one item, so equipping over an existing one returns the old item to the bag rather
 * than destroying it — an accidental drop should never cost the player an item.
 */
export function equipItem(run: RunState, instanceId: string, itemId: string): RunState {
  const target = allMons(run).find((m) => m.instanceId === instanceId);
  if (target === undefined) return run;
  if ((run.bag[itemId] ?? 0) <= 0) return run;
  if (target.heldItemId === itemId) return run;

  const bag = { ...run.bag, [itemId]: (run.bag[itemId] ?? 0) - 1 };
  // Whatever it was holding goes back on the shelf.
  if (target.heldItemId !== null) {
    bag[target.heldItemId] = (bag[target.heldItemId] ?? 0) + 1;
  }

  const apply = (mon: PokemonInstance): PokemonInstance =>
    mon.instanceId === instanceId ? { ...mon, heldItemId: itemId } : mon;

  return { ...run, bag, lineUp: run.lineUp.map(apply), box: run.box.map(apply) };
}

/** Takes a mon's item off and returns it to the bag. */
export function unequipItem(run: RunState, instanceId: string): RunState {
  const target = allMons(run).find((m) => m.instanceId === instanceId);
  if (target === undefined || target.heldItemId === null) return run;

  const bag = { ...run.bag, [target.heldItemId]: (run.bag[target.heldItemId] ?? 0) + 1 };
  const apply = (mon: PokemonInstance): PokemonInstance =>
    mon.instanceId === instanceId ? { ...mon, heldItemId: null } : mon;

  return { ...run, bag, lineUp: run.lineUp.map(apply), box: run.box.map(apply) };
}

/** Adds an item to the bag. */
export const addItem = (run: RunState, itemId: string, count = 1): RunState => ({
  ...run,
  bag: { ...run.bag, [itemId]: Math.max(0, (run.bag[itemId] ?? 0) + count) },
});

/** Every item id the bag currently holds at least one of. */
export const bagContents = (run: RunState): string[] =>
  Object.keys(run.bag).filter((id) => (run.bag[id] ?? 0) > 0);

export const healAll = (run: RunState): RunState => ({
  ...run,
  lineUp: run.lineUp.map((m) => ({ ...m, currentHP: null })),
  box: run.box.map((m) => ({ ...m, currentHP: null })),
});
