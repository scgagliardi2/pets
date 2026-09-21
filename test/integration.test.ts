/**
 * Content and sim together: real roster species actually fighting.
 *
 * The sim tests use hand-built combatants, and the content tests never run a battle. These cover
 * the seam, and record what the real content does to fight length — which is a balance property,
 * not just a correctness one.
 */

import { describe, expect, it } from 'vitest';

import { STEP_CAP, SUDDEN_DEATH_STEP, runPrecomputed } from '../src/sim/index.js';
import { createInstance, statsOf, toCombatant, toLineUp } from '../src/content/factory.js';
import { BASE_SPEED, SPECIES, speciesNamed, speciesOfType } from '../src/content/index.js';

const named = (name: string) => {
  const s = speciesNamed(name);
  expect(s, `${name} is in the roster`).not.toBeNull();
  return s!;
};

describe('building combatants from roster species', () => {
  it('carries stats, types and the passive onto the combatant', () => {
    const charmander = toCombatant(createInstance('Charmander', { instanceId: 'c1' }));

    // 4 base Health, tripled; Speed is the flat starting value, not the tier-derived one.
    expect(charmander.currentStats).toEqual({ attack: 3, health: 12, speed: BASE_SPEED, special: 3 });
    expect(charmander.currentHP).toBe(12);
    expect(charmander.types).toEqual(['Fire']);
    expect(charmander.passive?.displayName).toBe('Ember Burst');
  });

  it('starts a fresh mon at full HP and a clean slate', () => {
    const c = toCombatant(createInstance('Squirtle', { instanceId: 's1' }));

    expect(c.currentHP).toBe(c.currentStats.health);
    expect(c.status).toBeNull();
    expect(c.shield).toBe(0);
    expect(c.charge).toBe(0);
    expect(c.poisonStacks).toBe(0);
  });

  it('honours carried damage', () => {
    const c = toCombatant(createInstance('Squirtle', { instanceId: 's1', currentHP: 2 }));
    expect(c.currentHP).toBe(2);
  });

  it('derives stats from the allocation rather than storing them', () => {
    const fresh = createInstance('Charmander', { instanceId: 'c1' });
    const grown = {
      ...fresh,
      exp: 20,
      allocation: { attack: 20, health: 0, special: 0, speed: 0 },
    };

    expect(statsOf(fresh)).not.toEqual(statsOf(grown));
  });

  it('shows nothing for EXP that has not been spent yet', () => {
    const fresh = createInstance('Charmander', { instanceId: 'c1' });
    const unspent = { ...fresh, exp: 20 };

    expect(statsOf(unspent)).toEqual(statsOf(fresh));
  });

  it('rejects a species that is not in the curated roster', () => {
    // The roster is 183 curated species, not the whole dex.
    expect(() => createInstance('Wurmple')).toThrow();
    expect(() => createInstance(99999)).toThrow();
  });

  it('keeps the run record untouched by a battle', () => {
    // A PokemonInstance and a Combatant are different types precisely so that running a battle
    // can't write shields and poison onto the player's roster.
    const instance = createInstance('Charmander', { instanceId: 'c1' });
    runPrecomputed(toLineUp([instance]), toLineUp([createInstance('Squirtle', { instanceId: 's1' })]), 1);

    expect(instance.currentHP).toBeNull();
    expect(statsOf(instance)).toEqual({ attack: 3, health: 12, speed: BASE_SPEED, special: 3 });
  });
});

