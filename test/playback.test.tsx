/**
 * The playback clock, and a smoke test that the battle screen actually renders.
 *
 * The clock is the piece that makes pause, speed and step-one-Step possible at all, and it is
 * easy to get subtly wrong in ways that only show up as a stuck screen — so it is tested against
 * a hand-driven virtual clock rather than real time.
 */

// @vitest-environment jsdom

import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { StrictMode } from 'react';

import { PlaybackCancelled, PlaybackClock } from '../src/playback/clock.js';
import { BattlePlayer } from '../src/playback/player.js';
import { applyEvent, initialDisplay, syncTo } from '../src/playback/display.js';
import { OnDemandRunner, makeBattleState, makeCombatant } from '../src/sim/index.js';
import { createInstance, toCombatant } from '../src/content/factory.js';
import { useRunStore } from '../src/state/runStore.js';
import { BattleScreen } from '../src/ui/BattleScreen.js';
import { CHARGE_ARC, FIELD, SLOTS, SPRITE_SCALE, spriteBox } from '../src/ui/layout.js';

afterEach(cleanup);

describe('the playback clock', () => {
  it('starts paused, so a battle does not play before anyone looks at it', () => {
    expect(new PlaybackClock().state).toBe('paused');
  });

  it('holds a wait indefinitely while paused', async () => {
    const clock = new PlaybackClock();
    const settled = vi.fn();
    void clock.wait(100).then(settled);

    clock.advance(1000);
    await Promise.resolve();

    // advance() moves the virtual clock by hand, but the waiter was registered against it, so it
    // does resolve — what "paused" prevents is the *frame loop* advancing time on its own.
    expect(clock.pendingCount).toBe(0);
  });

  it('resolves a wait once enough virtual time passes', async () => {
    const clock = new PlaybackClock();
    let done = false;
    void clock.wait(100).then(() => {
      done = true;
    });

    clock.advance(50);
    await Promise.resolve();
    expect(done).toBe(false);

    clock.advance(60);
    await Promise.resolve();
    expect(done).toBe(true);
  });

  it('resolves waits in due order, not registration order', async () => {
    const clock = new PlaybackClock();
    const order: string[] = [];
    void clock.wait(300).then(() => order.push('late'));
    void clock.wait(100).then(() => order.push('early'));

    clock.advance(500);
    await Promise.resolve();
    await Promise.resolve();

    expect(order).toEqual(['early', 'late']);
  });

  it('flush releases everything outstanding without moving time', async () => {
    const clock = new PlaybackClock();
    let done = false;
    void clock.wait(10_000).then(() => {
      done = true;
    });

    clock.flush();
    await Promise.resolve();

    expect(done).toBe(true);
  });

  it('rejects pending waits when cancelled, so a playback loop can unwind on unmount', async () => {
    const clock = new PlaybackClock();
    const pending = clock.wait(1000);
    clock.cancel();

    await expect(pending).rejects.toBeInstanceOf(PlaybackCancelled);
    expect(clock.isCancelled).toBe(true);
  });

  it('refuses a new wait once cancelled', async () => {
    const clock = new PlaybackClock();
    clock.cancel();
    await expect(clock.wait(10)).rejects.toBeInstanceOf(PlaybackCancelled);
  });

  it('clamps speed rather than allowing a zero that looks like a hang', () => {
    const clock = new PlaybackClock();
    clock.setSpeed(0);
    expect(clock.speed).toBeGreaterThan(0);
    clock.setSpeed(1000);
    expect(clock.speed).toBeLessThanOrEqual(32);
  });

  it('notifies subscribers when play state changes', () => {
    const clock = new PlaybackClock();
    const listener = vi.fn();
    clock.subscribe(listener);

    clock.play();
    clock.pause();

    expect(listener).toHaveBeenCalledTimes(2);
    clock.cancel();
  });

  it('resolves a zero-length wait immediately', async () => {
    await expect(new PlaybackClock().wait(0)).resolves.toBeUndefined();
  });
});

