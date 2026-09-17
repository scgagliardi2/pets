/**
 * Encounters: the `?` nodes on a Location's map.
 *
 * Ported and widened from Unity's `RoadEvents.cs` (ADR 0014). A short scene, two or three
 * choices, each a stated trade with at most one coin flip in it — Slay the Spire's `?` room, and
 * for its reason: a map of nothing but fights is a map with one verb, and the run's character
 * lives in the beats between them.
 *
 * **Encounters are skewed positive, on purpose.** Taking one already costs you the EXP and money
 * the fight in that slot would have paid, and that is the price. A node that then *also* punished
 * you would be a node no one takes, and a choice nobody takes is not a choice. So seven of the
 * nine kinds cannot leave the run worse off, the two that can say so in the choice text before
 * the click, and every kind has at least one branch that is pure upside.
 *
 * **Pure, like the rest of `/src/meta`.** `roll` builds an encounter from a run and a seed;
 * `resolve` takes a choice index and returns a *new* run plus what to say about it. Nothing here
 * touches a store, a screen or a clock — the store decides when these run, and the map layer
 * narrates what comes back.
 *
 * **Balls are what "an item" means here.** They are the run's only consumable, so an encounter
 * that pays out in balls is paying in the thing a Center sells, which is the currency the player
 * already understands.
 */

import { baseForms, legendaries, speciesOf, type Species } from '../content/index.js';
import { createInstance, displayNameOf, type PokemonInstance } from '../content/factory.js';
import { createRandom, type PokemonType, type Rng } from '../sim/index.js';
import { nodeSeed } from './encounters.js';
import { ballName, type BallTier } from './balls.js';
import { grantExpTo, grantWinExp, raiseToExp, type GrowthReport } from './experience.js';
import type { Location } from './locations.js';
import { baselineExp, maxTier, MAX_PARTY_SIZE } from './progression.js';
import { addCaught, addMoney, grantBalls, type MapNode, type RunState } from './runState.js';

/** The nine encounters an Encounter node can roll. */
export type RoadEventKind =
  | 'LegendarySighting'
  | 'TravelingTrader'
  | 'WanderingProfessor'
  | 'FoundStash'
  | 'StrayFriend'
  | 'DayCare'
  | 'AncientShrine'
  | 'GameCorner'
  | 'RocketShakedown';

/**
 * How often each kind turns up, as a weight.
 *
 * The two that can cost you something are the two rarest. A Legendary is rare because it is the
 * encounter a player tells a story about afterwards, and that only survives if it isn't routine.
 */
const WEIGHTS: Readonly<Record<RoadEventKind, number>> = {
  StrayFriend: 14,
  WanderingProfessor: 13,
  FoundStash: 13,
  TravelingTrader: 12,
  DayCare: 12,
  AncientShrine: 11,
  GameCorner: 10,
  LegendarySighting: 8,
  RocketShakedown: 7,
};

export const ALL_EVENT_KINDS = Object.keys(WEIGHTS) as RoadEventKind[];

// --- the numbers ---------------------------------------------------------------------------------
// All first-pass values, in the same sense as the rest of the balance constants: chosen to make an
// Encounter worth about a fight and a half, since that is roughly what taking one costs.

export const LEGENDARY_BOUNTY_MONEY = 12;
export const LEGENDARY_BOUNTY_BALL: BallTier = 'Ultra';
/** What the Legendary leaves behind when you let it go. Declining is never nothing. */
export const LEGENDARY_TOKEN_BALL: BallTier = 'Great';

export const TRADER_DIRECTIONS = 5;

export const PROFESSOR_EXP = 2;
export const PROFESSOR_STIPEND = 8;

export const STASH_POKE_BALLS = 2;
export const STASH_GREAT_BALLS = 1;
export const STASH_SALE = 7;

export const STRAY_GIFT = 6;

export const DAYCARE_FEE = 4;
export const DAYCARE_CHAT_EXP = 1;

export const SHRINE_OFFERING = 3;
export const SHRINE_EXP = 3;
export const SHRINE_COINS = 5;

export const SLOTS_STAKE = 4;
export const SLOTS_PRIZE = 14;
export const SLOTS_WIN_PERCENT = 55;
export const LOOSE_COINS = 4;

export const ROCKET_PURSE = 12;
export const ROCKET_PURSE_BALL: BallTier = 'Great';
export const ROCKET_GRUNT_COUNT = 2;
export const ROCKET_TYPES: readonly PokemonType[] = ['Poison', 'Dark'];

