/**
 * Turning roster species into things the simulator can fight with.
 *
 * This is the seam between `/src/content` (what exists) and `/src/sim` (how it resolves). A
 * `PokemonInstance` is the persistent, run-level record — species, EXP, evolutions, nickname. A
 * `Combatant` is that mon's state *inside one battle*, and the two are deliberately different
 * types: before Unity split them, running a battle wrote shields, poison stacks and damage
 * permanently onto the player's roster.
 *
 * Stats are **derived, never stored**. A mon's stat line is rebuilt from its species, its EXP
 * count and how many times it has evolved, every time it's asked for.
 */

import {
  makeCombatant,
  type Combatant,
  type HeldItemEffects,
  type PokemonType,
  type Stats,
} from '../sim/index.js';
import { itemById, statEffectsOf } from './items.js';
import { baseFormOf, resolvePassive, speciesNamed, speciesOf, type Species } from './index.js';
import {
  emptyAllocation,
  statsFromAllocation,
  type Allocation,
} from './statGrowth.js';

/** A mon as the run holds it. Everything derivable is derived, not stored. */
export interface PokemonInstance {
  /** Stable and unique within a run: the stat-growth draw is keyed to it. */
  readonly instanceId: string;
  /** What the mon is *now*, which may be several evolutions along its chain. */
  readonly speciesId: number;
  /**
   * Progress toward evolving, and nothing else.
   *
   * EXP used to mean two things at once — how close a mon was to evolving *and* how many stat
   * increases it was owed — which made every screen ambiguous about what a number referred to.
   * Those are now separate: this is evolution progress, `statPoints` is spending power.
   */
  readonly exp: number;
  /** Stat increases earned but not yet assigned. */
  readonly statPoints: number;
  /** Where the player has already put this mon's stat points. */
  readonly allocation: Allocation;
  /** How many evolutions are behind it. */
  readonly timesEvolved: number;
  /** Damage carried between nodes, if the run carries it. Null means undamaged. */
  readonly currentHP: number | null;
  /** The item this mon is holding, or null. At most one. */
  readonly heldItemId: string | null;
  readonly nickname?: string;
}

let nextInstanceOrdinal = 0;

/**
 * A fresh instance id.
 *
 * Deliberately not random: the stat-growth draw is keyed to this string, so a run replayed from
 * its seed must produce the same ids. A caller that wants reproducibility across sessions should
 * pass its own id rather than relying on this counter.
 */
export function newInstanceId(prefix = 'mon'): string {
  nextInstanceOrdinal += 1;
  return `${prefix}-${nextInstanceOrdinal}`;
}

/** Resets the id counter. For tests, so ids don't drift between runs of a suite. */
export function resetInstanceIds(): void {
  nextInstanceOrdinal = 0;
}

export interface CreateOptions {
  exp?: number;
  statPoints?: number;
  allocation?: Allocation;
  timesEvolved?: number;
  instanceId?: string;
  nickname?: string;
  currentHP?: number | null;
  heldItemId?: string | null;
}

/** A new mon of a species. Accepts a dex id, a name, or the species itself. */
export function createInstance(
  species: Species | number | string,
  options: CreateOptions = {},
): PokemonInstance {
  const resolved = resolveSpecies(species);
  return {
    instanceId: options.instanceId ?? newInstanceId(resolved.name.toLowerCase()),
    speciesId: resolved.id,
    exp: Math.max(0, options.exp ?? 0),
    statPoints: Math.max(0, options.statPoints ?? 0),
    // A mon created with EXP but no allocation has stats spread evenly rather than left unassigned:
    // encounters and Gym Leaders arrive fully formed, and only the player's own mons go through
    // the assignment screen.
    allocation: options.allocation ?? spreadEvenly(Math.max(0, options.exp ?? 0)),
    timesEvolved: Math.max(0, options.timesEvolved ?? 0),
    currentHP: options.currentHP ?? null,
    heldItemId: options.heldItemId ?? null,
    nickname: options.nickname,
  };
}

/** An even split across the four stats, for mons that arrive already grown. */
export function spreadEvenly(points: number): Allocation {
  const a = { ...emptyAllocation() };
  const order = ['health', 'attack', 'special', 'speed'] as const;
  for (let i = 0; i < Math.max(0, points); i++) {
    // Speed last and least: it is the strongest per point, so an even split would hand every
    // wild mon a charge-rate advantage nobody chose to give it.
    const stat = order[i % (i < 4 ? 3 : order.length)]!;
    a[stat] += 1;
  }
  return a;
}

