/**
 * Golden fixture tests.
 *
 * These run the *same* JSON cases the Unity EditMode suite runs (see
 * test/fixtures/PROVENANCE.json). They are the acceptance criterion for the sim port: if these
 * pass, this implementation and the Unity one agree on how combat resolves, rather than this
 * implementation merely agreeing with one reading of the spec.
 *
 * **Run under `LEGACY_CHARGE_CONFIG`, not the game's charge numbers.** The game has deliberately
 * moved to a 100-point charge threshold so Speed can be invested in gradually; the fixtures were
 * calibrated against Unity's 1-3 Speed against a threshold of 3. Four of the six assert exact
 * charge values, and the threshold also decides when passives fire and therefore who wins — so
 * run on the new scale they would all fail, and "fixing" them by rewriting the expectations would
 * throw away the only cross-implementation guarantee this project has. Parameterising charge
 * instead keeps them a real check of the Step loop, the damage pipeline, statuses and synergies.
 *
 * Two shapes, per the source repo's shared/README.md:
 *  - **Outcome fixtures** (no `steps`, or `steps: 0`): run to completion, assert the outcome,
 *    the order mons fainted, and who survived at what HP.
 *  - **State fixtures** (`steps: N > 0`): run exactly N raw Steps and assert precise mid-battle
 *    state — the mechanics a terminal outcome alone wouldn't pin down.
 */

import { readFileSync, readdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

import {
  advanceStepMutable,
  applyOpeningMutable,
  createRandom,
  determineOutcome,
  makeBattleState,
  makeCombatant,
  runPrecomputed,
  EVENT_CAP,
  LEGACY_CHARGE_CONFIG,
  STEP_CAP,
  type BattleOutcome,
  type Combatant,
  type PassiveDefinition,
  type PokemonType,
  type StatusType,
  type StepEvent,
} from '../src/sim/index.js';

const fixturesDir = join(dirname(fileURLToPath(import.meta.url)), 'fixtures');

interface FixtureCombatant {
  instanceId: string;
  attack: number;
  health: number;
  speed: number;
  passive?: PassiveDefinition;
  types?: PokemonType[];
}

interface Fixture {
  seed: number;
  steps?: number;
  lineUpA: FixtureCombatant[];
  lineUpB: FixtureCombatant[];
  expected?: {
    outcome: BattleOutcome;
    faintOrder: string[];
    survivors: { instanceId: string; currentHP: number }[];
  };
  expectedState?: {
    instanceId: string;
    currentHP: number;
    shield: number;
    charge: number;
    status?: StatusType;
  }[];
}

const toCombatant = (spec: FixtureCombatant): Combatant =>
  makeCombatant({
    instanceId: spec.instanceId,
    attack: spec.attack,
    health: spec.health,
    speed: spec.speed,
    passive: spec.passive ?? null,
    types: spec.types ?? [],
  });

const fixtureFiles: string[] = readdirSync(fixturesDir)
  .filter((f: string) => f.endsWith('.json') && f !== 'PROVENANCE.json')
  .sort();

describe('golden fixtures (shared with the Unity implementation)', () => {
  it('finds the vendored fixtures', () => {
    expect(fixtureFiles.length).toBeGreaterThan(0);
  });

  for (const file of fixtureFiles) {
    const fixture = JSON.parse(readFileSync(join(fixturesDir, file), 'utf8')) as Fixture;
    const isStateFixture = (fixture.steps ?? 0) > 0;

    it(`${file} (${isStateFixture ? `${fixture.steps} step(s)` : 'to completion'})`, () => {
      const lineUpA = fixture.lineUpA.map(toCombatant);
      const lineUpB = fixture.lineUpB.map(toCombatant);

      if (isStateFixture) {
        // Run exactly N raw Steps, opening included, and read the board.
        const state = makeBattleState(lineUpA, lineUpB);
        const rng = createRandom(fixture.seed);
        applyOpeningMutable(state, LEGACY_CHARGE_CONFIG);
        for (let i = 0; i < fixture.steps!; i++) {
          advanceStepMutable(state, rng, LEGACY_CHARGE_CONFIG);
        }

        const byId = new Map<string, Combatant>();
        for (const c of [...state.lineUpA, ...state.lineUpB]) byId.set(c.instanceId, c);

        expect(fixture.expectedState).toBeDefined();
        for (const want of fixture.expectedState!) {
          const got = byId.get(want.instanceId);
          expect(got, `${want.instanceId} should still be on the field`).toBeDefined();
          expect(got!.currentHP, `${want.instanceId} currentHP`).toBe(want.currentHP);
          expect(got!.shield, `${want.instanceId} shield`).toBe(want.shield);
          expect(got!.charge, `${want.instanceId} charge`).toBe(want.charge);
          expect(got!.status ?? undefined, `${want.instanceId} status`).toBe(want.status);
        }
        return;
      }

      // Outcome fixture: run to completion.
      const log = runPrecomputed(lineUpA, lineUpB, fixture.seed, LEGACY_CHARGE_CONFIG);
      expect(fixture.expected).toBeDefined();
      const want = fixture.expected!;

      expect(log.outcome, 'outcome').toBe(want.outcome);

      const faintOrder = log.events
        .filter((e: StepEvent) => e.kind === 'Faint')
        .map((e: StepEvent) => e.sourceInstanceId);
      expect(faintOrder, 'faint order').toEqual(want.faintOrder);

      const survivors = [...log.finalState.lineUpA, ...log.finalState.lineUpB].map((c) => ({
        instanceId: c.instanceId,
        currentHP: c.currentHP,
      }));
      expect(survivors, 'survivors').toEqual(want.survivors);

      // No fixture should ever be ended by a safety cap; reaching one is a sim bug.
      expect(log.finalState.stepNumber, 'step cap not reached').toBeLessThan(STEP_CAP);
      expect(log.events.length, 'event cap not reached').toBeLessThan(EVENT_CAP);
    });
  }
});

describe('outcome determination', () => {
  it('reports a draw when both sides empty', () => {
    expect(determineOutcome(makeBattleState([], []))).toBe('Draw');
  });

  it('reports the surviving side as the winner', () => {
    const survivor = makeCombatant({ instanceId: 's', attack: 1, health: 1, speed: 1 });
    expect(determineOutcome(makeBattleState([survivor], []))).toBe('SideAWins');
    expect(determineOutcome(makeBattleState([], [survivor]))).toBe('SideBWins');
  });
});