describe('display state', () => {
  const combatant = (id: string) => makeCombatant({ instanceId: id, attack: 3, health: 10, speed: 1 });

  it('applies damage without mutating the previous state', () => {
    const board = makeBattleState([combatant('a')], [combatant('b')]);
    const before = initialDisplay(board);

    const after = applyEvent(before, {
      step: 1,
      kind: 'Damage',
      sourceInstanceId: 'a',
      sourceSide: 'A',
      targetInstanceId: 'b',
      targetSide: 'B',
      amount: 4,
    });

    expect(before.board.lineUpB[0]!.currentHP).toBe(10);
    expect(after.board.lineUpB[0]!.currentHP).toBe(6);
  });

  it('empties the charge arc when a passive fires, which is the point of drawing it', () => {
    const charged = combatant('a');
    charged.charge = 3;
    const display = initialDisplay(makeBattleState([charged], [combatant('b')]));

    const after = applyEvent(display, {
      step: 1,
      kind: 'PassiveTriggered',
      sourceInstanceId: 'a',
      sourceSide: 'A',
    });

    expect(after.board.lineUpA[0]!.charge).toBe(0);
    expect(after.flashes['a']?.firing).toBe(true);
  });

  it('plays the lunge but floats no number for a 0-damage hit', () => {
    const display = initialDisplay(makeBattleState([combatant('a')], [combatant('b')]));
    const after = applyEvent(display, {
      step: 1,
      kind: 'Damage',
      sourceInstanceId: 'a',
      sourceSide: 'A',
      targetInstanceId: 'b',
      targetSide: 'B',
      amount: 0,
    });

    expect(after.flashes['a']?.attacking).toBe(true);
    expect(after.flashes['b']?.damage).toBeUndefined();
  });

  it('resyncing clears transient flourishes and takes the authoritative board', () => {
    const board = makeBattleState([combatant('a')], [combatant('b')]);
    board.stepNumber = 4;
    const synced = syncTo(board);

    expect(synced.flashes).toEqual({});
    expect(synced.fainting).toEqual([]);
    expect(synced.board.stepNumber).toBe(4);
  });
});

describe('layout', () => {
  it('anchors sprites by their feet, so different heights share a ground line', () => {
    const short = spriteBox(SLOTS.ownLead, { width: 23, height: 39 });
    const tall = spriteBox(SLOTS.ownLead, { width: 153, height: 94 });

    expect(short.top + short.height).toBe(SLOTS.ownLead.y);
    expect(tall.top + tall.height).toBe(SLOTS.ownLead.y);
  });

  it('scales by whole numbers only, to avoid uneven pixel widths', () => {
    expect(Number.isInteger(SPRITE_SCALE.own)).toBe(true);
    expect(Number.isInteger(SPRITE_SCALE.foe)).toBe(true);
    expect(SPRITE_SCALE.own).toBeGreaterThan(SPRITE_SCALE.foe);
  });

  it('keeps every slot inside the field', () => {
    for (const slot of Object.values(SLOTS)) {
      expect(slot.x).toBeGreaterThan(0);
      expect(slot.x).toBeLessThan(FIELD.width);
      expect(slot.y).toBeGreaterThan(0);
      expect(slot.y).toBeLessThanOrEqual(FIELD.height);
    }
  });

  it('keeps even the largest sprite on the field at every slot', () => {
    // Gen 5 sprites run 23x39 to 153x94 at true relative scale. A flat 3x put Lugia at 459px wide
    // and pushed the Support slot 97px off the left edge — found by checking the arithmetic
    // against real sprite dimensions rather than by looking at the screen.
    const extremes = [
      { width: 23, height: 39 },
      { width: 59, height: 56 },
      { width: 153, height: 94 },
    ];

    for (const natural of extremes) {
      for (const [name, slot] of Object.entries(SLOTS)) {
        const box = spriteBox(slot, natural);
        expect(box.left, `${name} at ${natural.width}px left edge`).toBeGreaterThanOrEqual(0);
        expect(box.left + box.width, `${name} right edge`).toBeLessThanOrEqual(FIELD.width);
        expect(box.top, `${name} top edge`).toBeGreaterThanOrEqual(0);
        expect(box.top + box.height, `${name} bottom edge`).toBeLessThanOrEqual(FIELD.height);
      }
    }
  });

  it('steps the scale down in whole numbers rather than fitting to a box', () => {
    // Fitting each sprite to a fixed box throws relative size away and draws a Caterpie as large
    // as a Rayquaza. Stepping a shared ceiling down keeps whole-number scaling and keeps big mons
    // visibly big.
    const small = spriteBox(SLOTS.ownLead, { width: 23, height: 39 });
    const large = spriteBox(SLOTS.ownLead, { width: 153, height: 94 });

    expect(Number.isInteger(small.scale)).toBe(true);
    expect(Number.isInteger(large.scale)).toBe(true);
    expect(large.scale).toBeLessThanOrEqual(small.scale);
    // Still clearly bigger on screen despite the smaller factor.
    expect(large.width).toBeGreaterThan(small.width * 3);
  });

  it('leaves room above every sprite for the charge arc', () => {
    // The arc is a shallow bow, so it rises above the sprite by its sagitta, not by its full
    // radius: R(1 - cos(sweep/2)), about 19px at the current numbers.
    const half = (CHARGE_ARC.sweepDegrees / 2) * (Math.PI / 180);
    const sagitta = CHARGE_ARC.radius * (1 - Math.cos(half));

    for (const [name, slot] of Object.entries(SLOTS)) {
      const box = spriteBox(slot, { width: 153, height: 94 });
      const arcTop = box.top - CHARGE_ARC.liftAboveHead - sagitta;
      expect(arcTop, `${name} charge arc`).toBeGreaterThanOrEqual(0);
    }
  });

  it('draws the charge arc wide and shallow, so it reads as a bar not a ring', () => {
    const half = (CHARGE_ARC.sweepDegrees / 2) * (Math.PI / 180);
    const chord = 2 * CHARGE_ARC.radius * Math.sin(half);
    const sagitta = CHARGE_ARC.radius * (1 - Math.cos(half));

    expect(chord).toBeGreaterThan(110);
    expect(sagitta).toBeLessThan(chord / 4);
  });
});

