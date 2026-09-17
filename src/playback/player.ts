/**
 * The battle player: walks a fight forward, one beat at a time, through the playback clock.
 *
 * This is the only place that knows both the simulator and the clock. It owns an
 * `OnDemandRunner`, plays each Step's events as timed beats, and publishes a `DisplayState` for
 * the renderer to draw. React sees a subscribe/getSnapshot pair and nothing else.
 *
 * Battles open **paused** and with the **opening already applied and drawn** — both from hard
 * experience in the Unity build. Synergy effects applied inside Step 1 were invisible, because a
 * 1-point shield appeared and popped in the same frame; and autoplay-by-default meant the first
 * exchange was over before anyone looked at the board.
 */

import {
  OnDemandRunner,
  type BattleOutcome,
  type Combatant,
  type StepEvent,
} from '../sim/index.js';
import type { BallTier } from '../meta/balls.js';
import { chanceAgainst, rollCatch, type ThrowResult } from '../meta/catching.js';
import { PlaybackCancelled, PlaybackClock } from './clock.js';
import {
  applyEvent,
  chargeAt,
  chargePlan,
  initialDisplay,
  syncTo,
  withCharges,
  type ChargePlan,
  type DisplayState,
  type MonFlash,
} from './display.js';

/** How long each kind of beat holds the screen, in virtual milliseconds at 1x. */
export const BEAT_MS: Readonly<Record<string, number>> = {
  Damage: 420,
  ShieldAbsorbed: 260,
  Heal: 320,
  LifestealHeal: 260,
  Shield: 300,
  StatusApplied: 380,
  StatusBlocked: 380,
  StatusCleared: 320,
  StatusTick: 340,
  SuddenDeath: 500,
  PassiveTriggered: 440,
  Faint: 560,
  Caught: 560,
  Promotion: 460,
  TypeSynergy: 220,
  BattleEnd: 0,
  default: 240,
};

const beatLength = (event: StepEvent): number => BEAT_MS[event.kind] ?? BEAT_MS.default!;

/** A pause between Steps, so Step boundaries read as boundaries. */
export const STEP_GAP_MS = 320;

/**
 * The beats the arcs fill across: the Step's opening attack exchange, and nothing after it.
 *
 * A passive's own damage raises a `Damage` event too, but it comes after the `PassiveTriggered`
 * that introduces it, so it is never part of this leading run.
 */
const EXCHANGE_KINDS: ReadonlySet<string> = new Set(['Damage', 'ShieldAbsorbed', 'LifestealHeal']);

/**
 * The least time a Step's charge fill is given.
 *
 * The fill normally rides along with the exchange — the arcs creep up while the Leads trade blows,
 * which is where the charge came from — so it costs nothing. This floor only matters for a Step
 * with no exchange to ride along with, such as one where a side has no Lead left to swing: without
 * it the arcs would snap to their new marks with no travel to read.
 */
export const CHARGE_FILL_MS = 420;

/** How often the fill republishes. Small enough to read as movement rather than as ticks. */
const CHARGE_TICK_MS = 40;

/**
 * The board's hold on a full arc, before the passive that filled it fires.
 *
 * The beat the whole arc exists for: the bar lands full, everything stops, *then* the passive goes
 * off. Without it the arc's last pixel and the passive's flash land on the same frame and the
 * player sees a passive fire for no visible reason.
 */
export const CHARGE_FULL_HOLD_MS = 380;

/**
 * How long the finished board is held before the fight reports itself over.
 *
 * Without it the run layer cuts to the results screen the instant the last mon falls, on the same
 * frame as the killing blow — the faint never animates and the verdict caption is never read. The
 * hold is playback, not simulation: the fight is already decided, this only lets it land.
 */
export const END_HOLD_MS = 1100;

export interface BattlePlayerSnapshot {
  display: DisplayState;
  /** Every event so far, oldest first — what the log panel renders. */
  history: StepEvent[];
  stepNumber: number;
  outcome: BattleOutcome | null;
  isOver: boolean;
  clockState: 'paused' | 'running';
  speed: number;
  /** True while a Step's beats are mid-flight. */
  isPlayingStep: boolean;
  /** The foe's Lead — what a ball would be thrown at. Null when there is nobody to throw at. */
  throwTarget: Combatant | null;
  /** True when a throw would resolve right now: at a Step boundary, fight still running. */
  canThrow: boolean;
  /**
   * True once the fight is over *and* its last beats have played.
   *
   * Distinct from `isOver`, which reads the simulation and is true from the moment the killing
   * Step is computed — before any of that Step's beats have been drawn. Anything that reacts to a
   * fight ending should wait for this one, or it will react on the frame the death blow starts.
   */
  isFinished: boolean;
}

export class BattlePlayer {
  readonly #runner: OnDemandRunner;
  readonly #clock: PlaybackClock;
  #display: DisplayState;
  #history: StepEvent[] = [];
  #snapshot!: BattlePlayerSnapshot;
  #listeners = new Set<() => void>();
  #loopRunning = false;
  #playingStep = false;
  /** Set by stepOnce: run exactly one Step, then pause again. */
  #stopAfterStep = false;
  #disposed = false;
  #finished = false;

