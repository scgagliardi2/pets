/**
 * The two runners (battle-sim-spec.md §7). Both call the same `advanceStepMutable`; Step logic
 * is never duplicated between them.
 *
 * - **Precomputed** (Gym, and PvP later): nothing can interrupt, so the whole fight is computed
 *   up front into one event log and handed to the UI for playback.
 * - **On-demand** (wild PvE): one Step at a time, so the catching layer can remove a caught mon
 *   from the enemy line-up at a Step boundary before the next Step is taken.
 */

import {
  advanceStepMutable,
  determineOutcome,
  removeCaught as removeCaughtFrom,
} from './advanceStep.js';
import { EVENT_CAP, STEP_CAP } from './config.js';
import { createRandom, type Rng } from './rng.js';
import { applyOpeningMutable } from './synergy.js';
import {
  cloneBattleState,
  makeBattleState,
  type BattleOutcome,
  type BattleState,
  type Combatant,
  type Side,
  type StepEvent,
  type StepLog,
} from './types.js';

/**
 * Runs a whole fight and returns its event log, outcome and final state.
 *
 * The result has to be *reported* rather than read off the line-ups the caller passed in,
 * because the battle runs on copies — the line-ups in `finalState` hold only the mons still
 * standing, and the `Faint` events are the record of who fell and in what order.
 */
export function runPrecomputed(
  lineUpA: Combatant[],
  lineUpB: Combatant[],
  seed: number,
): StepLog {
  const state = makeBattleState(
    lineUpA.map((c) => ({ ...c, currentStats: { ...c.currentStats } })),
    lineUpB.map((c) => ({ ...c, currentStats: { ...c.currentStats } })),
  );
  const rng = createRandom(seed);
  const events: StepEvent[] = [];

  // The opening. A no-op for combatants without types.
  events.push(...applyOpeningMutable(state));

  while (state.lineUpA.length > 0 && state.lineUpB.length > 0) {
    if (state.stepNumber >= STEP_CAP || events.length >= EVENT_CAP) {
      // Safety caps are a last resort, not the thing that ends a stalled fight — sudden death
      // is. Reaching one is a bug in the sim, not in the content.
      events.push({ step: state.stepNumber, kind: 'BattleEnd', outcome: 'Draw' });
      return { events, outcome: 'Draw', finalState: state };
    }
    events.push(...advanceStepMutable(state, rng));
  }

  const outcome = determineOutcome(state);
  events.push({ step: state.stepNumber, kind: 'BattleEnd', outcome });
  return { events, outcome, finalState: state };
}

/**
 * One Step at a time, for fights a catch can interrupt.
 *
 * The fight's PRNG is exposed so a catch roll at a Step boundary draws from the same stream the
 * Steps do — a run is reproducible end to end from its seed, and a catch is part of what
 * happened in the fight. That does mean throwing a ball shifts every subsequent Step, so a
 * replay reproduces the fight only if the same throws are made at the same boundaries. That's
 * correct: the throws are part of the history, and it's why PvE uses this runner rather than a
 * precomputed log.
 */
export class OnDemandRunner {
  #state: BattleState;
  readonly #rng: Rng;
  #openingApplied = false;

  constructor(lineUpA: Combatant[], lineUpB: Combatant[], seed: number) {
    this.#state = makeBattleState(
      lineUpA.map((c) => ({ ...c, currentStats: { ...c.currentStats } })),
      lineUpB.map((c) => ({ ...c, currentStats: { ...c.currentStats } })),
    );
    this.#rng = createRandom(seed);
  }

  /** A copy, so a component holding it can't mutate the fight underneath the runner. */
  get state(): BattleState {
    return cloneBattleState(this.#state);
  }

  get rng(): Rng {
    return this.#rng;
  }

  /** True once the opening has been applied — what a caller checks before giving it its own beat. */
  get openingApplied(): boolean {
    return this.#openingApplied;
  }

  get isBattleOver(): boolean {
    return (
      this.#state.lineUpA.length === 0 ||
      this.#state.lineUpB.length === 0 ||
      this.#state.stepNumber >= STEP_CAP
    );
  }

  get outcome(): BattleOutcome | null {
    return this.isBattleOver ? determineOutcome(this.#state) : null;
  }

  /**
   * Applies both sides' synergies if they haven't been applied yet, stamped Step 0. Idempotent,
   * so a caller that wants the opening as its own beat can take it here and then go on calling
   * `nextStep` as normal.
   *
   * The battle screen takes it as the screen opens (ADR 0016). The openings are mostly small
   * numbers and the defences smallest of all, so a 1-point Shell Guard shield raised and spent
   * inside one call to `nextStep` is never drawn at all — there is no board state in which it
   * exists. The simulation is identical either way; this only decides whether anything gets to
   * look at it.
   */
  applyOpening(): StepEvent[] {
    if (this.#openingApplied) return [];
    this.#openingApplied = true;
    return applyOpeningMutable(this.#state);
  }

  /**
   * Advances exactly one Step. Returns no events if the battle is already over — callers should
   * check `isBattleOver` rather than relying on an empty result.
   */
  nextStep(): StepEvent[] {
    if (this.isBattleOver) return [];

    const events = this.applyOpening();
    // An opening that emptied a side ends the fight before Step 1 is ever taken.
    if (this.isBattleOver) return events;

    events.push(...advanceStepMutable(this.#state, this.#rng));
    return events;
  }

  /**
   * Takes a caught mon out of the fight. Call only at a Step boundary — between `nextStep`
   * returning and the next call.
   */
  removeCaught(side: Side, instanceId: string): StepEvent[] {
    const { state, events } = removeCaughtFrom(this.#state, side, instanceId);
    this.#state = state;
    return events;
  }
}
