/**
 * The run store: the one mutable thing in the app.
 *
 * Everything it does is delegated to the pure functions in `/src/meta` — this layer only decides
 * *when* they run and holds the result. Keeping the rules pure is what lets them be tested
 * without a store, a component or a clock.
 */

import { create } from 'zustand';

import type { PokemonInstance } from '../content/factory.js';
import {
  applyItemTraining,
  applyStatRewards,
  catchUpFloor,
  combineMons,
  grantWinExp,
  monsAwaitingChoice,
  spendPoint,
  type Evolution,
  type GrowthReport,
} from '../meta/experience.js';
import type { GrowableStat } from '../content/statGrowth.js';
import { hasBall } from '../meta/balls.js';
import { caughtInstance, type ThrowResult } from '../meta/catching.js';
import { defaultStarters, opponentsFor } from '../meta/encounters.js';
import { generateLocationMap, reachableFrom, type LocationMap } from '../meta/mapGenerator.js';
import { LOCATIONS, locationFor, type Location } from '../meta/locations.js';
import { buffTotal, buffsFor, type TrainerBuff } from '../meta/badges.js';
import {
  ADOPTION_SLOTS,
  REROLL_COST,
  adoptionCost,
  buy,
  type ShopItem,
} from '../meta/shop.js';
import { encounterPool } from '../meta/encounters.js';
import { baselineExp, MAX_PARTY_SIZE } from '../meta/progression.js';
import { createInstance } from '../content/factory.js';
import { plateItems, shopItemPool, type Item } from '../content/items.js';
import { speciesOf } from '../content/index.js';
import {
  BADGES_TO_WIN,
  EXP_PER_WIN,
  moneyForWin,
} from '../meta/progression.js';
import {
  addCaught,
  addItem,
  addMoney,
  equipItem,
  unequipItem,
  addBuff,
  awardBadge,
  benchToBox,
  completeNode,
  createRun,
  swapMons,
  enterNode,
  healAll,
  isRunOver,
  isRunWon,
  promoteFromBox,
  reorderLineUp,
  spendMorale,
  useBall,
  visitRegion,
  type MapNode,
  type RunState,
} from '../meta/runState.js';
import { createRandom, type BattleOutcome } from '../sim/index.js';
import { encounterFor, type Encounter } from '../meta/encountersEvents.js';
import { applyEncounterEffect } from '../meta/encounterEffects.js';

/** Where the player is in the loop. */
export type Phase =
  | 'map'
  /** The trainer token is walking the line to the node it is about to enter. */
  | 'travelling'
  | 'battle'
  | 'result'
  | 'shop'
  | 'encounter'
  /** Gym beaten: badge, leader, and a choice of where to go next. */
  | 'celebration'
  /** Arrived somewhere new: pick a permanent trainer buff. */
  | 'buff'
  | 'box'
  | 'over';

/** What a fight did, for the results screen. */
export interface BattleResult {
  outcome: BattleOutcome;
  nodeId: string;
  isGym: boolean;
  expEach: number;
  /** The two stats the node awarded, for the result screen to name. */
  statRewards: readonly string[];
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
  /** The encounter being shown, while phase is 'encounter'. */
  encounter: Encounter | null;
  /** What the last encounter choice did, for the screen to narrate. */
  encounterOutcome: string | null;
  /** The node the trainer is walking toward, while phase is 'travelling'. */
  travellingTo: string | null;
  /** The three regions offered after a Gym. */
  regionChoices: Location[];
  /** The three buffs offered on arrival. */
  buffChoices: TrainerBuff[];
  /**
   * Evolutions waiting to be shown, oldest first.
   *
   * A queue rather than a single value: one grant can evolve several mons at once, and a single
   * mon can cross two thresholds in one go. Each is shown in turn and dismissed individually.
   */
  evolutionQueue: Evolution[];
  /** The Pokémon this Center is currently offering for adoption. */
  adoptable: PokemonInstance[];
  /** The three items this Center is offering, rerolled alongside the Pokémon. */
  purchasableItems: Item[];
  /** Whether the Bag popup is open. Independent of `phase`, like the detail modal. */
  bagOpen: boolean;
  /** Bumped by a reroll, so the next draw differs. */
  adoptionRoll: number;
  /** Throws this fight, newest first — what the battle screen narrates. */
  throwLog: ThrowResult[];

