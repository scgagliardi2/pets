/**
 * Drag and drop, the team builder, and the Box.
 *
 * The payload plumbing is tested directly; the screens are tested through fired drag events,
 * which is as close to a real drag as jsdom gets — it has no drag simulation, so `dataTransfer`
 * is supplied by hand.
 */

// @vitest-environment jsdom

import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';

import { DRAG_MIME, readDragPayload, setDragPayload } from '../src/ui/dragDrop.js';
import { BoxScreen, TeamBuilder } from '../src/ui/RunScreen.js';
import { useRunStore } from '../src/state/runStore.js';
import { createInstance, statsOf } from '../src/content/factory.js';
import { createRun } from '../src/meta/runState.js';
import { MAX_PARTY_SIZE } from '../src/meta/progression.js';

afterEach(cleanup);

/** jsdom has no DataTransfer, so stand one up that behaves the way the handlers expect. */
function fakeDataTransfer(initial: Record<string, string> = {}) {
  const store: Record<string, string> = { ...initial };
  return {
    data: store,
    types: Object.keys(store),
    effectAllowed: 'move',
    dropEffect: 'move',
    setData(type: string, value: string) {
      store[type] = value;
      (this as { types: string[] }).types = Object.keys(store);
    },
    getData(type: string) {
      return store[type] ?? '';
    },
  };
}

const payloadFor = (instanceId: string, from: 'lineUp' | 'box') =>
  fakeDataTransfer({
    [DRAG_MIME]: JSON.stringify({ kind: 'mon', instanceId, from }),
    'text/plain': 'mon',
  });

describe('the drag payload', () => {
  it('round-trips through a dataTransfer', () => {
    const dt = fakeDataTransfer();
    const event = { dataTransfer: dt } as unknown as React.DragEvent;

    setDragPayload(event, { kind: 'ball', tier: 'Ultra' });
    expect(readDragPayload(event)).toEqual({ kind: 'ball', tier: 'Ultra' });
  });

  it('ignores a drag that is not ours', () => {
    const event = {
      dataTransfer: fakeDataTransfer({ 'text/plain': 'some selected words' }),
    } as unknown as React.DragEvent;

    expect(readDragPayload(event)).toBeNull();
  });

  it('ignores a malformed payload rather than throwing inside an event handler', () => {
    const event = {
      dataTransfer: fakeDataTransfer({ [DRAG_MIME]: 'not json' }),
    } as unknown as React.DragEvent;

    expect(readDragPayload(event)).toBeNull();
  });

  it('carries the kind, so a ball dropped on the roster is not treated as a mon', () => {
    const event = { dataTransfer: fakeDataTransfer() } as unknown as React.DragEvent;
    setDragPayload(event, { kind: 'ball', tier: 'Poke' });
    expect(readDragPayload(event)?.kind).toBe('ball');
  });
});

describe('the team builder', () => {
  beforeEach(() => {
    useRunStore.setState({
      run: createRun(1, [
        createInstance('Charmander', { instanceId: 'a' }),
        createInstance('Squirtle', { instanceId: 'b' }),
        createInstance('Pidgey', { instanceId: 'c' }),
      ]),
      phase: 'map',
    });
  });

  it('shows the line-up in slot order', () => {
    render(<TeamBuilder />);
    const names = screen.getAllByText(/Charmander|Squirtle|Pidgey/).map((n) => n.textContent);
    expect(names).toEqual(['Charmander', 'Squirtle', 'Pidgey']);
  });

  it('reorders with the arrow buttons', () => {
    render(<TeamBuilder />);
    // The second card's "move forward" arrow.
    const backs = screen.getAllByTitle('Move forward');
    fireEvent.click(backs[1]!);

    expect(useRunStore.getState().run.lineUp.map((m) => m.instanceId)).toEqual(['b', 'a', 'c']);
  });

  it('sends a mon to the Box', () => {
    render(<TeamBuilder />);
    fireEvent.click(screen.getAllByTitle('Send to the Box')[0]!);

    const run = useRunStore.getState().run;
    expect(run.lineUp.map((m) => m.instanceId)).toEqual(['b', 'c']);
    expect(run.box.map((m) => m.instanceId)).toEqual(['a']);
  });

  it('will not empty the line-up', () => {
    useRunStore.setState({ run: createRun(1, [createInstance('Charmander', { instanceId: 'x' })]) });
    render(<TeamBuilder />);

    const button = screen.getByTitle('Send to the Box') as HTMLButtonElement;
    expect(button.disabled).toBe(true);
  });

  it('reorders by dropping into a gap between slots', () => {
    const { container } = render(<TeamBuilder />);
    const gaps = container.querySelectorAll('.slot-gap');

    // Drop the third mon into the first gap — it should land at the front.
    fireEvent.drop(gaps[0]!, { dataTransfer: payloadFor('c', 'lineUp') });

    expect(useRunStore.getState().run.lineUp.map((m) => m.instanceId)).toEqual(['c', 'a', 'b']);
  });

  it('ignores a ball dropped on the roster', () => {
    const { container } = render(<TeamBuilder />);
    const before = useRunStore.getState().run.lineUp.map((m) => m.instanceId);

    fireEvent.drop(container.querySelector('.slot-gap')!, {
      dataTransfer: fakeDataTransfer({
        [DRAG_MIME]: JSON.stringify({ kind: 'ball', tier: 'Poke' }),
      }),
    });

    expect(useRunStore.getState().run.lineUp.map((m) => m.instanceId)).toEqual(before);
  });

  it('offers the Box with a count', () => {
    useRunStore.setState({
      run: {
        ...useRunStore.getState().run,
        box: [createInstance('Oddish', { instanceId: 'boxed' })],
      },
    });
    render(<TeamBuilder />);
    expect(screen.getByRole('button', { name: /Box \(1\)/ })).toBeDefined();
  });
});