describe('the battle screen renders', () => {
  const own = [
    createInstance('Charmander', { instanceId: 'own-0' }),
    createInstance('Pidgey', { instanceId: 'own-1' }),
  ];
  const foe = [
    createInstance('Squirtle', { instanceId: 'foe-0' }),
    createInstance('Geodude', { instanceId: 'foe-1' }),
  ];

  it('draws all four mons on the field with the right sprite facing', () => {
    render(<BattleScreen own={own} foe={foe} seed={1} scenarioNote="test" runKey="t1" />);

    // Own side uses back sprites, the foe front sprites.
    const charmander = screen.getAllByAltText('Charmander')[0]!;
    expect(charmander.getAttribute('src')).toContain('/back/');

    const squirtle = screen.getAllByAltText('Squirtle')[0]!;
    expect(squirtle.getAttribute('src')).not.toContain('/back/');
  });

  it('opens paused', () => {
    render(<BattleScreen own={own} foe={foe} seed={1} scenarioNote="test" runKey="t2" />);
    expect(screen.getByRole('button', { name: 'Play' })).toBeDefined();
    expect(screen.queryByRole('button', { name: 'Pause' })).toBeNull();
  });

  it('opens at Step 0 with the synergy opening already applied and shown', () => {
    render(<BattleScreen own={own} foe={foe} seed={1} scenarioNote="test" runKey="t3" />);

    expect(screen.getByText('STEP 0')).toBeDefined();
    expect(screen.getByText('OPENING')).toBeDefined();

    // Trait counters are a type icon plus a count. Charmander/Pidgey carries Fire, Normal and
    // Flying, so those icons are on screen before Step 1 is taken.
    const fire = screen.getAllByAltText('Fire');
    expect(fire.length).toBeGreaterThan(0);
    expect(fire[0]!.getAttribute('src')).toBe('/icons/types/fire.png');
  });

  it('shows the foe line-up across the top', () => {
    render(<BattleScreen own={own} foe={foe} seed={1} scenarioNote="test" runKey="t5" />);
    // Both foe mons are listed by name in the roster strip.
    expect(screen.getAllByText('Squirtle').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Geodude').length).toBeGreaterThan(0);
  });

  it('names mons by species rather than leaking instance ids', () => {
    render(<BattleScreen own={own} foe={foe} seed={1} scenarioNote="test" runKey="t4" />);
    expect(screen.queryByText(/own-0/)).toBeNull();
  });
});

describe('the controls actually work', () => {
  const own = [createInstance('Charmander', { instanceId: 'p-own-0' })];
  const foe = [createInstance('Squirtle', { instanceId: 'p-foe-0' })];

  it('Play starts the fight', async () => {
    render(<BattleScreen own={own} foe={foe} seed={1} scenarioNote="t" runKey="c1" />);

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Play' }));
    });

    expect(screen.getByRole('button', { name: 'Pause' })).toBeDefined();
  });

  it('Play still starts the fight after a StrictMode mount/unmount/remount cycle', async () => {
    // StrictMode deliberately mounts, tears down and remounts every effect in development. A
    // player built in useMemo and disposed in an effect cleanup is dead by the second mount --
    // the clock is cancelled for good and every control silently no-ops.
    render(
      <StrictMode>
        <BattleScreen own={own} foe={foe} seed={1} scenarioNote="t" runKey="c2" />
      </StrictMode>,
    );

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Play' }));
    });

    expect(screen.getByRole('button', { name: 'Pause' })).toBeDefined();
  });

  it('One Step advances a Step and pauses again', async () => {
    render(<BattleScreen own={own} foe={foe} seed={1} scenarioNote="t" runKey="c3" />);
    expect(screen.getByText('STEP 0')).toBeDefined();

    fireEvent.click(screen.getByRole('button', { name: /one Step/i }));

    // A Step is several beats and plays in real time, so wait for the condition rather than for
    // a guessed duration.
    await waitFor(() => expect(screen.getByText('STEP 1')).toBeDefined(), { timeout: 4000 });
    await waitFor(() => expect(screen.getByRole('button', { name: 'Play' })).toBeDefined(), {
      timeout: 4000,
    });
  });

  it('keeps playing past one Step when Play is used instead', async () => {
    render(<BattleScreen own={own} foe={foe} seed={1} scenarioNote="t" runKey="c4" />);
    fireEvent.click(screen.getByRole('button', { name: 'Play' }));

    // This pairing resolves on its own, so autoplay should carry it to a verdict unattended.
    await waitFor(() => expect(screen.getByText(/You win|You lose|Draw/)).toBeDefined(), {
      timeout: 8000,
    });
  }, 20_000);
});

