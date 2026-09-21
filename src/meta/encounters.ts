/**
 * Who the player fights, and where.
 *
 * Encounters are generated from the run's progress — badge count and how deep into the Location a
 * node sits — and never from the player's own team. Rubber-banding would make EXP worthless: a
 * player who fights more should be ahead, and one who dodges fights should feel it.
 *
 * The Location itself is hand-authored. Map *generation* is a later stage; one fixed path is
 * enough to play a run end to end and is much easier to reason about while the rest settles.
 */

import { baseForms, speciesOf, type Species } from '../content/index.js';
import { createInstance, type PokemonInstance } from '../content/factory.js';
import { createRandom, type PokemonType } from '../sim/index.js';
import {
  baselineExp,
  gymExp,
  gymTeamSize,
  maxTier,
  wildEncounterSize,
  wildExp,
} from './progression.js';
import type { Location } from './locations.js';
import type { MapNode } from './runState.js';

/**
 * The first Location: three wild encounters, a Center, and the Gym.
 *
 * Deliberately linear. The branching map is generated later; this is the shape a Location has —
 * a few fights, somewhere to spend money, and a Gym that pays the badge.
 */
export const FIRST_LOCATION: { name: string; nodes: MapNode[] } = {
  name: 'Verdant Path',
  nodes: [
    { id: 'n1', type: 'Wild', layer: 1, label: 'Tall grass' },
    { id: 'n2', type: 'Wild', layer: 2, label: 'Forest edge' },
    { id: 'n3', type: 'Center', layer: 3, label: 'Pokémon Center' },
    { id: 'n4', type: 'Wild', layer: 4, label: 'Deep woods' },
    { id: 'n5', type: 'Gym', layer: 5, label: 'Gym Leader' },
  ],
};

/**
 * The species a node may draw from: base forms only, at or under the tier cap for this badge
 * count.
 *
 * Base forms only because a mon should arrive at the start of its own chain and grow — an
 * encounter that fielded a Charizard would be fielding a mon with no history, and its tier line
 * is only a Pokedex entry anyway.
 */
export function encounterPool(badges: number, typeBias?: readonly PokemonType[]): Species[] {
  const cap = maxTier(badges);
  let pool = baseForms().filter((s) => !s.isLegendary && (cap === null || s.tier <= cap));

  if (typeBias !== undefined && typeBias.length > 0) {
    const biased = pool.filter((s) => s.types.some((t) => typeBias.includes(t)));
    if (biased.length > 0) pool = biased;
  }

  // The cap can exclude everything if content changes underneath it; falling back to tier 1
  // beats generating an empty encounter and an unwinnable node.
  return pool.length > 0 ? pool : baseForms().filter((s) => s.tier <= 1);
}

/** A wild encounter for a node, drawn from the run's seed so a run replays identically. */
export function generateWildEncounter(
  seed: number,
  badges: number,
  node: MapNode,
  typeBias?: readonly PokemonType[],
): PokemonInstance[] {
  // Seeded per node, so re-entering the same node gives the same encounter and the map can be
  // previewed without committing to it.
  const rng = createRandom(seed + node.layer * 7919 + badges * 104729);
  const pool = encounterPool(badges, typeBias);
  const size = wildEncounterSize(badges);
  const exp = wildExp(badges, node.layer);

  return Array.from({ length: size }, (_, i) => {
    const species = pool[rng.nextInt(pool.length)]!;
    return createInstance(species, { exp, instanceId: `wild-${node.id}-${i}` });
  });
}

/**
 * The Gym Leader's team: a themed line-up, pitched above the Location's baseline, and at least as
 * large as the player's — a line-up is a train, so a longer one is simply more to chew through.
 */
export function generateGymTeam(
  seed: number,
  badges: number,
  playerLineUpSize: number,
  typeBias: readonly PokemonType[],
): PokemonInstance[] {
  const rng = createRandom(seed + 31337 + badges * 65537);
  const pool = encounterPool(badges, typeBias);
  const size = gymTeamSize(badges, playerLineUpSize);
  const exp = gymExp(badges);

  return Array.from({ length: size }, (_, i) => {
    const species = pool[rng.nextInt(pool.length)]!;
    return createInstance(species, { exp, instanceId: `gym-${badges}-${i}` });
  });
}

/**
 * Another trainer's team.
 *
 * The placeholder for asynchronous multiplayer: eventually this is a real player's line-up,
 * fetched rather than generated. Until then it is a mixed, untyped team pitched at the Location's
 * baseline — untyped deliberately, since another player's team would not follow the local theme.
 *
 * Its Pokémon belong to someone, so they cannot be caught. The reward is money instead.
 */
export function generateTrainerTeam(
  seed: number,
  badges: number,
  node: MapNode,
  playerLineUpSize: number,
): PokemonInstance[] {
  const rng = createRandom(seed + node.layer * 5231 + badges * 7919 + 97);
  const pool = encounterPool(badges);
  const size = Math.max(2, Math.min(playerLineUpSize, wildEncounterSize(badges) + 1));
  const exp = baselineExp(badges);

  return Array.from({ length: size }, (_, i) => {
    const species = pool[rng.nextInt(pool.length)]!;
    return createInstance(species, { exp, instanceId: `trainer-${node.id}-${i}` });
  });
}

/** The Gym's type theme for a Location. One entry per badge; the first Location is Bug. */
export const GYM_THEMES: readonly PokemonType[][] = [
  ['Bug'],
  ['Water'],
  ['Electric'],
  ['Grass'],
  ['Poison'],
  ['Psychic'],
  ['Fire'],
  ['Dragon'],
];

export const gymThemeFor = (badges: number): readonly PokemonType[] =>
  GYM_THEMES[Math.min(badges, GYM_THEMES.length - 1)]!;

/**
 * The opposition for a node, whatever kind it is. Centers field nobody.
 *
 * The Location's bias colours its wild encounters and sharpens to one type for its Gym, so a
 * Location plays like somewhere rather than a reskinned node list — and so a player can prepare
 * for what is coming instead of only reacting to it.
 */
export function opponentsFor(
  seed: number,
  badges: number,
  node: MapNode,
  playerLineUpSize: number,
  location?: Location,
): PokemonInstance[] {
  if (node.type === 'Center' || node.type === 'Encounter') return [];
  if (node.type === 'MysteryTrainer') {
    return generateTrainerTeam(seed, badges, node, playerLineUpSize);
  }
  if (node.type === 'Gym') {
    const theme = location?.gymTheme ?? gymThemeFor(badges);
    return generateGymTeam(seed, badges, playerLineUpSize, theme);
  }
  return generateWildEncounter(seed, badges, node, location?.typeBias);
}

/**
 * The two mons a run opens with.
 *
 * Fixed rather than chosen, for now — Character Select is its own screen and a later stage. A
 * Fire starter and a Normal/Flying partner is the mainline opening, and the pair gives the player
 * both an active Lead and a Support from the first fight so the formation rules are visible
 * immediately.
 */
export function defaultStarters(): PokemonInstance[] {
  const charmander = speciesOf(4);
  const pidgey = speciesOf(16);
  return [
    createInstance(charmander ?? baseForms()[0]!, { instanceId: 'starter-1' }),
    createInstance(pidgey ?? baseForms()[1]!, { instanceId: 'starter-2' }),
  ];
}
