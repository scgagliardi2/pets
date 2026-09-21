/**
 * Content integrity and the derivation rules.
 *
 * `species.json` is generated and already validated against the 183 Unity assets by
 * `scripts/build_species.py`. These tests cover what that check can't: that the TypeScript around
 * the data is correct, that the invariants the design depends on actually hold across the whole
 * roster, and that the EXP growth rule behaves the way ADR 0009 says.
 */

import { describe, expect, it } from 'vitest';

import { POKEMON_TYPES, type PokemonType } from '../src/sim/index.js';
import {
  BASE_SPEED,
  PASSIVES,
  SPECIES,
  baseFormOf,
  baseForms,
  baseStatsOf,
  chainOf,
  evolutionOf,
  legendaries,
  passiveOf,
  resolvePassive,
  speciesNamed,
  speciesOf,
  speciesOfTier,
  speciesOfType,
  typesInRoster,
} from '../src/content/index.js';
import {
  ATTACK_PER_EVOLUTION,
  GROWABLE_STATS,
  HEALTH_MULTIPLIER,
  HEALTH_PER_EVOLUTION,
  MAX_GROWN_SPEED,
  allocate,
  emptyAllocation,
  statsFromAllocation,
  statsFor,
  totalAllocated,
} from '../src/content/statGrowth.js';

const named = (name: string) => {
  const s = speciesNamed(name);
  expect(s, `${name} should be in the roster`).not.toBeNull();
  return s!;
};

describe('the roster', () => {
  it('holds the curated 183 species', () => {
    expect(SPECIES).toHaveLength(183);
  });

  it('has unique dex ids and unique names', () => {
    expect(new Set(SPECIES.map((s) => s.id)).size).toBe(SPECIES.length);
    expect(new Set(SPECIES.map((s) => s.name)).size).toBe(SPECIES.length);
  });

  it('only uses real types', () => {
    const valid = new Set<string>(POKEMON_TYPES);
    for (const s of SPECIES) {
      expect(s.types.length, `${s.name} type count`).toBeGreaterThanOrEqual(1);
      expect(s.types.length, `${s.name} type count`).toBeLessThanOrEqual(2);
      for (const t of s.types) expect(valid.has(t), `${s.name} has type ${t}`).toBe(true);
    }
  });

  it('gives every species a passive that exists', () => {
    for (const s of SPECIES) {
      expect(s.passiveId, `${s.name} has a passive`).not.toBeNull();
      expect(passiveOf(s.passiveId!), `${s.name}'s passive resolves`).not.toBeNull();
    }
  });

  it('keeps every species inside the tier range', () => {
    for (const s of SPECIES) {
      expect(s.tier, `${s.name} tier`).toBeGreaterThanOrEqual(1);
      expect(s.tier, `${s.name} tier`).toBeLessThanOrEqual(6);
    }
  });

  it('points every evolution link at a species that exists', () => {
    for (const s of SPECIES) {
      if (s.evolvesIntoId !== null) {
        expect(speciesOf(s.evolvesIntoId), `${s.name} evolves into a real species`).not.toBeNull();
      }
    }
  });
});

