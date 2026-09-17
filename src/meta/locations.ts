/**
 * The eight Locations a run passes through, one per badge.
 *
 * Each has a type bias that colours both its wild encounters and its Gym, so a Location feels
 * like somewhere rather than a reskinned node list — and so a player can plan for what is coming
 * rather than only reacting to it.
 *
 * The bias is a *preference*, not a filter: `encounterPool` falls back to the whole tier-legal
 * pool when a bias would leave nothing, which matters early when the tier cap is tight and a
 * themed pool can be empty.
 *
 * Each Location also names the art the map is drawn over. The backdrop is what actually makes the
 * eight read as eight places — the type bias is a rule you infer over several fights, the art is
 * legible the moment the map opens. `art` is a path under `public/`; the map falls back to a
 * tinted gradient built from `tint` if the file isn't there, so a missing backdrop degrades
 * instead of breaking.
 */

import type { PokemonType } from '../sim/index.js';

export interface Location {
  /** Stable, lowercase, used in class names and asset paths. */
  readonly slug: string;
  readonly name: string;
  readonly blurb: string;
  readonly typeBias: readonly PokemonType[];
  /** The Gym's theme. Usually the Location's own bias, sharpened to one type. */
  readonly gymTheme: readonly PokemonType[];
  readonly gymLeader: string;
  /** The map backdrop for this Location, as a URL under `public/`. */
  readonly art: string;
  /** The backdrop's dominant colour. Tints the map chrome, and stands in when the art is missing. */
  readonly tint: string;
}

export const REGION_ART_DIR = '/art/regions';

export const LOCATIONS: readonly Location[] = [
  {
    slug: 'verdant-path',
    name: 'Verdant Path',
    blurb: 'The route out of town, and the first trainers on it.',
    typeBias: ['Bug', 'Grass', 'Normal'],
    gymTheme: ['Bug'],
    gymLeader: 'Hollis',
    art: `${REGION_ART_DIR}/town.png`,
    tint: '#4f7a3f',
  },
  {
    slug: 'tidewater-cove',
    name: 'Tidewater Cove',
    blurb: 'Salt air, rock pools, and something in the shallows.',
    typeBias: ['Water', 'Rock'],
    gymTheme: ['Water'],
    gymLeader: 'Marin',
    art: `${REGION_ART_DIR}/coast.png`,
    tint: '#1f6fa8',
  },
  {
    slug: 'sparkfield',
    name: 'Sparkfield',
    blurb: 'Pylons humming over sun-cracked flats.',
    typeBias: ['Electric', 'Normal', 'Flying'],
    gymTheme: ['Electric'],
    gymLeader: 'Volt',
    art: `${REGION_ART_DIR}/desert.png`,
    tint: '#b8823f',
  },
  {
    slug: 'bramblewood',
    name: 'Bramblewood',
    blurb: 'Old forest. The canopy closes over the path.',
    typeBias: ['Grass', 'Bug', 'Poison'],
    gymTheme: ['Grass'],
    gymLeader: 'Thistle',
    art: `${REGION_ART_DIR}/forest.png`,
    tint: '#2f6b35',
  },
  {
    slug: 'sunken-works',
    name: 'Sunken Works',
    blurb: 'Flooded workings under the crystal seam. The carts are still loaded.',
    typeBias: ['Poison', 'Steel', 'Water'],
    gymTheme: ['Poison'],
    gymLeader: 'Slag',
    art: `${REGION_ART_DIR}/cavern.png`,
    tint: '#3b3f7a',
  },
  {
    slug: 'mirror-hollow',
    name: 'Mirror Hollow',
    blurb: 'Old stones, and statues that watch you back.',
    typeBias: ['Psychic', 'Ghost', 'Fairy'],
    gymTheme: ['Psychic'],
    gymLeader: 'Oleander',
    art: `${REGION_ART_DIR}/ruins.png`,
    tint: '#7a7f4a',
  },
  {
    slug: 'cinder-reach',
    name: 'Cinder Reach',
    blurb: 'Black rock, warm underfoot.',
    typeBias: ['Fire', 'Ground', 'Rock'],
    gymTheme: ['Fire'],
    gymLeader: 'Ember',
    art: `${REGION_ART_DIR}/volcano.png`,
    tint: '#a8391b',
  },
  {
    slug: 'wyrms-rest',
    name: "Wyrm's Rest",
    blurb: 'The last climb. Nothing small lives up here.',
    typeBias: ['Dragon', 'Ice', 'Flying'],
    gymTheme: ['Dragon'],
    gymLeader: 'Sable',
    art: `${REGION_ART_DIR}/tundra.png`,
    tint: '#6d86a6',
  },
];

/** The Location for a badge count, clamped so an overrun can't crash a run. */
export const locationFor = (badges: number): Location =>
  LOCATIONS[Math.max(0, Math.min(badges, LOCATIONS.length - 1))]!;
