/**
 * Catching: the odds, the roll, the ball economy, and what a catch produces.
 */

import { describe, expect, it } from 'vitest';

import {
  BALL_TIERS,
  STARTING_BALLS,
  addBalls,
  baseChance,
  ballName,
  emptyInventory,
  expCap,
  hasBall,
  spendBall,
  totalBalls,

} from '../src/meta/balls.js';
import {
  MAX_CHANCE,
  STATUS_BONUS,
  WEAKENED_BONUS,
  caughtInstance,
  chanceAgainst,
  chanceFor,
  percentAgainst,
  rollCatch,
} from '../src/meta/catching.js';
import { createRandom, makeCombatant, type Combatant } from '../src/sim/index.js';
import { speciesNamed } from '../src/content/index.js';
import { statsOf } from '../src/content/factory.js';

const target = (hp: number, maxHP = 20, status: Combatant['status'] = null): Combatant => {
  const c = makeCombatant({ instanceId: 't', attack: 3, health: maxHP, speed: 1 });
  c.currentHP = hp;
  c.status = status;
  return c;
};

describe('ball tiers', () => {
  it('orders weakest to strongest, and the order is meaningful', () => {
    expect(BALL_TIERS).toEqual(['Poke', 'Great', 'Ultra']);
    expect(baseChance('Poke')).toBeLessThan(baseChance('Great'));
    expect(baseChance('Great')).toBeLessThan(baseChance('Ultra'));
  });

  it('names every tier', () => {
    for (const tier of BALL_TIERS) expect(ballName(tier).length).toBeGreaterThan(0);
  });

  it('caps what a weaker ball lets a catch keep, and an Ultra Ball caps nothing', () => {
    expect(expCap('Poke')).toBeLessThan(expCap('Great')!);
    expect(expCap('Ultra')).toBeNull();
  });
});

describe('the inventory', () => {
  it('starts with a spread across the tiers', () => {
    expect(totalBalls(STARTING_BALLS)).toBeGreaterThan(0);
    expect(STARTING_BALLS.Poke).toBeGreaterThan(STARTING_BALLS.Ultra);
  });

  it('spends one ball at a time', () => {
    const after = spendBall(STARTING_BALLS, 'Poke');
    expect(after.Poke).toBe(STARTING_BALLS.Poke - 1);
    expect(after.Great).toBe(STARTING_BALLS.Great);
  });

  it('refuses to spend a ball it does not have', () => {
    const empty = emptyInventory();
    expect(spendBall(empty, 'Ultra')).toBe(empty);
    expect(hasBall(empty, 'Ultra')).toBe(false);
  });

  it('adds balls without going negative', () => {
    expect(addBalls(emptyInventory(), 'Great', -5).Great).toBe(0);
    expect(addBalls(emptyInventory(), 'Great', 3).Great).toBe(3);
  });
});

describe('the odds', () => {
  it('are just the ball at full health with no status', () => {
    for (const tier of BALL_TIERS) {
      expect(chanceFor(tier, 20, 20, null)).toBeCloseTo(baseChance(tier), 5);
    }
  });

  it('rise as the target weakens — the reason to fight before throwing', () => {
    const full = chanceFor('Poke', 20, 20, null);
    const half = chanceFor('Poke', 10, 20, null);
    const nearly = chanceFor('Poke', 1, 20, null);

    expect(half).toBeGreaterThan(full);
    expect(nearly).toBeGreaterThan(half);
    expect(chanceFor('Poke', 0, 20, null)).toBeCloseTo(baseChance('Poke') + WEAKENED_BONUS, 5);
  });

  it('scale the weakening bonus linearly', () => {
    const at75 = chanceFor('Poke', 15, 20, null) - baseChance('Poke');
    expect(at75).toBeCloseTo(WEAKENED_BONUS * 0.25, 5);
  });

  it('add a flat bonus for any status, equal across all four', () => {
    const bare = chanceFor('Poke', 20, 20, null);
    for (const status of ['Poisoned', 'Burned', 'Paralyzed', 'Asleep'] as const) {
      expect(chanceFor('Poke', 20, 20, status)).toBeCloseTo(bare + STATUS_BONUS, 5);
    }
  });

  it('never reach certainty, however weakened', () => {
    expect(chanceFor('Ultra', 0, 20, 'Asleep')).toBeLessThanOrEqual(MAX_CHANCE);
    expect(chanceFor('Ultra', 0, 20, 'Asleep')).toBe(MAX_CHANCE);
  });

  it('never go below zero, and a null target is hopeless', () => {
    expect(chanceAgainst('Poke', null)).toBe(0);
    expect(chanceFor('Poke', 20, 0, null)).toBeGreaterThanOrEqual(0);
  });

  it('read max HP off the combatant, so a battle Health buff counts against the catch', () => {
    const buffed = target(20, 20);
    buffed.currentStats.health = 40;
    // Same current HP, bigger ceiling: the mon is now at 50% and easier to catch.
    expect(chanceAgainst('Poke', buffed)).toBeGreaterThan(chanceAgainst('Poke', target(20, 20)));
  });

  it('round to whole percent for display rather than truncating', () => {
    // 0.10 + 0.45 * (1 - 13/20) = 0.2575 -> 26%, not 25%.
    expect(percentAgainst('Poke', target(13, 20))).toBe(26);
  });
});

