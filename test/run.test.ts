/**
 * The run layer: EXP, evolution, catch-up, roster moves, encounter scaling, and the loop's own
 * rules.
 *
 * All of it is pure, so none of these tests need a store or a component.
 */

import { describe, expect, it } from 'vitest';

import {
  createInstance,
  statsOf,
  unspentPoints,
  type PokemonInstance,
} from '../src/content/factory.js';
import { HEALTH_MULTIPLIER } from '../src/content/statGrowth.js';
import { speciesNamed, speciesOf } from '../src/content/index.js';
import {
  CATCH_UP_EXP_GAP,
  EXP_PER_COMBINE,
  EXP_PER_EVOLUTION,
  STAT_POINTS_PER_EVOLUTION,
  catchUpFloor,
  combineMons,
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
  ADOPTION_SLOTS,
  REROLL_COST,
  adoptionCost,
  canReroll,
} from '../src/meta/shop.js';
import {
  addCaught,
  benchToBox,
  canCombine,
  createRun,
  duplicatesOf,
  healAll,
  isRunOver,
  promoteFromBox,
  reorderLineUp,
  spendMorale,
  swapMons,
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
    const spent = {
      ...mon,
      allocation: { attack: EXP_PER_EVOLUTION, health: 0, special: 0, speed: 0 },
    };
    const stats = statsOf(spent);
    const base = speciesNamed('Charmander')!;

    expect(stats.attack).toBe(base.baseAttack + EXP_PER_EVOLUTION + 3);
    expect(stats.health / HEALTH_MULTIPLIER).toBe(base.baseHealth + 3);
  });

  it('hands out stat points on evolving, not on every point of EXP', () => {
    // EXP is evolution progress only now; evolving is what gives the player something to assign.
    const short = raiseToExp(charmander('a'), EXP_PER_EVOLUTION - 1).mon;
    expect(unspentPoints(short)).toBe(0);
    expect(statsOf(short)).toEqual(statsOf(charmander('a')));

    const evolved = raiseToExp(charmander('b'), EXP_PER_EVOLUTION).mon;
    expect(unspentPoints(evolved)).toBe(STAT_POINTS_PER_EVOLUTION);
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

  it('a starter that won every fight in the first Location is still behind a second-Location wild', () => {
    const starter = defaultStarters()[0]!;
    const afterLocationOne = raiseToExp(starter, EXP_PER_BADGE).mon;
    const mine = statsOf(afterLocationOne);

    const node = FIRST_LOCATION.nodes[0]!;
    const theirs = generateWildEncounter(1, 1, node).map((m) => statsOf(m));
    const strongest = Math.max(...theirs.map((s) => s.attack + s.health));

    expect(mine.attack + mine.health).toBeLessThan(strongest);
  });
});

describe('EXP goes only to the mons that fought', () => {
  it('pays the participants and skips the back of the train', () => {
    // A dormant mon that never reached Lead or Support did not fight and should not be paid.
    const state = run([charmander('lead'), charmander('support'), charmander('dormant')]);
    const { run: after } = grantWinExp(state, 1, ['lead', 'support']);

    const exp = Object.fromEntries(after.lineUp.map((m) => [m.instanceId, m.exp]));
    expect(exp.lead).toBe(1);
    expect(exp.support).toBe(1);
    expect(exp.dormant).toBe(0);
  });

  it('pays everyone when no participant list is given', () => {
    // Which is right for an encounter that trains the whole team.
    const state = run([charmander('a'), charmander('b')]);
    const { run: after } = grantWinExp(state, 2);
    expect(after.lineUp.every((m) => m.exp === 2)).toBe(true);
  });

  it('still catches up a straggler that did fight', () => {
    const state = run([charmander('veteran', 10), charmander('rookie', 0), charmander('bench')]);
    const { run: after } = grantWinExp(state, 1, ['veteran', 'rookie']);

    const rookie = after.lineUp.find((m) => m.instanceId === 'rookie')!;
    const bench = after.lineUp.find((m) => m.instanceId === 'bench')!;

    expect(rookie.exp).toBeGreaterThan(0);
    expect(bench.exp).toBe(0);
  });
});

describe('adoption at a Center', () => {
  it('charges more for a higher tier', () => {
    expect(adoptionCost(3)).toBeGreaterThan(adoptionCost(1));
  });

  it('prices a reroll well below a Pokémon', () => {
    // A reroll is meant to be a nudge, not a purchase in itself.
    expect(REROLL_COST).toBeLessThan(adoptionCost(1));
  });

  it('only allows a reroll that can be paid for', () => {
    expect(canReroll(REROLL_COST)).toBe(true);
    expect(canReroll(REROLL_COST - 1)).toBe(false);
  });

  it('offers five slots', () => {
    expect(ADOPTION_SLOTS).toBe(5);
  });
});

