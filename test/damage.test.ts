/**
 * Damage pipeline, and the three bounds that stop a fight becoming unwinnable
 * (battle-sim-spec.md §9, REACT_REBUILD_REFERENCE.md §6.4).
 */

import { describe, expect, it } from 'vitest';

import {
  MAX_LIFESTEAL_PERCENT,
  MINIMUM_ATTACK_DAMAGE,
  STEP_CAP,
  SUDDEN_DEATH_STEP,
  makeCombatant,
  roundHalfAwayFromZero,
  runPrecomputed,
  takeHit,
  type Combatant,
  type CombatantSpec,
} from '../src/sim/index.js';

const mon = (spec: Partial<CombatantSpec> & { instanceId: string }): Combatant =>
  makeCombatant({ attack: 1, health: 10, speed: 1, ...spec });

describe('takeHit — reduction, then shield, then HP', () => {
  it('applies flat reduction before the shield sees the blow', () => {
    const target = mon({ instanceId: 't', health: 100 });
    target.damageReductionFlat = 3;
    target.shield = 10;

    const { hpDamage, shieldAbsorbed } = takeHit(target, 8);

    expect(shieldAbsorbed).toBe(5);
    expect(hpDamage).toBe(0);
    expect(target.shield).toBe(5);
    expect(target.currentHP).toBe(100);
  });

  it('spills past a partial shield onto HP', () => {
    const target = mon({ instanceId: 't', health: 100 });
    target.shield = 3;

    const { hpDamage, shieldAbsorbed } = takeHit(target, 10);

    expect(shieldAbsorbed).toBe(3);
    expect(hpDamage).toBe(7);
    expect(target.shield).toBe(0);
    expect(target.currentHP).toBe(93);
  });

  it('floors a connecting attack at the minimum, however much reduction has stacked', () => {
    const target = mon({ instanceId: 't', health: 100 });
    target.damageReductionFlat = 9999;

    const { hpDamage } = takeHit(target, 5);

    expect(hpDamage).toBe(MINIMUM_ATTACK_DAMAGE);
  });

  it('does not turn a zero-damage hit into the minimum', () => {
    const target = mon({ instanceId: 't', health: 100 });
    const { hpDamage } = takeHit(target, 0);

    expect(hpDamage).toBe(0);
    expect(target.currentHP).toBe(100);
  });
});

describe('lifesteal', () => {
  it('heals off HP damage actually dealt, not the raw amount', () => {
    const attacker = mon({ instanceId: 'a', attack: 10, health: 100 });
    attacker.currentHP = 50;
    attacker.lifestealPercent = 0.5;

    // The defender's shield eats 6 of the 10, so only 4 reaches HP and only 2 heals back.
    const defender = mon({ instanceId: 'd', health: 100 });
    defender.shield = 6;

    const log = runPrecomputed([attacker], [defender], 1);
    const heal = log.events.find((e) => e.kind === 'LifestealHeal');

    expect(heal?.amount).toBe(2);
  });

  it('never heals past max HP', () => {
    const attacker = mon({ instanceId: 'a', attack: 10, health: 100 });
    attacker.currentHP = 99;
    attacker.lifestealPercent = 1;

    const log = runPrecomputed([attacker], [mon({ instanceId: 'd', attack: 0, health: 100 })], 1);
    const heal = log.events.find((e) => e.kind === 'LifestealHeal');

    expect(heal?.amount).toBe(1);
  });

  it('applies to the basic attack exchange, not just passive damage', () => {
    const attacker = mon({ instanceId: 'a', attack: 4, health: 100 });
    attacker.currentHP = 10;
    attacker.lifestealPercent = 0.5;

    const log = runPrecomputed([attacker], [mon({ instanceId: 'd', attack: 1, health: 100 })], 1);

    expect(log.events.some((e) => e.kind === 'LifestealHeal')).toBe(true);
  });

  it('rounds half away from zero, matching the C# semantics', () => {
    expect(roundHalfAwayFromZero(2.5)).toBe(3);
    expect(roundHalfAwayFromZero(-2.5)).toBe(-3);
    expect(roundHalfAwayFromZero(2.4)).toBe(2);
  });
});