// --- shapes ---------------------------------------------------------------------------------------

export interface RoadEventChoice {
  readonly label: string;
  /** The trade, spelled out. A choice whose cost is a surprise is a trap, not a decision. */
  readonly detail: string;
  /** False when the run can't take this choice right now. Shown, but dead. */
  readonly available: boolean;
}

/** One encounter as rolled for one node: its scene, its choices, and anything it pre-rolled. */
export interface RoadEvent {
  readonly kind: RoadEventKind;
  readonly title: string;
  /** Who you are dealing with, for the scene's byline. */
  readonly speaker: string;
  readonly body: string;
  readonly choices: readonly RoadEventChoice[];
  /**
   * The node's seed. Every chance an outcome rolls is drawn from it, so an encounter is as
   * reproducible as the fight a Wild node leads to — and previewing a node cannot re-roll it.
   */
  readonly seed: number;
  /** The line-up a choice would fight, when one does. Empty otherwise. */
  readonly foes: readonly PokemonInstance[];
  /** TravelingTrader and StrayFriend: the mon on offer. */
  readonly offer: PokemonInstance | null;
  /** TravelingTrader: which of the run's mons would go. */
  readonly tradeAwayId: string | null;
}

/** What beating an encounter's fight pays on top of an ordinary win. */
export interface Bounty {
  readonly money: number;
  readonly ball: BallTier | null;
  readonly ballCount: number;
}

/** What one choice did. The run is already the new one — callers swap, they don't apply. */
export interface RoadEventOutcome {
  readonly run: RunState;
  readonly message: string;
  /** Non-empty when the choice starts a fight; the store opens the battle screen against it. */
  readonly foes: readonly PokemonInstance[];
  /** Paid by the store when that fight is won. */
  readonly bounty: Bounty | null;
  /** Growth the choice itself granted, so the screen can play an evolution it caused. */
  readonly report: GrowthReport;
}

const EMPTY_REPORT: GrowthReport = { gained: {}, evolutions: [] };

const outcome = (
  run: RunState,
  message: string,
  extra: Partial<Omit<RoadEventOutcome, 'run' | 'message'>> = {},
): RoadEventOutcome => ({
  run,
  message,
  foes: extra.foes ?? [],
  bounty: extra.bounty ?? null,
  report: extra.report ?? EMPTY_REPORT,
});

// --- rolling ---------------------------------------------------------------------------------------

/**
 * The seed an Encounter node draws from. Stable for a node within a run.
 *
 * The node's whole id is folded in, not just its layer: two Encounters can sit side by side in one
 * layer, and a seed that ignored which of the two this is would roll them the same encounter.
 */
export const eventSeedFor = (runSeed: number, badges: number, node: MapNode): number =>
  nodeSeed(runSeed, badges, node, EVENT_SEED_SALT);

/** Keeps an Encounter's roll off the stream its node's wild encounter would have used. */
const EVENT_SEED_SALT = 60013;

/** Picks which encounter a node is, by weight. */
export function pickKind(seed: number): RoadEventKind {
  const total = ALL_EVENT_KINDS.reduce((sum, k) => sum + WEIGHTS[k], 0);
  let roll = createRandom(seed).nextInt(total);
  for (const kind of ALL_EVENT_KINDS) {
    roll -= WEIGHTS[kind];
    if (roll < 0) return kind;
  }
  return 'FoundStash';
}

/** Rolls which encounter a node is, then builds it. */
export function rollRoadEvent(run: RunState, location: Location, seed: number): RoadEvent {
  return buildRoadEvent(pickKind(seed), run, location, seed);
}

export function buildRoadEvent(
  kind: RoadEventKind,
  run: RunState,
  location: Location,
  seed: number,
): RoadEvent {
  const rng = createRandom(seed ^ 0x1b873593);
  switch (kind) {
    case 'LegendarySighting':
      return buildLegendary(run, seed, rng) ?? buildStash(seed);
    case 'TravelingTrader':
      return buildTrader(run, seed, rng);
    case 'WanderingProfessor':
      return buildProfessor(run, seed);
    case 'StrayFriend':
      return buildStray(run, location, seed, rng) ?? buildStash(seed);
    case 'DayCare':
      return buildDayCare(run, seed);
    case 'AncientShrine':
      return buildShrine(run, location, seed);
    case 'GameCorner':
      return buildGameCorner(run, seed);
    case 'RocketShakedown':
      return buildRocket(run, seed, rng);
    default:
      return buildStash(seed);
  }
}