describe('the fight holds on its verdict before reporting', () => {
  const own = [createInstance('Charmander', { instanceId: 'h-own' })];
  const foe = [createInstance('Squirtle', { instanceId: 'h-foe' })];

  it('does not report finished on the frame the killing Step is computed', async () => {
    // `isOver` reads the simulation and is true the moment the last mon is removed, which happens
    // while that Step's beats are still queued. Reporting on it cut straight to the results screen
    // over the top of the death blow. Playback completion is the signal, not sim state.
    const finished: string[] = [];
    render(
      <BattleScreen
        own={own}
        foe={foe}
        seed={1}
        scenarioNote="t"
        runKey="hold1"
        onFinished={(o) => finished.push(o)}
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Play' }));

    // The verdict appears on the field first...
    await waitFor(() => expect(screen.getByText(/You win|You lose|Draw/)).toBeDefined(), {
      timeout: 8000,
    });
    expect(finished).toHaveLength(0);

    // ...and only after the hold does the run layer hear about it.
    await waitFor(() => expect(finished).toHaveLength(1), { timeout: 8000 });
  }, 20_000);

  it('reports exactly once', async () => {
    const finished: string[] = [];
    render(
      <BattleScreen
        own={own}
        foe={foe}
        seed={1}
        scenarioNote="t"
        runKey="hold2"
        onFinished={(o) => finished.push(o)}
      />,
    );

    // "Run to the end" rather than Play: this test is about the callback firing once, not about
    // playback pacing, and fights now run long enough at normal speed to make an 8-second wait
    // marginal. Both routes end through the same finish path.
    fireEvent.click(screen.getByRole('button', { name: /Run to the end/i }));
    await waitFor(() => expect(finished).toHaveLength(1), { timeout: 15_000 });
    await act(async () => {
      await new Promise((r) => setTimeout(r, 400));
    });

    expect(finished).toHaveLength(1);
  }, 25_000);
});

describe('catching the last foe does not lock the screen', () => {
  it('reports the fight finished after a catch ends it while paused', async () => {
    // The freeze: a battle opens paused, you throw and catch the only foe, and the runner is now
    // over — which disabled every control — while the playback loop was never running, so nothing
    // ever ran the end handling and the fight never reported itself. Dead screen.
    const own = [createInstance('Charmander', { instanceId: 'lock-own' })];
    const foe = [createInstance('Squirtle', { instanceId: 'lock-foe' })];
    const finished: string[] = [];

    useRunStore.setState({
      run: { ...useRunStore.getState().run, balls: { Poke: 0, Great: 0, Ultra: 9 } },
    });

    render(
      <BattleScreen
        own={own}
        foe={foe}
        seed={1}
        scenarioNote="t"
        runKey="lock1"
        onFinished={(o) => finished.push(o)}
      />,
    );

    // Throw Ultra Balls until one lands. Each is a real roll, so this may take a few.
    for (let i = 0; i < 40 && finished.length === 0; i++) {
      const buttons = screen.queryAllByRole('button', { name: /%/ });
      const usable = buttons.find((b) => !(b as HTMLButtonElement).disabled);
      if (usable === undefined) break;
      fireEvent.click(usable);
      await act(async () => {
        await new Promise((r) => setTimeout(r, 30));
      });
    }

    await waitFor(() => expect(finished).toHaveLength(1), { timeout: 8000 });
    expect(finished[0]).toBe('SideAWins');
  }, 20_000);
});

describe('charge fills during the attack beat, not after', () => {
  it('applies a ChargeGained event in the same display update as the attack that precedes it', () => {
    // Before the fix, charge was applied in its own sequential iteration after the attack's beat
    // had already finished waiting — the arc snapped to its new value once the lunge was over.
    // The fix folds a zero-length beat into whichever visible beat precedes it, so both changes
    // land in one display update and the arc's CSS transition runs while the lunge plays.
    // Speed 1, not 3: at 3 the mon would also cross the charge threshold this same Step and its
    // passive would reset charge straight back to 0, which is correct simulation behaviour but
    // would obscure what this test is checking.
    const attacker = makeCombatant({ instanceId: 'a', attack: 3, health: 20, speed: 1 });
    const defender = makeCombatant({ instanceId: 'b', attack: 1, health: 20, speed: 1 });
    const board = makeBattleState([attacker], [defender]);
    const runner = new OnDemandRunner([attacker], [defender], 1);
    const events = runner.nextStep();

    const damageIndex = events.findIndex((e) => e.kind === 'Damage');
    const chargeIndex = events.findIndex((e) => e.kind === 'ChargeGained');
    expect(damageIndex).toBeGreaterThanOrEqual(0);
    expect(chargeIndex).toBeGreaterThan(damageIndex);

    // Simulate the player's grouping: apply events until a nonzero-beat event, folding any
    // trailing zero-beat events into the same update.
    let display = initialDisplay(board);
    const BEAT: Record<string, number> = { Damage: 420, ChargeGained: 0 };
    const updates: number[] = [];
    for (let i = 0; i < events.length; i++) {
      display = applyEvent(display, events[i]!);
      const wait = BEAT[events[i]!.kind] ?? 240;
      while (i + 1 < events.length && (BEAT[events[i + 1]!.kind] ?? 240) === 0) {
        i++;
        display = applyEvent(display, events[i]!);
      }
      updates.push(wait);
    }

    // The attack and the charge gain that follows it land in the same grouped update — there is
    // no separate zero-length update between them.
    expect(updates.filter((w) => w === 0)).toHaveLength(0);
    expect(display.board.lineUpA[0]!.charge).toBeGreaterThan(0);
  });
});

describe('the charge fill duration spans the remaining beat, not a short snap', () => {
  it('gives a gain a duration well past the old fixed short transition', async () => {
    // Speed 1 so the mon does not also cross the charge threshold this Step, which would fire
    // its passive and immediately reset the duration to the fast ability-snap value instead.
    const attacker = createInstance('Charmander', { instanceId: 'a' });
    const defender = createInstance('Squirtle', { instanceId: 'b' });

    let seen: number | undefined;
    const player = new BattlePlayer([attacker].map(toCombatant), [defender].map(toCombatant), 1);
    const unsub = player.subscribe(() => {
      const value = player.getSnapshot().chargeDurationMs[attacker.instanceId];
      if (value !== undefined) seen = value;
    });

    player.stepOnce();
    // Two Damage beats (420ms each) precede the merged ChargeGained group in this exchange, so
    // the duration isn't recorded until partway through the second one.
    await new Promise((r) => setTimeout(r, 900));

    unsub();
    player.dispose();
    // The old fixed transition was 280ms; a duration meant to span the rest of the Step plus the
    // inter-Step gap should clear it by a wide margin.
    expect(seen).toBeGreaterThan(280);
  });
});
