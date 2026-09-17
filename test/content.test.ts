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
  HEALTH_PER_EVOLUTION,
  gainsHealth,
  healthGainsIn,
  statsAtExp,
  statsFor,
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

describe('stat growth from EXP', () => {
  const charmander = named('Charmander');

  it('a point of EXP buys Attack or Health, never both', () => {
    // The rule ADR 0009 exists for. Total stats rise by exactly the EXP spent.
    const base = baseStatsOf(charmander);
    for (const exp of [1, 5, 12, 40]) {
      const grown = statsAtExp(charmander, 'test-mon', exp, 0);
      expect(grown.attack + grown.health).toBe(base.attack + base.health + exp);
    }
  });

  it('is deterministic — the same mon and EXP always give the same line', () => {
    const a = statsAtExp(charmander, 'mon-1', 17, 0);
    const b = statsAtExp(charmander, 'mon-1', 17, 0);
    expect(a).toEqual(b);
  });

  it('grows two mons of the same species into different lines', () => {
    const a = statsAtExp(charmander, 'mon-1', 20, 0);
    const b = statsAtExp(charmander, 'mon-2', 20, 0);
    expect(a).not.toEqual(b);
  });

  it('is rebuildable in any order — point 7 answers the same however you ask', () => {
    // Stats are rebuilt from scratch on every grant, so a draw has to be answerable on its own.
    const direct = gainsHealth(charmander.healthGrowthPercent, 'mon-1', 7);
    const viaWalk = healthGainsIn(charmander.healthGrowthPercent, 'mon-1', 8) -
      healthGainsIn(charmander.healthGrowthPercent, 'mon-1', 7);
    expect(viaWalk === 1).toBe(direct);
  });

  it('follows the species growth bias in aggregate', () => {
    // 72% to Health. Averaged over many mons rather than one, because a single mon's 500 draws
    // have real sampling variance and a tight bound on one id would be a flaky test.
    let health = 0;
    const mons = 100;
    const points = 200;
    for (let m = 0; m < mons; m++) {
      health += healthGainsIn(charmander.healthGrowthPercent, `bias-${m}`, points);
    }
    const share = health / (mons * points);

    expect(share).toBeGreaterThan(0.69);
    expect(share).toBeLessThan(0.75);
  });

  it('leaves adjacent point indices uncorrelated', () => {
    // What the hash's final avalanche is for: without it, adjacent indices correlate and a mon's
    // growth comes out in visible blocks. Run length alone is a bad test of this, because a 72%
    // bias produces long Health runs all by itself — a true biased coin averages a longest run of
    // about 13 over 200 draws. So measure the conditional probability instead: if the draws are
    // independent, P(Health | previous was Health) should sit near the bias itself.
    const pct = charmander.healthGrowthPercent;
    let afterHealth = 0;
    let health = 0;
    let total = 0;

    for (let m = 0; m < 200; m++) {
      let previous = gainsHealth(pct, `corr-${m}`, 0);
      for (let i = 1; i < 200; i++) {
        const current = gainsHealth(pct, `corr-${m}`, i);
        if (previous) {
          afterHealth += current ? 1 : 0;
          health++;
        }
        total++;
        previous = current;
      }
    }

    const conditional = afterHealth / health;
    expect(conditional).toBeGreaterThan(pct / 100 - 0.05);
    expect(conditional).toBeLessThan(pct / 100 + 0.05);
    expect(total).toBeGreaterThan(0);
  });

  it('never gives Shedinja a point of Health', () => {
    const shedinja = named('Shedinja');
    const base = baseStatsOf(shedinja);
    const grown = statsAtExp(shedinja, 'shed-1', 30, 0);

    expect(grown.health).toBe(base.health);
    expect(grown.attack).toBe(base.attack + 30);
  });

  it('never changes Speed, with EXP or with evolution', () => {
    const base = baseStatsOf(charmander);
    expect(statsAtExp(charmander, 'mon-1', 100, 3).speed).toBe(base.speed);
  });

  it('adds a flat bonus per evolution', () => {
    const none = statsAtExp(charmander, 'mon-1', 10, 0);
    const twice = statsAtExp(charmander, 'mon-1', 10, 2);

    expect(twice.attack).toBe(none.attack + 2 * ATTACK_PER_EVOLUTION);
    expect(twice.health).toBe(none.health + 2 * HEALTH_PER_EVOLUTION);
  });

  it('grows an evolved mon from its base form, not from what it became', () => {
    // A Charizard is a Charmander with EXP and two evolutions behind it — Charizard's own tier
    // line is only a Pokedex entry. So a caught Charmeleon and a raised one are the same mon.
    const asCharizard = statsFor(named('Charizard'), 'mon-1', 24, 2);
    const fromCharmander = statsAtExp(charmander, 'mon-1', 24, 2);

    expect(asCharizard).toEqual(fromCharmander);
  });

  it('treats negative EXP and evolutions as zero rather than going backwards', () => {
    expect(statsAtExp(charmander, 'mon-1', -5, -2)).toEqual(baseStatsOf(charmander));
  });
});
