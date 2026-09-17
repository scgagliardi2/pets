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
 */

import type { PokemonType } from '../sim/index.js';

export interface Location {
  readonly name: string;
  readonly blurb: string;
  readonly typeBias: readonly PokemonType[];
  /** The Gym's theme. Usually the Location's own bias, sharpened to one type. */
  readonly gymTheme: readonly PokemonType[];
  readonly gymLeader: string;
}

export const LOCATIONS: readonly Location[] = [
  {
    name: 'Verdant Path',
    blurb: 'Tall grass and the first trainers.',
    typeBias: ['Bug', 'Grass', 'Normal'],
    gymTheme: ['Bug'],
    gymLeader: 'Hollis',
  },
  {
    name: 'Tidewater Cove',
    blurb: 'Salt air, rock pools, and something in the shallows.',
    typeBias: ['Water', 'Rock'],
    gymTheme: ['Water'],
    gymLeader: 'Marin',
  },
  {
    name: 'Sparkfield',
    blurb: 'Pylons humming over dry grass.',
    typeBias: ['Electric', 'Normal', 'Flying'],
    gymTheme: ['Electric'],
    gymLeader: 'Volt',
  },
  {
    name: 'Bramblewood',
    blurb: 'Old forest. The canopy closes over the path.',
    typeBias: ['Grass', 'Bug', 'Poison'],
    gymTheme: ['Grass'],
    gymLeader: 'Thistle',
  },
  {
    name: 'Sunken Works',
    blurb: 'Flooded machinery and a smell you cannot place.',
    typeBias: ['Poison', 'Steel', 'Water'],
    gymTheme: ['Poison'],
    gymLeader: 'Slag',
  },
  {
    name: 'Mirror Hollow',
    blurb: 'Quiet enough that you hear yourself thinking.',
    typeBias: ['Psychic', 'Ghost', 'Fairy'],
    gymTheme: ['Psychic'],
    gymLeader: 'Oleander',
  },
  {
    name: 'Cinder Reach',
    blurb: 'Black rock, warm underfoot.',
    typeBias: ['Fire', 'Ground', 'Rock'],
    gymTheme: ['Fire'],
    gymLeader: 'Ember',
  },
  {
    name: "Wyrm's Rest",
    blurb: 'The last climb. Nothing small lives up here.',
    typeBias: ['Dragon', 'Ice', 'Flying'],
    gymTheme: ['Dragon'],
    gymLeader: 'Sable',
  },
];

/** The Location for a badge count, clamped so an overrun can't crash a run. */
export const locationFor = (badges: number): Location =>
  LOCATIONS[Math.max(0, Math.min(badges, LOCATIONS.length - 1))]!;
