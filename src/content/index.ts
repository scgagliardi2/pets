/**
 * The content layer: the curated roster, the passive definitions, and the lookups over them.
 *
 * `species.json` and `passives.json` are **generated**, not hand-authored — `scripts/build_species.py`
 * derives them from the Unity repo's source data (the roster spreadsheet, the PokeAPI id cache and
 * the evolution chains) using the same rules the Unity importer uses, then validates every derived
 * value against the 183 Unity species assets. Don't edit them by hand; change the rule and re-run
 * the script.
 *
 * Like `/src/sim`, nothing here imports React.
 */

import type { PassiveDefinition, PokemonType, Stats } from '../sim/index.js';
import { POKEMON_TYPES } from '../sim/index.js';

import speciesData from './species.json' with { type: 'json' };
import passivesData from './passives.json' with { type: 'json' };

/** One curated species, as the generator emits it. */
export interface Species {
  /** National dex id. */
  readonly id: number;
  readonly name: string;
  readonly types: readonly PokemonType[];
  /** 1-6. A stat *budget*, not a level. */
  readonly tier: number;
  readonly baseAttack: number;
  readonly baseHealth: number;
  readonly baseSpeed: number;
  /** The magnitude of this species' ability. Derived from its real Special Attack. */
  readonly baseSpecial: number;
  /**
   * How likely a point of EXP goes to Health rather than Attack, 0-100. The species' real Health
   * share rescaled above a 50% floor, so every mon favours Health at least evenly — except the
   * one species the real games give a single hit point, which never gains Health at all.
   */
  readonly healthGrowthPercent: number;
  readonly passiveId: string | null;
  /** 0 for a base form. */
  readonly evolutionStage: number;
  readonly evolvesIntoId: number | null;
  readonly isLegendary: boolean;
}

/** A passive, as authored. `magnitudeByStage` is every entry `[1]` today — nothing scales yet. */
export interface PassiveContent extends PassiveDefinition {
  readonly description: string;
  readonly magnitudeByStage: readonly number[];
}

/**
 * Speed every species starts at, overriding the tier-derived 1-3 value.
 *
 * Applied at derivation rather than baked into `species.json`, like the Health multiplier, so the
 * generated roster still validates against the Unity assets number for number.
 *
 * Flat for every species for now, deliberately: the old 1-3 spread was calibrated to a charge
 * threshold of 3 and means nothing against 100. Re-deriving a per-species Speed from the real
 * base-stat spread is a balance job, and doing it badly is worse than starting everyone level.
 *
 * Lives here rather than in `statGrowth` — which is where it belongs conceptually — because
 * `statGrowth` already imports from this module, and putting it there would close an import
 * cycle that ESM resolves by handing one side `undefined` at module-init time.
 */
export const BASE_SPEED = 10;

export const SPECIES: readonly Species[] = speciesData as readonly Species[];
export const PASSIVES: readonly PassiveContent[] = passivesData as readonly PassiveContent[];

/**
 * What a mon does when its charge fills, if nothing else is specified: damage to the enemy Lead
 * equal to its Special.
 *
 * The magnitude is not written here — `scalesWithSpecial` tells the simulator to read it off the
 * acting mon when the ability fires. An authored ability that shields, heals or inflicts a status
 * simply does not carry that flag, and so replaces this rather than stacking with it.
 */
export const DEFAULT_ABILITY: PassiveContent = {
  id: 'default-special-strike',
  displayName: 'Focus Strike',
  description: "Strikes the foe's Lead for damage equal to this Pokémon's Special.",
  effects: [{ type: 'DealDamage', target: 'EnemyLead', amount: 0, scalesWithSpecial: true }],
  magnitudeByStage: [1],
};

const speciesById = new Map<number, Species>(SPECIES.map((s) => [s.id, s]));
const speciesByName = new Map<string, Species>(SPECIES.map((s) => [s.name.toLowerCase(), s]));
const passiveById = new Map<string, PassiveContent>(PASSIVES.map((p) => [p.id, p]));

export function speciesOf(id: number): Species | null {
  return speciesById.get(id) ?? null;
}