describe('real fights resolve', () => {
  it('runs a starter mirror to a real outcome', () => {
    const log = runPrecomputed(
      toLineUp([createInstance('Charmander', { instanceId: 'a1' })]),
      toLineUp([createInstance('Squirtle', { instanceId: 'b1' })]),
      1,
    );

    expect(['SideAWins', 'SideBWins', 'Draw']).toContain(log.outcome);
    expect(log.finalState.stepNumber).toBeLessThan(STEP_CAP);
  });

  it('never hits the safety cap across a broad sweep of the roster', () => {
    // The cap is an engineering backstop; reaching it is a bug, not content. Sudden death is what
    // is supposed to end a fight that won't resolve itself.
    let longest = 0;
    for (let i = 0; i < 300; i++) {
      const pick = (n: number) => SPECIES[(i * 7 + n * 31) % SPECIES.length]!;
      const side = (p: string) =>
        toLineUp([0, 1, 2].map((j) => createInstance(pick(j), { exp: i % 40, instanceId: `${p}${i}-${j}` })));

      const log = runPrecomputed(side('a'), side('b'), i + 1);
      longest = Math.max(longest, log.finalState.stepNumber);
      expect(log.finalState.stepNumber, `fight ${i}`).toBeLessThan(STEP_CAP);
      expect(log.events.at(-1)?.kind).toBe('BattleEnd');
    }
    expect(longest).toBeGreaterThan(0);
  });
});

describe('what the real content does to fight length', () => {
  it('stacked Grass lifesteal still reaches sudden death', () => {
    // The mechanism earns its place against real content, not just against a synthetic case.
    // Six Grass-types give +60% lifesteal from the synergy alone, before a passive fires.
    const grass = speciesOfType('Grass').slice(0, 6);
    const side = (p: string) =>
      toLineUp(grass.map((s, i) => createInstance(s, { exp: 20, instanceId: `${p}${i}` })));

    const log = runPrecomputed(side('a'), side('b'), 1);

    expect(log.events.some((e) => e.kind === 'SuddenDeath')).toBe(true);
    expect(log.finalState.stepNumber).toBeGreaterThanOrEqual(SUDDEN_DEATH_STEP);
    expect(log.finalState.stepNumber).toBeLessThan(STEP_CAP);
  });

  it("ADR 0014's own two-Bulbasaur example no longer stalls", () => {
    // Recorded rather than fixed. The ADR says two Bulbasaurs with Vine Drain become unkillable
    // from Step 12; under ADR 0009's retuned tier lines they resolve in a handful of Steps,
    // because Attack now grows alongside Health instead of the pair staying locked. Sudden death
    // is still right — the Grass-stack case above proves the class of problem is real — but the
    // canonical example in the ADR is stale and would mislead anyone checking it by hand.
    const log = runPrecomputed(
      toLineUp([createInstance('Bulbasaur', { exp: 6, instanceId: 'a1' })]),
      toLineUp([createInstance('Bulbasaur', { exp: 6, instanceId: 'b1' })]),
      1,
    );

    expect(log.finalState.stepNumber).toBeLessThan(12);
    expect(log.events.some((e) => e.kind === 'SuddenDeath')).toBe(false);
  });

  it('a tier-6 mirror is as short as a tier-1 one', () => {
    // Attack is capped at just under half of what Speed leaves, so Health only ever edges ahead
    // by a point or two: Mewtwo is 27/28. Tier buys bigger numbers, not longer fights — it is EXP
    // growth that lengthens them, which is what ADR 0009 intended.
    const t1 = runPrecomputed(
      toLineUp([createInstance('Charmander', { instanceId: 'a1' })]),
      toLineUp([createInstance('Squirtle', { instanceId: 'b1' })]),
      1,
    );
    const t6 = runPrecomputed(
      toLineUp([createInstance('Mewtwo', { instanceId: 'a2' })]),
      toLineUp([createInstance('Rayquaza', { instanceId: 'b2' })]),
      1,
    );

    expect(Math.abs(t6.finalState.stepNumber - t1.finalState.stepNumber)).toBeLessThanOrEqual(2);
  });

  it('invested stats are what lengthen a fight', () => {
    // EXP is evolution progress now and moves no stats on its own, so the thing that lengthens a
    // fight is what has actually been put into Health.
    const fight = (health: number) =>
      runPrecomputed(
        toLineUp([
          createInstance('Charmander', {
            instanceId: 'a1',
            allocation: { attack: 0, health, special: 0, speed: 0 },
          }),
        ]),
        toLineUp([
          createInstance('Squirtle', {
            instanceId: 'b1',
            allocation: { attack: 0, health, special: 0, speed: 0 },
          }),
        ]),
        1,
      ).finalState.stepNumber;

    expect(fight(30)).toBeGreaterThan(fight(0));
  });

  it('a fight can legitimately run past the Step the spec assumed was the ceiling', () => {
    // battle-sim-spec.md puts the longest real fight at 17 Steps, and SUDDEN_DEATH_STEP (30) was
    // chosen against that figure. The Grass-stack case above runs well past it, so sudden death
    // can now decide a fight that was resolving on its own. Recorded so the number gets revisited
    // deliberately rather than discovered during balancing.
    const grass = speciesOfType('Grass').slice(0, 6);
    const side = (p: string) =>
      toLineUp(grass.map((s, i) => createInstance(s, { exp: 20, instanceId: `${p}${i}` })));

    expect(runPrecomputed(side('a'), side('b'), 1).finalState.stepNumber).toBeGreaterThan(17);
  });
});