function resolveSpecies(species: Species | number | string): Species {
  if (typeof species === 'number') {
    const found = speciesOf(species);
    if (found === null) throw new Error(`No species with dex id ${species}`);
    return found;
  }
  if (typeof species === 'string') {
    const found = speciesNamed(species);
    if (found === null) throw new Error(`No species named "${species}"`);
    return found;
  }
  return species;
}

export function speciesOfInstance(instance: PokemonInstance): Species {
  const found = speciesOf(instance.speciesId);
  if (found === null) throw new Error(`Instance ${instance.instanceId} has unknown species`);
  return found;
}

/** A mon's current stat line, derived from scratch. */
export function statsOf(instance: PokemonInstance): Stats {
  const species = speciesOfInstance(instance);
  const base = statsFromAllocation(baseFormOf(species), instance.allocation, instance.timesEvolved);
  const item = statEffectsOf(itemById(instance.heldItemId));

  return {
    attack: base.attack + item.attack,
    health: base.health + item.health,
    special: base.special + item.special,
    speed: base.speed + item.speed,
  };
}

/** The mon's typing, after any type-setting item replaces it. */
export function typesOf(instance: PokemonInstance): readonly PokemonType[] {
  const override = statEffectsOf(itemById(instance.heldItemId)).overrideTypes;
  return override ?? speciesOfInstance(instance).types;
}

/** The battle behaviour of whatever this mon is holding, resolved for the simulator. */
export function heldItemEffectsOf(instance: PokemonInstance): HeldItemEffects | null {
  const item = itemById(instance.heldItemId);
  if (item === null) return null;

  switch (item.kind) {
    case 'regen':
      return { regenPerStep: item.amount ?? 0 };
    case 'lastStand':
      return { healBelowHalf: item.amount ?? 0 };
    case 'cureStatus':
      return { curesStatus: true };
    default:
      // Flat stats and typing are folded into the stat line; training and EXP items act between
      // battles. None of them need the simulator to know anything.
      return null;
  }
}

/**
 * Stat points earned but not yet assigned.
 *
 * Read straight off the mon rather than derived as `exp - totalAllocated`, now that EXP means
 * evolution progress only and the two no longer move together.
 */
export const unspentPoints = (instance: PokemonInstance): number =>
  Math.max(0, instance.statPoints);

/** Max HP is just the Health stat; there is no separate ceiling. */
export function maxHPOf(instance: PokemonInstance): number {
  return statsOf(instance).health;
}

/**
 * The battle-side copy of a mon.
 *
 * Nothing the simulator does reaches back through this, by design — writing a result to the run
 * (carried damage, EXP, a caught mon joining the Box) is an explicit decision afterwards, not a
 * side effect of having simulated.
 */
export function toCombatant(instance: PokemonInstance): Combatant {
  const species = speciesOfInstance(instance);
  const stats = statsOf(instance);

  return makeCombatant({
    instanceId: instance.instanceId,
    sourceId: instance.instanceId,
    attack: stats.attack,
    health: stats.health,
    speed: stats.speed,
    special: stats.special,
    currentHP: instance.currentHP ?? stats.health,
    // Resolved against the mon's *current* species and stage, so an evolved mon's passive scales
    // if its content ever declares a magnitude table.
    passive: resolvePassive(species, species.evolutionStage),
    types: typesOf(instance),
    heldItem: heldItemEffectsOf(instance),
  });
}

/** Flat, run-long bonuses from trainer buffs, applied when a battle line-up is built. */
export interface TeamBonuses {
  attack?: number;
  health?: number;
  special?: number;
  startingCharge?: number;
}

/**
 * A whole line-up, in order. Position 0 is the Lead, 1 the Support, the rest dormant.
 *
 * Bonuses are applied here rather than folded into `statsOf`, because they belong to the *run*,
 * not to the mon: a mon moved to the Box and back should not carry them, and the roster screens
 * should show what a mon actually is.
 */
export function toLineUp(
  instances: readonly PokemonInstance[],
  bonuses: TeamBonuses = {},
): Combatant[] {
  return instances.map((instance) => {
    const combatant = toCombatant(instance);
    if (bonuses.attack !== undefined) combatant.currentStats.attack += bonuses.attack;
    if (bonuses.special !== undefined) combatant.currentStats.special += bonuses.special;
    if (bonuses.health !== undefined) {
      combatant.currentStats.health += bonuses.health;
      combatant.currentHP += bonuses.health;
    }
    if (bonuses.startingCharge !== undefined) combatant.charge += bonuses.startingCharge;
    return combatant;
  });
}

/** What a mon is called: its nickname if it has one, otherwise its species name. */
export function displayNameOf(instance: PokemonInstance): string {
  return instance.nickname ?? speciesOfInstance(instance).name;
}