describe('combining Pokémon in the same evolution line', () => {
  it("adds a flat amount of EXP, not the sacrifice's own progress", () => {
    const a = charmander('a', 5);
    const b = charmander('b', 40);
    const after = combineMons(run([a, b]), 'a', 'b');

    const kept = after.lineUp.find((m) => m.instanceId === 'a')!;
    expect(kept.exp).toBe(5 + EXP_PER_COMBINE);
  });

  it('takes three sacrifices to evolve a fresh mon — four mons into one', () => {
    // The headline case: four Charmanders combine into a Charmeleon.
    let state = run([
      charmander('keep'),
      charmander('b'),
      charmander('c'),
      charmander('d'),
    ]);
    for (const id of ['b', 'c', 'd']) state = combineMons(state, 'keep', id);

    const kept = state.lineUp.find((m) => m.instanceId === 'keep')!;
    expect(kept.exp).toBe(EXP_PER_COMBINE * 3);
    expect(speciesOf(kept.speciesId)?.name).toBe('Charmeleon');
    expect(unspentPoints(kept)).toBe(STAT_POINTS_PER_EVOLUTION);
  });

  it('does not evolve the keeper on a single combine', () => {
    const after = combineMons(run([charmander('a'), charmander('b')]), 'a', 'b');
    const kept = after.lineUp.find((m) => m.instanceId === 'a')!;

    expect(kept.exp).toBe(EXP_PER_COMBINE);
    expect(speciesOf(kept.speciesId)?.name).toBe('Charmander');
    expect(unspentPoints(kept)).toBe(0);
  });

  it('combines across stages of the same line', () => {
    const charmeleon = createInstance('Charmeleon', { instanceId: 'evolved', timesEvolved: 1 });
    const state = { ...run([charmander('a', 3)]), box: [charmeleon] };

    const after = combineMons(state, 'a', 'evolved');
    expect(after.box).toHaveLength(0);
    expect(after.lineUp[0]!.exp).toBe(3 + EXP_PER_COMBINE);
    expect(speciesOf(after.lineUp[0]!.speciesId)?.name).toBe('Charmander');
  });

  it('removes the consumed mon from wherever it was, line-up or Box', () => {
    const state = { ...run([charmander('a')]), box: [charmander('b')] };
    const after = combineMons(state, 'a', 'b');
    expect(after.lineUp.map((m) => m.instanceId)).toEqual(['a']);
    expect(after.box).toHaveLength(0);
  });

  it('keeps each mon in its own group — combining does not relocate the survivor', () => {
    const state = { ...run([charmander('a')]), box: [charmander('b')] };
    const after = combineMons(state, 'b', 'a');
    expect(after.box.map((m) => m.instanceId)).toEqual(['b']);
    expect(after.lineUp).toHaveLength(0);
  });

  it('refuses to combine two different evolution lines', () => {
    const state = run([charmander('a'), createInstance('Squirtle', { instanceId: 'b' })]);
    expect(combineMons(state, 'a', 'b')).toBe(state);
  });

  it('refuses a mon combined with itself', () => {
    const state = run([charmander('a')]);
    expect(combineMons(state, 'a', 'a')).toBe(state);
  });

  it('leaves the run untouched if either id does not exist', () => {
    const state = run([charmander('a')]);
    expect(combineMons(state, 'a', 'ghost')).toBe(state);
    expect(combineMons(state, 'ghost', 'a')).toBe(state);
  });

  it('finds duplicates across both the line-up and the Box, and across stages', () => {
    const charmeleon = createInstance('Charmeleon', { instanceId: 'evolved', timesEvolved: 1 });
    const state = {
      ...run([charmander('a'), createInstance('Squirtle', { instanceId: 'x' })]),
      box: [charmander('b'), charmeleon],
    };
    expect(duplicatesOf(state, 'a').map((m) => m.instanceId).sort()).toEqual(['b', 'evolved']);
  });

  it('does not treat a different line, or the mon itself, as a duplicate', () => {
    const state = run([charmander('a'), createInstance('Squirtle', { instanceId: 'x' })]);
    expect(duplicatesOf(state, 'a')).toEqual([]);
  });

  it('reports combinability the same way duplicatesOf does', () => {
    const state = { ...run([charmander('a')]), box: [charmander('b')] };
    expect(canCombine(state, 'a', 'b')).toBe(true);
    expect(canCombine(state, 'a', 'a')).toBe(false);
  });
});

describe('swapping two mons', () => {
  it('swaps two positions within the line-up', () => {
    const state = run([charmander('a'), charmander('b'), charmander('c')]);
    const after = swapMons(state, 'a', 'c');
    expect(after.lineUp.map((m) => m.instanceId)).toEqual(['c', 'b', 'a']);
  });

  it('swaps two positions within the Box', () => {
    const state = { ...run([charmander('lead')]), box: [charmander('x'), charmander('y')] };
    const after = swapMons(state, 'x', 'y');
    expect(after.box.map((m) => m.instanceId)).toEqual(['y', 'x']);
  });

  it('swaps across the line-up and the Box, each landing in the other\'s exact slot', () => {
    const state = {
      ...run([charmander('lead'), charmander('support')]),
      box: [charmander('boxed')],
    };
    const after = swapMons(state, 'support', 'boxed');

    expect(after.lineUp.map((m) => m.instanceId)).toEqual(['lead', 'boxed']);
    expect(after.box.map((m) => m.instanceId)).toEqual(['support']);
  });

  it('does not change how many mons are in each group', () => {
    const state = {
      ...run([charmander('lead'), charmander('support')]),
      box: [charmander('boxed')],
    };
    const after = swapMons(state, 'lead', 'boxed');
    expect(after.lineUp).toHaveLength(2);
    expect(after.box).toHaveLength(1);
  });

  it('does nothing for a mon swapped with itself', () => {
    const state = run([charmander('a')]);
    expect(swapMons(state, 'a', 'a')).toBe(state);
  });

  it('does nothing if either id is unknown', () => {
    const state = run([charmander('a')]);
    expect(swapMons(state, 'a', 'ghost')).toBe(state);
  });
});