/**
 * Applies choice `index`.
 *
 * Null — changing nothing — when the choice doesn't exist or isn't available, so a stale click or
 * a hand-made index can't take a branch the encounter never offered. Resolving twice is the
 * caller's problem to prevent, because only the caller knows whether the first one is still on
 * screen.
 */
export function resolveRoadEvent(
  event: RoadEvent,
  index: number,
  run: RunState,
): RoadEventOutcome | null {
  const choice = event.choices[index];
  if (choice === undefined || !choice.available) return null;

  // Salted, so a coin flip isn't correlated with the roll that picked the encounter.
  const rng = createRandom(event.seed ^ 0x5bd1e995);
  switch (event.kind) {
    case 'LegendarySighting':
      return resolveLegendary(event, index, run);
    case 'TravelingTrader':
      return resolveTrader(event, index, run);
    case 'WanderingProfessor':
      return resolveProfessor(index, run);
    case 'FoundStash':
      return resolveStash(index, run);
    case 'StrayFriend':
      return resolveStray(event, index, run);
    case 'DayCare':
      return resolveDayCare(event, index, run);
    case 'AncientShrine':
      return resolveShrine(index, run);
    case 'GameCorner':
      return resolveGameCorner(index, run, rng);
    case 'RocketShakedown':
      return resolveRocket(event, index, run);
    default:
      return outcome(run, 'The path is clear again.');
  }
}

/** Pays a beaten encounter's bounty into the run. */
export function grantBounty(run: RunState, bounty: Bounty | null): RunState {
  if (bounty === null) return run;
  let next = addMoney(run, bounty.money);
  if (bounty.ball !== null) next = grantBalls(next, bounty.ball, bounty.ballCount);
  return next;
}

/** A one-line summary of a bounty, for the results screen. */
export const describeBounty = (bounty: Bounty): string =>
  [
    bounty.money > 0 ? `$${bounty.money}` : null,
    bounty.ball !== null && bounty.ballCount > 0
      ? `${ballName(bounty.ball)}${bounty.ballCount > 1 ? ` ×${bounty.ballCount}` : ''}`
      : null,
  ]
    .filter((part): part is string => part !== null)
    .join(' and ');

// --- shared helpers --------------------------------------------------------------------------------

/** A mon of a species at a given lifetime EXP, evolved as far as that total carries it. */
function createAtExp(species: Species, instanceId: string, exp: number): PokemonInstance {
  return raiseToExp(createInstance(species, { instanceId }), Math.max(0, exp)).mon;
}

/** The run's least-grown mon: the one the player is least attached to. Box first on a tie. */
export function leastGrown(run: RunState): PokemonInstance | null {
  const all = [...run.box, ...run.lineUp];
  if (all.length === 0) return null;
  return all.reduce((best, mon) => (mon.exp < best.exp ? mon : best));
}

const nameOfInstance = (mon: PokemonInstance | null): string =>
  mon === null ? 'your Pokémon' : displayNameOf(mon);

/** Base forms this Location would field, tier-legal for the badge count. */
function themedPool(run: RunState, location: Location): Species[] {
  const cap = maxTier(run.badges);
  const legal = baseForms().filter((s) => !s.isLegendary && (cap === null || s.tier <= cap));
  const biased = legal.filter((s) => s.types.some((t) => location.typeBias.includes(t)));
  return biased.length > 0 ? biased : legal;
}

// --- Legendary Sighting ------------------------------------------------------------------------

function buildLegendary(run: RunState, seed: number, rng: Rng): RoadEvent | null {
  const pool = legendaries();
  if (pool.length === 0) return null;

  const species = pool[rng.nextInt(pool.length)]!;
  // Pitched at the Location's baseline like any other opponent. What makes it dangerous is its
  // tier, and that it is the one Legendary a run is likely to meet before the last Gym.
  const mon = createAtExp(species, `legendary-${seed}`, baselineExp(run.badges));

  return {
    kind: 'LegendarySighting',
    title: 'Legendary Sighting',
    speaker: species.name,
    body:
      `${species.name} stands in the path, watching you. A tier ${species.tier} Legendary — ` +
      'few trainers get this close, and fewer still walk away from the fight.',
    seed,
    foes: [mon],
    offer: null,
    tradeAwayId: null,
    choices: [
      {
        label: `Challenge ${species.name}`,
        detail: `Win: $${LEGENDARY_BOUNTY_MONEY} and an ${ballName(LEGENDARY_BOUNTY_BALL)}. Lose: 1 morale. Bring a ball.`,
        available: true,
      },
      {
        label: 'Watch it go',
        detail: `It leaves something behind: a ${ballName(LEGENDARY_TOKEN_BALL)}.`,
        available: true,
      },
    ],
  };
}

