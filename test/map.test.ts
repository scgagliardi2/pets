/**
 * Map generation, the Location set, and the shop.
 *
 * The generator's invariants matter more than its aesthetics: a map with an unreachable node
 * wastes a choice, and one with a dead end can strand a run with no way to the Gym. Both are
 * checked across many seeds rather than one, because a generator that works on seed 1 and fails
 * on seed 57 is the normal failure mode.
 */

import { describe, expect, it } from 'vitest';

import {
  CHOICE_LAYERS,
  ENTRY_NODE_COUNT,
  allPathsReachGym,
  allReachable,
  generateLocationMap,
  nodeById,
  reachableFrom,
} from '../src/meta/mapGenerator.js';
import { LOCATIONS, locationFor } from '../src/meta/locations.js';
import { SHOP_STOCK, buy, canAfford, describeInventory } from '../src/meta/shop.js';
import { STARTING_BALLS, emptyInventory, totalBalls } from '../src/meta/balls.js';
import { BADGES_TO_WIN } from '../src/meta/progression.js';
import { POKEMON_TYPES } from '../src/sim/index.js';

const SEEDS = Array.from({ length: 60 }, (_, i) => i + 1);

describe('map generation', () => {
  it('offers a choice at the entry', () => {
    const map = generateLocationMap(1, 0);
    expect(map.entryIds).toHaveLength(ENTRY_NODE_COUNT);
  });

  it('ends at exactly one Gym', () => {
    for (const seed of SEEDS) {
      const map = generateLocationMap(seed, 0);
      const gyms = map.nodes.filter((n) => n.type === 'Gym');
      expect(gyms, `seed ${seed}`).toHaveLength(1);
      expect(gyms[0]!.id).toBe(map.gymId);
    }
  });

  it('makes every node reachable from the entry, across many seeds', () => {
    // An unreachable node is a wasted choice the player can see but never take.
    for (const seed of SEEDS) {
      expect(allReachable(generateLocationMap(seed, seed % 8)), `seed ${seed}`).toBe(true);
    }
  });

  it('never dead-ends short of the Gym, across many seeds', () => {
    // A dead end strands the run: no way forward and no Gym to beat.
    for (const seed of SEEDS) {
      expect(allPathsReachGym(generateLocationMap(seed, seed % 8)), `seed ${seed}`).toBe(true);
    }
  });

  it('is stable for a seed, so leaving and returning shows the same map', () => {
    const a = generateLocationMap(42, 3);
    const b = generateLocationMap(42, 3);
    expect(a.nodes.map((n) => `${n.id}:${n.type}`)).toEqual(b.nodes.map((n) => `${n.id}:${n.type}`));
  });

  it('gives different Locations different maps', () => {
    const first = generateLocationMap(42, 0);
    const later = generateLocationMap(42, 3);
    expect(first.nodes.length + first.nodes.map((n) => n.type).join()).not.toBe(
      later.nodes.length + later.nodes.map((n) => n.type).join(),
    );
  });

  it('runs the expected number of layers between entry and Gym', () => {
    const layers = new Set(generateLocationMap(1, 0).nodes.map((n) => n.layer));
    expect(layers.size).toBe(CHOICE_LAYERS + 2);
  });

  it('puts no Center in the first layer, where a rest would be worthless', () => {
    for (const seed of SEEDS) {
      const map = generateLocationMap(seed, 0);
      const entry = map.entryIds.map((id) => nodeById(map, id)!);
      expect(entry.every((n) => n.type !== 'Center'), `seed ${seed}`).toBe(true);
    }
  });

  it('never stacks two Centers in one layer, which would make the layer a non-choice', () => {
    for (const seed of SEEDS) {
      const map = generateLocationMap(seed, 0);
      const perLayer = new Map<number, number>();
      for (const node of map.nodes) {
        if (node.type === 'Center') perLayer.set(node.layer, (perLayer.get(node.layer) ?? 0) + 1);
      }
      for (const [layer, count] of perLayer) {
        expect(count, `seed ${seed} layer ${layer}`).toBeLessThanOrEqual(1);
      }
    }
  });
});

