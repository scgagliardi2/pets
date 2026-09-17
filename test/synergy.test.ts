/**
 * Team type synergies (battle-sim-spec.md §8).
 *
 * The opening is applied and observable *before* Step 1 (ADR 0016). That isn't cosmetic: most of
 * the opening's defences are 1-3 points and the first exchange spends them, so a Shell Guard
 * shield applied inside Step 1 never appears on screen at all.
 */

import { describe, expect, it } from 'vitest';

import {
  CHARGE_THRESHOLD,
  POKEMON_TYPES,
  activeSynergies,
  applyOpeningMutable,
  countTypes,
  effectAtCount,
  makeBattleState,
  makeCombatant,
  synergyDisplayName,
  type Combatant,
  type CombatantSpec,
  type PokemonType,
  type StepEvent,
} from '../src/sim/index.js';

const mon = (
  instanceId: string,
  types: PokemonType[],
  spec: Partial<CombatantSpec> = {},
): Combatant =>
  makeCombatant({ instanceId, attack: 2, health: 10, speed: 1, types, ...spec });

/** Applies the opening to a fresh state and hands back both sides plus the events. */
const open = (a: Combatant[], b: Combatant[]) => {
  const state = makeBattleState(a, b);
  const events = applyOpeningMutable(state);
  return { state, events };
};

const byId = (state: ReturnType<typeof open>['state'], id: string): Combatant =>
  [...state.lineUpA, ...state.lineUpB].find((c) => c.instanceId === id)!;

describe('counting', () => {
  it('counts a dual-type mon toward both of its types', () => {
    const counts = countTypes([mon('m', ['Water', 'Ground'])]);
    expect(counts.Water).toBe(1);
    expect(counts.Ground).toBe(1);
  });

  it('counts a mon listing the same type twice only once', () => {
    const counts = countTypes([mon('m', ['Fire', 'Fire'])]);
    expect(counts.Fire).toBe(1);
  });

  it('counts dormant mons, not just the active pair', () => {
    const counts = countTypes([
      mon('lead', ['Fire']),
      mon('support', ['Fire']),
      mon('dormant', ['Fire']),
    ]);
    expect(counts.Fire).toBe(3);
  });

  it('counts nothing for a typeless line-up, so fixtures play as if synergies did not exist', () => {
    const plain = makeCombatant({ instanceId: 'p', attack: 2, health: 10, speed: 1 });
    const { events } = open([plain], [plain]);
    expect(events).toHaveLength(0);
  });
});

describe('announcements', () => {
  it('emits one TypeSynergy per side per type present, stamped Step 0', () => {
    const { events } = open([mon('a', ['Fire', 'Water'])], [mon('b', ['Steel'])]);
    const announcements = events.filter((e: StepEvent) => e.kind === 'TypeSynergy');

    expect(announcements).toHaveLength(3);
    expect(announcements.every((e) => e.step === 0)).toBe(true);
  });

  it('announces in type-enum order, which is part of the observable stream', () => {
    const { events } = open([mon('a', ['Steel', 'Fire', 'Water'])], []);
    const order = events
      .filter((e) => e.kind === 'TypeSynergy' && e.sourceSide === 'A')
      .map((e) => e.synergyType);

    expect(order).toEqual(['Fire', 'Water', 'Steel']);
  });
});

