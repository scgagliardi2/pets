/**
 * The run store: the one mutable thing in the app.
 *
 * Everything it does is delegated to the pure functions in `/src/meta` — this layer only decides
 * *when* they run and holds the result. Keeping the rules pure is what lets them be tested
 * without a store, a component or a clock.
 */

import { create } from 'zustand';

import type { PokemonInstance } from '../content/factory.js';
import { catchUpFloor, grantWinExp, type GrowthReport } from '../meta/experience.js';
import { hasBall } from '../meta/balls.js';
import { caughtInstance, type ThrowResult } from '../meta/catching.js';
import { defaultStarters, opponentsFor } from '../meta/encounters.js';
import { generateLocationMap, reachableFrom, type LocationMap } from '../meta/mapGenerator.js';
import { locationFor, type Location } from '../meta/locations.js';
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
export type Phase = 'map' | 'battle' | 'result' | 'shop' | 'box' | 'over';

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
  /** Throws this fight, newest first — what the battle screen narrates. */
  throwLog: ThrowResult[];
  /** The most recent fusion, so the team screens can say what came out of it. */
  lastFusion: Fusion | null;

  startRun: (seed?: number) => void;
  enter: (nodeId: string) => void;
  purchase: (item: ShopItem) => void;
  leaveShop: () => void;
  openBox: () => void;
  closeBox: () => void;
  /** Moves a mon to an index in the line-up, pulling it out of the Box if that is where it is. */
  placeInLineUp: (instanceId: string, toIndex: number) => void;
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

export const useRunStore = create<RunStore>((set, get) => ({
  run: freshRun(1),
  phase: 'map',
  map: generateLocationMap(1, 0),
  location: locationFor(0),
  available: generateLocationMap(1, 0).entryIds.slice(),
  activeNode: null,
  opponents: [],
  lastResult: null,
  throwLog: [],
  lastFusion: null,

  startRun: (seed = Math.floor(Math.random() * 1_000_000)) => {
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

    const opponents = opponentsFor(run.seed, run.badges, node, run.lineUp.length, get().location);
    set({
      run: enterNode(run, nodeId),
      phase: 'battle',
      activeNode: node,
      opponents,
      throwLog: [],
    });
  },

  finishBattle: (outcome) => {
    const { run, activeNode } = get();
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
      money = moneyForWin(isGym);
      next = addMoney(next, money);
      if (isGym) {
        next = awardBadge(next);
        badge = true;
      }
    } else if (moraleLost > 0) {
      next = spendMorale(next, moraleLost);
    }

    // Damage never carries: HP is restored and the fainted are back, win or lose.
    next = healAll(next);
    // A lost node is still resolved — you don't get to retry it. Losing costs the node and the
    // Morale, which is what makes a run finite.
    next = completeNode(next, activeNode.id);

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
      lastResult: {
        outcome,
        nodeId: activeNode.id,
        isGym,
        expEach: won ? EXP_PER_WIN : 0,
        money,
        badge,
        moraleLost,
        report,
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

  placeInLineUp: (instanceId, toIndex) => {
    const { run } = get();
    const inBox = run.box.some((m) => m.instanceId === instanceId);
    // Promote first if it is coming from the Box, then position it — one action from the player's
    // point of view, two from the run's.
    const promoted = inBox ? promoteFromBox(run, instanceId) : run;
    if (inBox && promoted === run) return; // line-up was full
    set({ run: reorderLineUp(promoted, instanceId, toIndex) });
  },

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
