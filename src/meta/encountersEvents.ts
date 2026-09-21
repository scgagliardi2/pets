/**
 * Road encounters: the non-combat nodes.
 *
 * Slay the Spire's "?" rooms. An encounter presents a short piece of fiction and two or three
 * choices, and each choice resolves to an effect on the run — a mon, a stat, an item, money, a
 * fight, or nothing much.
 *
 * **Usually positive, occasionally a gamble, never a trap.** The purpose is to vary the texture of
 * a Location and to give the player a decision that isn't "which fight", not to punish curiosity.
 * Where an outcome can go badly it says so in the choice text, so a bad result is a risk taken
 * rather than a rug pulled.
 *
 * Effects are *declared*, not executed here. This module stays pure and knows nothing about the
 * store; the store reads the declaration and applies it. That keeps the whole catalogue testable
 * by reading it, and stops an encounter reaching into run state in its own bespoke way.
 */

import type { PokemonType } from '../sim/index.js';
import type { BallTier } from './balls.js';

/** What taking a choice does. One effect per choice, deliberately — a choice should be legible. */
export type EncounterEffect =
  | { kind: 'money'; amount: number }
  | { kind: 'balls'; tier: BallTier; count: number }
  /** A held item. A specific one when named, otherwise drawn from the shop pool. */
  | { kind: 'item'; itemId?: string }
  /** A free mon, drawn from the current Location's pool at the given tier offset. */
  | { kind: 'gift'; tierOffset: number }
  /** Give up your weakest line-up mon; receive a stronger one. */
  | { kind: 'trade'; tierOffset: number }
  /** Free EXP to every mon in the line-up, spendable as normal. */
  | { kind: 'exp'; points: number }
  /** A fight against a legendary. Winning gives the usual rewards; it is catchable. */
  | { kind: 'legendary' }
  /** A coin flip between two effects, stated up front. */
  | { kind: 'gamble'; onWin: EncounterEffect; onLose: EncounterEffect; winChance: number }
  | { kind: 'nothing' };

export interface EncounterChoice {
  readonly label: string;
  /** What the player is told will happen. Never lies, even about a gamble. */
  readonly detail: string;
  readonly effect: EncounterEffect;
}

export interface Encounter {
  readonly id: string;
  readonly title: string;
  readonly body: string;
  readonly choices: readonly EncounterChoice[];
  /** Restricts this encounter to Locations biased toward one of these types, if set. */
  readonly typeAffinity?: readonly PokemonType[];
  /** Not offered before this badge count. Used to hold the legendary back. */
  readonly minBadges?: number;
}

