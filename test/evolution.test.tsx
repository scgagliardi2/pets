// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { cleanup, render, screen, waitFor } from '@testing-library/react';

import { EvolutionPopup } from '../src/ui/EvolutionPopup.js';
import { useRunStore } from '../src/state/runStore.js';
import { createRun } from '../src/meta/runState.js';
import { createInstance } from '../src/content/factory.js';
import { speciesNamed } from '../src/content/index.js';
import { EXP_PER_COMBINE, EXP_PER_EVOLUTION } from '../src/meta/experience.js';

afterEach(cleanup);

const evolution = {
  instanceId: 'a',
  from: speciesNamed('Charmander')!,
  to: speciesNamed('Charmeleon')!,
};

describe('the evolution popup', () => {
  it('opens on the old form and announces that it is evolving', () => {
    render(<EvolutionPopup evolution={evolution} />);
    expect(screen.getByText(/Charmander is evolving/)).toBeDefined();
    // No way out until the animation has played — the Continue button is not there yet.
    expect(screen.queryByRole('button', { name: 'Continue' })).toBeNull();
  });

  it('settles on the new form and offers a way out', async () => {
    render(<EvolutionPopup evolution={evolution} />);
    await waitFor(() => expect(screen.getByRole('button', { name: 'Continue' })).toBeDefined(), {
      timeout: 9000,
    });
    expect(screen.getByText('Charmeleon')).toBeDefined();
    expect(screen.getByAltText('Charmeleon')).toBeDefined();
  }, 15_000);
});

describe('evolutions reach the queue', () => {
  beforeEach(() => {
    useRunStore.setState({
      run: createRun(1, [
        createInstance('Charmander', { instanceId: 'keep', exp: EXP_PER_EVOLUTION - EXP_PER_COMBINE }),
        createInstance('Charmander', { instanceId: 'food' }),
      ]),
      evolutionQueue: [],
    });
  });

  it('queues an evolution caused by combining', () => {
    useRunStore.getState().combine('keep', 'food');

    const queue = useRunStore.getState().evolutionQueue;
    expect(queue).toHaveLength(1);
    expect(queue[0]!.from.name).toBe('Charmander');
    expect(queue[0]!.to.name).toBe('Charmeleon');
  });

  it('queues nothing when the combine does not cross a threshold', () => {
    useRunStore.setState({
      run: createRun(1, [
        createInstance('Charmander', { instanceId: 'keep' }),
        createInstance('Charmander', { instanceId: 'food' }),
      ]),
      evolutionQueue: [],
    });
    useRunStore.getState().combine('keep', 'food');
    expect(useRunStore.getState().evolutionQueue).toHaveLength(0);
  });

  it('dismisses one at a time, so a batch is shown in turn', () => {
    useRunStore.setState({
      evolutionQueue: [evolution, { ...evolution, instanceId: 'b' }],
    });
    useRunStore.getState().dismissEvolution();
    expect(useRunStore.getState().evolutionQueue).toHaveLength(1);
    useRunStore.getState().dismissEvolution();
    expect(useRunStore.getState().evolutionQueue).toHaveLength(0);
  });
});
