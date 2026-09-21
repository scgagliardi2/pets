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
import { LOCATIONS, REGION_ART, locationFor } from '../src/meta/locations.js';
import { BADGES, badgeFor, buffTotal, buffsFor } from '../src/meta/badges.js';
import { GROWABLE_STATS } from '../src/content/statGrowth.js';
import { applyStatRewards } from '../src/meta/experience.js';
import { createRun } from '../src/meta/runState.js';
import { createInstance, unspentPoints } from '../src/content/factory.js';
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

describe('the wider map', () => {
  it('runs eight columns from entry to Gym', () => {
    const layers = new Set(generateLocationMap(1, 0).nodes.map((n) => n.layer));
    expect(layers.size).toBe(8);
  });

  it('offers three options in most layers', () => {
    // Three is the normal width: enough that the choice is real, few enough to read at a glance.
    let three = 0;
    let total = 0;
    for (const seed of SEEDS) {
      const perLayer = new Map<number, number>();
      for (const node of generateLocationMap(seed, 0).nodes) {
        perLayer.set(node.layer, (perLayer.get(node.layer) ?? 0) + 1);
      }
      // Skip the entry and the Gym, whose widths are fixed.
      for (const [layer, count] of perLayer) {
        if (layer === 1 || layer === 8) continue;
        total++;
        if (count === 3) three++;
      }
    }
    expect(three / total).toBeGreaterThan(0.5);
  });

  it('never puts two shop layers back to back', () => {
    // Two rests in a row is a stretch of the Location with no fight in it, which is neither a
    // decision nor a difficulty curve.
    for (const seed of SEEDS) {
      const layersWithShop = new Set(
        generateLocationMap(seed, seed % 8)
          .nodes.filter((n) => n.type === 'Center')
          .map((n) => n.layer),
      );
      for (const layer of layersWithShop) {
        expect(layersWithShop.has(layer + 1), `seed ${seed}, layers ${layer} and ${layer + 1}`).toBe(
          false,
        );
      }
    }
  });

  it('puts mystery trainers on the map', () => {
    let found = 0;
    for (const seed of SEEDS) {
      if (generateLocationMap(seed, 0).nodes.some((n) => n.type === 'MysteryTrainer')) found++;
    }
    expect(found).toBeGreaterThan(SEEDS.length / 2);
  });

  it('still keeps every node reachable and every path leading to the Gym', () => {
    for (const seed of SEEDS) {
      const map = generateLocationMap(seed, seed % 8);
      expect(allReachable(map), `seed ${seed}`).toBe(true);
      expect(allPathsReachGym(map), `seed ${seed}`).toBe(true);
    }
  });
});

describe('badges and regional buffs', () => {
  it('has a badge per Location, each with a leader and a line', () => {
    expect(BADGES).toHaveLength(LOCATIONS.length);
    for (const badge of BADGES) {
      expect(badge.name.length).toBeGreaterThan(0);
      expect(badge.leader.length).toBeGreaterThan(0);
      expect(badge.quote.length).toBeGreaterThan(0);
      expect(badge.glyph.length).toBeGreaterThan(0);
    }
  });

  it('names the leader the Location says runs the Gym', () => {
    LOCATIONS.forEach((loc, i) => {
      expect(badgeFor(i).leader, loc.name).toBe(loc.gymLeader);
    });
  });

  it('clamps an out-of-range badge index rather than failing a run', () => {
    expect(badgeFor(-1)).toBe(BADGES[0]);
    expect(badgeFor(99)).toBe(BADGES.at(-1));
  });

  it('offers exactly three buffs in every region', () => {
    for (const region of REGION_ART) {
      expect(buffsFor(region), region).toHaveLength(3);
    }
  });

  it('gives every buff a unique id, so two cannot collide in a run', () => {
    const ids = REGION_ART.flatMap((r) => buffsFor(r).map((b) => b.id));
    expect(new Set(ids).size).toBe(ids.length);
  });

  it('describes every buff in words', () => {
    for (const region of REGION_ART) {
      for (const buff of buffsFor(region)) {
        expect(buff.name.length, buff.id).toBeGreaterThan(0);
        expect(buff.blurb.length, buff.id).toBeGreaterThan(0);
      }
    }
  });

  it('totals the buffs a run has picked, ignoring other kinds', () => {
    const income = REGION_ART.flatMap((r) => buffsFor(r)).filter(
      (b) => b.effect.kind === 'income',
    );
    const two = income.slice(0, 2).map((b) => b.id);
    const expected = income.slice(0, 2).reduce((sum, b) => sum + b.effect.amount, 0);

    expect(buffTotal(two, 'income')).toBe(expected);
    expect(buffTotal(two, 'teamAttack')).toBe(0);
  });

  it('ignores an unknown buff id rather than throwing', () => {
    expect(buffTotal(['not-a-buff'], 'income')).toBe(0);
  });
});

describe('the stats a battle node pays', () => {
  it('gives every battle node exactly two distinct stats', () => {
    // Two distinct, so the route choice is between combinations rather than a double helping of
    // one thing — that pairing is where the strategy lives.
    for (const seed of SEEDS) {
      for (const node of generateLocationMap(seed, seed % 8).nodes) {
        if (node.type === 'Center' || node.type === 'Encounter') continue;
        expect(node.statRewards, `${seed}/${node.id}`).toBeDefined();
        expect(node.statRewards!.length, `${seed}/${node.id}`).toBe(2);
        expect(new Set(node.statRewards).size, `${seed}/${node.id}`).toBe(2);
      }
    }
  });

  it('gives nothing to nodes that field nobody', () => {
    for (const seed of SEEDS) {
      for (const node of generateLocationMap(seed, 0).nodes) {
        if (node.type === 'Center' || node.type === 'Encounter') {
          expect(node.statRewards, `${seed}/${node.id}`).toBeUndefined();
        }
      }
    }
  });

  it('varies the pairing across a Location, so the route is a real choice', () => {
    const pairings = new Set(
      generateLocationMap(7, 2)
        .nodes.filter((n) => n.statRewards !== undefined)
        .map((n) => [...n.statRewards!].sort().join('+')),
    );
    expect(pairings.size).toBeGreaterThan(1);
  });

  it('uses only real growable stats', () => {
    const valid = new Set<string>(GROWABLE_STATS);
    for (const node of generateLocationMap(3, 1).nodes) {
      for (const stat of node.statRewards ?? []) {
        expect(valid.has(stat), stat).toBe(true);
      }
    }
  });
});

describe('awarding those stats after a win', () => {
  it('applies them straight to the allocation of everyone who fought', () => {
    // Directly, not as points to assign: the node already said which two, so making the player
    // click the same buttons afterwards is upkeep without a decision.
    const state = createRun(1, [
      createInstance('Charmander', { instanceId: 'fought' }),
      createInstance('Squirtle', { instanceId: 'benched' }),
    ]);
    const after = applyStatRewards(state, ['attack', 'health'], ['fought']);

    const fought = after.lineUp.find((m) => m.instanceId === 'fought')!;
    const benched = after.lineUp.find((m) => m.instanceId === 'benched')!;

    expect(fought.allocation.attack).toBe(1);
    expect(fought.allocation.health).toBe(1);
    expect(unspentPoints(fought)).toBe(0);
    expect(benched.allocation.attack).toBe(0);
  });

  it('leaves the run alone when nobody fought or nothing was offered', () => {
    const state = createRun(1, [createInstance('Charmander', { instanceId: 'a' })]);
    expect(applyStatRewards(state, [], ['a'])).toBe(state);
    expect(applyStatRewards(state, ['attack'], [])).toBe(state);
  });
});
