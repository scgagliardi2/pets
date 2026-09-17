/**
 * The run layer: EXP, evolution, catch-up, roster moves, encounter scaling, and the loop's own
 * rules.
 *
 * All of it is pure, so none of these tests need a store or a component.
 */

import { describe, expect, it } from 'vitest';

import { createInstance, statsOf, type PokemonInstance } from '../src/content/factory.js';
import { speciesNamed, speciesOf } from '../src/content/index.js';
import { locationFor } from '../src/meta/locations.js';
import {
  CATCH_UP_EXP_GAP,
  EXP_PER_EVOLUTION,
  catchUpFloor,
  expSinceEvolution,
  grantWinExp,
  raiseToExp,
} from '../src/meta/experience.js';
import {
  defaultStarters,
  FIRST_LOCATION,
  encounterPool,
  generateGymTeam,
  generateWildEncounter,
  gymThemeFor,
  opponentsFor,
} from '../src/meta/encounters.js';
import {
  BADGES_TO_WIN,
  EXP_PER_BADGE,
  EXP_PER_WIN,
  MAX_PARTY_SIZE,
  STARTING_MORALE,
  baselineExp,
  gymExp,
  gymTeamSize,
  maxTier,
  wildEncounterSize,
  wildExp,
} from '../src/meta/progression.js';
import {
  addCaught,
  benchToBox,
  createRun,
  healAll,
  isRunOver,
  promoteFromBox,
  reorderLineUp,
  spendMorale,
  type MapNode,
  type RunState,
} from '../src/meta/runState.js';

const run = (mons: PokemonInstance[]): RunState => createRun(1, mons);

const charmander = (id: string, exp = 0) =>
  createInstance('Charmander', { instanceId: id, exp });

describe('EXP and evolution', () => {
  it('a point of EXP is banked as lifetime total, not a level', () => {
    const { mon } = raiseToExp(charmander('a'), 5);
    expect(mon.exp).toBe(5);
    expect(mon.timesEvolved).toBe(0);
  });

  it('evolves at twelve points and keeps the lifetime total', () => {
    const { mon, evolutions } = raiseToExp(charmander('a'), EXP_PER_EVOLUTION);

    expect(evolutions).toHaveLength(1);
    expect(evolutions[0]!.from.name).toBe('Charmander');
    expect(evolutions[0]!.to.name).toBe('Charmeleon');
    expect(mon.exp).toBe(EXP_PER_EVOLUTION);
    expect(mon.timesEvolved).toBe(1);
  });

  it('crosses two evolutions in one grant when the jump is big enough', () => {
    const { mon, evolutions } = raiseToExp(charmander('a'), EXP_PER_EVOLUTION * 2);

    expect(evolutions.map((e) => e.to.name)).toEqual(['Charmeleon', 'Charizard']);
    expect(mon.timesEvolved).toBe(2);
    expect(speciesOf(mon.speciesId)?.name).toBe('Charizard');
  });

  it('a mon at the end of its chain still banks the EXP', () => {
    // Nothing left to become, so it stops evolving — but the points keep buying stats.
    const mewtwo = createInstance('Mewtwo', { instanceId: 'm' });
    const { mon, evolutions } = raiseToExp(mewtwo, EXP_PER_EVOLUTION * 3);

    expect(evolutions).toHaveLength(0);
    expect(mon.exp).toBe(EXP_PER_EVOLUTION * 3);
    expect(mon.timesEvolved).toBe(0);
  });

  it('an evolved mon grows from its base form, so it is worth more but not rebased', () => {
    const { mon } = raiseToExp(charmander('a'), EXP_PER_EVOLUTION);
    const stats = statsOf(mon);
    const base = speciesNamed('Charmander')!;

    // 12 points split between attack and health, plus the flat +3/+3 for the evolution.
    expect(stats.attack + stats.health).toBe(
      base.baseAttack + base.baseHealth + EXP_PER_EVOLUTION + 6,
    );
    // Speed never changes, not with EXP and not with evolution.
    expect(stats.speed).toBe(base.baseSpeed);
  });

  it('reports progress toward the next evolution', () => {
    const { mon } = raiseToExp(charmander('a'), EXP_PER_EVOLUTION + 3);
    expect(expSinceEvolution(mon)).toBe(3);
  });
});