describe('the invariants the design depends on', () => {
  it('Health is strictly greater than Attack for every species', () => {
    // A mon that starts able to kill its own mirror in one exchange leaves nothing for a passive
    // or a Speed advantage to decide (ADR 0009 decision 5).
    for (const s of SPECIES) {
      expect(s.baseHealth, `${s.name} ${s.baseAttack}/${s.baseHealth}`).toBeGreaterThan(
        s.baseAttack,
      );
    }
  });

  it('never gives a species 0 Attack, which could never win', () => {
    for (const s of SPECIES) {
      expect(s.baseAttack, `${s.name} attack`).toBeGreaterThanOrEqual(1);
    }
  });

  it('spends exactly the tier budget on every species', () => {
    // Tier 1 spends 8 points, +10 a tier.
    for (const s of SPECIES) {
      const budget = 8 + 10 * (s.tier - 1);
      const spent = s.baseAttack + s.baseHealth + s.baseSpeed;
      expect(spent, `${s.name} (tier ${s.tier})`).toBe(budget);
    }
  });

  it('keeps Speed scarce, since it doubles how often a passive fires', () => {
    const atSpeed1 = SPECIES.filter((s) => s.baseSpeed === 1).length;
    expect(atSpeed1 / SPECIES.length).toBeGreaterThan(0.6);

    for (const s of SPECIES) {
      expect(s.baseSpeed).toBeGreaterThanOrEqual(1);
      expect(s.baseSpeed).toBeLessThanOrEqual(3);
    }
  });

  it('gates Speed behind the tier that can afford it', () => {
    for (const s of SPECIES) {
      if (s.baseSpeed >= 2) expect(s.tier, `${s.name}`).toBeGreaterThanOrEqual(2);
      if (s.baseSpeed >= 3) expect(s.tier, `${s.name}`).toBeGreaterThanOrEqual(3);
    }
  });

  it('places every evolution at least a tier above what it came from', () => {
    // Ten real chains need this — a Metapod is a genuinely worse Pokemon than the Caterpie it
    // came from, and left alone it would evolve into a downgrade.
    for (const s of SPECIES) {
      const next = evolutionOf(s);
      if (next !== null && s.tier < 6) {
        expect(next.tier, `${s.name} (t${s.tier}) -> ${next.name} (t${next.tier})`).toBeGreaterThan(
          s.tier,
        );
      }
    }
  });

  it('keeps health growth above the floor, bar the one 1-HP species', () => {
    for (const s of SPECIES) {
      if (s.healthGrowthPercent === 0) continue; // Shedinja
      expect(s.healthGrowthPercent, `${s.name}`).toBeGreaterThanOrEqual(50);
      expect(s.healthGrowthPercent, `${s.name}`).toBeLessThanOrEqual(100);
    }
  });

  it('spreads health growth across many values rather than piling on the floor', () => {
    // Flooring the raw share at 50 would have pinned 135 of 183 species to exactly 50; the
    // rescaling exists so the number says something about each species (ADR 0009 decision 2).
    const distinct = new Set(SPECIES.map((s) => s.healthGrowthPercent));
    expect(distinct.size).toBeGreaterThan(15);

    const atFloor = SPECIES.filter((s) => s.healthGrowthPercent === 50).length;
    expect(atFloor).toBeLessThan(20);
  });

  it('has exactly one species that never gains Health', () => {
    const none = SPECIES.filter((s) => s.healthGrowthPercent === 0);
    expect(none).toHaveLength(1);
    expect(none[0]!.name).toBe('Shedinja');
  });
});

describe('the figures ADR 0009 quotes', () => {
  it('Charmander is 3/4/1 at 72% health growth', () => {
    const c = named('Charmander');
    expect([c.baseAttack, c.baseHealth, c.baseSpeed]).toEqual([3, 4, 1]);
    expect(c.healthGrowthPercent).toBe(72);
  });

  it('Magikarp is 2/5/1 at 84%', () => {
    const m = named('Magikarp');
    expect([m.baseAttack, m.baseHealth, m.baseSpeed]).toEqual([2, 5, 1]);
    expect(m.healthGrowthPercent).toBe(84);
  });

  it('Metapod banks 86% into Health', () => {
    expect(named('Metapod').healthGrowthPercent).toBe(86);
  });
});

describe('passives', () => {
  it('holds the 20 authored passives', () => {
    expect(PASSIVES).toHaveLength(20);
  });

  it('covers all 18 types with a default', () => {
    const flavours = new Set(PASSIVES.map((p) => p.typeFlavor));
    for (const t of POKEMON_TYPES) {
      expect(flavours.has(t), `something is flavoured ${t}`).toBe(true);
    }
  });

  it('gives every passive at least one effect', () => {
    for (const p of PASSIVES) {
      expect(p.effects.length, `${p.id}`).toBeGreaterThan(0);
    }
  });

  it('names a status on every ApplyStatus effect', () => {
    for (const p of PASSIVES) {
      for (const e of p.effects) {
        if (e.type === 'ApplyStatus') {
          expect(e.status, `${p.id}`).toBeDefined();
        }
      }
    }
  });

  it('keeps hand-authored assignments rather than overwriting with the type default', () => {
    // Oddish keeps Spore Cloud rather than taking Grass's Vine Drain, and Weedle keeps Poison
    // Sting rather than Bug's Swarm Scurry.
    expect(named('Oddish').passiveId).toBe('oddish-spore-cloud');
    expect(named('Weedle').passiveId).toBe('weedle-poison-sting');
  });

  it('resolves a passive for a species', () => {
    const passive = resolvePassive(named('Charmander'));
    expect(passive?.displayName).toBe('Ember Burst');
    expect(passive?.effects[0]?.status).toBe('Burned');
  });

  it('does not scale with stage yet, since every table is a single entry', () => {
    for (const p of PASSIVES) {
      expect(p.magnitudeByStage, `${p.id}`).toEqual([1]);
    }
    const base = resolvePassive(named('Squirtle'), 0);
    const evolved = resolvePassive(named('Squirtle'), 2);
    expect(evolved?.effects[0]?.amount).toBe(base?.effects[0]?.amount);
  });
});