describe('own stat bonuses', () => {
  it('Normal raises both max Health and current HP, so it reads as a real gain', () => {
    const { state } = open([mon('a', ['Normal']), mon('b', ['Normal'])], []);
    const a = byId(state, 'a');

    expect(a.currentStats.health).toBe(12);
    expect(a.currentHP).toBe(12);
  });

  it('Fighting raises Attack for every mon', () => {
    const { state } = open([mon('a', ['Fighting']), mon('b', [])], []);
    expect(byId(state, 'b').currentStats.attack).toBe(3);
  });

  it('Flying grants Speed per two Flying-types, capped', () => {
    const { state } = open([mon('a', ['Flying']), mon('b', ['Flying'])], []);
    expect(byId(state, 'a').currentStats.speed).toBe(2);
  });

  it('Flying never lowers a mon already past the cap', () => {
    const fast = mon('fast', ['Flying'], { speed: 9 });
    const { state } = open([fast, mon('b', ['Flying'])], []);
    expect(byId(state, 'fast').currentStats.speed).toBe(9);
  });

  it('Bug grants Speed per three Bug-types, uncapped', () => {
    const swarm = [0, 1, 2].map((i) => mon(`bug${i}`, ['Bug'], { speed: 3 }));
    const { state } = open(swarm, []);
    expect(byId(state, 'bug0').currentStats.speed).toBe(4);
  });

  it('Rock grants at least one point per Rock-type, since 10% of a small mon rounds away', () => {
    const { state } = open([mon('lead', ['Rock'], { health: 4 })], []);
    // 10% of 4 rounds to 0, so the floor of 1 per Rock-type applies.
    expect(byId(state, 'lead').currentStats.health).toBe(5);
  });

  it('Rock uses the percentage when it beats the floor', () => {
    const { state } = open([mon('lead', ['Rock'], { health: 30 })], []);
    expect(byId(state, 'lead').currentStats.health).toBe(33);
  });

  it('Ghost costs the Lead HP and pays everyone behind it', () => {
    const { state } = open([mon('lead', ['Ghost'], { health: 20 }), mon('back', [])], []);

    expect(byId(state, 'lead').currentHP).toBe(18);
    expect(byId(state, 'back').currentStats.attack).toBe(3);
    expect(byId(state, 'back').currentStats.health).toBe(11);
  });

  it('Ghost is skipped when the Lead has nobody to give to', () => {
    const { state } = open([mon('lone', ['Ghost'], { health: 20 })], []);
    expect(byId(state, 'lone').currentHP).toBe(20);
  });

  it('Ghost never takes the Lead below 1 HP', () => {
    const frail = mon('frail', ['Ghost', 'Ghost'], { health: 1 });
    const { state } = open([frail, mon('back', [])], []);
    expect(byId(state, 'frail').currentHP).toBe(1);
  });
});

describe('Dragon — Intimidate, applied to the other side', () => {
  it('lowers every enemy mon Attack, floored at 1', () => {
    const { state } = open([mon('d', ['Dragon'])], [mon('e1', [], { attack: 3 }), mon('e2', [])]);

    expect(byId(state, 'e1').currentStats.attack).toBe(2);
    expect(byId(state, 'e2').currentStats.attack).toBe(1);
  });

  it('does not lift a 0-Attack mon up to the floor', () => {
    const { state } = open([mon('d', ['Dragon'])], [mon('pacifist', [], { attack: 0 })]);
    expect(byId(state, 'pacifist').currentStats.attack).toBe(0);
  });
});

describe('defences', () => {
  it('Water shields the front N, and the shield exists before Step 1', () => {
    const { state } = open(
      [mon('a', ['Water']), mon('b', ['Water']), mon('c', [])],
      [],
    );

    expect(byId(state, 'a').shield).toBe(2);
    expect(byId(state, 'b').shield).toBe(2);
    expect(byId(state, 'c').shield).toBe(0);
  });

  it('Steel puts damage reduction on the Lead only', () => {
    const { state } = open([mon('lead', ['Steel']), mon('back', [])], []);

    expect(byId(state, 'lead').damageReductionFlat).toBe(1);
    expect(byId(state, 'back').damageReductionFlat).toBe(0);
  });

  it('Grass gives lifesteal to every mon, capped at 100%', () => {
    const swarm = Array.from({ length: 12 }, (_, i) => mon(`g${i}`, ['Grass']));
    const { state } = open(swarm, []);
    expect(byId(state, 'g0').lifestealPercent).toBe(1);
  });

  it('Fairy wards the back N', () => {
    const { state } = open([mon('front', ['Fairy']), mon('middle', []), mon('back', [])], []);

    expect(byId(state, 'back').statusWards).toBe(1);
    expect(byId(state, 'front').statusWards).toBe(0);
  });

  it('a ward blocks the opening poison and spends itself', () => {
    const { state, events } = open([mon('p', ['Poison'])], [mon('warded', ['Fairy'])]);

    expect(events.some((e) => e.kind === 'StatusBlocked')).toBe(true);
    expect(byId(state, 'warded').status).toBeNull();
    expect(byId(state, 'warded').statusWards).toBe(0);
  });
});

