/**
 * The run store: the one mutable thing in the app.
 *
 * Everything it does is delegated to the pure functions in `/src/meta` — this layer only decides
 * *when* they run and holds the result. Keeping the rules pure is what lets them be tested
 * without a store, a component or a clock.
 */

import { create } from 'zustand';

import type { PokemonInstance } from '../content/factory.js';
import { catchUpFloor, grantWinExp, mergeReports, type GrowthReport } from '../meta/experience.js';
import { hasBall } from '../meta/balls.js';
import { caughtInstance, type ThrowResult } from '../meta/catching.js';
import { defaultStarters, opponentsFor } from '../meta/encounters.js';
import { generateLocationMap, reachableFrom, type LocationMap } from '../meta/mapGenerator.js';
import { locationFor, type Location } from '../meta/locations.js';
import {
  eventSeedFor,
  grantBounty,
  resolveRoadEvent,
  rollRoadEvent,
  type Bounty,
  type RoadEvent,
} from '../meta/roadEvents.js';
import { buy, type ShopItem } from '../meta/shop.js';
import {
  BADGES_TO_WIN,
  EXP_PER_WIN,
  moneyForWin,
} from '../meta/progression.js';
import {
  addCaught,
  addMoney,
  awardBadge,
  benchToBox,
  combineMons,
  completeNode,
  createRun,
  enterNode,
  healAll,
  isRunOver,
  isRunWon,
  leaveNode,
  placeInSlot,
  promoteFromBox,
  reorderLineUp,
  spendMorale,
  useBall,
  type MapNode,
  type RunState,
} from '../meta/runState.js';
import type { Fusion } from '../meta/fusion.js';
import type { BattleOutcome } from '../sim/index.js';

/** Where the player is in the loop. */
export type Phase = 'map' | 'battle' | 'result' | 'shop' | 'encounter' | 'box' | 'over';

/** An Encounter after a choice has been taken: what it said, and what it grew. */
export interface EncounterResult {
  readonly message: string;
  readonly report: GrowthReport;
}

/** What a fight did, for the results screen. */
export interface BattleResult {
  outcome: BattleOutcome;
  nodeId: string;
  isGym: boolean;
  expEach: number;
  money: number;
  badge: boolean;
  moraleLost: number;
  report: GrowthReport;
  /** Paid on top of the ordinary win, when the fight came out of an Encounter. */
  bounty: Bounty | null;
}

interface RunStore {
  run: RunState;
  phase: Phase;
  map: LocationMap;
  location: Location;
  /** What the player may take next, given where they are. */
  available: string[];
  /** The node being fought, while phase is 'battle' or 'result'. */
  activeNode: MapNode | null;
  opponents: PokemonInstance[];
  lastResult: BattleResult | null;
  /** The Encounter being played, while phase is 'encounter'. */
  event: RoadEvent | null;
  /** What the chosen branch did, once one has been chosen. Null while the choice is still open. */
  eventResult: EncounterResult | null;
  /** What winning the fight an Encounter started will pay. Cleared once paid. */
  pendingBounty: Bounty | null;
  /** Throws this fight, newest first — what the battle screen narrates. */
  throwLog: ThrowResult[];
  /** The most recent fusion, so the team screens can say what came out of it. */
  lastFusion: Fusion | null;

  startRun: (seed?: number) => void;
  enter: (nodeId: string) => void;
  /** Takes an Encounter's branch. A no-op once one has been taken, so a double click can't take two. */
  chooseEncounter: (index: number) => void;
  /** Leaves a resolved Encounter, marking its node taken. */
  leaveEncounter: () => void;
  purchase: (item: ShopItem) => void;
  leaveShop: () => void;
  openBox: () => void;
  closeBox: () => void;
  /** Drops a mon on a line-up slot: swap with whoever is there, or take the slot if it is free. */
  dropOnSlot: (instanceId: string, slotIndex: number) => void;
  /** Called by the battle screen when the fight ends. */
  finishBattle: (outcome: BattleOutcome) => void;
  dismissResult: () => void;
  /**
   * Resolves a throw the battle player has already rolled.
   *
   * The player owns the roll, because it needs the fight's own RNG and has to take the mon off
   * the field; the store owns the consequences, because the ball and the Box belong to the run.
   */
  resolveThrow: (result: ThrowResult, speciesId: number, wildExp: number) => void;
  moveToLineUp: (instanceId: string) => void;
  moveToBox: (instanceId: string) => void;
  reorder: (instanceId: string, toIndex: number) => void;
  /** Folds two mons of a family into one. A no-op if they aren't a pair. */
  combine: (aId: string, bId: string) => void;
}