describe('catch-up', () => {
  it('raises a straggler in the line-up to within the gap', () => {
    const state = run([charmander('veteran', 10), charmander('rookie', 0)]);
    const { run: after } = grantWinExp(state, EXP_PER_WIN);

    const rookie = after.lineUp.find((m) => m.instanceId === 'rookie')!;
    const veteran = after.lineUp.find((m) => m.instanceId === 'veteran')!;

    expect(veteran.exp).toBe(11);
    expect(veteran.exp - rookie.exp).toBeLessThanOrEqual(CATCH_UP_EXP_GAP);
  });

  it('never lowers a mon that is already ahead', () => {
    const state = run([charmander('a', 20), charmander('b', 20)]);
    const { run: after } = grantWinExp(state, EXP_PER_WIN);
    expect(after.lineUp.every((m) => m.exp === 21)).toBe(true);
  });

  it('leaves the Box alone, because a mon that did not fight does not grow', () => {
    // Catch-up reaching storage made benching free and the party choice meaningless.
    let state = run([charmander('fighter', 10)]);
    state = { ...state, box: [charmander('benched', 0)] };

    const { run: after } = grantWinExp(state, EXP_PER_WIN);

    expect(after.box[0]!.exp).toBe(0);
  });

  it('brings a boxed mon back up on the grant after it joins the line-up', () => {
    let state = run([charmander('fighter', 10)]);
    state = { ...state, box: [charmander('benched', 0)] };
    state = promoteFromBox(state, 'benched');

    const { run: after } = grantWinExp(state, EXP_PER_WIN);
    const rejoined = after.lineUp.find((m) => m.instanceId === 'benched')!;

    expect(rejoined.exp).toBeGreaterThanOrEqual(11 - CATCH_UP_EXP_GAP);
  });

  it('computes the floor from the most-experienced mon the run owns', () => {
    let state = run([charmander('a', 3)]);
    state = { ...state, box: [charmander('b', 9)] };
    expect(catchUpFloor(state)).toBe(9 - CATCH_UP_EXP_GAP);
  });
});

describe('the roster', () => {
  it('moves a mon out of the Box into the line-up', () => {
    let state = run([charmander('a')]);
    state = addCaught(state, charmander('caught'));
    expect(state.box).toHaveLength(1);

    state = promoteFromBox(state, 'caught');
    expect(state.lineUp.map((m) => m.instanceId)).toEqual(['a', 'caught']);
    expect(state.box).toHaveLength(0);
  });

  it('refuses to overfill the line-up', () => {
    let state = run(Array.from({ length: MAX_PARTY_SIZE }, (_, i) => charmander(`m${i}`)));
    state = addCaught(state, charmander('extra'));
    const after = promoteFromBox(state, 'extra');

    expect(after.lineUp).toHaveLength(MAX_PARTY_SIZE);
    expect(after.box).toHaveLength(1);
  });

  it('refuses to empty the line-up entirely', () => {
    const state = run([charmander('only')]);
    expect(benchToBox(state, 'only')).toBe(state);
  });

  it('reorders the line-up, which is the whole of formation', () => {
    const state = run([charmander('a'), charmander('b'), charmander('c')]);
    const after = reorderLineUp(state, 'c', 0);
    expect(after.lineUp.map((m) => m.instanceId)).toEqual(['c', 'a', 'b']);
  });

  it('a caught mon goes to the Box when the line-up is full', () => {
    const full = run(Array.from({ length: MAX_PARTY_SIZE }, (_, i) => charmander(`m${i}`)));
    const after = addCaught(full, charmander('new'), true);
    expect(after.box).toHaveLength(1);
  });
});

describe('damage does not carry between fights', () => {
  it('restores everyone to full after a battle', () => {
    let state = run([charmander('a'), charmander('b')]);
    state = {
      ...state,
      lineUp: state.lineUp.map((m) => ({ ...m, currentHP: 1 })),
    };

    const after = healAll(state);
    expect(after.lineUp.every((m) => m.currentHP === null)).toBe(true);
  });

  it('heals the Box too, so a mon swapped in is never carrying old damage', () => {
    let state = run([charmander('a')]);
    state = { ...state, box: [{ ...charmander('b'), currentHP: 2 }] };
    expect(healAll(state).box[0]!.currentHP).toBeNull();
  });
});

describe('morale is the loss condition', () => {
  it('starts at the full amount and ends the run at zero', () => {
    let state = run([charmander('a')]);
    expect(state.morale).toBe(STARTING_MORALE);
    expect(isRunOver(state)).toBe(false);

    for (let i = 0; i < STARTING_MORALE; i++) state = spendMorale(state);
    expect(isRunOver(state)).toBe(true);
  });

  it('never goes below zero', () => {
    const state = spendMorale(run([charmander('a')]), 99);
    expect(state.morale).toBe(0);
  });
});

