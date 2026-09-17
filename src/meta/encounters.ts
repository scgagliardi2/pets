/**
 * Who the player fights, and where.
 *
 * Encounters are generated from the run's progress — badge count and how deep into the Location a
 * node sits — and never from the player's own team. Rubber-banding would make EXP worthless: a
 * player who fights more should be ahead, and one who dodges fights should feel it.
 *
 * `FIRST_LOCATION` below is the hand-authored linear path the run layer was first built against.
 * The generated branching maps replaced it in play; it is kept because the scaling tests read
 * better against a fixed shape than a rolled one.
 */

import { baseForms, speciesOf, type Species } from '../content/index.js';
import { createInstance, type PokemonInstance } from '../content/factory.js';
import { createRandom, hashString, type PokemonType, type Rng } from '../sim/index.js';
import {
  gymExp,
  gymTeamSize,
  maxTier,
  trainerExp,
  trainerTeamSize,
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
 * The seed a node's opposition is drawn from.
 *
 * The node's **id** is in here, not just its depth. Seeding from the position alone gave every
 * node in a layer the same team — all three entry nodes fielding the same Eevee — because the
 * position is exactly what the nodes in a layer have in common. The `salt` separates the
 * different questions asked about one node, so a node's wild encounter and its Encounter roll
 * aren't drawn from the same stream.
 */
export const nodeSeed = (runSeed: number, badges: number, node: MapNode, salt: number): number =>
  (runSeed + badges * 104729 + node.layer * 7919 + hashString(node.id) + salt) | 0;

/** Salts, so each thing asked of a node gets its own stream. Values are arbitrary but fixed. */
export const SEED_SALT = { wild: 0, trainer: 4409, gym: 31337, battle: 90001 } as const;

/**
 * `count` species drawn from `pool`, avoiding repeats while the pool is big enough to.
 *
 * Drawing with replacement is what made a three-mon team come up as the same species three times
 * often enough to read as a bug rather than as luck — at a pool of seventeen and a team of three
 * that is about one encounter in four. A team is still allowed to repeat a species once the pool
 * is smaller than the team, because the alternative is an encounter that can't be generated.
 */
export function drawTeam(rng: Rng, pool: readonly Species[], count: number): Species[] {
  const drawn: Species[] = [];
  let remaining = [...pool];

  for (let i = 0; i < count; i++) {
    if (remaining.length === 0) remaining = [...pool];
    const index = rng.nextInt(remaining.length);
    drawn.push(remaining[index]!);
    remaining.splice(index, 1);
  }
  return drawn;
}

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
  // Seeded per node — by its id, not just its depth — so each node on a layer is its own
  // encounter, and re-entering one gives the same fight rather than a re-roll.
  const rng = createRandom(nodeSeed(seed, badges, node, SEED_SALT.wild));
  const pool = encounterPool(badges, typeBias);
  const exp = wildExp(badges, node.layer);

  return drawTeam(rng, pool, wildEncounterSize(badges)).map((species, i) =>
    createInstance(species, { exp, instanceId: `wild-${node.id}-${i}` }),
  );
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
  const rng = createRandom((seed + SEED_SALT.gym + badges * 65537) | 0);
  const pool = encounterPool(badges, typeBias);
  const exp = gymExp(badges);

  // Drawn without repeats: a Bug Leader fielding four Caterpie is not a themed line-up, it is the
  // same fight four times over.
  return drawTeam(rng, pool, gymTeamSize(badges, playerLineUpSize)).map((species, i) =>
    createInstance(species, { exp, instanceId: `gym-${badges}-${i}` }),
  );
}

/**
 * The names a Mystery Trainer can turn out to be.
 *
 * A class rather than a person: the point of the node is that you don't know what is coming, and a
 * class ("Hiker", "Bug Catcher") tells you just enough to guess wrong. The name is drawn from the
 * node's own seed, so it is the same trainer every time you look at that node.
 */
export const TRAINER_CLASSES: readonly string[] = [
  'Hiker',
  'Bug Catcher',
  'Lass',
  'Youngster',
  'Picnicker',
  'Fisherman',
  'Ace Trainer',
  'Bird Keeper',
  'Hex Maniac',
  'Blackbelt',
  'Ranger',
  'Sailor',
];

/** Who a Mystery Trainer node turns out to be. Stable for the node. */
export function trainerNameFor(seed: number, node: MapNode): string {
  const rng = createRandom(nodeSeed(seed, 0, node, SEED_SALT.trainer + 1));
  return TRAINER_CLASSES[rng.nextInt(TRAINER_CLASSES.length)]!;
}

/**
 * A Mystery Trainer's team.
 *
 * Themed to the Location like a wild encounter, but a body larger and a point of EXP ahead — a
 * trainer is the node you take when you want the money and the EXP and can afford the fight, and
 * it has to actually be the harder of the two to be that.
 */
export function generateTrainerTeam(
  seed: number,
  badges: number,
  node: MapNode,
  typeBias?: readonly PokemonType[],
): PokemonInstance[] {
  const rng = createRandom(nodeSeed(seed, badges, node, SEED_SALT.trainer));
  const pool = encounterPool(badges, typeBias);
  const exp = trainerExp(badges, node.layer);

  return drawTeam(rng, pool, trainerTeamSize(badges)).map((species, i) =>
    createInstance(species, { exp, instanceId: `trainer-${node.id}-${i}` }),
  );
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
  // A Center is a shop and an Encounter is a scene; neither fields anybody, and both are resolved
  // on the map layer rather than by opening the battle screen.
  if (node.type === 'Center' || node.type === 'Encounter') return [];
  if (node.type === 'Gym') {
    const theme = location?.gymTheme ?? gymThemeFor(badges);
    return generateGymTeam(seed, badges, playerLineUpSize, theme);
  }
  if (node.type === 'Trainer') {
    return generateTrainerTeam(seed, badges, node, location?.typeBias);
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
