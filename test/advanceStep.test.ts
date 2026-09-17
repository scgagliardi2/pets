/**
 * Step-loop unit tests. The beat ordering in battle-sim-spec.md §3 is the thing small deviations
 * silently break, so each beat gets its own cases.
 */

import { describe, expect, it } from 'vitest';

import {
  CHARGE_THRESHOLD,
  SUDDEN_DEATH_STEP,
  advanceStep,
  advanceStepMutable,
  createRandom,
  makeBattleState,
  makeCombatant,
  removeCaught,
  suddenDeathDamageAt,
  type Combatant,
  type CombatantSpec,
  type EffectDefinition,
  type StepEvent,
} from '../src/sim/index.js';

const mon = (spec: Partial<CombatantSpec> & { instanceId: string }): Combatant =>
  makeCombatant({ attack: 1, health: 10, speed: 1, ...spec });

const withPassive = (
  instanceId: string,
  effects: EffectDefinition[],
  spec: Partial<CombatantSpec> = {},
): Combatant =>
  mon({ instanceId, speed: CHARGE_THRESHOLD, ...spec, passive: { id: `${instanceId}-p`, effects } });

const rng = () => createRandom(1);

const step = (a: Combatant[], b: Combatant[]) => {
  const state = makeBattleState(a, b);
  const events = advanceStepMutable(state, rng());
  return { state, events };
};

const kinds = (events: StepEvent[]) => events.map((e) => e.kind);

describe('beat 1 — attack exchange', () => {
  it('is simultaneous: both Leads trade even when the blow is lethal both ways', () => {
    const a = mon({ instanceId: 'a', attack: 10, health: 10 });
    const b = mon({ instanceId: 'b', attack: 10, health: 10 });
    const { state } = step([a], [b]);

    expect(state.lineUpA).toHaveLength(0);
    expect(state.lineUpB).toHaveLength(0);
  });

  it('does not happen when one side has no Lead', () => {
    const a = mon({ instanceId: 'a', attack: 5 });
    const { events } = step([a], []);
    expect(kinds(events)).not.toContain('Damage');
  });

  it('ignores Speed entirely', () => {
    const fast = mon({ instanceId: 'fast', attack: 3, health: 20, speed: 3 });
    const slow = mon({ instanceId: 'slow', attack: 3, health: 20, speed: 1 });
    const { state } = step([fast], [slow]);

    expect(state.lineUpA[0]!.currentHP).toBe(17);
    expect(state.lineUpB[0]!.currentHP).toBe(17);
  });
});