export const ENCOUNTERS: readonly Encounter[] = [
  {
    id: 'wandering-breeder',
    title: 'A Wandering Breeder',
    body: 'She has a crate of eggs and an eye for a trainer who looks like they need one.',
    choices: [
      {
        label: 'Take the egg',
        detail: 'A new Pokémon joins your Box.',
        effect: { kind: 'gift', tierOffset: 0 },
      },
      {
        label: 'Ask about the rare one',
        detail: 'She will swap it for your weakest — and it is a tier stronger.',
        effect: { kind: 'trade', tierOffset: 1 },
      },
    ],
  },
  {
    id: 'abandoned-pack',
    title: 'An Abandoned Pack',
    body: 'Left by the path, half-buried. Nobody has come back for it in a while.',
    choices: [
      {
        label: 'Take the balls',
        detail: 'Two Great Balls.',
        effect: { kind: 'balls', tier: 'Great', count: 2 },
      },
      {
        label: 'Take the coin purse',
        detail: '$8.',
        effect: { kind: 'money', amount: 8 },
      },
    ],
  },
  {
    id: 'old-trainer',
    title: 'An Old Trainer',
    body: 'He watches your team walk past and says he can teach them something.',
    choices: [
      {
        label: 'Train with him',
        detail: 'Every mon in your line-up earns 2 EXP.',
        effect: { kind: 'exp', points: 2 },
      },
      {
        label: 'Ask what he carries',
        detail: 'He sells you an Ultra Ball cheap. $3.',
        effect: { kind: 'money', amount: -3 },
      },
      {
        label: 'Walk on',
        detail: 'Nothing happens.',
        effect: { kind: 'nothing' },
      },
    ],
  },
  {
    id: 'shrine',
    title: 'A Weathered Shrine',
    body: 'Someone has left offerings. The stone underneath is warm.',
    choices: [
      {
        label: 'Leave an offering',
        detail: 'Costs $5. Your line-up earns 3 EXP.',
        effect: { kind: 'exp', points: 3 },
      },
      {
        label: 'Take what is there',
        detail: 'Probably $10. It might be nothing at all.',
        effect: {
          kind: 'gamble',
          winChance: 0.7,
          onWin: { kind: 'money', amount: 10 },
          onLose: { kind: 'nothing' },
        },
      },
    ],
  },
  {
    id: 'hidden-hollow',
    title: 'A Hidden Hollow',
    body: 'Something is asleep in there. It has not noticed you yet.',
    choices: [
      {
        label: 'Back away quietly',
        detail: 'You find a few balls on the way out. Three Poké Balls.',
        effect: { kind: 'balls', tier: 'Poke', count: 3 },
      },
      {
        label: 'Reach in',
        detail: 'Usually a strong Pokémon. Sometimes it wakes up angry.',
        effect: {
          kind: 'gamble',
          winChance: 0.65,
          onWin: { kind: 'gift', tierOffset: 1 },
          onLose: { kind: 'money', amount: -4 },
        },
      },
    ],
  },
  {
    id: 'roadside-peddler',
    title: 'A Roadside Peddler',
    body: 'His cart is mostly junk. Mostly.',
    choices: [
      {
        label: 'Buy the odd-looking one',
        detail: 'Costs $4. You get a held item.',
        effect: { kind: 'item' },
      },
      {
        label: 'Take the free sample',
        detail: 'A Lum Berry. Cures one status per battle.',
        effect: { kind: 'item', itemId: 'lum-berry' },
      },
    ],
  },
  {
    id: 'collectors-cache',
    title: "A Collector's Cache",
    body: 'Somebody buried this and never came back for it.',
    choices: [
      {
        label: 'Take the whole thing',
        detail: 'A held item, and you keep looking.',
        effect: { kind: 'item' },
      },
      {
        label: 'Sell it on',
        detail: '$9.',
        effect: { kind: 'money', amount: 9 },
      },
    ],
  },
  {
    id: 'the-challenger',
    title: 'A Challenger',
    body: 'She blocks the path with her arms folded. "One battle. Then you pass."',
    choices: [
      {
        label: 'Accept',
        detail: 'A hard fight. Your line-up earns 3 EXP for taking it.',
        effect: { kind: 'exp', points: 3 },
      },
      {
        label: 'Pay her off',
        detail: '$6 and she steps aside.',
        effect: { kind: 'money', amount: -6 },
      },
    ],
  },
  {
    id: 'legendary-stirring',
    title: 'Something Stirring',
    body: 'The air here is wrong. Whatever is coming is much larger than you.',
    minBadges: 3,
    choices: [
      {
        label: 'Stand your ground',
        detail: 'Fight a legendary. It can be caught.',
        effect: { kind: 'legendary' },
      },
      {
        label: 'Leave, quickly',
        detail: 'Nothing happens. You keep your morale.',
        effect: { kind: 'nothing' },
      },
    ],
  },
  {
    id: 'fishermans-luck',
    title: "A Fisherman's Luck",
    body: 'He has been out since dawn and has more than he can carry home.',
    typeAffinity: ['Water'],
    choices: [
      {
        label: 'Help him haul it in',
        detail: 'He gives you one of the catch.',
        effect: { kind: 'gift', tierOffset: 0 },
      },
      {
        label: 'Buy the good one',
        detail: '$7 for a Pokémon a tier above what is around here.',
        effect: { kind: 'gift', tierOffset: 1 },
      },
    ],
  },
];

/** Encounters legal at this point in a run, in a Location with this type bias. */
export function eligibleEncounters(
  badges: number,
  typeBias: readonly PokemonType[],
): Encounter[] {
  return ENCOUNTERS.filter((e) => {
    if (e.minBadges !== undefined && badges < e.minBadges) return false;
    if (e.typeAffinity !== undefined) {
      return e.typeAffinity.some((t) => typeBias.includes(t));
    }
    return true;
  });
}

/**
 * Picks the encounter for a node.
 *
 * Seeded on the node, so a node shows the same encounter every time it is looked at — an
 * encounter that re-rolled when you changed your mind would make the choice meaningless.
 */
export function encounterFor(
  seed: number,
  badges: number,
  nodeId: string,
  typeBias: readonly PokemonType[],
): Encounter {
  const pool = eligibleEncounters(badges, typeBias);
  let hash = 2166136261 >>> 0;
  const key = `${seed}:${badges}:${nodeId}`;
  for (let i = 0; i < key.length; i++) {
    hash = Math.imul(hash ^ key.charCodeAt(i), 16777619) >>> 0;
  }
  return pool[hash % pool.length] ?? ENCOUNTERS[0]!;
}

/** Plain text of what an effect did, for the log after it resolves. */
export function describeEffect(effect: EncounterEffect): string {
  switch (effect.kind) {
    case 'money':
      return effect.amount >= 0 ? `You gain $${effect.amount}.` : `You spend $${-effect.amount}.`;
    case 'balls':
      return `You gain ${effect.count} ${effect.tier} Ball${effect.count === 1 ? '' : 's'}.`;
    case 'item':
      return 'You gain an item.';
    case 'gift':
      return 'A Pokémon joins you.';
    case 'trade':
      return 'You trade your weakest away for something better.';
    case 'exp':
      return `Everyone in your line-up earns ${effect.points} EXP.`;
    case 'legendary':
      return 'A legendary appears.';
    case 'gamble':
      return 'You take the chance.';
    case 'nothing':
      return 'Nothing comes of it.';
  }
}
