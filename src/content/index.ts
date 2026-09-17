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

export const SPECIES: readonly Species[] = speciesData as readonly Species[];
export const PASSIVES: readonly PassiveContent[] = passivesData as readonly PassiveContent[];

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
export function baseStatsOf(species: Species): Stats {
  return {
    attack: species.baseAttack,
    health: species.baseHealth,
    speed: species.baseSpeed,
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
export function resolvePassive(species: Species, stage = 0): PassiveDefinition | null {
  if (species.passiveId === null) return null;
  const passive = passiveById.get(species.passiveId);
  if (passive === undefined) return null;

  const table = passive.magnitudeByStage;
  const magnitude = table[Math.min(Math.max(stage, 0), table.length - 1)] ?? 1;
  if (magnitude === 1) return passive;

  return {
    id: passive.id,
    displayName: passive.displayName,
    typeFlavor: passive.typeFlavor,
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