  startRun: (seed?: number) => void;
  /** Starts the walk to a node. The node's own event fires when `arrive` is called. */
  enter: (nodeId: string) => void;
  /** Called once the trainer token has finished moving. */
  arrive: () => void;
  takeEncounterChoice: (index: number) => void;
  leaveEncounter: () => void;
  purchase: (item: ShopItem) => void;
  leaveShop: () => void;
  /** Spends one of a mon's pending EXP points on a stat. */
  choose: (instanceId: string, stat: GrowableStat) => void;
  /** Mons with points still to spend. */
  pending: () => ReturnType<typeof monsAwaitingChoice>;
  chooseRegion: (name: string) => void;
  chooseBuff: (buffId: string) => void;
  /** Dismisses the evolution currently on screen, revealing the next if there is one. */
  dismissEvolution: () => void;
  adopt: (instanceId: string) => void;
  buyItem: (itemId: string) => void;
  openBag: () => void;
  closeBag: () => void;
  equip: (instanceId: string, itemId: string) => void;
  unequip: (instanceId: string) => void;
  rerollAdoptions: () => void;
  /** Which mon's detail panel is open, independent of `phase` — reachable from map, shop or Box. */
  detailInstanceId: string | null;
  openDetail: (instanceId: string) => void;
  closeDetail: () => void;
  combine: (keepId: string, consumeId: string) => void;
  swap: (aId: string, bId: string) => void;
  openBox: () => void;
  closeBox: () => void;
  /** Moves a mon to an index in the line-up, pulling it out of the Box if that is where it is. */
  placeInLineUp: (instanceId: string, toIndex: number) => void;
  /** Called by the battle screen when the fight ends. */
  finishBattle: (outcome: BattleOutcome, participants: string[]) => void;
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
}

const freshRun = (seed: number) => createRun(seed, defaultStarters());

/**
 * Three places to go next.
 *
 * Drawn from Locations the run has not already been to, so a route never doubles back, and
 * always three where three remain — the choice is the point. Near the end of a run there may be
 * fewer left than that, and offering two is better than padding with a repeat.
 */
/**
 * The five Pokémon a Center is offering.
 *
 * Drawn from the Location's own pool, so a shop sells what lives nearby and a region's theme
 * reaches the roster as well as the fights. Seeded on the node and the reroll count, so walking
 * away and coming back shows the same five — a shelf that re-rolled on every glance would make
 * the price meaningless.
 */
function rollAdoptions(
  run: RunState,
  location: Location,
  roll: number,
  nodeId: string,
): PokemonInstance[] {
  const rng = createRandom(run.seed + run.badges * 3137 + roll * 811 + nodeId.length * 29);
  const pool = encounterPool(run.badges, location.typeBias);
  const exp = baselineExp(run.badges);

  const offered: PokemonInstance[] = [];
  const seen = new Set<number>();
  // Distinct species where the pool allows it: five of the same mon is not five options.
  for (let guard = 0; guard < 60 && offered.length < ADOPTION_SLOTS; guard++) {
    const species = pool[rng.nextInt(pool.length)]!;
    if (seen.has(species.id) && seen.size < pool.length) continue;
    seen.add(species.id);
    offered.push(
      createInstance(species, {
        instanceId: `adopt-${run.badges}-${roll}-${offered.length}-${species.id}`,
        exp,
      }),
    );
  }
  return offered;
}

/**
 * The three items a Center is offering.
 *
 * Plates are drawn from a separate pool and capped at one per shelf: there are eighteen of them
 * against a dozen everything-else, so an unweighted draw would leave most shops selling nothing
 * but typing.
 */