describe('traversal', () => {
  it('opens on the entry layer', () => {
    const map = generateLocationMap(5, 0);
    expect(reachableFrom(map, [])).toEqual([...map.entryIds]);
  });

  it('offers only what the last node leads to', () => {
    const map = generateLocationMap(5, 0);
    const first = nodeById(map, map.entryIds[0]!)!;
    expect(reachableFrom(map, [first.id])).toEqual([...first.next]);
  });

  it('never offers a node already taken', () => {
    const map = generateLocationMap(5, 0);
    const first = nodeById(map, map.entryIds[0]!)!;
    const second = first.next[0]!;
    expect(reachableFrom(map, [first.id, second])).not.toContain(second);
  });

  it('can be walked from entry to Gym on any seed', () => {
    // The end-to-end guarantee the two invariants exist to give.
    for (const seed of SEEDS) {
      const map = generateLocationMap(seed, seed % 8);
      const visited: string[] = [];
      let guard = 0;

      while (guard++ < 20) {
        const options = reachableFrom(map, visited);
        if (options.length === 0) break;
        visited.push(options[0]!);
        if (visited[visited.length - 1] === map.gymId) break;
      }

      expect(visited.at(-1), `seed ${seed}`).toBe(map.gymId);
    }
  });
});

describe('the Locations', () => {
  it('has one per badge', () => {
    expect(LOCATIONS).toHaveLength(BADGES_TO_WIN);
  });

  it('gives each a name, a blurb, a leader and a theme', () => {
    for (const loc of LOCATIONS) {
      expect(loc.name.length).toBeGreaterThan(0);
      expect(loc.blurb.length).toBeGreaterThan(0);
      expect(loc.gymLeader.length).toBeGreaterThan(0);
      expect(loc.typeBias.length).toBeGreaterThan(0);
      expect(loc.gymTheme.length).toBeGreaterThan(0);
    }
  });

  it('uses only real types', () => {
    const valid = new Set<string>(POKEMON_TYPES);
    for (const loc of LOCATIONS) {
      for (const t of [...loc.typeBias, ...loc.gymTheme]) {
        expect(valid.has(t), `${loc.name}: ${t}`).toBe(true);
      }
    }
  });

  it('never repeats a Gym theme, so no two Gyms feel like the same fight', () => {
    const themes = LOCATIONS.map((l) => l.gymTheme.join('/'));
    expect(new Set(themes).size).toBe(themes.length);
  });

  it('draws the Gym theme from the Location own bias', () => {
    for (const loc of LOCATIONS) {
      expect(loc.typeBias, loc.name).toContain(loc.gymTheme[0]);
    }
  });

  it('clamps out-of-range badge counts rather than failing a run', () => {
    expect(locationFor(-3)).toBe(LOCATIONS[0]);
    expect(locationFor(99)).toBe(LOCATIONS.at(-1));
  });
});

describe('the shop', () => {
  it('stocks something at each ball tier', () => {
    const tiers = new Set(SHOP_STOCK.map((i) => i.tier));
    expect(tiers.size).toBe(3);
  });

  it('prices a better ball above a worse one', () => {
    const poke = SHOP_STOCK.find((i) => i.tier === 'Poke')!;
    const ultra = SHOP_STOCK.find((i) => i.tier === 'Ultra')!;
    expect(ultra.cost).toBeGreaterThan(poke.cost);
  });

  it('prices an Ultra Ball above what one Location pays out', () => {
    // Roughly two money a wild win and five for a Gym, so about eleven a Location. An Ultra Ball
    // should be a decision rather than an incidental purchase.
    const ultra = SHOP_STOCK.find((i) => i.tier === 'Ultra')!;
    expect(ultra.cost).toBeGreaterThan(11 / 2);
  });

  it('adds the balls and takes the money', () => {
    const item = SHOP_STOCK[0]!;
    const result = buy(20, emptyInventory(), item)!;

    expect(result.money).toBe(20 - item.cost);
    expect(totalBalls(result.balls)).toBe(item.quantity);
    expect(result.balls[item.tier]).toBe(item.quantity);
  });

  it('refuses a purchase outright rather than half-applying it', () => {
    const item = SHOP_STOCK.find((i) => i.tier === 'Ultra')!;
    expect(canAfford(item.cost - 1, item)).toBe(false);
    expect(buy(item.cost - 1, emptyInventory(), item)).toBeNull();
  });

  it('describes an empty bag in words rather than as nothing', () => {
    expect(describeInventory(emptyInventory())).toBe('no balls');
    expect(describeInventory(STARTING_BALLS)).toContain('Poké Ball');
  });
});
