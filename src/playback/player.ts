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
import { applyEvent, initialDisplay, syncTo, type DisplayState } from './display.js';

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
  // Zero, deliberately. Charge is applied the instant the accrual beat is reached and then the
  // arcs *animate* to their new value under CSS while the following beats play — which is what
  // "the bar fills during the Step" means. Giving it a beat of its own instead put a pause on
  // every mon every Step and added well over a second to each one.
  ChargeGained: 0,
  SuddenDeath: 500,
  // The longest beat in the Step: an ability firing is the thing the charge bar has been building
  // toward, so everything else holds while it lands.
  PassiveTriggered: 560,
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
  /**
   * How long each combatant's charge-fill should currently animate for, in ms — what MonView
   * feeds to the arc's CSS transition. A fixed short transition made the bar visibly snap to its
   * new value and then sit static for the rest of the beat; setting the duration to the actual
   * remaining time until the next update makes the fill run continuously across the beats and
   * the inter-Step pause, rather than jumping and holding.
   */
  chargeDurationMs: Record<string, number>;
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
  /**
   * Everyone on your side who was ever Lead or Support.
   *
   * A dormant mon at the back of the train never fought, so EXP goes to this set rather than to
   * the whole line-up.
   */
  participants: string[];
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
  /** See `BattlePlayerSnapshot.chargeDurationMs`. */
  #chargeDurationMs: Record<string, number> = {};
  /**
   * Everyone on your side who was ever on the field.
   *
   * A dormant mon at the back of the train never fought and should not be paid for it, so EXP
   * goes to this set rather than to the whole line-up. Recorded as the fight goes rather than
   * derived afterwards, because by the end the fallen have been removed from the line-up and
   * there is nothing left to read.
   */
  #participants = new Set<string>();

  constructor(lineUpA: Combatant[], lineUpB: Combatant[], seed: number) {
    this.#runner = new OnDemandRunner(lineUpA, lineUpB, seed);
    this.#clock = new PlaybackClock({ autostart: false });
    this.#clock.subscribe(() => this.#publish());

    // The opening is applied and drawn before anything is played, so its shields and charge
    // offsets are on screen before the first exchange can spend them.
    this.#noteParticipants();
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

  /** Adds whoever is currently Lead or Support on your side. Called at every Step boundary. */
  #noteParticipants(): void {
    for (const mon of this.#runner.state.lineUpA.slice(0, 2)) {
      this.#participants.add(mon.instanceId);
    }
  }

  /**
   * Records how long a combatant's charge arc should animate for, given this event.
   *
   * On a gain, the duration spans everything left in the Step (`totalStepMs - elapsedMs`) plus
   * the pause before the next one begins, so the fill is still moving when the next Step's own
   * gain arrives and simply retargets a transition already in flight. On an ability firing, the
   * bar empties instead — a fast, fixed snap rather than a slow slide back to zero at whatever
   * duration happened to be active, since draining is the payoff landing, not more charging.
   */
  #noteChargeTiming(event: StepEvent, totalStepMs: number, elapsedMs: number): void {
    const id = event.sourceInstanceId;
    if (id === undefined) return;

    if (event.kind === 'ChargeGained') {
      this.#chargeDurationMs[id] = Math.max(50, totalStepMs - elapsedMs) + STEP_GAP_MS;
    } else if (event.kind === 'PassiveTriggered') {
      this.#chargeDurationMs[id] = 150;
    }
  }

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
      chargeDurationMs: { ...this.#chargeDurationMs },
      isFinished: this.#finished,
      participants: [...this.#participants],
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

  async #loop(): Promise<void> {
    if (this.#loopRunning) return;
    this.#loopRunning = true;

    try {
      while (!this.#disposed && !this.#runner.isBattleOver) {
        this.#noteParticipants();
        const events = this.#runner.nextStep();
        this.#noteParticipants();
        this.#playingStep = true;

        // A zero-length beat (ChargeGained) is folded into whichever visible beat precedes it,
        // rather than played as its own sequential step. Applied one at a time, charge updated
        // in its own iteration *after* the attack's wait had already elapsed — so the arc filled
        // once the lunge was over rather than while it played. Applying it in the same display
        // update as the attack, before that attack's wait starts, means the arc's CSS transition
        // runs concurrently with the lunge, inside the same beat.
        //
        // The transition's *duration* is set to the actual time remaining in this Step plus the
        // inter-Step pause, computed from the beats still to come — so the fill runs continuously
        // across the rest of the Step and the gap rather than reaching its target in a fixed
        // short burst and then sitting static until the next Step's own charge event retargets it.
        const totalStepMs = events.reduce((sum, e) => sum + beatLength(e), 0);
        let elapsedMs = 0;

        for (let i = 0; i < events.length; i++) {
          const event = events[i]!;
          this.#history = [...this.#history, event];
          this.#display = applyEvent(this.#display, event);
          this.#noteChargeTiming(event, totalStepMs, elapsedMs);

          const waitMs = beatLength(event);
          while (i + 1 < events.length && beatLength(events[i + 1]!) === 0) {
            i++;
            const merged = events[i]!;
            this.#history = [...this.#history, merged];
            this.#display = applyEvent(this.#display, merged);
            this.#noteChargeTiming(merged, totalStepMs, elapsedMs);
          }

          this.#publish();
          await this.#clock.wait(waitMs);
          elapsedMs += waitMs;
        }

        // Resync to the authoritative board — a correction, not the primary way charge reaches
        // the screen now that it has its own event.
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