function rollShopItems(run: RunState, roll: number, nodeId: string): Item[] {
  const rng = createRandom(run.seed + run.badges * 4159 + roll * 577 + nodeId.length * 31 + 7);
  const general = [...shopItemPool()];
  const plates = plateItems();

  const offered: Item[] = [];
  let platesOffered = 0;

  for (let guard = 0; guard < 40 && offered.length < 3; guard++) {
    const takePlate = platesOffered < 1 && rng.nextInt(100) < 30;
    const pool = takePlate ? plates : general;
    const pick = pool[rng.nextInt(pool.length)];
    if (pick === undefined || offered.some((i) => i.id === pick.id)) continue;
    if (takePlate) platesOffered++;
    offered.push(pick);
  }
  return offered;
}

function offerRegions(run: RunState): Location[] {
  const unseen = LOCATIONS.filter((l) => !run.regionsVisited.includes(l.region));
  const pool = unseen.length > 0 ? unseen : [...LOCATIONS];
  const rng = createRandom(run.seed + run.badges * 8191);

  const picked: Location[] = [];
  const remaining = [...pool];
  while (picked.length < 3 && remaining.length > 0) {
    picked.push(remaining.splice(rng.nextInt(remaining.length), 1)[0]!);
  }
  return picked;
}

export const useRunStore = create<RunStore>((set, get) => ({
  run: freshRun(1),
  phase: 'map',
  map: generateLocationMap(1, 0),
  location: locationFor(0),
  available: generateLocationMap(1, 0).entryIds.slice(),
  activeNode: null,
  opponents: [],
  lastResult: null,
  encounter: null,
  encounterOutcome: null,
  travellingTo: null,
  regionChoices: [],
  buffChoices: [],
  evolutionQueue: [],
  adoptable: [],
  purchasableItems: [],
  bagOpen: false,
  adoptionRoll: 0,
  throwLog: [],

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
      encounter: null,
      encounterOutcome: null,
      travellingTo: null,
      regionChoices: [],
      buffChoices: [],
      evolutionQueue: [],
      adoptable: [],
      purchasableItems: [],
      bagOpen: false,
      adoptionRoll: 0,
      throwLog: [],
    });
  },

  enter: (nodeId) => {
    const { run, map, available } = get();
    const node = map.nodes.find((n) => n.id === nodeId);
    // Only a node the map actually offers from here — a stale click or a hand-crafted id must not
    // let the player skip a layer.
    if (node === undefined || !available.includes(nodeId)) return;

    // Walk first, resolve second. The token moving along the line is the beat that makes the map
    // a journey; firing the node's event immediately would cut straight over it.
    set({ run: enterNode(run, nodeId), phase: 'travelling', activeNode: node, travellingTo: nodeId });
  },

  arrive: () => {
    const { run, activeNode, location } = get();
    if (activeNode === null) return;
    set({ travellingTo: null });

    if (activeNode.type === 'Center') {
      set({
        phase: 'shop',
        adoptable: rollAdoptions(run, location, 0, activeNode.id),
        purchasableItems: rollShopItems(run, 0, activeNode.id),
        adoptionRoll: 0,
      });
      return;
    }

    if (activeNode.type === 'Encounter') {
      set({
        phase: 'encounter',
        encounter: encounterFor(run.seed, run.badges, activeNode.id, location.typeBias),
        encounterOutcome: null,
      });
      return;
    }

    set({
      phase: 'battle',
      opponents: opponentsFor(run.seed, run.badges, activeNode, run.lineUp.length, location),
      throwLog: [],
    });
  },

  takeEncounterChoice: (index) => {
    const { run, encounter, location, map } = get();
    if (encounter === null) return;
    const choice = encounter.choices[index];
    if (choice === undefined) return;

    const rng = createRandom(run.seed + run.visited.length * 7717 + index);
    const { run: next, text, legendary } = applyEncounterEffect(
      run,
      choice.effect,
      location,
      rng,
    );

    // A legendary is a fight, so it leaves the encounter screen entirely rather than resolving in
    // place — the reward for winning is the same as any other fight, plus a catchable legendary.
    if (legendary !== null) {
      set({ run: next, phase: 'battle', opponents: legendary, throwLog: [], encounter: null });
      return;
    }

    const completed = completeNode(healAll(next), encounter.id === '' ? '' : get().activeNode!.id);
    set({
      run: completed,
      encounterOutcome: text,
      available: reachableFrom(map, completed.visited),
    });
  },

  leaveEncounter: () =>
    set({ phase: 'map', activeNode: null, encounter: null, encounterOutcome: null }),

  finishBattle: (outcome, participants) => {
    const { run, activeNode } = get();
    if (activeNode === null) return;

    const won = outcome === 'SideAWins';
    const isGym = activeNode.type === 'Gym';
    const kind = activeNode.type === 'Gym'
      ? 'Gym'
      : activeNode.type === 'MysteryTrainer'
        ? 'MysteryTrainer'
        : 'Wild';
    // A draw counts as a loss for progress but costs no Morale: nobody won, so nothing is owed
    // either way, and charging for it would make a mutual knockout the worst outcome in the game.
    const moraleLost = outcome === 'SideBWins' ? 1 : 0;

    let next = run;
    let report: GrowthReport = { gained: {}, evolutions: [] };
    let money = 0;
    let badge = false;

    if (won) {
      const granted = grantWinExp(
        next,
        EXP_PER_WIN + buffTotal(next.buffs, 'scholar'),
        participants,
      );
      next = granted.run;
      report = granted.report;
      if (granted.report.evolutions.length > 0) {
        set({ evolutionQueue: [...get().evolutionQueue, ...granted.report.evolutions] });
      }
      // The node advertised which two stats it pays; everyone who fought gets them directly.
      next = applyStatRewards(next, activeNode.statRewards ?? [], participants);
      next = applyItemTraining(next, participants);
      money = moneyForWin(kind) + buffTotal(next.buffs, 'income');
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

    // A won Gym ends the Location. The map is not regenerated here — the player picks where to
    // go next on the celebration screen, and the new Location is built from that choice.
    const map = get().map;
    const location = get().location;

    const finishedRun = isRunOver(next) || isRunWon(next, BADGES_TO_WIN);
    set({
      run: next,
      map,
      location,
      available: reachableFrom(map, next.visited),
      regionChoices: badge && !finishedRun ? offerRegions(next) : [],
      phase: finishedRun ? 'over' : 'result',
      lastResult: {
        outcome,
        nodeId: activeNode.id,
        isGym,
        expEach: won ? EXP_PER_WIN : 0,
        statRewards: won ? (activeNode.statRewards ?? []) : [],
        money,
        badge,
        moraleLost,
        report,
      },
    });
  },

  dismissResult: () => {
    const { run, map, lastResult } = get();
    // A badge earns the celebration; everything else goes back to the map.
    if (lastResult?.badge === true) {
      set({ phase: 'celebration', activeNode: null, opponents: [] });
      return;
    }
    set({ phase: 'map', activeNode: null, opponents: [], available: reachableFrom(map, run.visited) });
  },

  chooseRegion: (name) => {
    const { run, regionChoices } = get();
    const location = regionChoices.find((l) => l.name === name) ?? locationFor(run.badges);
    const moved = visitRegion({ ...run, visited: [] }, location.region);
    const map = generateLocationMap(moved.seed + moved.badges * 601, moved.badges);

    set({
      run: moved,
      location,
      map,
      available: reachableFrom(map, moved.visited),
      regionChoices: [],
      buffChoices: [...buffsFor(location.region)],
      phase: 'buff',
    });
  },

  chooseBuff: (buffId) => {
    set({ run: addBuff(get().run, buffId), buffChoices: [], phase: 'map' });
  },

  purchase: (item) => {
    const { run } = get();
    const result = buy(run.money, run.balls, item);
    if (result === null) return;
    set({ run: { ...run, money: result.money, balls: result.balls } });
  },

  adopt: (instanceId) => {
    const { run, adoptable } = get();
    const mon = adoptable.find((m) => m.instanceId === instanceId);
    if (mon === undefined) return;

    const species = speciesOf(mon.speciesId);
    if (species === null) return;
    const cost = adoptionCost(species.tier);
    if (run.money < cost) return;

    // Taken off the shelf as well as paid for, so one Pokémon cannot be adopted twice.
    set({
      run: addCaught({ ...run, money: run.money - cost }, mon, run.lineUp.length < MAX_PARTY_SIZE),
      adoptable: adoptable.filter((m) => m.instanceId !== instanceId),
    });
  },

  buyItem: (itemId) => {
    const { run, purchasableItems } = get();
    const item = purchasableItems.find((i) => i.id === itemId);
    if (item === undefined || run.money < item.cost) return;

    set({
      run: addItem({ ...run, money: run.money - item.cost }, item.id),
      // Off the shelf as well as paid for, so one item can't be bought twice from one visit.
      purchasableItems: purchasableItems.filter((i) => i.id !== itemId),
    });
  },

  openBag: () => set({ bagOpen: true }),
  closeBag: () => set({ bagOpen: false }),
  equip: (instanceId, itemId) => set({ run: equipItem(get().run, instanceId, itemId) }),
  unequip: (instanceId) => set({ run: unequipItem(get().run, instanceId) }),

  rerollAdoptions: () => {
    const { run, location, activeNode, adoptionRoll } = get();
    if (run.money < REROLL_COST || activeNode === null) return;
    const roll = adoptionRoll + 1;
    set({
      run: { ...run, money: run.money - REROLL_COST },
      adoptionRoll: roll,
      adoptable: rollAdoptions(run, location, roll, activeNode.id),
      // Rerolled together, as one shelf: paying twice to refresh two halves of the same shop
      // would be fiddly without being a more interesting decision.
      purchasableItems: rollShopItems(run, roll, activeNode.id),
    });
  },

  leaveShop: () => {
    const { run, activeNode, map } = get();
    if (activeNode === null) return;
    const next = completeNode(healAll(run), activeNode.id);
    set({
      run: next,
      phase: 'map',
      activeNode: null,
      adoptable: [],
      purchasableItems: [],
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

  choose: (instanceId, stat) => set({ run: spendPoint(get().run, instanceId, stat) }),
  pending: () => monsAwaitingChoice(get().run),

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

  detailInstanceId: null,
  openDetail: (instanceId) => set({ detailInstanceId: instanceId }),
  closeDetail: () => set({ detailInstanceId: null }),
  combine: (keepId, consumeId) => {
    const before = get().run;
    const after = combineMons(before, keepId, consumeId);

    // Detected by comparing the kept mon's species across the call rather than by having
    // combineMons report it: the evolution happens inside raiseToExp, and threading a report back
    // out through a pure state transformer for one caller's benefit is not worth the signature.
    const was = [...before.lineUp, ...before.box].find((m) => m.instanceId === keepId);
    const now = [...after.lineUp, ...after.box].find((m) => m.instanceId === keepId);
    const queued: Evolution[] = [];
    if (was !== undefined && now !== undefined && was.speciesId !== now.speciesId) {
      const from = speciesOf(was.speciesId);
      const to = speciesOf(now.speciesId);
      if (from !== null && to !== null) queued.push({ instanceId: keepId, from, to });
    }

    set({
      run: after,
      evolutionQueue: queued.length > 0 ? [...get().evolutionQueue, ...queued] : get().evolutionQueue,
    });
  },

  dismissEvolution: () => set({ evolutionQueue: get().evolutionQueue.slice(1) }),
  swap: (aId, bId) => set({ run: swapMons(get().run, aId, bId) }),

  moveToLineUp: (instanceId) => set({ run: promoteFromBox(get().run, instanceId) }),
  moveToBox: (instanceId) => set({ run: benchToBox(get().run, instanceId) }),
  reorder: (instanceId, toIndex) => set({ run: reorderLineUp(get().run, instanceId, toIndex) }),
}));