function resolveLegendary(event: RoadEvent, index: number, run: RunState): RoadEventOutcome {
  const name = event.speaker;
  if (index !== 0) {
    return outcome(
      grantBalls(run, LEGENDARY_TOKEN_BALL, 1),
      `You hold still until ${name} loses interest. Where it stood is a ${ballName(LEGENDARY_TOKEN_BALL)}, ` +
        'left by someone who was not so patient.',
    );
  }

  return outcome(run, `${name} turns to face you.`, {
    foes: event.foes,
    bounty: { money: LEGENDARY_BOUNTY_MONEY, ball: LEGENDARY_BOUNTY_BALL, ballCount: 1 },
  });
}

// --- Traveling Trader --------------------------------------------------------------------------

/**
 * A non-Legendary base form one tier above the traded mon's, widening outwards while nothing
 * fits. A trade should be an upgrade you can see on the card, not a lateral move.
 */
function offerFor(mon: PokemonInstance, rng: Rng): Species | null {
  const current = speciesOf(mon.speciesId);
  if (current === null) return null;

  const candidates = baseForms().filter((s) => !s.isLegendary && s.id !== current.id);
  const target = current.tier + 1;
  for (let widen = 0; widen <= 6; widen++) {
    const pool = candidates.filter((s) => Math.abs(s.tier - target) === widen);
    if (pool.length > 0) return pool[rng.nextInt(pool.length)]!;
  }
  return null;
}

function buildTrader(run: RunState, seed: number, rng: Rng): RoadEvent {
  const yours = leastGrown(run);
  const species = yours === null ? null : offerFor(yours, rng);

  if (yours === null || species === null) {
    return {
      kind: 'TravelingTrader',
      title: 'Traveling Trader',
      speaker: 'Trader',
      body: 'A trader looks over your team, finds nothing they want, and tips their hat anyway.',
      seed,
      foes: [],
      offer: null,
      tradeAwayId: null,
      choices: [
        { label: 'Trade', detail: 'Nothing on offer for what you are carrying.', available: false },
        { label: 'Ask for directions', detail: `They point out a shortcut. +$${TRADER_DIRECTIONS}.`, available: true },
      ],
    };
  }

  const offer = createAtExp(species, `trade-${seed}`, yours.exp);
  return {
    kind: 'TravelingTrader',
    title: 'Traveling Trader',
    speaker: 'Trader',
    body:
      `A trader has their eye on your ${nameOfInstance(yours)}, and offers a tier ${species.tier} ` +
      `${species.name} raised to the same ${yours.exp} EXP in return.`,
    seed,
    foes: [],
    offer,
    tradeAwayId: yours.instanceId,
    choices: [
      {
        label: `Trade ${nameOfInstance(yours)} for ${displayNameOf(offer)}`,
        detail: 'Same EXP, a tier higher. It lands in the slot the old one had.',
        available: true,
      },
      {
        label: 'Ask for directions instead',
        detail: `They point out a shortcut. +$${TRADER_DIRECTIONS}.`,
        available: true,
      },
    ],
  };
}

function resolveTrader(event: RoadEvent, index: number, run: RunState): RoadEventOutcome {
  if (index !== 0 || event.offer === null || event.tradeAwayId === null) {
    return outcome(
      addMoney(run, TRADER_DIRECTIONS),
      `The trader sketches the road ahead in the dirt, and slips you $${TRADER_DIRECTIONS} for the company.`,
    );
  }

  // The replacement takes the exact slot the old mon had, so trading the only mon in the line-up
  // still leaves the line-up with one.
  const swap = (list: readonly PokemonInstance[]): PokemonInstance[] =>
    list.map((m) => (m.instanceId === event.tradeAwayId ? event.offer! : m));
  const given = [...run.lineUp, ...run.box].find((m) => m.instanceId === event.tradeAwayId) ?? null;

  return outcome(
    { ...run, lineUp: swap(run.lineUp), box: swap(run.box) },
    `You wave goodbye to ${nameOfInstance(given)}. ${displayNameOf(event.offer)} takes its place.`,
  );
}

