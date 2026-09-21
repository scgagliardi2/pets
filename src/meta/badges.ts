/**
 * Badges, and the permanent trainer buffs a new region offers.
 *
 * Both are placeholders in the sense that matters: the *effects* are deliberately simple and
 * unbalanced, because they exist so the choice is in the game and can be felt, not because these
 * are the right three buffs. The shape — three region-flavoured options, picked once on arrival,
 * permanent for the run — is the part meant to survive.
 */


import type { RegionArt } from './locations.js';

export interface Badge {
  readonly id: string;
  readonly name: string;
  /** The emoji stands in until badge art exists. */
  readonly glyph: string;
  readonly leader: string;
  /** What the leader says when you beat them. */
  readonly quote: string;
}

/** A permanent, run-long effect the player picks on entering a region. */
export type BuffEffect =
  /** Flat bonus to every mon in the line-up, applied when a battle is built. */
  | { kind: 'teamAttack'; amount: number }
  | { kind: 'teamHealth'; amount: number }
  | { kind: 'teamSpecial'; amount: number }
  /** Every mon starts each battle with this much charge already banked. */
  | { kind: 'startingCharge'; amount: number }
  /** Flat bonus to catch odds, as a fraction. */
  | { kind: 'catchRate'; amount: number }
  /** Extra money from every win. */
  | { kind: 'income'; amount: number }
  /** Extra EXP from every win. */
  | { kind: 'scholar'; amount: number };

export interface TrainerBuff {
  readonly id: string;
  readonly name: string;
  readonly blurb: string;
  readonly effect: BuffEffect;
}

/**
 * Three buffs per region, flavoured to the place.
 *
 * Three rather than a longer list so the choice can be read in a couple of seconds, and so no
 * option is obviously dominant by being one of many.
 */
export const REGION_BUFFS: Readonly<Record<RegionArt, readonly TrainerBuff[]>> = {
  forest: [
    {
      id: 'forest-forager',
      name: "Forager's Eye",
      blurb: 'You spot things others walk past. +1 money from every win.',
      effect: { kind: 'income', amount: 1 },
    },
    {
      id: 'forest-quiet-step',
      name: 'Quiet Step',
      blurb: 'You get closer before they notice. +8% catch rate.',
      effect: { kind: 'catchRate', amount: 0.08 },
    },
    {
      id: 'forest-deep-roots',
      name: 'Deep Roots',
      blurb: 'Your team is hardier here. +2 Health to every mon.',
      effect: { kind: 'teamHealth', amount: 2 },
    },
  ],
  ocean: [
    {
      id: 'ocean-tidal-pull',
      name: 'Tidal Pull',
      blurb: 'Your team starts every fight part-charged. +1 starting charge.',
      effect: { kind: 'startingCharge', amount: 1 },
    },
    {
      id: 'ocean-salvage',
      name: 'Salvage Rights',
      blurb: 'The sea gives things back. +2 money from every win.',
      effect: { kind: 'income', amount: 2 },
    },
    {
      id: 'ocean-deep-breath',
      name: 'Deep Breath',
      blurb: 'Abilities hit harder. +1 Special to every mon.',
      effect: { kind: 'teamSpecial', amount: 1 },
    },
  ],
  town: [
    {
      id: 'town-apprentice',
      name: 'Apprenticeship',
      blurb: 'Somebody is teaching you. +1 EXP from every win.',
      effect: { kind: 'scholar', amount: 1 },
    },
    {
      id: 'town-market-day',
      name: 'Market Day',
      blurb: 'You know who to talk to. +2 money from every win.',
      effect: { kind: 'income', amount: 2 },
    },
    {
      id: 'town-sparring',
      name: 'Sparring Partners',
      blurb: 'Constant practice. +1 Attack to every mon.',
      effect: { kind: 'teamAttack', amount: 1 },
    },
  ],
  ruins: [
    {
      id: 'ruins-old-words',
      name: 'Old Words',
      blurb: 'Something in the carvings sticks. +2 Special to every mon.',
      effect: { kind: 'teamSpecial', amount: 2 },
    },
    {
      id: 'ruins-relic-hunter',
      name: 'Relic Hunter',
      blurb: 'You know where to dig. +1 EXP from every win.',
      effect: { kind: 'scholar', amount: 1 },
    },
    {
      id: 'ruins-warded',
      name: 'Warded',
      blurb: 'The old protections still hold. +3 Health to every mon.',
      effect: { kind: 'teamHealth', amount: 3 },
    },
  ],
  cave: [
    {
      id: 'cave-dark-adapted',
      name: 'Dark-Adapted',
      blurb: 'You move first down here. +1 starting charge.',
      effect: { kind: 'startingCharge', amount: 1 },
    },
    {
      id: 'cave-prospector',
      name: 'Prospector',
      blurb: 'The walls are worth something. +2 money from every win.',
      effect: { kind: 'income', amount: 2 },
    },
    {
      id: 'cave-narrow-ways',
      name: 'Narrow Ways',
      blurb: 'Nothing escapes a dead end. +12% catch rate.',
      effect: { kind: 'catchRate', amount: 0.12 },
    },
  ],
  mountain: [
    {
      id: 'mountain-thin-air',
      name: 'Thin Air',
      blurb: 'Your team is conditioned. +3 Health to every mon.',
      effect: { kind: 'teamHealth', amount: 3 },
    },
    {
      id: 'mountain-sure-footed',
      name: 'Sure-Footed',
      blurb: 'You never waste a step. +1 starting charge.',
      effect: { kind: 'startingCharge', amount: 1 },
    },
    {
      id: 'mountain-summit-nerve',
      name: 'Summit Nerve',
      blurb: 'Hitting harder is the only way down. +2 Attack to every mon.',
      effect: { kind: 'teamAttack', amount: 2 },
    },
  ],
  volcano: [
    {
      id: 'volcano-forge-tempered',
      name: 'Forge-Tempered',
      blurb: 'Everything here is sharpened. +2 Attack to every mon.',
      effect: { kind: 'teamAttack', amount: 2 },
    },
    {
      id: 'volcano-heat-haze',
      name: 'Heat Haze',
      blurb: 'They cannot see you coming. +10% catch rate.',
      effect: { kind: 'catchRate', amount: 0.1 },
    },
    {
      id: 'volcano-ashfall',
      name: 'Ashfall',
      blurb: 'Abilities come faster and harder. +2 Special to every mon.',
      effect: { kind: 'teamSpecial', amount: 2 },
    },
  ],
  desert: [
    {
      id: 'desert-water-discipline',
      name: 'Water Discipline',
      blurb: 'Nothing is wasted. +4 Health to every mon.',
      effect: { kind: 'teamHealth', amount: 4 },
    },
    {
      id: 'desert-caravan-routes',
      name: 'Caravan Routes',
      blurb: 'You travel with people who trade. +3 money from every win.',
      effect: { kind: 'income', amount: 3 },
    },
    {
      id: 'desert-long-march',
      name: 'Long March',
      blurb: 'Endurance pays. +2 EXP from every win.',
      effect: { kind: 'scholar', amount: 2 },
    },
  ],
};

