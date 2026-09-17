/**
 * The evolution ceremony: its timeline, its escape hatch, and the results screen waiting behind it.
 *
 * Timers are faked, so the beats are asserted rather than waited for — what matters is the order
 * (the old mon, the flash, the new mon, then the screen behind it) and that nothing can leave the
 * player stuck inside the animation.
 */

// @vitest-environment jsdom

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';

import { CHARGE_MS, FLASH_MS, HOLD_MS, EvolutionScene } from '../src/ui/EvolutionScene.js';
import { ResultPanel } from '../src/ui/RunScreen.js';
import { speciesNamed } from '../src/content/index.js';
import { createInstance } from '../src/content/factory.js';
import { createRun } from '../src/meta/runState.js';
import { useRunStore } from '../src/state/runStore.js';
import type { Evolution } from '../src/meta/experience.js';

const evolution = (from: string, to: string, instanceId = 'a'): Evolution => ({
  instanceId,
  from: speciesNamed(from)!,
  to: speciesNamed(to)!,
});

const WHOLE_BEAT = CHARGE_MS + FLASH_MS + HOLD_MS;

beforeEach(() => vi.useFakeTimers());
afterEach(() => {
  vi.useRealTimers();
  cleanup();
});

describe('the evolution scene', () => {
  it('names the mon that is changing before it changes', () => {
    render(<EvolutionScene evolutions={[evolution('Charmander', 'Charmeleon')]} onDone={() => {}} />);

    expect(screen.getByText(/Charmander is evolving/)).toBeDefined();
    expect(screen.queryByText(/evolved into/)).toBeNull();
  });

  it('reveals the new mon after the flash', () => {
    const { container } = render(
      <EvolutionScene evolutions={[evolution('Charmander', 'Charmeleon')]} onDone={() => {}} />,
    );

    act(() => vi.advanceTimersByTime(CHARGE_MS));
    expect(container.querySelector('.stage-flash')).not.toBeNull();

    act(() => vi.advanceTimersByTime(FLASH_MS));
    expect(screen.getByText(/evolved into/)).toBeDefined();
    expect(screen.getByText('Charmeleon')).toBeDefined();
  });

  it('finishes on its own once the new mon has been held', () => {
    const done = vi.fn();
    render(<EvolutionScene evolutions={[evolution('Charmander', 'Charmeleon')]} onDone={done} />);

    act(() => vi.advanceTimersByTime(CHARGE_MS + FLASH_MS));
    expect(done).not.toHaveBeenCalled();

    act(() => vi.advanceTimersByTime(HOLD_MS));
    expect(done).toHaveBeenCalledTimes(1);
  });

  it('plays several evolutions one after another rather than at once', () => {
    const done = vi.fn();
    render(
      <EvolutionScene
        evolutions={[evolution('Charmander', 'Charmeleon', 'a'), evolution('Squirtle', 'Wartortle', 'b')]}
        onDone={done}
      />,
    );

    expect(screen.getByText('1 of 2')).toBeDefined();
    expect(screen.getByText(/Charmander is evolving/)).toBeDefined();

    act(() => vi.advanceTimersByTime(WHOLE_BEAT));
    expect(screen.getByText('2 of 2')).toBeDefined();
    expect(screen.getByText(/Squirtle is evolving/)).toBeDefined();
    expect(done).not.toHaveBeenCalled();

    act(() => vi.advanceTimersByTime(WHOLE_BEAT));
    expect(done).toHaveBeenCalledTimes(1);
  });

  it('can be skipped outright, however many are queued', () => {
    const done = vi.fn();
    render(
      <EvolutionScene
        evolutions={[evolution('Charmander', 'Charmeleon', 'a'), evolution('Squirtle', 'Wartortle', 'b')]}
        onDone={done}
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Skip' }));
    expect(done).toHaveBeenCalledTimes(1);
  });
});

describe('the results screen', () => {
  const resultWith = (evolutions: Evolution[]) => ({
    outcome: 'SideAWins' as const,
    nodeId: 'node-1',
    isGym: false,
    expEach: 1,
    money: 4,
    badge: false,
    moraleLost: 0,
    report: { gained: {}, evolutions },
  });

  beforeEach(() => {
    useRunStore.setState({
      run: createRun(1, [createInstance('Charmander', { instanceId: 'a' })]),
      phase: 'result',
    });
  });

  it('plays the ceremony before the tally when something evolved', () => {
    useRunStore.setState({ lastResult: resultWith([evolution('Charmander', 'Charmeleon')]) });
    render(<ResultPanel />);

    expect(screen.getByText(/Charmander is evolving/)).toBeDefined();
    expect(screen.queryByRole('button', { name: 'Continue' })).toBeNull();
  });

  it('shows the tally once the ceremony is over', () => {
    useRunStore.setState({ lastResult: resultWith([evolution('Charmander', 'Charmeleon')]) });
    render(<ResultPanel />);

    act(() => vi.advanceTimersByTime(WHOLE_BEAT));

    expect(screen.getByRole('button', { name: 'Continue' })).toBeDefined();
    expect(screen.getByText('Victory')).toBeDefined();
  });

  it('goes straight to the tally when nothing evolved', () => {
    useRunStore.setState({ lastResult: resultWith([]) });
    render(<ResultPanel />);

    expect(screen.getByRole('button', { name: 'Continue' })).toBeDefined();
  });
});