describe('beat 2 — charge accumulation', () => {
  it('grants exactly Speed per Step', () => {
    const a = mon({ instanceId: 'a', attack: 0, speed: 2 });
    const b = mon({ instanceId: 'b', attack: 0, speed: 1 });
    const { state } = step([a], [b]);

    expect(state.lineUpA[0]!.charge).toBe(2);
    expect(state.lineUpB[0]!.charge).toBe(1);
  });

  it('accrues for the Support as well as the Lead', () => {
    const lead = mon({ instanceId: 'lead', attack: 0, speed: 1 });
    const support = mon({ instanceId: 'support', attack: 0, speed: 2 });
    const { state } = step([lead, support], [mon({ instanceId: 'x', attack: 0 })]);

    expect(state.lineUpA[1]!.charge).toBe(2);
  });

  it('does not accrue for dormant mons behind the Support', () => {
    const third = mon({ instanceId: 'third', attack: 0, speed: 3 });
    const { state } = step(
      [mon({ instanceId: 'l', attack: 0 }), mon({ instanceId: 's', attack: 0 }), third],
      [mon({ instanceId: 'x', attack: 0 })],
    );

    expect(state.lineUpA[2]!.charge).toBe(0);
  });

  it('is halved by Paralyzed, truncating toward zero', () => {
    const a = mon({ instanceId: 'a', attack: 0, speed: 3 });
    a.status = 'Paralyzed';
    const { state } = step([a], [mon({ instanceId: 'x', attack: 0 })]);

    // 3 * 0.5 = 1.5, truncated to 1. This truncation is why Ice and Ground are a flat charge
    // deficit rather than a rate multiplier.
    expect(state.lineUpA[0]!.charge).toBe(1);
  });

  it('is zeroed by Asleep', () => {
    const a = mon({ instanceId: 'a', attack: 0, speed: 3 });
    a.status = 'Asleep';
    const { state } = step([a], [mon({ instanceId: 'x', attack: 0 })]);

    expect(state.lineUpA[0]!.charge).toBe(0);
  });

  it('a mon promoted this Step does not accrue this Step', () => {
    // 'dormant' is third in line and inactive during beat 2, so it accrues nothing even though
    // beat 4 promotes it to Support. It starts accruing next Step.
    const dyingLead = mon({ instanceId: 'dying', attack: 0, health: 1 });
    const support = mon({ instanceId: 'support', attack: 0, speed: 1 });
    const dormant = mon({ instanceId: 'dormant', attack: 0, speed: 3 });
    const killer = mon({ instanceId: 'killer', attack: 5, health: 99 });

    const { state } = step([dyingLead, support, dormant], [killer]);

    expect(state.lineUpA.map((c) => c.instanceId)).toEqual(['support', 'dormant']);
    expect(state.lineUpA[1]!.charge).toBe(0);
    // The old Support was already active, so it did accrue — as a Support, once.
    expect(state.lineUpA[0]!.charge).toBe(1);
  });

  it('a mon with no passive still resets its charge at the threshold', () => {
    // Surprising but deliberate, and matching the Unity implementation: the trigger set is built
    // from charge alone, and the meter empties whether or not there was anything to spend it on.
    // A UI reading charge as "progress toward something" must not assume a passive exists.
    const plain = mon({ instanceId: 'plain', attack: 0, speed: 3 });
    const { state, events } = step([plain], [mon({ instanceId: 'x', attack: 0 })]);

    expect(kinds(events)).toContain('PassiveTriggered');
    expect(state.lineUpA[0]!.charge).toBe(0);
  });
});

