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

import { makeCombatant, type Combatant, type Stats } from '../sim/index.js';
import { baseFormOf, resolvePassive, speciesNamed, speciesOf, type Species } from './index.js';
import { statsAtExp } from './statGrowth.js';

/** A mon as the run holds it. Everything derivable is derived, not stored. */
export interface PokemonInstance {
  /** Stable and unique within a run: the stat-growth draw is keyed to it. */
  readonly instanceId: string;
  /** What the mon is *now*, which may be several evolutions along its chain. */
  readonly speciesId: number;
  /** Lifetime EXP. A flat counter — no level, no curve, no EXP-to-next-level. */
  readonly exp: number;
  /** How many evolutions are behind it. */
  readonly timesEvolved: number;
  /** Damage carried between nodes, if the run carries it. Null means undamaged. */
  readonly currentHP: number | null;
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
  timesEvolved?: number;
  instanceId?: string;
  nickname?: string;
  currentHP?: number | null;
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
    timesEvolved: Math.max(0, options.timesEvolved ?? 0),
    currentHP: options.currentHP ?? null,
    nickname: options.nickname,
  };
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
  return statsAtExp(baseFormOf(species), instance.instanceId, instance.exp, instance.timesEvolved);
}

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
    currentHP: instance.currentHP ?? stats.health,
    // Resolved against the mon's *current* species and stage, so an evolved mon's passive scales
    // if its content ever declares a magnitude table.
    passive: resolvePassive(species, species.evolutionStage),
    types: species.types,
  });
}

/** A whole line-up, in order. Position 0 is the Lead, 1 the Support, the rest dormant. */
export function toLineUp(instances: readonly PokemonInstance[]): Combatant[] {
  return instances.map(toCombatant);
}

/** What a mon is called: its nickname if it has one, otherwise its species name. */
export function displayNameOf(instance: PokemonInstance): string {
  return instance.nickname ?? speciesOfInstance(instance).name;
}
