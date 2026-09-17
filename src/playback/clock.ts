/**
 * A virtual clock for animation playback.
 *
 * The obvious way to pace a Step is `for (const beat of beats) { render(); await delay(120); }`,
 * and that's the right shape — but a bare `await delay(120)` is a promise wrapped around a
 * `setTimeout` that has already been scheduled. Once it's sleeping you cannot pause it, you cannot
 * change its speed, and you cannot cancel it when the component unmounts. The battle screen needs
 * all three: it opens paused, it has autoplay and fast-forward, and it has a step-one-Step control.
 *
 * So instead of scheduling real timeouts, this advances a **virtual millisecond count** on every
 * animation frame, scaled by the current speed, and only while running. A waiter resolves when the
 * virtual clock passes its due mark. Pausing simply stops advancing the count — every pending wait
 * holds exactly where it was, and resumes from there.
 *
 * Nothing here knows what a battle is; it's a clock. The battle-specific part is in `player.ts`.
 */

export type ClockState = 'paused' | 'running';

interface Waiter {
  due: number;
  resolve: () => void;
  reject: (reason: unknown) => void;
}

/** Thrown into pending waits when the clock is cancelled, so a playback loop can unwind. */
export class PlaybackCancelled extends Error {
  constructor() {
    super('Playback cancelled');
    this.name = 'PlaybackCancelled';
  }
}

export interface ClockOptions {
  /** Speed multiplier to start at. */
  speed?: number;
  /** Start running rather than paused. Battles open paused, so this defaults to false. */
  autostart?: boolean;
  /**
   * Frame source. Defaults to `requestAnimationFrame`, falling back to a timer where that isn't
   * available — which is what lets the clock be tested in Node without a DOM.
   */
  now?: () => number;
}

export class PlaybackClock {
  #virtualNow = 0;
  #speed: number;
  #state: ClockState;
  #waiters: Waiter[] = [];
  #cancelled = false;
  #lastRealTime: number | null = null;
  #frameHandle: ReturnType<typeof setTimeout> | number | null = null;
  #now: () => number;
  #listeners = new Set<() => void>();

  constructor(options: ClockOptions = {}) {
    this.#speed = options.speed ?? 1;
    this.#state = options.autostart === true ? 'running' : 'paused';
    this.#now = options.now ?? (() => (typeof performance === 'undefined' ? Date.now() : performance.now()));
    if (this.#state === 'running') this.#scheduleFrame();
  }

  get state(): ClockState {
    return this.#state;
  }

  get speed(): number {
    return this.#speed;
  }

  get isCancelled(): boolean {
    return this.#cancelled;
  }

  /** How many waits are outstanding. Test affordance. */
  get pendingCount(): number {
    return this.#waiters.length;
  }

  /** Called whenever the clock's state or speed changes, so React can re-render controls. */
  subscribe(listener: () => void): () => void {
    this.#listeners.add(listener);
    return () => this.#listeners.delete(listener);
  }

  #emit(): void {
    for (const l of this.#listeners) l();
  }

  /**
   * Resolves after `ms` of *virtual* time. A wait taken while paused simply never progresses
   * until the clock runs, which is what makes "opens paused" work without special-casing the
   * first beat.
   */
  wait(ms: number): Promise<void> {
    if (this.#cancelled) return Promise.reject(new PlaybackCancelled());
    if (ms <= 0) return Promise.resolve();

    return new Promise<void>((resolve, reject) => {
      this.#waiters.push({ due: this.#virtualNow + ms, resolve, reject });
      this.#waiters.sort((a, b) => a.due - b.due);
    });
  }

  play(): void {
    if (this.#cancelled || this.#state === 'running') return;
    this.#state = 'running';
    this.#lastRealTime = null;
    this.#scheduleFrame();
    this.#emit();
  }

  pause(): void {
    if (this.#state === 'paused') return;
    this.#state = 'paused';
    this.#cancelFrame();
    this.#emit();
  }

  toggle(): void {
    if (this.#state === 'running') this.pause();
    else this.play();
  }

  setSpeed(speed: number): void {
    // Clamped rather than trusted: a zero or negative speed would stall the clock in a way that
    // looks exactly like a hang, and pause already covers "stop".
    this.#speed = Math.max(0.1, Math.min(32, speed));
    this.#emit();
  }

  /**
   * Resolves every wait outstanding right now, without advancing time.
   *
   * This is what fast-forward and step-one-Step are built from: the playback loop is sitting in a
   * `wait`, and releasing it lets the loop run on to its next decision point.
   */
  flush(): void {
    const waiters = this.#waiters;
    this.#waiters = [];
    for (const w of waiters) w.resolve();
  }

  /**
   * Advances the virtual clock by hand. Used by tests, and by anything that wants to drive
   * playback deterministically rather than in real time.
   */
  advance(ms: number): void {
    this.#virtualNow += ms;
    this.#releaseDue();
  }

  /** Stops the clock permanently and rejects every pending wait. Call on unmount. */
  cancel(): void {
    if (this.#cancelled) return;
    this.#cancelled = true;
    this.#state = 'paused';
    this.#cancelFrame();

    const waiters = this.#waiters;
    this.#waiters = [];
    for (const w of waiters) w.reject(new PlaybackCancelled());
    this.#emit();
  }

  #releaseDue(): void {
    while (this.#waiters.length > 0 && this.#waiters[0]!.due <= this.#virtualNow) {
      this.#waiters.shift()!.resolve();
    }
  }

  #scheduleFrame(): void {
    if (this.#cancelled || this.#state !== 'running' || this.#frameHandle !== null) return;

    const tick = (): void => {
      this.#frameHandle = null;
      if (this.#cancelled || this.#state !== 'running') return;

      const real = this.#now();
      if (this.#lastRealTime !== null) {
        // Clamped so a backgrounded tab doesn't return and skip the whole fight in one frame.
        const delta = Math.min(100, real - this.#lastRealTime);
        this.#virtualNow += delta * this.#speed;
        this.#releaseDue();
      }
      this.#lastRealTime = real;
      this.#scheduleFrame();
    };

    this.#frameHandle =
      typeof requestAnimationFrame === 'function'
        ? requestAnimationFrame(tick)
        : setTimeout(tick, 16);
  }

  #cancelFrame(): void {
    if (this.#frameHandle === null) return;
    if (typeof cancelAnimationFrame === 'function' && typeof this.#frameHandle === 'number') {
      cancelAnimationFrame(this.#frameHandle);
    } else {
      clearTimeout(this.#frameHandle as ReturnType<typeof setTimeout>);
    }
    this.#frameHandle = null;
  }
}