// --- Wandering Professor -----------------------------------------------------------------------

function buildProfessor(run: RunState, seed: number): RoadEvent {
  return {
    kind: 'WanderingProfessor',
    title: 'Field Research',
    speaker: 'Professor',
    body:
      'A professor has a folding table up beside the path and a clipboard already out. They would ' +
      `like an hour with your ${run.lineUp.length === 1 ? 'Pokémon' : 'whole line-up'} — and they pay either way.`,
    seed,
    foes: [],
    offer: null,
    tradeAwayId: null,
    choices: [
      {
        label: 'Train with them for an hour',
        detail: `+${PROFESSOR_EXP} EXP to everyone in the line-up. Worth two wins.`,
        available: run.lineUp.length > 0,
      },
      {
        label: 'Take the research stipend',
        detail: `+$${PROFESSOR_STIPEND} and you keep walking.`,
        available: true,
      },
    ],
  };
}

function resolveProfessor(index: number, run: RunState): RoadEventOutcome {
  if (index !== 0) {
    return outcome(
      addMoney(run, PROFESSOR_STIPEND),
      `They count out $${PROFESSOR_STIPEND}, thank you for nothing in particular, and go back to the clipboard.`,
    );
  }

  const granted = grantWinExp(run, PROFESSOR_EXP);
  return outcome(
    granted.run,
    `An hour of drills on the roadside. Everyone who fights for you is +${PROFESSOR_EXP} EXP.`,
    { report: granted.report },
  );
}

// --- Found Stash -------------------------------------------------------------------------------

function buildStash(seed: number): RoadEvent {
  return {
    kind: 'FoundStash',
    title: 'Abandoned Pack',
    speaker: 'Nobody',
    body:
      'A pack is wedged under a rock, half-buried, with nobody around to claim it. Inside: balls, ' +
      'a damp map, and a receipt from a town two Locations back.',
    seed,
    foes: [],
    offer: null,
    tradeAwayId: null,
    choices: [
      {
        label: 'Keep the balls',
        detail: `${ballName('Poke')} ×${STASH_POKE_BALLS} and a ${ballName('Great')}.`,
        available: true,
      },
      {
        label: 'Sell the lot at the next Center',
        detail: `+$${STASH_SALE}.`,
        available: true,
      },
    ],
  };
}

function resolveStash(index: number, run: RunState): RoadEventOutcome {
  if (index !== 0) {
    return outcome(addMoney(run, STASH_SALE), `The counter gives you $${STASH_SALE} for the lot, no questions.`);
  }
  const next = grantBalls(grantBalls(run, 'Poke', STASH_POKE_BALLS), 'Great', STASH_GREAT_BALLS);
  return outcome(next, `Into your bag: ${ballName('Poke')} ×${STASH_POKE_BALLS} and a ${ballName('Great')}.`);
}

// --- Stray Friend ------------------------------------------------------------------------------

function buildStray(run: RunState, location: Location, seed: number, rng: Rng): RoadEvent | null {
  const pool = themedPool(run, location);
  if (pool.length === 0) return null;

  const species = pool[rng.nextInt(pool.length)]!;
  // Pitched at the Location's baseline so it is worth having rather than a mouth to feed, but not
  // ahead of the team that earned its EXP.
  const offer = createAtExp(species, `stray-${seed}`, baselineExp(run.badges));
  const roomInLineUp = run.lineUp.length < MAX_PARTY_SIZE;

  return {
    kind: 'StrayFriend',
    title: 'It Followed You',
    speaker: species.name,
    body:
      `A ${species.name} has been trailing you for the last mile and is no longer pretending ` +
      'otherwise. It sits down when you do.',
    seed,
    foes: [],
    offer,
    tradeAwayId: null,
    choices: [
      {
        label: `Let ${species.name} come along`,
        detail: roomInLineUp
          ? 'It joins the line-up at the back, already worth its slot.'
          : 'Line-up is full, so it waits in the Box.',
        available: true,
      },
      {
        label: 'Send it home',
        detail: `It digs up something shiny first. +$${STRAY_GIFT}.`,
        available: true,
      },
    ],
  };
}