describe('beat 3 — passive resolution', () => {
  it('fires at the threshold and resets charge to zero, discarding overshoot', () => {
    // Speed 4 overshoots a threshold of 3; the meter still resets to 0, not to 1.
    const a = withPassive('a', [{ type: 'Shield', target: 'Self', amount: 1 }], {
      attack: 0,
      speed: 4,
    });
    const { state } = step([a], [mon({ instanceId: 'x', attack: 0 })]);

    expect(state.lineUpA[0]!.charge).toBe(0);
    expect(state.lineUpA[0]!.shield).toBe(1);
  });

  it('does not fire below the threshold', () => {
    const a = withPassive('a', [{ type: 'Shield', target: 'Self', amount: 1 }], {
      attack: 0,
      speed: 1,
    });
    const { state, events } = step([a], [mon({ instanceId: 'x', attack: 0 })]);

    expect(kinds(events)).not.toContain('PassiveTriggered');
    expect(state.lineUpA[0]!.shield).toBe(0);
  });

  it('fixes the trigger set at the start of the beat — no same-Step cascade', () => {
    // The Lead's passive dumps charge onto its ally; the ally must not also fire this Step.
    const lead = withPassive('lead', [{ type: 'ModifyChargeRate', target: 'Ally', amount: 900 }], {
      attack: 0,
      speed: 3,
    });
    const support = withPassive('support', [{ type: 'Shield', target: 'Self', amount: 5 }], {
      attack: 0,
      speed: 1,
    });

    const { state, events } = step([lead, support], [mon({ instanceId: 'x', attack: 0 })]);

    const triggered = events
      .filter((e) => e.kind === 'PassiveTriggered')
      .map((e) => e.sourceInstanceId);
    expect(triggered).toEqual(['lead']);
    expect(state.lineUpA[1]!.shield).toBe(0);
  });

  it('a mon at 0 HP still fires its passive; only its targets are HP-gated', () => {
    // 'doomed' dies in beat 1 but is not removed until beat 4, so it still triggers.
    const doomed = withPassive('doomed', [{ type: 'DealDamage', target: 'EnemyLead', amount: 4 }], {
      attack: 0,
      health: 1,
      speed: 3,
    });
    const killer = mon({ instanceId: 'killer', attack: 5, health: 20 });

    const { state, events } = step([doomed], [killer]);

    expect(kinds(events)).toContain('PassiveTriggered');
    expect(state.lineUpB[0]!.currentHP).toBe(16);
  });

  it('skips an effect whose target is already at 0 HP', () => {
    const attacker = withPassive(
      'attacker',
      [{ type: 'DealDamage', target: 'EnemyLead', amount: 5 }],
      { attack: 10, health: 50, speed: 3 },
    );
    const victim = mon({ instanceId: 'victim', attack: 0, health: 10 });

    const { events } = step([attacker], [victim]);

    // The exchange already put the victim at 0; the passive's damage finds no valid target.
    const toVictim = events.filter((e) => e.kind === 'Damage' && e.targetInstanceId === 'victim');
    expect(toVictim).toHaveLength(1);
    expect(toVictim[0]!.amount).toBe(10);
  });

  it('the exchange emits a Damage event even for a 0-Attack Lead', () => {
    // Faithful to Unity: the exchange calls the damage path unconditionally when both Leads
    // exist. The animation layer therefore has to tolerate a Damage event of amount 0 rather
    // than treating every Damage event as a hit worth flashing.
    const a = mon({ instanceId: 'a', attack: 0, health: 10 });
    const b = mon({ instanceId: 'b', attack: 4, health: 10 });

    const { events } = step([a], [b]);
    const toB = events.filter((e) => e.kind === 'Damage' && e.targetInstanceId === 'b');

    expect(toB).toHaveLength(1);
    expect(toB[0]!.amount).toBe(0);
  });

  it('orders same-Step triggers: Leads before Supports', () => {
    const leadA = withPassive('leadA', [{ type: 'Shield', target: 'Self', amount: 1 }], {
      attack: 0,
      speed: 3,
    });
    const supportA = withPassive('supportA', [{ type: 'Shield', target: 'Self', amount: 1 }], {
      attack: 0,
      speed: 3,
    });
    const leadB = withPassive('leadB', [{ type: 'Shield', target: 'Self', amount: 1 }], {
      attack: 0,
      speed: 3,
    });

    const { events } = step([leadA, supportA], [leadB]);
    const triggered = events
      .filter((e) => e.kind === 'PassiveTriggered')
      .map((e) => e.sourceInstanceId);

    expect(triggered).toEqual(['leadA', 'leadB', 'supportA']);
  });

  it('breaks ties within a role by Speed, higher first', () => {
    const slowLead = withPassive('slow', [{ type: 'Shield', target: 'Self', amount: 1 }], {
      attack: 0,
      speed: 3,
    });
    const fastLead = withPassive('fast', [{ type: 'Shield', target: 'Self', amount: 1 }], {
      attack: 0,
      speed: 6,
    });

    const { events } = step([slowLead], [fastLead]);
    const triggered = events
      .filter((e) => e.kind === 'PassiveTriggered')
      .map((e) => e.sourceInstanceId);

    expect(triggered).toEqual(['fast', 'slow']);
  });

  it('breaks a full tie by side, A first', () => {
    const a = withPassive('a', [{ type: 'Shield', target: 'Self', amount: 1 }], {
      attack: 0,
      speed: 3,
    });
    const b = withPassive('b', [{ type: 'Shield', target: 'Self', amount: 1 }], {
      attack: 0,
      speed: 3,
    });

    const { events } = step([a], [b]);
    const triggered = events
      .filter((e) => e.kind === 'PassiveTriggered')
      .map((e) => e.sourceInstanceId);

    expect(triggered).toEqual(['a', 'b']);
  });
});