/** Case-insensitive lookup by display name. */
export function speciesNamed(name: string): Species | null {
  return speciesByName.get(name.toLowerCase()) ?? null;
}

export function passiveOf(id: string): PassiveContent | null {
  return passiveById.get(id) ?? null;
}

/** The species' own tier line, before any EXP or evolution. */
/**
 * The species' own tier line, before any EXP or evolution — and before the global Health
 * multiplier, which is applied when a mon's stats are derived rather than stored here.
 */
export function baseStatsOf(species: Species): Stats {
  return {
    attack: species.baseAttack,
    health: species.baseHealth,
    // The tier-derived 1-3 Speed is superseded by a flat starting value; see BASE_SPEED.
    speed: BASE_SPEED,
    special: species.baseSpecial,
  };
}

/**
 * A species' passive, resolved for a given evolution stage.
 *
 * `magnitudeByStage` scales a passive's numbers by how far along its chain the mon is: index 0 is
 * the base form. Every passive in the game today is a single-entry `[1]`, so nothing scales yet —
 * a passive is worth the same at Blastoise as at Squirtle — but the shape is here so content can
 * start using it without a sim change.
 */
export function resolvePassive(species: Species, stage = 0): PassiveContent | null {
  if (species.passiveId === null) return DEFAULT_ABILITY;
  const passive = passiveById.get(species.passiveId);
  if (passive === undefined) return DEFAULT_ABILITY;

  const table = passive.magnitudeByStage;
  const magnitude = table[Math.min(Math.max(stage, 0), table.length - 1)] ?? 1;
  if (magnitude === 1) return passive;

  return {
    ...passive,
    effects: passive.effects.map((e) => ({ ...e, amount: e.amount * magnitude })),
  };
}

// --- evolution chains --------------------------------------------------------------------------

/** The species this one evolves into, or null at the end of a chain. */
export function evolutionOf(species: Species): Species | null {
  return species.evolvesIntoId === null ? null : speciesOf(species.evolvesIntoId);
}

const baseFormCache = new Map<number, Species>();

/**
 * The root of a species' evolution chain.
 *
 * Load-bearing for stats: a mon grows from the tier line of the species it *started* as for its
 * whole life, plus a flat bonus per evolution. A Charizard is a Charmander with EXP and two
 * evolutions behind it, so an evolved species' own tier line is only ever a Pokédex entry.
 */
export function baseFormOf(species: Species): Species {
  const cached = baseFormCache.get(species.id);
  if (cached !== undefined) return cached;

  let root = species;
  // Guarded rather than unbounded: malformed content must not hang a run.
  for (let guard = 0; guard < 8; guard++) {
    const parent = SPECIES.find((s) => s.evolvesIntoId === root.id);
    if (parent === undefined) break;
    root = parent;
  }
  baseFormCache.set(species.id, root);
  return root;
}

/** A species' whole chain, base form first. */
export function chainOf(species: Species): Species[] {
  const chain: Species[] = [baseFormOf(species)];
  for (let guard = 0; guard < 8; guard++) {
    const next = evolutionOf(chain[chain.length - 1]!);
    if (next === null) break;
    chain.push(next);
  }
  return chain;
}

// --- queries a screen or a generator wants -------------------------------------------------------

export function speciesOfTier(tier: number): Species[] {
  return SPECIES.filter((s) => s.tier === tier);
}

export function speciesOfType(type: PokemonType): Species[] {
  return SPECIES.filter((s) => s.types.includes(type));
}

/** Base forms only — what a wild encounter or a starter choice draws from. */
export function baseForms(): Species[] {
  return SPECIES.filter((s) => s.evolutionStage === 0);
}

export function legendaries(): Species[] {
  return SPECIES.filter((s) => s.isLegendary);
}

/** Every type actually present in the roster, in canonical order. */
export function typesInRoster(): PokemonType[] {
  const present = new Set<PokemonType>();
  for (const s of SPECIES) for (const t of s.types) present.add(t);
  return POKEMON_TYPES.filter((t) => present.has(t));
}