describe('the roll', () => {
  it('is deterministic for a given stream', () => {
    const a = createRandom(7);
    const b = createRandom(7);
    const results = Array.from({ length: 30 }, () => rollCatch('Poke', target(10), a));
    const same = Array.from({ length: 30 }, () => rollCatch('Poke', target(10), b));
    expect(results).toEqual(same);
  });

  it('lands near the stated odds over many throws', () => {
    const rng = createRandom(99);
    const stated = chanceAgainst('Great', target(5, 20, 'Asleep'));

    let caught = 0;
    const trials = 20_000;
    for (let i = 0; i < trials; i++) {
      if (rollCatch('Great', target(5, 20, 'Asleep'), rng)) caught++;
    }

    expect(caught / trials).toBeGreaterThan(stated - 0.02);
    expect(caught / trials).toBeLessThan(stated + 0.02);
  });

  it('a weakened target really is caught more often than a healthy one', () => {
    const rng = createRandom(3);
    let healthy = 0;
    let hurt = 0;
    for (let i = 0; i < 4000; i++) {
      if (rollCatch('Poke', target(20, 20), rng)) healthy++;
      if (rollCatch('Poke', target(1, 20), rng)) hurt++;
    }
    expect(hurt).toBeGreaterThan(healthy * 2);
  });
});

describe('what a catch produces', () => {
  const charmander = speciesNamed('Charmander')!;

  it('is a fresh full-health instance of the species', () => {
    const mon = caughtInstance(charmander.id, 'Ultra', 5, 0, 'c1')!;
    expect(mon.speciesId).toBe(charmander.id);
    expect(mon.currentHP).toBeNull();
    expect(mon.timesEvolved).toBe(0);
  });

  it('gets its own instance id, so it does not grow along the enemy line', () => {
    // The growth draw is keyed to the id; reusing the wild mon's would make the catch a copy of
    // the mon that was about to beat you.
    const a = caughtInstance(charmander.id, 'Ultra', 10, 0, 'c-a')!;
    const b = caughtInstance(charmander.id, 'Ultra', 10, 0, 'c-b')!;
    expect(statsOf(a)).not.toEqual(statsOf(b));
  });

  it('keeps the target EXP under an Ultra Ball', () => {
    expect(caughtInstance(charmander.id, 'Ultra', 30, 0, 'c')!.exp).toBe(30);
  });

  it('caps what a weaker ball keeps, yielding an under-grown catch', () => {
    const poke = caughtInstance(charmander.id, 'Poke', 30, 0, 'c')!;
    const ultra = caughtInstance(charmander.id, 'Ultra', 30, 0, 'c')!;

    expect(poke.exp).toBe(expCap('Poke'));
    expect(poke.exp).toBeLessThan(ultra.exp);
  });

  it('never lands below the run catch-up floor, so a catch is usable straight away', () => {
    const mon = caughtInstance(charmander.id, 'Poke', 1, 9, 'c')!;
    expect(mon.exp).toBe(9);
  });

  it('returns nothing for a species that is not in the roster', () => {
    expect(caughtInstance(99999, 'Ultra', 0, 0, 'c')).toBeNull();
  });
});

describe('the loop the odds are meant to create', () => {
  it('makes a full-health target a poor use of any ball', () => {
    for (const tier of BALL_TIERS) {
      expect(chanceAgainst(tier, target(20, 20))).toBeLessThanOrEqual(0.35);
    }
  });

  it('makes a weakened, statused target worth the throw even with the cheapest ball', () => {
    expect(chanceAgainst('Poke', target(2, 20, 'Asleep'))).toBeGreaterThan(0.6);
  });

  it('keeps a better ball meaningfully better at every health level', () => {
    for (const hp of [20, 10, 1]) {
      const poke = chanceAgainst('Poke', target(hp, 20));
      const ultra = chanceAgainst('Ultra', target(hp, 20));
      expect(ultra).toBeGreaterThanOrEqual(poke);
    }
  });
});
