/**
 * What winning and losing a node do to the map.
 *
 * The asymmetry is the point: a win consumes the node, a loss does not. A loss already costs
 * Morale, and Morale is what makes a run finite; taking the node as well charges twice and can
 * strand a player on a layer that offered them a single way forward.
 */

import { beforeEach, describe, expect, it } from 'vitest';

import { createInstance } from '../src/content/factory.js';
import { generateLocationMap, nodeById } from '../src/meta/mapGenerator.js';
import { locationFor } from '../src/meta/locations.js';
import { createRun } from '../src/meta/runState.js';
import { useRunStore } from '../src/state/runStore.js';

const SEED = 4;

const aWildNode = () => {
  const map = generateLocationMap(SEED, 0);
  return { map, node: map.nodes.find((n) => n.type === 'Wild')! };
};

beforeEach(() => {
  const { map, node } = aWildNode();
  useRunStore.setState({
    run: createRun(SEED, [createInstance('Charmander', { instanceId: 'a' })]),
    map,
    location: locationFor(0),
    available: [node.id],
    phase: 'map',
    activeNode: null,
    event: null,
    eventResult: null,
    pendingBounty: null,
    lastResult: null,
  });
});

describe('losing a fight', () => {
  it('leaves the node on the map, so it can be taken again', () => {
    const { node } = aWildNode();
    useRunStore.getState().enter(node.id);
    useRunStore.getState().finishBattle('SideBWins');
    useRunStore.getState().dismissResult();

    const state = useRunStore.getState();
    expect(state.run.visited).not.toContain(node.id);
    expect(state.available).toContain(node.id);
    expect(state.run.currentNodeId).toBeNull();
  });

  it('still costs the Morale, which is what the retry is paid for with', () => {
    const { node } = aWildNode();
    const morale = useRunStore.getState().run.morale;

    useRunStore.getState().enter(node.id);
    useRunStore.getState().finishBattle('SideBWins');

    expect(useRunStore.getState().run.morale).toBe(morale - 1);
  });

  it('can actually be re-entered, against the same opposition', () => {
    const { node } = aWildNode();

    useRunStore.getState().enter(node.id);
    const first = useRunStore.getState().opponents.map((m) => m.speciesId);
    useRunStore.getState().finishBattle('SideBWins');
    useRunStore.getState().dismissResult();

    useRunStore.getState().enter(node.id);
    const state = useRunStore.getState();

    expect(state.phase).toBe('battle');
    // The node is not a re-roll: the same fight is waiting, so a second attempt has to be bought
    // by changing the line-up rather than by hoping for a softer draw.
    expect(state.opponents.map((m) => m.speciesId)).toEqual(first);
  });

  it('ends the run anyway once the Morale is gone', () => {
    const { node } = aWildNode();
    useRunStore.setState({ run: { ...useRunStore.getState().run, morale: 1 } });

    useRunStore.getState().enter(node.id);
    useRunStore.getState().finishBattle('SideBWins');

    expect(useRunStore.getState().phase).toBe('over');
  });
});

describe('winning a fight', () => {
  it('consumes the node and moves the traveller onto it', () => {
    const { node } = aWildNode();
    useRunStore.getState().enter(node.id);
    useRunStore.getState().finishBattle('SideAWins');
    useRunStore.getState().dismissResult();

    const state = useRunStore.getState();
    expect(state.run.visited).toContain(node.id);
    expect(state.available).not.toContain(node.id);
    expect(state.available).toEqual(nodeById(state.map, node.id)!.next);
  });

  it('consumes the node on a draw too, since a draw costs nothing to repeat', () => {
    const { node } = aWildNode();
    useRunStore.getState().enter(node.id);
    useRunStore.getState().finishBattle('Draw');

    expect(useRunStore.getState().run.visited).toContain(node.id);
    expect(useRunStore.getState().run.morale).toBe(3);
  });
});