  constructor(lineUpA: Combatant[], lineUpB: Combatant[], seed: number) {
    this.#runner = new OnDemandRunner(lineUpA, lineUpB, seed);
    this.#clock = new PlaybackClock({ autostart: false });
    this.#clock.subscribe(() => this.#publish());

    // The opening is applied and drawn before anything is played, so its shields and charge
    // offsets are on screen before the first exchange can spend them.
    const openingEvents = this.#runner.applyOpening();
    this.#history = [...openingEvents];
    this.#display = initialDisplay(this.#runner.state);
    this.#publish();
  }

  // --- React integration ---------------------------------------------------------------------

  subscribe = (listener: () => void): (() => void) => {
    this.#listeners.add(listener);
    return () => this.#listeners.delete(listener);
  };

  getSnapshot = (): BattlePlayerSnapshot => this.#snapshot;

  #publish(): void {
    this.#snapshot = {
      display: this.#display,
      history: this.#history,
      stepNumber: this.#display.board.stepNumber,
      outcome: this.#runner.outcome,
      isOver: this.#runner.isBattleOver,
      clockState: this.#clock.state,
      speed: this.#clock.speed,
      isPlayingStep: this.#playingStep,
      throwTarget: this.#runner.state.lineUpB[0] ?? null,
      canThrow: !this.#runner.isBattleOver && !this.#playingStep && !this.#disposed,
      isFinished: this.#finished,
    };
    for (const l of this.#listeners) l();
  }

  // --- controls ------------------------------------------------------------------------------

  play(): void {
    if (this.#disposed || this.#finished) return;
    this.#stopAfterStep = false;
    this.#clock.play();
    void this.#loop();
  }

  pause(): void {
    this.#clock.pause();
  }

  togglePlay(): void {
    if (this.#clock.state === 'running') this.pause();
    else this.play();
  }

  setSpeed(speed: number): void {
    this.#clock.setSpeed(speed);
  }

  /**
   * Play exactly one Step and stop.
   *
   * The loop is the same one autoplay uses — this just arms a flag it checks at the Step
   * boundary, so there is no second code path that could resolve a Step differently.
   */
  stepOnce(): void {
    if (this.#disposed || this.#finished) return;
    this.#stopAfterStep = true;
    this.#clock.play();
    void this.#loop();
  }

  /** Release whatever beat is waiting right now, without changing speed. */
  skipBeat(): void {
    this.#clock.flush();
  }

  /** Run to the end as fast as the loop allows, with no waiting between beats. */
  fastForward(): void {
    if (this.#disposed || this.#finished) return;
    this.#stopAfterStep = false;
    this.#clock.setSpeed(32);
    this.#clock.play();
    void this.#loop();
  }

  /**
   * Throws a ball at the foe's Lead.
   *
   * Only at a Step boundary — never mid-beat. A throw that landed halfway through a Step would
   * have to decide whether the damage already shown counts toward the odds, and whether the
   * remaining beats still happen to a mon that is now in a ball. Resolving between Steps makes
   * both questions vanish, and it is why wild fights use the on-demand runner rather than a
   * precomputed log.
   *
   * The roll draws from the fight's own RNG, so a catch is part of the fight's history and a run
   * stays reproducible from its seed.
   *
   * Spending the ball and adding the mon to the run are the caller's business; this only decides
   * what happened and takes the mon off the field.
   */
  throwBall(tier: BallTier): ThrowResult {
    const target = this.#runner.state.lineUpB[0] ?? null;
    if (this.#disposed || this.#runner.isBattleOver || this.#playingStep || target === null) {
      return { outcome: 'NotThrown', tier, targetInstanceId: null, caught: null, chance: 0 };
    }

    const chance = chanceAgainst(tier, target);
    const caught = rollCatch(tier, target, this.#runner.rng);

    const thrown: StepEvent = {
      step: this.#display.board.stepNumber,
      kind: 'BallThrown',
      sourceSide: 'A',
      targetSide: 'B',
      targetInstanceId: target.instanceId,
      amount: Math.round(chance * 100),
    };

    if (!caught) {
      this.#history = [...this.#history, thrown];
      this.#publish();
      return { outcome: 'BrokeFree', tier, targetInstanceId: target.instanceId, caught: null, chance };
    }

    const events = [thrown, ...this.#runner.removeCaught('B', target.instanceId)];
    this.#history = [...this.#history, ...events];
    this.#display = syncTo(this.#runner.state);
    this.#publish();

    // A catch can end the fight, and typically does so while the clock is paused — a throw is
    // only legal at a Step boundary, and that is exactly where a player sits to aim one. The
    // playback loop is not running at that moment, so nothing would run the end handling and the
    // fight would never report itself finished: the screen locks with every control disabled.
    // Starting the clock lets the same loop close the fight out, hold on the verdict, and report.
    if (this.#runner.isBattleOver) {
      this.#clock.play();
      void this.#loop();
    }

    return { outcome: 'Caught', tier, targetInstanceId: target.instanceId, caught: null, chance };
  }

  dispose(): void {
    this.#disposed = true;
    this.#clock.cancel();
    this.#listeners.clear();
  }

  // --- the loop ------------------------------------------------------------------------------

  /**
   * Plays a run of beats in order, filling the charge arcs across the whole run when given a plan.
   *
   * The fill rides along with the beats rather than taking a slot of its own, so the arcs move
   * while the Leads are trading blows — which is where the charge is coming from — instead of
   * adding a lull to every Step.
   */
  async #playBeats(events: StepEvent[], fill: ChargePlan | null): Promise<void> {
    const beats = events.reduce((total, event) => total + beatLength(event), 0);
    const window = fill === null ? 0 : Math.max(CHARGE_FILL_MS, beats);
    let elapsed = 0;

    for (const event of events) {
      this.#history = [...this.#history, event];
      this.#display = applyEvent(this.#display, event);
      this.#publish();
      elapsed = await this.#waitFilling(beatLength(event), fill, elapsed, window);
    }

    // Whatever of the window the beats didn't cover — the CHARGE_FILL_MS floor, on a Step whose
    // exchange was short or absent.
    await this.#waitFilling(window - elapsed, fill, elapsed, window);
  }

  /** Waits `ms`, republishing the arcs partway through their fill as it goes. */
  async #waitFilling(
    ms: number,
    fill: ChargePlan | null,
    elapsed: number,
    window: number,
  ): Promise<number> {
    if (fill === null || window <= 0) {
      await this.#clock.wait(ms);
      return elapsed;
    }

    let remaining = ms;
    while (remaining > 0) {
      const tick = Math.min(CHARGE_TICK_MS, remaining);
      await this.#clock.wait(tick);
      remaining -= tick;
      elapsed += tick;
      this.#display = withCharges(this.#display, chargeAt(fill, elapsed / window));
      this.#publish();
    }
    return elapsed;
  }

  /** Everything stops on a full arc, so the passive about to fire has a visible cause. */
  async #holdOnFull(plan: ChargePlan): Promise<void> {
    if (plan.full.length === 0) return;

    const flashes: Record<string, MonFlash> = {};
    for (const id of plan.full) flashes[id] = { charged: true };
    // Snapped to the targets rather than left wherever the fill got to: a Step whose beats ran
    // short must still show the arc full before the passive empties it.
    this.#display = withCharges(this.#display, chargeAt(plan, 1), flashes);
    this.#publish();

    await this.#clock.wait(CHARGE_FULL_HOLD_MS);
  }

  async #loop(): Promise<void> {
    if (this.#loopRunning) return;
    this.#loopRunning = true;

    try {
      while (!this.#disposed && !this.#runner.isBattleOver) {
        const events = this.#runner.nextStep();
        this.#playingStep = true;

        // A Step opens with the attack exchange and accrues its charge out of it, so the arcs
        // fill across exactly those beats and no further. Everything after — passives, status
        // ticks, faints — plays with the arcs already at their new marks, which is what stops a
        // bar creeping upward behind a faint or a verdict.
        let split = 0;
        while (split < events.length && EXCHANGE_KINDS.has(events[split]!.kind)) split++;
        const plan = chargePlan(this.#display.board, this.#runner.state, events);

        await this.#playBeats(events.slice(0, split), plan);
        await this.#holdOnFull(plan);
        await this.#playBeats(events.slice(split), null);

        // Resync to the authoritative board: the fill leaves the arcs at fractions of a charge
        // point, and anything the per-event walk missed is corrected at the boundary too.
        this.#display = syncTo(this.#runner.state);
        this.#playingStep = false;
        this.#publish();

        if (this.#stopAfterStep) {
          this.#stopAfterStep = false;
          // Unless that Step ended the fight — then fall through to the end handling rather than
          // leaving a finished battle that never reports itself.
          if (!this.#runner.isBattleOver) {
            this.#clock.pause();
            break;
          }
        }

        await this.#clock.wait(STEP_GAP_MS);
      }

      if (this.#runner.isBattleOver && !this.#finished) {
        const outcome = this.#runner.outcome;
        if (outcome !== null && !this.#history.some((e) => e.kind === 'BattleEnd')) {
          this.#history = [
            ...this.#history,
            { step: this.#display.board.stepNumber, kind: 'BattleEnd', outcome },
          ];
        }
        // Published before the hold, so the verdict is on screen while it runs.
        this.#publish();
        await this.#clock.wait(END_HOLD_MS);

        this.#finished = true;
        this.#clock.pause();
        this.#publish();
      }
    } catch (error) {
      // A cancelled clock is the component unmounting mid-beat; anything else is a real fault.
      if (!(error instanceof PlaybackCancelled)) throw error;
    } finally {
      this.#loopRunning = false;
      this.#playingStep = false;
    }
  }
}
