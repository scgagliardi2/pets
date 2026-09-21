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
import { BoxScreen, GrowthPanel, TeamBuilder } from '../src/ui/RunScreen.js';
import { PokemonDetailModal } from '../src/ui/PokemonDetailModal.js';
import { useRunStore } from '../src/state/runStore.js';
import { createInstance, statsOf, unspentPoints } from '../src/content/factory.js';
import { createRun } from '../src/meta/runState.js';
import { MAX_PARTY_SIZE } from '../src/meta/progression.js';
import { EXP_PER_COMBINE } from '../src/meta/experience.js';

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

  it('has no reorder or remove buttons — those are drag-only now', () => {
    // The X and the arrows were removed: drag-to-reorder and drag-to-Box cover both, and a card
    // full of small buttons was the thing this redesign was for.
    render(<TeamBuilder />);
    expect(screen.queryByTitle('Move forward')).toBeNull();
    expect(screen.queryByTitle('Send to the Box')).toBeNull();
  });

  it('opens the detail modal on click', () => {
    render(<TeamBuilder />);
    fireEvent.click(screen.getByText('Charmander'));
    expect(useRunStore.getState().detailInstanceId).toBe('a');
  });

  it('still reorders by dragging a card into a gap', () => {
    // Covered in depth further down; this just confirms the affordance survived the redesign.
    const { container } = render(<TeamBuilder />);
    fireEvent.drop(container.querySelectorAll('.slot-gap')[0]!, {
      dataTransfer: payloadFor('c', 'lineUp'),
    });
    expect(useRunStore.getState().run.lineUp.map((m) => m.instanceId)).toEqual(['c', 'a', 'b']);
  });

  it('reorders by dropping into a gap between slots', () => {
    const { container } = render(<TeamBuilder />);
    const gaps = container.querySelectorAll('.slot-gap');

    // Drop the third mon into the first gap — it should land at the front.
    fireEvent.drop(gaps[0]!, { dataTransfer: payloadFor('c', 'lineUp') });

    expect(useRunStore.getState().run.lineUp.map((m) => m.instanceId)).toEqual(['c', 'a', 'b']);
  });

  it('swaps two mons when one is dropped directly onto the other', () => {
    // The new model: a card drop always does something. Two different lines, so this swaps
    // rather than combines.
    render(<TeamBuilder />);
    const charmanderCard = screen.getByText('Charmander').closest('.team-card')!;
    fireEvent.drop(charmanderCard, { dataTransfer: payloadFor('c', 'lineUp') });

    expect(useRunStore.getState().run.lineUp.map((m) => m.instanceId)).toEqual(['c', 'b', 'a']);
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
    // The line-up section is now a SlottedRow (a .slot-row), not a .box-grid — dropping on its
    // background (not a gap, not a card) inserts at the end via the row's own catch-all handler.
    const { container } = render(<BoxScreen />);
    const lineUpRow = container.querySelector('.slot-row')!;

    fireEvent.drop(lineUpRow, { dataTransfer: payloadFor('y', 'box') });

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
    // The line-up section is a .slot-row now; .box-grid refers only to storage, so there's just
    // the one.
    const boxGrid = container.querySelector('.box-grid')!;

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

describe('spending EXP', () => {
  beforeEach(() => {
    useRunStore.setState({
      run: {
        ...createRun(1, [createInstance('Charmander', { instanceId: 'a' })]),
        lineUp: [createInstance('Charmander', { instanceId: 'a', statPoints: 2 })],
      },
      phase: 'map',
    });
  });

  it('offers a choice of all four stats', () => {
    render(<GrowthPanel />);
    for (const label of ['ATT', 'HEA', 'SP', 'SPE']) {
      expect(screen.getByRole('button', { name: label })).toBeDefined();
    }
  });

  it('spends one point on the chosen stat and no more', () => {
    render(<GrowthPanel />);
    const before = statsOf(useRunStore.getState().run.lineUp[0]!);

    fireEvent.click(screen.getByRole('button', { name: 'ATT' }));

    const mon = useRunStore.getState().run.lineUp[0]!;
    expect(statsOf(mon).attack).toBe(before.attack + 1);
    expect(unspentPoints(mon)).toBe(1);
  });

  it('disappears once every point is spent', () => {
    const { rerender } = render(<GrowthPanel />);
    fireEvent.click(screen.getByRole('button', { name: 'HEA' }));
    rerender(<GrowthPanel />);
    fireEvent.click(screen.getByRole('button', { name: 'SPE' }));
    rerender(<GrowthPanel />);

    expect(screen.queryByRole('button', { name: 'ATT' })).toBeNull();
  });

  it('will not spend a point a mon does not have', () => {
    useRunStore.setState({
      run: { ...useRunStore.getState().run, lineUp: [createInstance('Charmander', { instanceId: 'a' })] },
    });
    const { container } = render(<GrowthPanel />);
    expect(container.querySelector('.growth-panel')).toBeNull();
  });

  it('stops offering Speed once it is capped', () => {
    useRunStore.setState({
      run: {
        ...useRunStore.getState().run,
        lineUp: [
          {
            ...createInstance('Charmander', { instanceId: 'a' }),
            statPoints: 5,
            allocation: { attack: 0, health: 0, special: 0, speed: 200 },
          },
        ],
      },
    });
    render(<GrowthPanel />);
    expect((screen.getByRole('button', { name: 'SPE' }) as HTMLButtonElement).disabled).toBe(true);
  });
});

describe('the Pokémon detail modal', () => {
  beforeEach(() => {
    useRunStore.setState({
      run: {
        ...createRun(1, [createInstance('Charmander', { instanceId: 'a', statPoints: 3 })]),
        box: [createInstance('Charmander', { instanceId: 'b' })],
      },
      detailInstanceId: 'a',
    });
  });

  it('shows the species, its ability, and its stats', () => {
    render(<PokemonDetailModal instanceId="a" />);
    expect(screen.getByRole('heading', { name: 'Charmander' })).toBeDefined();
    expect(screen.getByText('Ember Burst')).toBeDefined();
    expect(screen.getByText(/burn/i)).toBeDefined();
  });

  it('offers the pending points to spend, and spending one updates the mon', () => {
    render(<PokemonDetailModal instanceId="a" />);
    expect(screen.getByText(/3 stat points to spend/i)).toBeDefined();

    fireEvent.click(screen.getByRole('button', { name: 'ATT' }));

    const mon = useRunStore.getState().run.lineUp[0]!;
    expect(mon.allocation.attack).toBe(1);
  });

  it('hides the points section once nothing is pending', () => {
    useRunStore.setState({
      run: { ...useRunStore.getState().run, lineUp: [createInstance('Charmander', { instanceId: 'a' })] },
    });
    render(<PokemonDetailModal instanceId="a" />);
    expect(screen.queryByText(/points to spend/i)).toBeNull();
  });

  it('lists a same-line duplicate and combines on click, adding a flat evolution\'s worth', () => {
    render(<PokemonDetailModal instanceId="a" />);
    fireEvent.click(screen.getByTitle(/Combine with this Charmander/));

    const run = useRunStore.getState().run;
    expect(run.box).toHaveLength(0);
    // A flat EXP_PER_COMBINE toward evolving, regardless of the sacrifice's own progress.
    expect(run.lineUp[0]!.exp).toBe(EXP_PER_COMBINE);
  });

  it('lists a duplicate elsewhere in the same evolution line, not just the same species', () => {
    useRunStore.setState({
      run: {
        ...useRunStore.getState().run,
        box: [createInstance('Charmeleon', { instanceId: 'evolved', timesEvolved: 1 })],
      },
    });
    render(<PokemonDetailModal instanceId="a" />);
    expect(screen.getByTitle(/Combine with this Charmeleon/)).toBeDefined();
  });

  it('closes on scrim click but not on panel click', () => {
    const { container } = render(<PokemonDetailModal instanceId="a" />);
    fireEvent.click(container.querySelector('.detail-modal')!);
    expect(useRunStore.getState().detailInstanceId).toBe('a');

    fireEvent.click(container.querySelector('.modal-scrim')!);
    expect(useRunStore.getState().detailInstanceId).toBeNull();
  });

  it('closes cleanly if the mon it was viewing disappears', () => {
    useRunStore.setState({ run: { ...useRunStore.getState().run, lineUp: [], box: [] } });
    render(<PokemonDetailModal instanceId="a" />);
    expect(useRunStore.getState().detailInstanceId).toBeNull();
  });
});

describe('card drops: combine or swap, always one or the other', () => {
  beforeEach(() => {
    useRunStore.setState({
      run: {
        ...createRun(1, [
          createInstance('Charmander', { instanceId: 'a' }),
          createInstance('Squirtle', { instanceId: 'x' }),
        ]),
        box: [createInstance('Charmander', { instanceId: 'dupe' })],
      },
      phase: 'map',
    });
  });

  it('combines when a same-line mon is dropped directly onto a card', () => {
    const { container } = render(<TeamBuilder />);
    const charmanderCard = screen.getByText('Charmander').closest('.team-card')!;

    fireEvent.drop(charmanderCard, { dataTransfer: payloadFor('dupe', 'box') });

    const run = useRunStore.getState().run;
    expect(run.box).toHaveLength(0);
    expect(run.lineUp.find((m) => m.instanceId === 'a')!.exp).toBe(EXP_PER_COMBINE);
    // The gap-based insert handler must not also have fired for this drop.
    expect(container.querySelectorAll('.team-card')).toHaveLength(2);
  });

  it('swaps rather than combining when the two are different lines', () => {
    // The old model silently declined a non-combinable card drop, which read as broken. The new
    // one always does something: a mismatched pair swaps places instead.
    render(<TeamBuilder />);
    const charmanderCard = screen.getByText('Charmander').closest('.team-card')!;

    fireEvent.drop(charmanderCard, { dataTransfer: payloadFor('x', 'lineUp') });

    expect(useRunStore.getState().run.lineUp.map((m) => m.instanceId)).toEqual(['x', 'a']);
  });

  it('swaps across the line-up and the Box in one drop', () => {
    render(<TeamBuilder />);
    // 'dupe' is a Charmander in the Box, same line as the line-up's Charmander, so drop it onto
    // Squirtle instead to force a cross-group swap rather than a combine.
    const squirtleCard = screen.getByText('Squirtle').closest('.team-card')!;

    fireEvent.drop(squirtleCard, { dataTransfer: payloadFor('dupe', 'box') });

    const run = useRunStore.getState().run;
    expect(run.lineUp.map((m) => m.instanceId)).toEqual(['a', 'dupe']);
    expect(run.box.map((m) => m.instanceId)).toEqual(['x']);
  });
});

describe('the Box screen line-up gains the same gaps and card behaviour', () => {
  beforeEach(() => {
    useRunStore.setState({
      run: {
        ...createRun(1, [
          createInstance('Charmander', { instanceId: 'a' }),
          createInstance('Squirtle', { instanceId: 'b' }),
        ]),
        box: [createInstance('Charmander', { instanceId: 'dupe' })],
      },
      phase: 'box',
    });
  });

  it('inserts via a gap in the Box screen\'s own line-up row, not just the bottom strip', () => {
    const { container } = render(<BoxScreen />);
    const gaps = container.querySelectorAll('.slot-row .slot-gap');
    expect(gaps.length).toBeGreaterThan(0);

    fireEvent.drop(gaps[0]!, { dataTransfer: payloadFor('dupe', 'box') });

    expect(useRunStore.getState().run.lineUp.map((m) => m.instanceId)).toEqual(['dupe', 'a', 'b']);
  });

  it('swaps by dropping one line-up card onto another, right there in the Box screen', () => {
    render(<BoxScreen />);
    // Two Charmanders exist (one line-up, one boxed) so name text alone is ambiguous; scope to
    // the line-up row specifically.
    const lineUpRow = document.querySelector('.slot-row')!;
    const charmanderCard = lineUpRow.querySelector('.team-card')!;
    fireEvent.drop(charmanderCard, { dataTransfer: payloadFor('b', 'lineUp') });

    expect(useRunStore.getState().run.lineUp.map((m) => m.instanceId)).toEqual(['b', 'a']);
  });
});