describe('the Box screen', () => {
  beforeEach(() => {
    useRunStore.setState({
      run: {
        ...createRun(1, [createInstance('Charmander', { instanceId: 'a' })]),
        box: [
          createInstance('Oddish', { instanceId: 'x' }),
          createInstance('Geodude', { instanceId: 'y' }),
        ],
      },
      phase: 'box',
    });
  });

  it('lists the line-up and the Box separately', () => {
    render(<BoxScreen />);
    expect(screen.getByText(/Line-up \(1\//)).toBeDefined();
    expect(screen.getByText('Box (2)')).toBeDefined();
  });

  it('adds a boxed mon to the line-up', () => {
    render(<BoxScreen />);
    fireEvent.click(screen.getAllByTitle('Add to the line-up')[0]!);

    const run = useRunStore.getState().run;
    expect(run.lineUp.map((m) => m.instanceId)).toContain('x');
    expect(run.box.map((m) => m.instanceId)).not.toContain('x');
  });

  it('moves a mon by dragging it from the Box onto the line-up', () => {
    const { container } = render(<BoxScreen />);
    const lineUpGrid = container.querySelectorAll('.box-grid')[0]!;

    fireEvent.drop(lineUpGrid, { dataTransfer: payloadFor('y', 'box') });

    expect(useRunStore.getState().run.lineUp.map((m) => m.instanceId)).toEqual(['a', 'y']);
  });

  it('moves a mon back by dragging it onto the Box', () => {
    useRunStore.setState({
      run: {
        ...useRunStore.getState().run,
        lineUp: [
          createInstance('Charmander', { instanceId: 'a' }),
          createInstance('Squirtle', { instanceId: 'b' }),
        ],
      },
    });
    const { container } = render(<BoxScreen />);
    const boxGrid = container.querySelectorAll('.box-grid')[1]!;

    fireEvent.drop(boxGrid, { dataTransfer: payloadFor('b', 'lineUp') });

    expect(useRunStore.getState().run.box.map((m) => m.instanceId)).toContain('b');
  });

  it('refuses to overfill the line-up', () => {
    useRunStore.setState({
      run: {
        ...useRunStore.getState().run,
        lineUp: Array.from({ length: MAX_PARTY_SIZE }, (_, i) =>
          createInstance('Charmander', { instanceId: `m${i}` }),
        ),
      },
    });
    render(<BoxScreen />);

    const add = screen.getAllByTitle('Line-up is full')[0] as HTMLButtonElement;
    expect(add.disabled).toBe(true);
  });

  it('says so when the Box is empty rather than showing a blank panel', () => {
    useRunStore.setState({ run: { ...useRunStore.getState().run, box: [] } });
    render(<BoxScreen />);
    expect(screen.getByText(/Nothing stored/)).toBeDefined();
  });
});

describe('the type buffs outside battle', () => {
  beforeEach(() => {
    useRunStore.setState({
      run: createRun(1, [
        createInstance('Charmander', { instanceId: 'a' }),
        createInstance('Squirtle', { instanceId: 'b' }),
        createInstance('Pidgey', { instanceId: 'c' }),
      ]),
      phase: 'map',
      lastFusion: null,
    });
  });

  it('names every synergy the line-up is carrying, with its count', () => {
    render(<TeamBuilder />);

    // One of each: the same counts the battle screen's corners would show for this team.
    expect(screen.getByText('Ember Burst x1')).toBeDefined();
    expect(screen.getByText('Shell Guard x1')).toBeDefined();
    expect(screen.getByText('Steady Growth x1')).toBeDefined();
    expect(screen.getByText('Tailwind x1')).toBeDefined();
  });

  it('counts a second mon of a type, so the readout moves as the team is edited', () => {
    useRunStore.setState({
      run: {
        ...useRunStore.getState().run,
        lineUp: [
          createInstance('Charmander', { instanceId: 'a' }),
          createInstance('Charmander', { instanceId: 'd' }),
        ],
      },
    });
    render(<TeamBuilder />);

    expect(screen.getByText('Ember Burst x2')).toBeDefined();
  });

  it('spells the effect out where there is room for it, on the Box screen', () => {
    render(<BoxScreen />);
    expect(screen.getByText(/damage to the foe's Lead as the fight opens/)).toBeDefined();
  });
});

describe('combining two of a family', () => {
  beforeEach(() => {
    useRunStore.setState({
      run: {
        ...createRun(1, [
          createInstance('Charmander', { instanceId: 'a', exp: 3 }),
          createInstance('Charmeleon', { instanceId: 'b', exp: 4, timesEvolved: 1 }),
          createInstance('Squirtle', { instanceId: 'c' }),
        ]),
        box: [createInstance('Charmander', { instanceId: 'boxed' })],
      },
      phase: 'map',
      lastFusion: null,
    });
  });

  it('offers a mon with a partner and refuses one without', () => {
    render(<TeamBuilder />);

    expect(screen.getAllByTitle('Combine with another of its family')).toHaveLength(2);
    const alone = screen.getByTitle('Nothing of its family to combine with') as HTMLButtonElement;
    expect(alone.disabled).toBe(true);
  });

  it('merges the pair the player picks, keeping the evolved form', () => {
    render(<TeamBuilder />);
    const before = {
      charmander: statsOf(useRunStore.getState().run.lineUp[0]!),
      charmeleon: statsOf(useRunStore.getState().run.lineUp[1]!),
    };

    // Pick the Charmander, then commit into the Charmeleon.
    fireEvent.click(screen.getAllByTitle('Combine with another of its family')[0]!);
    fireEvent.click(screen.getByTitle('Combine into Charmeleon'));

    const lineUp = useRunStore.getState().run.lineUp;
    expect(lineUp.map((m) => m.instanceId)).toEqual(['b', 'c']);

    const merged = statsOf(lineUp[0]!);
    expect(merged.attack).toBe(Math.max(before.charmander.attack, before.charmeleon.attack) + 1);
    expect(merged.health).toBe(Math.max(before.charmander.health, before.charmeleon.health) + 1);
  });

  it('shows what the merge would produce before it is committed', () => {
    render(<TeamBuilder />);
    fireEvent.click(screen.getAllByTitle('Combine with another of its family')[0]!);

    expect(screen.getByText(/if combined/)).toBeDefined();
  });

  it('cancels on a second press, leaving the team alone', () => {
    render(<TeamBuilder />);
    const start = screen.getAllByTitle('Combine with another of its family')[0]!;

    fireEvent.click(start);
    fireEvent.click(screen.getByTitle('Cancel combine'));

    expect(screen.queryByTitle('Combine into Charmeleon')).toBeNull();
    expect(useRunStore.getState().run.lineUp).toHaveLength(3);
  });

  it('merges by dragging one mon onto another of its family', () => {
    const { container } = render(<TeamBuilder />);
    const cards = container.querySelectorAll('.team-card');

    // The drag has to start for the target to become a drop target at all — that is what tells
    // the card whether this particular mon would merge into it.
    fireEvent.dragStart(cards[0]!, { dataTransfer: payloadFor('a', 'lineUp') });
    fireEvent.drop(container.querySelectorAll('.team-card')[1]!, {
      dataTransfer: payloadFor('a', 'lineUp'),
    });

    expect(useRunStore.getState().run.lineUp.map((m) => m.instanceId)).toEqual(['b', 'c']);
  });

  it('will not merge a mon onto something outside its family', () => {
    const { container } = render(<TeamBuilder />);
    const cards = container.querySelectorAll('.team-card');

    fireEvent.dragStart(cards[0]!, { dataTransfer: payloadFor('a', 'lineUp') });
    fireEvent.drop(container.querySelectorAll('.team-card')[2]!, {
      dataTransfer: payloadFor('a', 'lineUp'),
    });

    // Both are still there: a drop on a mon of another family is not a merge.
    expect(useRunStore.getState().run.lineUp).toHaveLength(3);
  });

  it('feeds a boxed mon to one that is fighting, and says what came out', () => {
    render(<BoxScreen />);

    fireEvent.click(screen.getAllByTitle('Combine with another of its family').pop()!);
    fireEvent.click(screen.getByTitle('Combine into Charmeleon'));

    const run = useRunStore.getState().run;
    expect(run.box).toHaveLength(0);
    expect(run.lineUp.map((m) => m.instanceId)).toEqual(['a', 'b', 'c']);
    expect(screen.getByText(/absorbed its own kind/)).toBeDefined();
  });

  it('marks a mon that has been fused', () => {
    render(<TeamBuilder />);
    fireEvent.click(screen.getAllByTitle('Combine with another of its family')[0]!);
    fireEvent.click(screen.getByTitle('Combine into Charmeleon'));

    expect(screen.getByTitle('1 mon folded into this one')).toBeDefined();
  });
});