describe('beat 3.5 — status ticks', () => {
  it('poison grows each tick and resets to severity 1 on re-application', () => {
    const victim = mon({ instanceId: 'victim', attack: 0, health: 100 });
    victim.status = 'Poisoned';
    victim.statusTickDamage = 2;
    victim.poisonStacks = 1;

    const state = makeBattleState([victim], [mon({ instanceId: 'x', attack: 0 })]);
    const r = rng();
    advanceStepMutable(state, r); // 2 * 1
    advanceStepMutable(state, r); // 2 * 2
    advanceStepMutable(state, r); // 2 * 3

    expect(state.lineUpA[0]!.currentHP).toBe(100 - (2 + 4 + 6));
  });

  it('burn does not stack', () => {
    const victim = mon({ instanceId: 'victim', attack: 0, health: 100 });
    victim.status = 'Burned';
    victim.statusTickDamage = 3;

    const state = makeBattleState([victim], [mon({ instanceId: 'x', attack: 0 })]);
    const r = rng();
    advanceStepMutable(state, r);
    advanceStepMutable(state, r);

    expect(state.lineUpA[0]!.currentHP).toBe(94);
  });

  it('bypasses shield and damage reduction', () => {
    const victim = mon({ instanceId: 'victim', attack: 0, health: 100 });
    victim.status = 'Burned';
    victim.statusTickDamage = 5;
    victim.shield = 50;
    victim.damageReductionFlat = 50;

    const { state } = step([victim], [mon({ instanceId: 'x', attack: 0 })]);

    expect(state.lineUpA[0]!.shield).toBe(50);
    expect(state.lineUpA[0]!.currentHP).toBe(95);
  });

  it('a status applied by a passive this Step already ticks this Step', () => {
    const poisoner = withPassive(
      'poisoner',
      [{ type: 'ApplyStatus', target: 'EnemyLead', amount: 3, status: 'Poisoned' }],
      { attack: 0, speed: 3 },
    );
    const victim = mon({ instanceId: 'victim', attack: 0, health: 20 });

    const { state } = step([poisoner], [victim]);

    expect(state.lineUpB[0]!.currentHP).toBe(17);
  });

  it('a tick can itself cause a faint', () => {
    const victim = mon({ instanceId: 'victim', attack: 0, health: 2 });
    victim.status = 'Poisoned';
    victim.statusTickDamage = 5;
    victim.poisonStacks = 1;

    const { state, events } = step([victim], [mon({ instanceId: 'x', attack: 0 })]);

    expect(state.lineUpA).toHaveLength(0);
    expect(kinds(events)).toContain('Faint');
  });
});

describe('beat 3.6 — sudden death', () => {
  it('deals nothing before its Step', () => {
    expect(suddenDeathDamageAt(SUDDEN_DEATH_STEP - 1)).toBe(0);
  });

  it('escalates by one each Step from its Step', () => {
    expect(suddenDeathDamageAt(SUDDEN_DEATH_STEP)).toBe(1);
    expect(suddenDeathDamageAt(SUDDEN_DEATH_STEP + 1)).toBe(2);
    expect(suddenDeathDamageAt(SUDDEN_DEATH_STEP + 5)).toBe(6);
  });

  it('hits both Leads and never the Supports', () => {
    const state = makeBattleState(
      [mon({ instanceId: 'leadA', attack: 0, health: 999 }), mon({ instanceId: 'supportA', attack: 0, health: 999 })],
      [mon({ instanceId: 'leadB', attack: 0, health: 999 })],
    );
    state.stepNumber = SUDDEN_DEATH_STEP - 1;
    const events = advanceStepMutable(state, rng());

    const hit = events.filter((e) => e.kind === 'SuddenDeath').map((e) => e.targetInstanceId);
    expect(hit).toEqual(['leadA', 'leadB']);
    expect(state.lineUpA[1]!.currentHP).toBe(999);
  });

  it('bypasses shield, reduction and lifesteal', () => {
    const tank = mon({ instanceId: 'tank', attack: 0, health: 100 });
    tank.shield = 50;
    tank.damageReductionFlat = 50;
    tank.lifestealPercent = 1;

    const state = makeBattleState([tank], [mon({ instanceId: 'x', attack: 0, health: 100 })]);
    state.stepNumber = SUDDEN_DEATH_STEP - 1;
    advanceStepMutable(state, rng());

    expect(state.lineUpA[0]!.shield).toBe(50);
    expect(state.lineUpA[0]!.currentHP).toBe(99);
  });
});