export const buffsFor = (region: RegionArt): readonly TrainerBuff[] => REGION_BUFFS[region];

export const buffById = (id: string): TrainerBuff | null => {
  for (const list of Object.values(REGION_BUFFS)) {
    const found = list.find((b) => b.id === id);
    if (found !== undefined) return found;
  }
  return null;
};

/** The total of one kind of buff across everything the player has picked. */
export function buffTotal(buffIds: readonly string[], kind: BuffEffect['kind']): number {
  let total = 0;
  for (const id of buffIds) {
    const buff = buffById(id);
    if (buff !== null && buff.effect.kind === kind) total += buff.effect.amount;
  }
  return total;
}

/** The badge each Gym awards. Parallel to LOCATIONS. */
export const BADGES: readonly Badge[] = [
  {
    id: 'thicket',
    name: 'Thicket Badge',
    glyph: '🍃',
    leader: 'Hollis',
    quote: 'You read the swarm better than I did. Take it.',
  },
  {
    id: 'tide',
    name: 'Tide Badge',
    glyph: '🌊',
    leader: 'Marin',
    quote: 'The water never yields. You did not either.',
  },
  {
    id: 'current',
    name: 'Current Badge',
    glyph: '⚡',
    leader: 'Volt',
    quote: 'Fast hands. Faster team. Go on, then.',
  },
  {
    id: 'bramble',
    name: 'Bramble Badge',
    glyph: '🌿',
    leader: 'Thistle',
    quote: 'Everything here grows back. So will I. Well fought.',
  },
  {
    id: 'rust',
    name: 'Rust Badge',
    glyph: '☣️',
    leader: 'Slag',
    quote: 'Most trainers turn back at the works. You went through.',
  },
  {
    id: 'mirror',
    name: 'Mirror Badge',
    glyph: '🔮',
    leader: 'Oleander',
    quote: 'I saw three ways this ended. You found the fourth.',
  },
  {
    id: 'cinder',
    name: 'Cinder Badge',
    glyph: '🔥',
    leader: 'Ember',
    quote: 'You held your nerve on burning ground. That is the whole test.',
  },
  {
    id: 'wyrm',
    name: 'Wyrm Badge',
    glyph: '🐉',
    leader: 'Sable',
    quote: 'Nothing small lives up here. Neither, it turns out, do you.',
  },
];

export const badgeFor = (index: number): Badge =>
  BADGES[Math.max(0, Math.min(index, BADGES.length - 1))]!;