describe('progression scaling', () => {
  it('pitches each Location forward by exactly what one is worth', () => {
    // The two have to climb at the same rate or the run drifts out of balance.
    expect(baselineExp(1) - baselineExp(0)).toBe(EXP_PER_BADGE);
    expect(baselineExp(3) - baselineExp(2)).toBe(EXP_PER_BADGE);
  });

  it('opens wild encounters below the baseline and lets them catch up deeper in', () => {
    expect(wildExp(1, 1)).toBeLessThan(baselineExp(1));
    expect(wildExp(1, 5)).toBeGreaterThan(wildExp(1, 1));
  });

  it('puts the Gym above its own Location baseline', () => {
    expect(gymExp(2)).toBeGreaterThan(baselineExp(2));
  });

  it('grows encounters from one mon to three across a run', () => {
    expect(wildEncounterSize(0)).toBe(1);
    expect(wildEncounterSize(2)).toBe(2);
    expect(wildEncounterSize(5)).toBe(3);
  });

  it('never fields a Gym team smaller than the player brings', () => {
    for (const badges of [0, 3, 7]) {
      for (const size of [1, 3, 6]) {
        expect(gymTeamSize(badges, size)).toBeGreaterThanOrEqual(Math.min(size, MAX_PARTY_SIZE));
      }
    }
  });

  it('raises the tier cap per badge and lifts it for the last Location', () => {
    expect(maxTier(0)).toBe(1);
    expect(maxTier(3)).toBe(4);
    expect(maxTier(BADGES_TO_WIN - 1)).toBeNull();
  });
});

describe('encounters', () => {
  it('draws only base forms, so every opponent starts at the front of its chain', () => {
    for (const species of encounterPool(3)) {
      expect(species.evolutionStage, species.name).toBe(0);
    }
  });

  it('keeps legendaries out', () => {
    expect(encounterPool(6).some((s) => s.isLegendary)).toBe(false);
  });

  it('respects the tier cap', () => {
    for (const species of encounterPool(0)) {
      expect(species.tier).toBeLessThanOrEqual(1);
    }
  });

  it('never generates an empty pool, however tight the cap', () => {
    for (const badges of [0, 1, 4, 7]) {
      expect(encounterPool(badges).length).toBeGreaterThan(0);
    }
  });

  it('is deterministic for a seed, so a node previews as what it will be', () => {
    const node = FIRST_LOCATION.nodes[0]!;
    const a = generateWildEncounter(42, 0, node);
    const b = generateWildEncounter(42, 0, node);

    expect(a.map((m) => m.speciesId)).toEqual(b.map((m) => m.speciesId));
  });

  it('gives different nodes different encounters', () => {
    const first = generateWildEncounter(42, 0, FIRST_LOCATION.nodes[0]!);
    const later = generateWildEncounter(42, 0, FIRST_LOCATION.nodes[3]!);
    expect(first[0]!.instanceId).not.toBe(later[0]!.instanceId);
  });

  it('builds a Gym team on its theme where the pool allows', () => {
    const theme = gymThemeFor(0);
    const team = generateGymTeam(1, 0, 2, theme);

    expect(team.length).toBeGreaterThanOrEqual(2);
    const onTheme = team.filter((m) =>
      speciesOf(m.speciesId)!.types.some((t) => theme.includes(t)),
    );
    expect(onTheme.length).toBe(team.length);
  });

  it('fields nobody at a Center', () => {
    const center = FIRST_LOCATION.nodes.find((n) => n.type === 'Center')!;
    expect(opponentsFor(1, 0, center, 2)).toEqual([]);
  });

  it('scales enemies with progress and never with the player', () => {
    const node = FIRST_LOCATION.nodes[0]!;
    const early = generateWildEncounter(1, 0, node);
    const late = generateWildEncounter(1, 4, node);

    expect(late[0]!.exp).toBeGreaterThan(early[0]!.exp);
  });
});

describe('the Location', () => {
  it('runs wild encounters through a Center to a Gym', () => {
    const types = FIRST_LOCATION.nodes.map((n) => n.type);
    expect(types[0]).toBe('Wild');
    expect(types).toContain('Center');
    expect(types.at(-1)).toBe('Gym');
  });

  it('numbers its layers in order, since encounter scaling reads them', () => {
    const layers = FIRST_LOCATION.nodes.map((n) => n.layer);
    expect(layers).toEqual([...layers].sort((a, b) => a - b));
  });

  it('opens with a pair, so the Lead and Support rules are visible from the first fight', () => {
    const starters = defaultStarters();
    expect(starters).toHaveLength(2);
    expect(new Set(starters.map((m) => m.instanceId)).size).toBe(2);
  });
});