describe('beat 4 — faints and promotion', () => {
  it('promotes the Support to Lead and the next dormant mon to Support', () => {
    const state = makeBattleState(
      [
        mon({ instanceId: 'l', attack: 0, health: 1 }),
        mon({ instanceId: 's', attack: 0, health: 50 }),
        mon({ instanceId: 'd', attack: 0, health: 50 }),
      ],
      [mon({ instanceId: 'killer', attack: 5, health: 50 })],
    );
    const events = advanceStepMutable(state, rng());

    expect(state.lineUpA.map((c) => c.instanceId)).toEqual(['s', 'd']);
    const promoted = events
      .filter((e) => e.kind === 'Promotion')
      .map((e) => e.sourceInstanceId);
    expect(promoted).toEqual(['s', 'd']);
  });

  it('removes a fainted Support without disturbing the Lead', () => {
    const lead = mon({ instanceId: 'l', attack: 0, health: 50 });
    const doomedSupport = mon({ instanceId: 's', attack: 0, health: 50 });
    doomedSupport.status = 'Burned';
    doomedSupport.statusTickDamage = 99;

    const state = makeBattleState([lead, doomedSupport], [mon({ instanceId: 'x', attack: 0 })]);
    advanceStepMutable(state, rng());

    expect(state.lineUpA.map((c) => c.instanceId)).toEqual(['l']);
  });

  it('is the only place removal happens — a 0 HP mon still acts earlier in its Step', () => {
    const doomed = mon({ instanceId: 'doomed', attack: 7, health: 1 });
    const killer = mon({ instanceId: 'killer', attack: 5, health: 20 });

    const { state } = step([doomed], [killer]);

    // The doomed Lead still landed its blow in beat 1 despite dying to the same exchange.
    expect(state.lineUpB[0]!.currentHP).toBe(13);
  });
});

describe('immutability of the public entry point', () => {
  it('advanceStep leaves the caller state untouched', () => {
    const a = mon({ instanceId: 'a', attack: 3, health: 10 });
    const b = mon({ instanceId: 'b', attack: 3, health: 10 });
    const before = makeBattleState([a], [b]);

    const { state: after } = advanceStep(before, rng());

    expect(before.stepNumber).toBe(0);
    expect(before.lineUpA[0]!.currentHP).toBe(10);
    expect(after.stepNumber).toBe(1);
    expect(after.lineUpA[0]!.currentHP).toBe(7);
  });
});

describe('removeCaught', () => {
  it('promotes exactly as a faint would, but raises Caught rather than Faint', () => {
    const state = makeBattleState(
      [mon({ instanceId: 'mine' })],
      [mon({ instanceId: 'target' }), mon({ instanceId: 'next' })],
    );
    const { state: after, events } = removeCaught(state, 'B', 'target');

    expect(kinds(events)).toEqual(['Caught', 'Promotion']);
    expect(after.lineUpB.map((c) => c.instanceId)).toEqual(['next']);
  });

  it('no-ops on a mon that has already left, so a stale throw cannot remove twice', () => {
    const state = makeBattleState([mon({ instanceId: 'mine' })], [mon({ instanceId: 'other' })]);
    const { events } = removeCaught(state, 'B', 'ghost');

    expect(events).toHaveLength(0);
  });
});