describe('charge', () => {
  it('Electric front-loads the Lead', () => {
    const { state } = open([mon('lead', ['Electric'])], []);
    expect(byId(state, 'lead').charge).toBe(2);
  });

  it('Electric never pushes the Lead past the threshold, so it buys at most one early trigger', () => {
    const { state } = open([mon('lead', ['Electric']), mon('support', ['Electric'])], []);
    // 2 per Electric-type x2 would be 4; the cap holds it at the threshold.
    expect(byId(state, 'lead').charge).toBe(CHARGE_THRESHOLD);
  });

  it('Psychic front-loads the Lead and Support', () => {
    const { state } = open([mon('lead', ['Psychic']), mon('support', [])], []);

    expect(byId(state, 'lead').charge).toBe(1);
    expect(byId(state, 'support').charge).toBe(1);
  });

  it('Ice puts a deficit on the enemy Lead and Support, and charge may go negative', () => {
    const { state } = open([mon('i', ['Ice'])], [mon('lead', []), mon('support', [])]);

    expect(byId(state, 'lead').charge).toBe(-1);
    expect(byId(state, 'support').charge).toBe(-1);
  });

  it('Ground puts a deeper deficit on the enemy Lead alone', () => {
    const { state } = open([mon('g', ['Ground'])], [mon('lead', []), mon('support', [])]);

    expect(byId(state, 'lead').charge).toBe(-2);
    expect(byId(state, 'support').charge).toBe(0);
  });
});

describe('opening blows', () => {
  it('Fire goes through shield and reduction like an attack', () => {
    const { state } = open([mon('f', ['Fire'])], [mon('shielded', ['Water'])]);

    // The Water shield absorbs the 1 point of Ember Burst.
    expect(byId(state, 'shielded').shield).toBe(0);
    expect(byId(state, 'shielded').currentHP).toBe(10);
  });

  it('Dark is true damage and ignores the shield entirely', () => {
    const { state } = open([mon('d', ['Dark'])], [mon('shielded', ['Water'])]);

    expect(byId(state, 'shielded').shield).toBe(1);
    expect(byId(state, 'shielded').currentHP).toBe(9);
  });

  it('Poison opens the enemy Lead poisoned with tick damage scaled by count', () => {
    const { state } = open([mon('p1', ['Poison']), mon('p2', ['Poison'])], [mon('victim', [])]);
    const victim = byId(state, 'victim');

    expect(victim.status).toBe('Poisoned');
    expect(victim.statusTickDamage).toBe(2);
    expect(victim.poisonStacks).toBe(1);
  });

  it('an opening that KOs a Lead promotes before Step 1', () => {
    const burst = Array.from({ length: 5 }, (_, i) => mon(`f${i}`, ['Fire']));
    const { state, events } = open(burst, [
      mon('frail', [], { health: 3 }),
      mon('next', []),
    ]);

    expect(events.some((e) => e.kind === 'Faint' && e.sourceInstanceId === 'frail')).toBe(true);
    expect(state.lineUpB[0]!.instanceId).toBe('next');
  });

  it('an opening that empties a side ends the battle with no Step taken', () => {
    const burst = Array.from({ length: 5 }, (_, i) => mon(`f${i}`, ['Fire']));
    const { state } = open(burst, [mon('frail', [], { health: 3 })]);

    expect(state.lineUpB).toHaveLength(0);
    expect(state.stepNumber).toBe(0);
  });
});

describe('the display helpers a screen reads', () => {
  it('names every one of the 18 types', () => {
    for (const type of POKEMON_TYPES) {
      expect(synergyDisplayName(type).length).toBeGreaterThan(0);
    }
  });

  it('describes every type at a plausible count without falling through', () => {
    for (const type of POKEMON_TYPES) {
      expect(effectAtCount(type, 2)).toMatch(/\S/);
    }
  });

  it('lists a line-up active synergies in type order with resolved numbers', () => {
    const active = activeSynergies([mon('a', ['Steel', 'Fire']), mon('b', ['Fire'])]);

    expect(active.map((s) => s.type)).toEqual(['Fire', 'Steel']);
    expect(active[0]!.title).toBe('Ember Burst x2');
    expect(active[0]!.count).toBe(2);
  });
});
