/**
 * Held items.
 *
 * Each maps to a real Pokémon item wherever the mechanic has an honest counterpart, because a
 * player who knows the games should be able to guess what something does from its name. Where the
 * fit is loose it is noted rather than papered over.
 *
 * A mon holds at most one. Everything here is *declarative* — an item says what it is, and the
 * layers that care (the combatant factory for stats and typing, the Step loop for in-battle
 * behaviour, the run store for between-battle rewards) read it. No item contains logic.
 */

import { POKEMON_TYPES, type PokemonType } from '../sim/index.js';
import type { GrowableStat } from './statGrowth.js';

export type ItemKind =
  /** Cures a status the moment it lands, then is spent for the rest of the battle. */
  | 'cureStatus'
  /** Overrides the holder's typing entirely. */
  | 'setType'
  /** Heals a little at the end of every Step. */
  | 'regen'
  /** Heals once, the first time the holder drops below half health. */
  | 'lastStand'
  /** A flat bonus to one stat while held. */
  | 'flatStat'
  /** Adds to one stat permanently after every battle the holder fought in. */
  | 'training'
  /** Extra EXP toward evolving from every win. */
  | 'exp';

export interface Item {
  readonly id: string;
  readonly name: string;
  readonly kind: ItemKind;
  readonly blurb: string;
  /** How much, for the kinds that take a number. */
  readonly amount?: number;
  /** Which stat, for `flatStat` and `training`. */
  readonly stat?: GrowableStat;
  /** Which type, for `setType`. */
  readonly type?: PokemonType;
  /** Roughly what it is worth, for shop pricing. */
  readonly cost: number;
}

/**
 * The type-setting items.
 *
 * Arceus's Plates, which is exactly what they do in canon — one per type, and holding one makes
 * Arceus that type. Normal is the exception: Arceus holds nothing to stay Normal, so that slot
 * uses the Silk Scarf, the Normal-type item.
 *
 * These matter more here than in the real games, because typing drives the team synergies rather
 * than a damage chart — a Plate is a way to buy into a synergy you are one mon short of.
 */
const PLATE_NAMES: Readonly<Record<PokemonType, string>> = {
  Normal: 'Silk Scarf',
  Fire: 'Flame Plate',
  Water: 'Splash Plate',
  Electric: 'Zap Plate',
  Grass: 'Meadow Plate',
  Ice: 'Icicle Plate',
  Fighting: 'Fist Plate',
  Poison: 'Toxic Plate',
  Ground: 'Earth Plate',
  Flying: 'Sky Plate',
  Psychic: 'Mind Plate',
  Bug: 'Insect Plate',
  Rock: 'Stone Plate',
  Ghost: 'Spooky Plate',
  Dragon: 'Draco Plate',
  Dark: 'Dread Plate',
  Steel: 'Iron Plate',
  Fairy: 'Pixie Plate',
};

const plates: Item[] = POKEMON_TYPES.map((type) => ({
  id: `plate-${type.toLowerCase()}`,
  name: PLATE_NAMES[type],
  kind: 'setType',
  type,
  blurb: `The holder becomes ${type}-type, replacing whatever it was.`,
  cost: 7,
}));

/**
 * Flat stat items: the Vitamins.
 *
 * A liberty — in canon these are one-use consumables, not held items — but the mapping from
 * vitamin to stat is the most recognisable one the games have, and each of the four maps cleanly
 * onto one of our four stats.
 */
const vitamins: { id: string; name: string; stat: GrowableStat; amount: number }[] = [
  { id: 'hp-up', name: 'HP Up', stat: 'health', amount: 8 },
  { id: 'protein', name: 'Protein', stat: 'attack', amount: 3 },
  { id: 'calcium', name: 'Calcium', stat: 'special', amount: 3 },
  { id: 'carbos', name: 'Carbos', stat: 'speed', amount: 6 },
];

/**
 * Per-battle growth items: the Power items.
 *
 * The closest thing to a literal match in the whole catalogue — in canon these grant extra EVs in
 * one stat for every battle fought, which is exactly "a small permanent boost after every
 * battle". Power Weight is HP, Bracer Attack, Lens Special Attack, Anklet Speed.
 */
const powerItems: { id: string; name: string; stat: GrowableStat }[] = [
  { id: 'power-weight', name: 'Power Weight', stat: 'health' },
  { id: 'power-bracer', name: 'Power Bracer', stat: 'attack' },
  { id: 'power-lens', name: 'Power Lens', stat: 'special' },
  { id: 'power-anklet', name: 'Power Anklet', stat: 'speed' },
];

export const ITEMS: readonly Item[] = [
  {
    id: 'lum-berry',
    name: 'Lum Berry',
    kind: 'cureStatus',
    blurb: 'Cures a status the moment it lands. One use per battle.',
    cost: 5,
  },
  {
    id: 'leftovers',
    name: 'Leftovers',
    kind: 'regen',
    amount: 2,
    blurb: 'Restores 2 health at the end of every Step.',
    cost: 9,
  },
  {
    id: 'sitrus-berry',
    name: 'Sitrus Berry',
    kind: 'lastStand',
    amount: 12,
    blurb: 'Restores 12 health the first time the holder drops below half. Once per battle.',
    cost: 7,
  },
  {
    id: 'lucky-egg',
    name: 'Lucky Egg',
    kind: 'exp',
    amount: 1,
    blurb: '+1 EXP toward evolving from every win.',
    cost: 8,
  },
  ...vitamins.map(
    (v): Item => ({
      id: v.id,
      name: v.name,
      kind: 'flatStat',
      stat: v.stat,
      amount: v.amount,
      blurb: `+${v.amount} ${v.stat} while held.`,
      cost: 6,
    }),
  ),
  ...powerItems.map(
    (p): Item => ({
      id: p.id,
      name: p.name,
      kind: 'training',
      stat: p.stat,
      amount: 1,
      blurb: `+1 ${p.stat} permanently after every battle the holder fights in.`,
      cost: 10,
    }),
  ),
  ...plates,
];

const byId = new Map(ITEMS.map((i) => [i.id, i]));

export const itemById = (id: string | null | undefined): Item | null =>
  id === null || id === undefined ? null : (byId.get(id) ?? null);

/** Items a shop may stock, weighted only by exclusion: Plates are common enough to crowd out. */
export const shopItemPool = (): Item[] => ITEMS.filter((i) => i.kind !== 'setType');

/** Plates, offered separately so eighteen of them can't flood a three-slot shelf. */
export const plateItems = (): Item[] => ITEMS.filter((i) => i.kind === 'setType');

/**
 * What a held item does to a combatant's *stats and typing*, as opposed to its behaviour during a
 * battle. Returned as plain data so the factory can apply it without knowing about items.
 */
export interface ItemStatEffects {
  readonly attack: number;
  readonly health: number;
  readonly special: number;
  readonly speed: number;
  /** Replaces the holder's typing entirely when set. */
  readonly overrideTypes: readonly PokemonType[] | null;
}

export function statEffectsOf(item: Item | null): ItemStatEffects {
  const none: ItemStatEffects = {
    attack: 0,
    health: 0,
    special: 0,
    speed: 0,
    overrideTypes: null,
  };
  if (item === null) return none;

  if (item.kind === 'setType' && item.type !== undefined) {
    return { ...none, overrideTypes: [item.type] };
  }
  if (item.kind === 'flatStat' && item.stat !== undefined) {
    return { ...none, [item.stat]: item.amount ?? 0 };
  }
  return none;
}