function resolveStray(event: RoadEvent, index: number, run: RunState): RoadEventOutcome {
  if (index !== 0 || event.offer === null) {
    return outcome(
      addMoney(run, STRAY_GIFT),
      `It watches you go, then trots off with something in its mouth — but not before dropping $${STRAY_GIFT} at your feet.`,
    );
  }

  const next = addCaught(run, event.offer, true);
  const joined = next.lineUp.some((m) => m.instanceId === event.offer!.instanceId);
  return outcome(
    next,
    `${displayNameOf(event.offer)} joins you${joined ? ', at the back of the line-up' : ' and waits in the Box'}.`,
  );
}

// --- Day Care ----------------------------------------------------------------------------------

function buildDayCare(run: RunState, seed: number): RoadEvent {
  const candidate = leastGrown(run);
  const best = [...run.lineUp, ...run.box].reduce((max, m) => Math.max(max, m.exp), 0);
  const gain = candidate === null ? 0 : Math.max(0, best - candidate.exp);

  return {
    kind: 'DayCare',
    title: 'The Day-Care Couple',
    speaker: 'Day-Care Couple',
    body:
      'A low fence, a hand-painted sign, and two people who have clearly done this for forty ' +
      `years. "Leave us the one that's behind," they say. "We'll have it caught up by supper."`,
    seed,
    foes: [],
    offer: candidate,
    tradeAwayId: candidate?.instanceId ?? null,
    choices: [
      {
        label:
          candidate === null
            ? 'Leave a Pokémon with them'
            : `Leave ${nameOfInstance(candidate)} with them ($${DAYCARE_FEE})`,
        detail:
          gain > 0
            ? `It comes back at ${best} EXP — +${gain}, level with your best. Evolutions included.`
            : 'Nothing of yours is behind; there is nothing for them to fix.',
        available: candidate !== null && gain > 0 && run.money >= DAYCARE_FEE,
      },
      {
        label: 'Stay for tea instead',
        detail: `Forty years of advice. +${DAYCARE_CHAT_EXP} EXP to everyone in the line-up.`,
        available: run.lineUp.length > 0,
      },
    ],
  };
}

function resolveDayCare(event: RoadEvent, index: number, run: RunState): RoadEventOutcome {
  if (index !== 0 || event.tradeAwayId === null) {
    const granted = grantWinExp(run, DAYCARE_CHAT_EXP);
    return outcome(
      granted.run,
      `Two cups of tea and a lot of opinions. Everyone comes away +${DAYCARE_CHAT_EXP} EXP.`,
      { report: granted.report },
    );
  }

  const best = [...run.lineUp, ...run.box].reduce((max, m) => Math.max(max, m.exp), 0);
  const paid = addMoney(run, -DAYCARE_FEE);
  const before = [...paid.lineUp, ...paid.box].find((m) => m.instanceId === event.tradeAwayId) ?? null;
  const raised = grantExpTo(paid, event.tradeAwayId, Math.max(0, best - (before?.exp ?? 0)));
  const after = [...raised.run.lineUp, ...raised.run.box].find((m) => m.instanceId === event.tradeAwayId) ?? null;

  return outcome(
    raised.run,
    `You come back at dusk. ${nameOfInstance(after)} is level with the rest of them, at ${after?.exp ?? best} EXP.`,
    { report: raised.report },
  );
}

// --- Ancient Shrine ----------------------------------------------------------------------------

function buildShrine(run: RunState, location: Location, seed: number): RoadEvent {
  const lead = run.lineUp[0] ?? null;
  return {
    kind: 'AncientShrine',
    title: 'Roadside Shrine',
    speaker: location.name,
    body:
      'Someone built this a very long time ago and someone else has been sweeping it ever since. ' +
      'There are coins in the bowl at its base, and room for more.',
    seed,
    foes: [],
    offer: lead,
    tradeAwayId: lead?.instanceId ?? null,
    choices: [
      {
        label: lead === null ? 'Make an offering' : `Make an offering for ${nameOfInstance(lead)} ($${SHRINE_OFFERING})`,
        detail: `+${SHRINE_EXP} EXP to your Lead alone. The steepest single-mon growth in the game.`,
        available: lead !== null && run.money >= SHRINE_OFFERING,
      },
      {
        label: 'Take the coins from the bowl',
        detail: `+$${SHRINE_COINS}, and whatever you think about yourself afterwards.`,
        available: true,
      },
    ],
  };
}