const freshRun = (seed: number) => createRun(seed, defaultStarters());

/** A seed for a run nobody asked for a seed for. */
export const randomSeed = (): number => Math.floor(Math.random() * 1_000_000);

// The store's *initial* state is a real run, not a placeholder — the app opens straight onto the
// map without anyone pressing "New run". It was seeded 1, which meant every page load replayed the
// same Location against the same opposition and the game looked like it had no randomness in it at
// all. Tests that care about a specific run set one; nothing else should depend on this value.
const openingSeed = randomSeed();
const openingMap = generateLocationMap(openingSeed, 0);

export const useRunStore = create<RunStore>((set, get) => ({
  run: freshRun(openingSeed),
  phase: 'map',
  map: openingMap,
  location: locationFor(0),
  available: [...openingMap.entryIds],
  activeNode: null,
  opponents: [],
  lastResult: null,
  event: null,
  eventResult: null,
  pendingBounty: null,
  throwLog: [],
  lastFusion: null,

  startRun: (seed = randomSeed()) => {
    const map = generateLocationMap(seed, 0);
    set({
      run: freshRun(seed),
      phase: 'map',
      map,
      location: locationFor(0),
      available: [...map.entryIds],
      activeNode: null,
      opponents: [],
      lastResult: null,
      event: null,
      eventResult: null,
      pendingBounty: null,
      throwLog: [],
      lastFusion: null,
    });
  },

  enter: (nodeId) => {
    const { run, map, available } = get();
    const node = map.nodes.find((n) => n.id === nodeId);
    // Only a node the map actually offers from here — a stale click or a hand-crafted id must not
    // let the player skip a layer.
    if (node === undefined || !available.includes(nodeId)) return;

    if (node.type === 'Center') {
      set({ run: enterNode(run, nodeId), phase: 'shop', activeNode: node });
      return;
    }

    if (node.type === 'Encounter') {
      // Rolled from the node's own seed rather than from the moment of entry, so the Encounter a
      // node holds is a fact about the map — the same one however often you look at it.
      const event = rollRoadEvent(run, get().location, eventSeedFor(run.seed, run.badges, node));
      set({
        run: enterNode(run, nodeId),
        phase: 'encounter',
        activeNode: node,
        event,
        eventResult: null,
        pendingBounty: null,
        opponents: [],
      });
      return;
    }

    const opponents = opponentsFor(run.seed, run.badges, node, run.lineUp.length, get().location);
    set({
      run: enterNode(run, nodeId),
      phase: 'battle',
      activeNode: node,
      opponents,
      pendingBounty: null,
      throwLog: [],
    });
  },

  chooseEncounter: (index) => {
    const { run, event, eventResult } = get();
    // One branch per Encounter. The event object is pre-rolled and pure, so without this a second
    // click would resolve it again against the run its first click had already changed.
    if (event === null || eventResult !== null) return;

    const result = resolveRoadEvent(event, index, run);
    if (result === null) return;

    if (result.foes.length > 0) {
      set({
        run: result.run,
        phase: 'battle',
        opponents: [...result.foes],
        pendingBounty: result.bounty,
        eventResult: { message: result.message, report: result.report },
        throwLog: [],
      });
      return;
    }

    set({
      run: result.run,
      eventResult: { message: result.message, report: result.report },
    });
  },

  leaveEncounter: () => {
    const { run, activeNode, map } = get();
    if (activeNode === null) return;
    const next = completeNode(healAll(run), activeNode.id);
    set({
      run: next,
      phase: isRunOver(next) ? 'over' : 'map',
      activeNode: null,
      event: null,
      eventResult: null,
      available: reachableFrom(map, next.visited),
    });
  },

  finishBattle: (outcome) => {
    const { run, activeNode, pendingBounty, eventResult } = get();
    if (activeNode === null) return;

    const won = outcome === 'SideAWins';
    const isGym = activeNode.type === 'Gym';
    // A draw counts as a loss for progress but costs no Morale: nobody won, so nothing is owed
    // either way, and charging for it would make a mutual knockout the worst outcome in the game.
    const moraleLost = outcome === 'SideBWins' ? 1 : 0;

    let next = run;
    let report: GrowthReport = { gained: {}, evolutions: [] };
    let money = 0;
    let badge = false;

    if (won) {
      const granted = grantWinExp(next, EXP_PER_WIN);
      next = granted.run;
      report = granted.report;
      money = moneyForWin(activeNode.type);
      next = addMoney(next, money);
      // The bounty is banked here but reported separately, so the result panel can name what the
      // Encounter paid rather than folding it into the node's ordinary takings.
      if (pendingBounty !== null) next = grantBounty(next, pendingBounty);
      if (isGym) {
        next = awardBadge(next);
        badge = true;
      }
    } else if (moraleLost > 0) {
      next = spendMorale(next, moraleLost);
    }

    // Growth an Encounter's own branch granted is folded in, so one screen narrates the whole node
    // rather than the evolution being lost behind the fight it paid for.
    if (eventResult !== null) report = mergeReports(eventResult.report, report);

    // Damage never carries: HP is restored and the fainted are back, win or lose.
    next = healAll(next);
    // A win consumes the node; a loss does not. What a loss costs is the Morale, and Morale is
    // already what makes a run finite — taking the node as well can strand a player on a layer
    // that offered them one way forward. The retry is not free: the opponents and the battle seed
    // are drawn from the node, so the same team fights the same fight, and something about the
    // line-up has to change for the second attempt to go differently.
    //
    // A draw still consumes the node. It costs no Morale, so a retryable draw is an unlimited
    // number of identical re-runs at no price at all.
    next = won || outcome === 'Draw' ? completeNode(next, activeNode.id) : leaveNode(next);

    // A won Gym ends the Location: a fresh map, a fresh Location, and the visited list reset so
    // the new map's entry layer is what's on offer.
    let map = get().map;
    let location = get().location;
    if (badge) {
      next = { ...next, visited: [] };
      map = generateLocationMap(next.seed, next.badges);
      location = locationFor(next.badges);
    }

    set({
      run: next,
      map,
      location,
      available: reachableFrom(map, next.visited),
      phase: isRunOver(next) || isRunWon(next, BADGES_TO_WIN) ? 'over' : 'result',
      event: null,
      eventResult: null,
      pendingBounty: null,
      lastResult: {
        outcome,
        nodeId: activeNode.id,
        isGym,
        expEach: won ? EXP_PER_WIN : 0,
        money,
        badge,
        moraleLost,
        report,
        bounty: won ? pendingBounty : null,
      },
    });
  },

  dismissResult: () => {
    const { run, map } = get();
    set({ phase: 'map', activeNode: null, opponents: [], available: reachableFrom(map, run.visited) });
  },

  purchase: (item) => {
    const { run } = get();
    const result = buy(run.money, run.balls, item);
    if (result === null) return;
    set({ run: { ...run, money: result.money, balls: result.balls } });
  },

  leaveShop: () => {
    const { run, activeNode, map } = get();
    if (activeNode === null) return;
    const next = completeNode(healAll(run), activeNode.id);
    set({
      run: next,
      phase: 'map',
      activeNode: null,
      available: reachableFrom(map, next.visited),
    });
  },

  resolveThrow: (result, speciesId, wildExp) => {
    const { run, throwLog } = get();
    if (result.outcome === 'NotThrown' || !hasBall(run.balls, result.tier)) return;

    // The ball is spent either way. A throw that breaks free still costs you the ball, which is
    // what makes weakening the target first worth doing.
    let next = useBall(run, result.tier);

    let caught = null;
    if (result.outcome === 'Caught') {
      caught = caughtInstance(
        speciesId,
        result.tier,
        wildExp,
        catchUpFloor(next),
        `caught-${speciesId}-${next.visited.length}-${throwLog.length}`,
      );
      if (caught !== null) next = addCaught(next, caught);
    }

    set({ run: next, throwLog: [{ ...result, caught }, ...throwLog] });
  },

  openBox: () => set({ phase: 'box' }),
  closeBox: () => set({ phase: 'map' }),

  dropOnSlot: (instanceId, slotIndex) => set({ run: placeInSlot(get().run, instanceId, slotIndex) }),

  moveToLineUp: (instanceId) => set({ run: promoteFromBox(get().run, instanceId) }),
  moveToBox: (instanceId) => set({ run: benchToBox(get().run, instanceId) }),
  reorder: (instanceId, toIndex) => set({ run: reorderLineUp(get().run, instanceId, toIndex) }),

  combine: (aId, bId) => {
    const result = combineMons(get().run, aId, bId);
    // Null means the two aren't of a family, or one of them has already been merged away by an
    // earlier click. Either way there is nothing to do and nothing to say.
    if (result === null) return;
    set({ run: result.run, lastFusion: result.fusion });
  },
}));