describe('evolution chains', () => {
  it('walks the Charmander line', () => {
    expect(chainOf(named('Charmander')).map((s) => s.name)).toEqual([
      'Charmander',
      'Charmeleon',
      'Charizard',
    ]);
  });

  it('finds the base form from anywhere in a chain', () => {
    expect(baseFormOf(named('Charizard')).name).toBe('Charmander');
    expect(baseFormOf(named('Charmeleon')).name).toBe('Charmander');
    expect(baseFormOf(named('Charmander')).name).toBe('Charmander');
  });

  it('treats a species with no chain as its own base form', () => {
    const solo = named('Mewtwo');
    expect(baseFormOf(solo).name).toBe('Mewtwo');
    expect(chainOf(solo)).toHaveLength(1);
  });

  it('marks base forms consistently with the chain data', () => {
    for (const s of baseForms()) {
      expect(baseFormOf(s).id, `${s.name} is its own base form`).toBe(s.id);
    }
  });
});

describe('queries', () => {
  it('finds a species by id and by name, case-insensitively', () => {
    expect(speciesOf(4)?.name).toBe('Charmander');
    expect(speciesNamed('charmander')?.id).toBe(4);
    expect(speciesNamed('CHARMANDER')?.id).toBe(4);
    expect(speciesNamed('Missingno')).toBeNull();
    expect(speciesOf(99999)).toBeNull();
  });

  it('filters by tier and by type', () => {
    expect(speciesOfTier(1).every((s) => s.tier === 1)).toBe(true);
    expect(speciesOfType('Fire').every((s) => s.types.includes('Fire'))).toBe(true);
  });

  it('counts a dual-type species under both of its types', () => {
    const shedinja = named('Shedinja');
    expect(speciesOfType('Bug')).toContain(shedinja);
    expect(speciesOfType('Ghost')).toContain(shedinja);
  });

  it('reports which types the roster actually covers, in canonical order', () => {
    const present = typesInRoster();
    expect(present.length).toBeGreaterThan(0);
    const indices = present.map((t: PokemonType) => POKEMON_TYPES.indexOf(t));
    expect(indices).toEqual([...indices].sort((x, y) => x - y));
  });

  it('finds the legendaries', () => {
    const legends = legendaries();
    expect(legends.length).toBeGreaterThan(0);
    expect(legends.every((s) => s.isLegendary)).toBe(true);
  });
});

describe('stat growth, now chosen by the player', () => {
  const charmander = named('Charmander');

  /** n points, all into one stat. */
  const into = (stat: (typeof GROWABLE_STATS)[number], n: number) => {
    let a = emptyAllocation();
    for (let i = 0; i < n; i++) a = allocate(a, stat);
    return a;
  };

  it('puts a point exactly where it is sent', () => {
    const base = baseStatsOf(charmander);

    expect(statsFromAllocation(charmander, into('attack', 5), 0).attack).toBe(base.attack + 5);
    expect(statsFromAllocation(charmander, into('health', 5), 0).health).toBe(
      (base.health + 5) * HEALTH_MULTIPLIER,
    );
    expect(statsFromAllocation(charmander, into('special', 5), 0).special).toBeGreaterThan(
      statsFromAllocation(charmander, emptyAllocation(), 0).special,
    );
    expect(statsFromAllocation(charmander, into('speed', 1), 0).speed).toBe(BASE_SPEED + 1);
  });

  it('lets the same species grow into different mons', () => {
    // The whole point of the change: EXP is a decision, not something that happens to you.
    const wall = statsFromAllocation(charmander, into('health', 10), 0);
    const cannon = statsFromAllocation(charmander, into('attack', 10), 0);

    expect(wall.health).toBeGreaterThan(cannon.health);
    expect(cannon.attack).toBeGreaterThan(wall.attack);
  });

  it('is deterministic, with no hidden roll left in it', () => {
    const a = statsFromAllocation(charmander, into('attack', 7), 0);
    const b = statsFromAllocation(charmander, into('attack', 7), 0);
    expect(a).toEqual(b);
  });

  it('caps Speed at the charge scale calibration point', () => {
    // 100 Speed is three ability activations per attack — the top of the scale, so the natural
    // ceiling rather than an arbitrary one.
    const stats = statsFromAllocation(charmander, into('speed', 500), 0);
    expect(stats.speed).toBe(MAX_GROWN_SPEED);
  });

  it('starts every species at the same flat base Speed', () => {
    // The tier-derived 1-3 spread was calibrated to a threshold of 3 and means nothing against
    // 100; everyone starts level until a per-species spread is re-derived.
    for (const s of [named('Charmander'), named('Mewtwo'), named('Caterpie')]) {
      expect(statsFromAllocation(s, emptyAllocation(), 0).speed, s.name).toBe(BASE_SPEED);
    }
  });

  it('never changes Speed by accident', () => {
    expect(statsFromAllocation(charmander, into('attack', 30), 3).speed).toBe(BASE_SPEED);
  });

  it('adds a flat bonus per evolution on top of what was chosen', () => {
    const none = statsFromAllocation(charmander, into('attack', 4), 0);
    const twice = statsFromAllocation(charmander, into('attack', 4), 2);

    expect(twice.attack).toBe(none.attack + 2 * ATTACK_PER_EVOLUTION);
    expect(twice.health).toBe(none.health + 2 * HEALTH_PER_EVOLUTION * HEALTH_MULTIPLIER);
  });

  it('grows an evolved mon from its base form, not from what it became', () => {
    const alloc = into('attack', 12);
    expect(statsFor(named('Charizard'), alloc, 2)).toEqual(
      statsFromAllocation(charmander, alloc, 2),
    );
  });

  it('ignores negative allocations rather than going backwards', () => {
    const base = baseStatsOf(charmander);
    const broken = { attack: -5, health: -5, special: -5, speed: -5 };
    const stats = statsFromAllocation(charmander, broken, -2);

    expect(stats.attack).toBe(base.attack);
    expect(stats.health).toBe(base.health * HEALTH_MULTIPLIER);
  });

  it('counts what has been spent', () => {
    let a = emptyAllocation();
    a = allocate(a, 'attack', 3);
    a = allocate(a, 'speed', 1);
    expect(totalAllocated(a)).toBe(4);
  });

  it('multiplies Health for the whole of a mon life, not just its base', () => {
    const base = baseStatsOf(charmander);
    expect(statsFromAllocation(charmander, emptyAllocation(), 0).health).toBe(
      base.health * HEALTH_MULTIPLIER,
    );
  });
});