describe('who a node fields', () => {
  const wildNode = (layer: number, id: string): MapNode => ({ id, type: 'Wild', layer, label: '' });

  it('gives two nodes on the same layer different opposition', () => {
    // They were seeded from position alone, so every node on a layer fielded the same team — all
    // three entry nodes the same Eevee. Nothing about the game looked random after that.
    const teamAt = (id: string) =>
      generateWildEncounter(1, 2, wildNode(2, id)).map((m) => m.speciesId).join();

    const siblings = ['L0-1-0', 'L0-1-1', 'L0-1-2', 'L0-1-3'].map(teamAt);
    expect(new Set(siblings).size).toBe(siblings.length);
  });

  it('is still the same fight every time that one node is entered', () => {
    // The other half: stable per node, so re-entering after a loss is the same fight rather than
    // a re-roll, and previewing a node costs nothing.
    const node = wildNode(3, 'L0-2-1');
    expect(generateWildEncounter(9, 1, node).map((m) => m.speciesId)).toEqual(
      generateWildEncounter(9, 1, node).map((m) => m.speciesId),
    );
  });

  it('gives different runs different opposition at the same node', () => {
    const node = wildNode(2, 'L0-1-0');
    const runs = [1, 2, 3, 4, 5, 6].map((seed) =>
      generateWildEncounter(seed, 1, node).map((m) => m.speciesId).join(),
    );
    expect(new Set(runs).size).toBeGreaterThan(1);
  });

  it('does not field the same species twice while the pool has others', () => {
    // Drawing with replacement put the same mon up two or three times often enough to read as a
    // bug. A team may still repeat once the pool is smaller than the team, which is the only case
    // where the alternative is no encounter at all.
    for (let badges = 3; badges < BADGES_TO_WIN; badges++) {
      const size = wildEncounterSize(badges);
      const pool = encounterPool(badges);
      if (pool.length < size) continue;

      for (let i = 0; i < 25; i++) {
        const team = generateWildEncounter(i + 1, badges, wildNode(3, `n-${i}`));
        const species = new Set(team.map((m) => m.speciesId));
        expect(species.size, `badges ${badges}, seed ${i + 1}`).toBe(team.length);
      }
    }
  });

  it('gives a Gym Leader a line-up rather than one species repeated', () => {
    for (let badges = 0; badges < BADGES_TO_WIN; badges++) {
      const team = generateGymTeam(5, badges, 4, locationFor(badges).gymTheme);
      const pool = encounterPool(badges, locationFor(badges).gymTheme);
      if (pool.length < team.length) continue;
      expect(new Set(team.map((m) => m.speciesId)).size, `badge ${badges}`).toBe(team.length);
    }
  });
});

describe('the tier curve outpaces EXP, which is what catching is for', () => {
  it('a Location is worth exactly the EXP the next one is pitched forward by', () => {
    // These two have to climb together or the player falls behind by design.
    const winsPerLocation = FIRST_LOCATION.nodes.filter((n) => n.type !== 'Center').length;
    expect(winsPerLocation * EXP_PER_WIN).toBe(EXP_PER_BADGE);
  });

  it('but the tier cap more than doubles the opposition budget between Locations', () => {
    // Tier 1 spends 8 points across its three stats, tier 2 spends 18. A starter gains
    // EXP_PER_BADGE points over the same stretch, so a tier-1 team cannot keep pace on EXP alone.
    // Recorded as a fact about the design, not a bug: catching higher-tier mons is the intended
    // answer, and until Stage 5 lands a run is not winnable.
    const tier1Budget = 8;
    const tier2Budget = 18;

    expect(tier2Budget - tier1Budget).toBeGreaterThan(EXP_PER_BADGE);
    expect(maxTier(0)).toBe(1);
    expect(maxTier(1)).toBe(2);
  });

  it('puts something in the second Location a fully-grown starter cannot match', () => {
    // The claim is about the *pool*, not about one draw. It used to be checked against a single
    // seeded encounter, which passed only because that seed happened to draw the one tier-2
    // species in the Location's themed pool — the other twelve are tier 1, and the assertion
    // flipped the moment the seeding changed. What is actually true, and is what catching exists
    // to answer, is that the tier cap puts something out of the starter's reach into the pool.
    const mine = statsOf(raiseToExp(defaultStarters()[0]!, EXP_PER_BADGE).mon);
    const budget = (s: { attack: number; health: number }): number => s.attack + s.health;

    const pool = encounterPool(1, locationFor(1).typeBias).map((species) =>
      budget(statsOf(createInstance(species, { exp: wildExp(1, 2), instanceId: 'probe' }))),
    );

    expect(Math.max(...pool)).toBeGreaterThan(budget(mine));
  });

  it('but most of what that Location fields is still beatable, or the run would be over', () => {
    // The other half of the same fact, and the reason a run is playable at all: the tier cap makes
    // a stronger mon *available*, it does not make every encounter one.
    const mine = statsOf(raiseToExp(defaultStarters()[0]!, EXP_PER_BADGE).mon);
    const budget = (s: { attack: number; health: number }): number => s.attack + s.health;

    const pool = encounterPool(1, locationFor(1).typeBias).map((species) =>
      budget(statsOf(createInstance(species, { exp: wildExp(1, 2), instanceId: 'probe' }))),
    );
    const beatable = pool.filter((b) => b <= budget(mine)).length;

    expect(beatable).toBeGreaterThan(pool.length / 2);
  });
});