describe('the stalemate that motivated sudden death', () => {
  it('two mons whose lifesteal exceeds their incoming damage still finish the fight', () => {
    // Before sudden death existed, this ground out all 200 Steps of STEP_CAP with the HP bars
    // frozen and then called itself a draw, which on screen is indistinguishable from a hang.
    const drain = (id: string): Combatant => {
      const c = makeCombatant({ instanceId: id, attack: 1, health: 30, speed: 1 });
      c.lifestealPercent = MAX_LIFESTEAL_PERCENT;
      return c;
    };

    const log = runPrecomputed([drain('a')], [drain('b')], 1);

    expect(log.finalState.stepNumber).toBeLessThan(STEP_CAP);
    expect(log.finalState.stepNumber).toBeGreaterThanOrEqual(SUDDEN_DEATH_STEP);
    expect(log.events.some((e) => e.kind === 'SuddenDeath')).toBe(true);
    // A real result, not a timeout.
    expect(log.events.at(-1)?.kind).toBe('BattleEnd');
  });

  it('two mutually unhittable tanks still finish, via the minimum-damage floor', () => {
    const tank = (id: string): Combatant => {
      const c = makeCombatant({ instanceId: id, attack: 1, health: 40, speed: 1 });
      c.damageReductionFlat = 9999;
      return c;
    };

    const log = runPrecomputed([tank('a')], [tank('b')], 1);

    expect(log.finalState.stepNumber).toBeLessThan(STEP_CAP);
    expect(log.outcome).toBe('Draw'); // even trade, both fall together
  });

  it('a lifesteal cap applies however many triggers stack', () => {
    const greedy = makeCombatant({
      instanceId: 'greedy',
      attack: 2,
      health: 50,
      speed: 3,
      passive: {
        id: 'stacking-drain',
        effects: [{ type: 'Lifesteal', target: 'Self', amount: 500 }],
      },
    });

    const log = runPrecomputed([greedy], [mon({ instanceId: 'x', attack: 1, health: 50 })], 1);
    const survivor = [...log.finalState.lineUpA, ...log.finalState.lineUpB].find(
      (c) => c.instanceId === 'greedy',
    );

    if (survivor) {
      expect(survivor.lifestealPercent).toBeLessThanOrEqual(MAX_LIFESTEAL_PERCENT);
    }
    expect(log.finalState.stepNumber).toBeLessThan(STEP_CAP);
  });
});

describe('battle end', () => {
  it('reports a draw when both sides fall in the same Step', () => {
    const log = runPrecomputed(
      [mon({ instanceId: 'a', attack: 10, health: 10 })],
      [mon({ instanceId: 'b', attack: 10, health: 10 })],
      1,
    );

    expect(log.outcome).toBe('Draw');
    expect(log.finalState.lineUpA).toHaveLength(0);
    expect(log.finalState.lineUpB).toHaveLength(0);
  });

  it('closes the log with a BattleEnd carrying the outcome', () => {
    const log = runPrecomputed(
      [mon({ instanceId: 'a', attack: 10, health: 50 })],
      [mon({ instanceId: 'b', attack: 1, health: 5 })],
      1,
    );

    const last = log.events.at(-1);
    expect(last?.kind).toBe('BattleEnd');
    expect(last?.outcome).toBe('SideAWins');
  });

  it('reports survivors through finalState rather than the caller line-ups', () => {
    const mine = mon({ instanceId: 'mine', attack: 10, health: 50 });
    const log = runPrecomputed([mine], [mon({ instanceId: 'theirs', attack: 2, health: 5 })], 1);

    // The caller's own combatant is untouched; the result has to be read off the log.
    expect(mine.currentHP).toBe(50);
    expect(log.finalState.lineUpA[0]!.currentHP).toBe(48);
  });
});