function resolveShrine(index: number, run: RunState): RoadEventOutcome {
  const lead = run.lineUp[0] ?? null;
  if (index !== 0 || lead === null) {
    return outcome(addMoney(run, SHRINE_COINS), `You take the $${SHRINE_COINS} and do not look back at it.`);
  }

  const granted = grantExpTo(addMoney(run, -SHRINE_OFFERING), lead.instanceId, SHRINE_EXP);
  const after = granted.run.lineUp[0] ?? lead;
  return outcome(
    granted.run,
    `The coins go in the bowl. ${nameOfInstance(after)} comes away +${SHRINE_EXP} EXP and standing a little straighter.`,
    { report: granted.report },
  );
}

// --- Game Corner -------------------------------------------------------------------------------

function buildGameCorner(run: RunState, seed: number): RoadEvent {
  return {
    kind: 'GameCorner',
    title: 'Game Corner',
    speaker: 'Game Corner',
    body:
      'A roadside Game Corner hums with lights and jingles at nobody. One machine is blinking, and ' +
      'a few coins glint on the floor underneath it.',
    seed,
    foes: [],
    offer: null,
    tradeAwayId: null,
    choices: [
      {
        label: `Play the slots ($${SLOTS_STAKE})`,
        detail: `${SLOTS_WIN_PERCENT}% to win $${SLOTS_PRIZE}. The only coin flip on this screen.`,
        available: run.money >= SLOTS_STAKE,
      },
      {
        label: 'Pocket the loose coins',
        detail: `+$${LOOSE_COINS}, no risk, nobody looking.`,
        available: true,
      },
    ],
  };
}

function resolveGameCorner(index: number, run: RunState, rng: Rng): RoadEventOutcome {
  if (index !== 0) {
    return outcome(addMoney(run, LOOSE_COINS), `Nobody is looking. You pocket $${LOOSE_COINS}.`);
  }

  const staked = addMoney(run, -SLOTS_STAKE);
  if (rng.nextInt(100) < SLOTS_WIN_PERCENT) {
    return outcome(addMoney(staked, SLOTS_PRIZE), `7 — 7 — 7. The machine pours out $${SLOTS_PRIZE}.`);
  }
  return outcome(staked, `Cherry, bar, Voltorb. The machine keeps your $${SLOTS_STAKE}.`);
}

// --- Team Rocket -------------------------------------------------------------------------------

function buildRocket(run: RunState, seed: number, rng: Rng): RoadEvent {
  const cap = maxTier(run.badges);
  const legal = baseForms().filter((s) => !s.isLegendary && (cap === null || s.tier <= cap));
  const themed = legal.filter((s) => s.types.some((t) => ROCKET_TYPES.includes(t)));
  const pool = themed.length > 0 ? themed : legal;

  const foes = Array.from({ length: ROCKET_GRUNT_COUNT }, (_, i) =>
    createAtExp(pool[rng.nextInt(pool.length)]!, `rocket-${seed}-${i}`, baselineExp(run.badges)),
  );

  return {
    kind: 'RocketShakedown',
    title: 'Shakedown',
    speaker: 'Team Rocket',
    body:
      '"Prepare for trouble." Two Rocket grunts step out of the treeline, and a third is very ' +
      'obviously behind you. Their bag of stolen goods sits just out of reach.',
    seed,
    foes,
    offer: null,
    tradeAwayId: null,
    choices: [
      {
        label: 'Fight them off',
        detail: `Win: $${ROCKET_PURSE} and a ${ballName(ROCKET_PURSE_BALL)} from the bag. Lose: 1 morale.`,
        available: true,
      },
      {
        label: `Pay the toll (lose $${toll(run)})`,
        detail: 'Half of what you are carrying, rounded up. Nothing else is touched.',
        available: run.money > 0,
      },
    ],
  };
}

/** Team Rocket's toll: half the run's money, rounded up. */
export const toll = (run: RunState): number => Math.ceil(Math.max(0, run.money) / 2);

function resolveRocket(event: RoadEvent, index: number, run: RunState): RoadEventOutcome {
  if (index === 0) {
    return outcome(run, 'They send out their Pokémon before you finish answering.', {
      foes: event.foes,
      bounty: { money: ROCKET_PURSE, ball: ROCKET_PURSE_BALL, ballCount: 1 },
    });
  }

  const paid = toll(run);
  return outcome(addMoney(run, -paid), `They count out your $${paid} and are gone before you look up.`);
}
