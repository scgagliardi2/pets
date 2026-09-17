/**
 * Encounter nodes through the run store: entering one, taking a branch, and what a branch that
 * starts a fight does to the loop.
 *
 * The rules themselves are tested pure in `roadEvents.test.ts`. What is checked here is the
 * plumbing the store owns and the pure layer cannot: that a node resolves once, that a branch
 * which starts a fight hands the battle screen the right opponents, and that the bounty is paid
 * by the win rather than by the choice.
 */

import { beforeEach, describe, expect, it } from 'vitest';

import { createInstance } from '../src/content/factory.js';
import { buildRoadEvent } from '../src/meta/roadEvents.js';
import { generateLocationMap } from '../src/meta/mapGenerator.js';
import { locationFor } from '../src/meta/locations.js';
import { createRun, type MapNode } from '../src/meta/runState.js';
import { useRunStore } from '../src/state/runStore.js';

const party = () => [
  createInstance('Charmander', { instanceId: 'a', exp: 6 }),
  createInstance('Pidgey', { instanceId: 'b', exp: 2 }),
];

/** The first Encounter node on a generated map. Generation guarantees there is one. */
function anEncounterNode(seed: number): { node: MapNode; map: ReturnType<typeof generateLocationMap> } {
  const map = generateLocationMap(seed, 0);
  const node = map.nodes.find((n) => n.type === 'Encounter')!;
  return { node, map };
}

beforeEach(() => {
  const { node, map } = anEncounterNode(1);
  useRunStore.setState({
    run: { ...createRun(1, party()), money: 20 },
    map,
    location: locationFor(0),
    // Offered directly, so the test doesn't have to walk a generated path to reach it.
    available: [node.id],
    phase: 'map',
    activeNode: null,
    event: null,
    eventResult: null,
    pendingBounty: null,
    lastResult: null,
  });
});

describe('entering an Encounter', () => {
  it('opens the scene instead of the battle screen', () => {
    const { node } = anEncounterNode(1);
    useRunStore.getState().enter(node.id);

    const state = useRunStore.getState();
    expect(state.phase).toBe('encounter');
    expect(state.event).not.toBeNull();
    expect(state.opponents).toHaveLength(0);
    expect(state.activeNode!.id).toBe(node.id);
  });

  it('rolls the same encounter every time that node is entered', () => {
    const { node } = anEncounterNode(1);

    useRunStore.getState().enter(node.id);
    const first = useRunStore.getState().event!;

    // Back to the map without resolving, then in again.
    useRunStore.setState({ phase: 'map', event: null, activeNode: null });
    useRunStore.getState().enter(node.id);

    expect(useRunStore.getState().event!.kind).toBe(first.kind);
    expect(useRunStore.getState().event!.body).toBe(first.body);
  });

  it('resolves exactly one branch, however many times the button is pressed', () => {
    const { node } = anEncounterNode(1);
    useRunStore.setState({
      phase: 'encounter',
      activeNode: node,
      event: buildRoadEvent('FoundStash', useRunStore.getState().run, locationFor(0), 3),
    });

    const before = useRunStore.getState().run.balls.Poke;
    useRunStore.getState().chooseEncounter(0);
    const after = useRunStore.getState().run.balls.Poke;

    useRunStore.getState().chooseEncounter(0);
    useRunStore.getState().chooseEncounter(1);

    expect(after).toBeGreaterThan(before);
    expect(useRunStore.getState().run.balls.Poke).toBe(after);
  });

  it('marks the node taken and returns to the map', () => {
    const { node } = anEncounterNode(1);
    useRunStore.getState().enter(node.id);
    useRunStore.getState().chooseEncounter(1);
    useRunStore.getState().leaveEncounter();

    const state = useRunStore.getState();
    expect(state.phase).toBe('map');
    expect(state.run.visited).toContain(node.id);
    expect(state.event).toBeNull();
    expect(state.available).not.toContain(node.id);
  });
});

describe('an Encounter that starts a fight', () => {
  const armLegendary = () => {
    const { node } = anEncounterNode(1);
    const run = useRunStore.getState().run;
    useRunStore.setState({
      phase: 'encounter',
      activeNode: node,
      event: buildRoadEvent('LegendarySighting', run, locationFor(0), 5),
      eventResult: null,
    });
    return node;
  };

  it('hands the battle screen the Legendary and holds the bounty', () => {
    armLegendary();
    useRunStore.getState().chooseEncounter(0);

    const state = useRunStore.getState();
    expect(state.phase).toBe('battle');
    expect(state.opponents).toHaveLength(1);
    expect(state.pendingBounty).not.toBeNull();
    // Not paid yet: the fight has to be won first.
    expect(state.run.money).toBe(20);
  });

  it('pays the bounty on a win, on top of the node own takings', () => {
    armLegendary();
    useRunStore.getState().chooseEncounter(0);
    const bounty = useRunStore.getState().pendingBounty!;
    const balls = useRunStore.getState().run.balls[bounty.ball!];

    useRunStore.getState().finishBattle('SideAWins');

    const state = useRunStore.getState();
    expect(state.run.money).toBe(20 + bounty.money);
    expect(state.run.balls[bounty.ball!]).toBe(balls + bounty.ballCount);
    expect(state.lastResult!.bounty).toEqual(bounty);
    expect(state.pendingBounty).toBeNull();
  });

  it('pays nothing on a loss, and costs the morale a lost fight costs', () => {
    armLegendary();
    useRunStore.getState().chooseEncounter(0);
    const morale = useRunStore.getState().run.morale;

    useRunStore.getState().finishBattle('SideBWins');

    const state = useRunStore.getState();
    expect(state.run.money).toBe(20);
    expect(state.run.morale).toBe(morale - 1);
    expect(state.lastResult!.bounty).toBeNull();
  });

  it('does not also pay the wild rate for the node, which would pay twice for one node', () => {
    armLegendary();
    useRunStore.getState().chooseEncounter(0);
    const bounty = useRunStore.getState().pendingBounty!;

    useRunStore.getState().finishBattle('SideAWins');

    expect(useRunStore.getState().lastResult!.money).toBe(0);
    expect(useRunStore.getState().run.money).toBe(20 + bounty.money);
  });
});