describe('type synergies fire off real species types', () => {
  it('a mono-type team gets its synergy announced', () => {
    const fire = ['Charmander', 'Cyndaquil', 'Torchic'].map((n, i) =>
      createInstance(named(n), { instanceId: `f${i}` }),
    );
    const log = runPrecomputed(
      toLineUp(fire),
      toLineUp([createInstance('Squirtle', { instanceId: 'b1' })]),
      1,
    );

    const announced = log.events.filter(
      (e) => e.kind === 'TypeSynergy' && e.sourceSide === 'A' && e.synergyType === 'Fire',
    );
    expect(announced).toHaveLength(1);
    expect(announced[0]?.amount).toBe(3);
  });

  it('counts a dual-type species toward both synergies', () => {
    // Shedinja is Bug/Ghost.
    const log = runPrecomputed(
      toLineUp([createInstance('Shedinja', { instanceId: 'a1' })]),
      toLineUp([createInstance('Squirtle', { instanceId: 'b1' })]),
      1,
    );

    const types = log.events
      .filter((e) => e.kind === 'TypeSynergy' && e.sourceSide === 'A')
      .map((e) => e.synergyType);
    expect(types).toEqual(['Bug', 'Ghost']);
  });
});

describe('what tripled Health did to fight length', () => {
  const fightLengths = (count: number): number[] => {
    const lengths: number[] = [];
    for (let i = 0; i < count; i++) {
      const pick = (n: number) => SPECIES[(i * 13 + n * 47) % SPECIES.length]!;
      const side = (p: string) =>
        toLineUp([0, 1, 2].map((j) => createInstance(pick(j), { exp: i % 30, instanceId: `${p}${i}-${j}` })));
      lengths.push(runPrecomputed(side('a'), side('b'), i + 1).finalState.stepNumber);
    }
    return lengths;
  };

  it('makes a typical fight run well past a handful of Steps', () => {
    const lengths = fightLengths(200).sort((a, b) => a - b);
    const median = lengths[Math.floor(lengths.length / 2)]!;

    // Before the multiplier this sat around six or seven.
    expect(median).toBeGreaterThan(8);
  });

  it('still never reaches the safety cap', () => {
    // Longer fights must not become unresolvable ones; sudden death has to keep working against
    // three times the health it was tuned for.
    for (const length of fightLengths(200)) {
      expect(length).toBeLessThan(STEP_CAP);
    }
  });

  it('now routinely outruns the Step sudden death starts at', () => {
    // Recorded, not fixed. SUDDEN_DEATH_STEP is 30 and was chosen when the longest real fight was
    // 17. Fights now reach the low forties, so sudden death has gone from a safety valve that
    // fired once in four thousand fights to something that decides roughly one in sixteen. The
    // constant needs raising in the balance pass; this test will fail when it is, which is the
    // point.
    const longest = Math.max(...fightLengths(200));
    expect(longest).toBeGreaterThan(SUDDEN_DEATH_STEP);
  });
});