describe('Special, and the ability it drives', () => {
  it('gives every species a Special of at least one', () => {
    for (const s of SPECIES) {
      expect(s.baseSpecial, s.name).toBeGreaterThanOrEqual(1);
    }
  });

  it('makes special-leaning species special-leaning here too', () => {
    // Alakazam is a glass cannon in the real games and should be one here; Machop is not.
    const alakazam = named('Alakazam');
    const machop = named('Machop');

    expect(alakazam.baseSpecial).toBeGreaterThan(alakazam.baseAttack);
    expect(machop.baseSpecial).toBeLessThan(machop.baseAttack);
  });

  it('clamps the ratio, so no ability is lethal on its own at its tier', () => {
    // Unclamped, Alakazam's real 2.7x ratio produced an ability that nearly one-shot anything at
    // tier 3. Special stays within twice Attack and no lower than half.
    for (const s of SPECIES) {
      expect(s.baseSpecial, s.name).toBeLessThanOrEqual(s.baseAttack * 2);
      expect(s.baseSpecial * 2, s.name).toBeGreaterThanOrEqual(s.baseAttack);
    }
  });

  it('moves only when Special is chosen', () => {
    // An earlier version had Special ride the Attack line so an un-invested ability kept pace.
    // That meant picking Attack silently raised two stats, which makes the choice a lie: a player
    // told they are choosing one thing has to actually be choosing one thing.
    const caterpie = named('Caterpie');
    let attackOnly = emptyAllocation();
    for (let i = 0; i < 20; i++) attackOnly = allocate(attackOnly, 'attack');

    const fresh = statsFromAllocation(caterpie, emptyAllocation(), 0);
    const pumped = statsFromAllocation(caterpie, attackOnly, 0);

    expect(pumped.attack).toBeGreaterThan(fresh.attack);
    expect(pumped.special).toBe(fresh.special);

    // And it does move when it is the one chosen.
    const special = statsFromAllocation(caterpie, allocate(emptyAllocation(), 'special', 4), 0);
    expect(special.special).toBe(fresh.special + 4);
  });

  it('never lets an evolution or a Health point leak into Special either', () => {
    const charmander = named('Charmander');
    const base = statsFromAllocation(charmander, emptyAllocation(), 0);

    expect(statsFromAllocation(charmander, emptyAllocation(), 3).special).toBe(base.special);
    expect(statsFromAllocation(charmander, allocate(emptyAllocation(), 'health', 9), 0).special).toBe(
      base.special,
    );
  });

  it('falls back to a Special strike when a species has no authored ability', () => {
    const withoutOne = { ...named('Charmander'), passiveId: null };
    const ability = resolvePassive(withoutOne);

    expect(ability?.id).toBe('default-special-strike');
    expect(ability?.effects[0]?.scalesWithSpecial).toBe(true);
  });

  it('marks enemy damage as Special-scaled and leaves everything else alone', () => {
    // Damage at an enemy IS the default ability, so it reads its magnitude off the mon. A shield,
    // heal or status keeps its authored number, which is what makes it an override.
    for (const passive of PASSIVES) {
      for (const effect of passive.effects) {
        const isEnemyDamage = effect.type === 'DealDamage' && effect.target.startsWith('Enemy');
        expect(effect.scalesWithSpecial === true, `${passive.id}: ${effect.type}`).toBe(
          isEnemyDamage,
        );
      }
    }
  });
});
