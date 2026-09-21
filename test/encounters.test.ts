/**
 * Road encounters: the catalogue, and what each effect does to a run.
 *
 * The catalogue is checked by reading it — that is the point of declaring effects rather than
 * executing them — and the effects are applied against real runs.
 */

import { describe, expect, it } from 'vitest';

import {
  ENCOUNTERS,
  eligibleEncounters,
  encounterFor,
  type EncounterEffect,
} from '../src/meta/encountersEvents.js';
import { applyEncounterEffect } from '../src/meta/encounterEffects.js';
import { LOCATIONS, REGION_ART, locationFor } from '../src/meta/locations.js';
import { createRun, type RunState } from '../src/meta/runState.js';
import { createInstance, statsOf } from '../src/content/factory.js';
import { opponentsFor } from '../src/meta/encounters.js';
import { moneyForWin } from '../src/meta/progression.js';
import { speciesOf } from '../src/content/index.js';
import { MAX_PARTY_SIZE } from '../src/meta/progression.js';
import { totalBalls } from '../src/meta/balls.js';
import { createRandom } from '../src/sim/index.js';
import { generateLocationMap } from '../src/meta/mapGenerator.js';

const forest = locationFor(0);
const runWith = (mons: string[]): RunState =>
  createRun(1, mons.map((n, i) => createInstance(n, { instanceId: `m${i}` })));

const apply = (run: RunState, effect: EncounterEffect, seed = 1) =>
  applyEncounterEffect(run, effect, forest, createRandom(seed));

describe('the encounter catalogue', () => {
  it('gives every encounter a title, a body and at least two choices', () => {
    for (const e of ENCOUNTERS) {
      expect(e.title.length, e.id).toBeGreaterThan(0);
      expect(e.body.length, e.id).toBeGreaterThan(0);
      expect(e.choices.length, e.id).toBeGreaterThanOrEqual(2);
    }
  });

  it('tells the player what every choice does before they take it', () => {
    // An encounter should never be a rug pull: where an outcome can go badly, the choice says so.
    for (const e of ENCOUNTERS) {
      for (const c of e.choices) {
        expect(c.label.length, `${e.id}/${c.label}`).toBeGreaterThan(0);
        expect(c.detail.length, `${e.id}/${c.label}`).toBeGreaterThan(0);
      }
    }
  });

  it('is mostly positive', () => {
    // The purpose is to vary a Location and offer a decision that isn't "which fight", not to
    // punish curiosity. Count outcomes that cost the player something outright.
    const costly = ENCOUNTERS.flatMap((e) => e.choices).filter(
      (c) => c.effect.kind === 'money' && c.effect.amount < 0,
    );
    const total = ENCOUNTERS.flatMap((e) => e.choices).length;
    expect(costly.length / total).toBeLessThan(0.3);
  });

  it('uses unique ids', () => {
    expect(new Set(ENCOUNTERS.map((e) => e.id)).size).toBe(ENCOUNTERS.length);
  });

  it('holds the legendary back until a run is under way', () => {
    const legendary = ENCOUNTERS.find((e) =>
      e.choices.some((c) => c.effect.kind === 'legendary'),
    )!;
    expect(legendary.minBadges).toBeGreaterThan(0);
    expect(eligibleEncounters(0, forest.typeBias).map((e) => e.id)).not.toContain(legendary.id);
    expect(eligibleEncounters(6, forest.typeBias).map((e) => e.id)).toContain(legendary.id);
  });

  it('offers a type-flavoured encounter only where it fits', () => {
    const water = ENCOUNTERS.find((e) => e.typeAffinity?.includes('Water'))!;
    expect(eligibleEncounters(8, ['Fire', 'Ground']).map((e) => e.id)).not.toContain(water.id);
    expect(eligibleEncounters(8, ['Water']).map((e) => e.id)).toContain(water.id);
  });

  it('always has something to offer, at every badge count and bias', () => {
    for (let badges = 0; badges < 8; badges++) {
      for (const loc of LOCATIONS) {
        expect(eligibleEncounters(badges, loc.typeBias).length, loc.name).toBeGreaterThan(0);
      }
    }
  });

  it('shows the same encounter every time a node is looked at', () => {
    // One that re-rolled when you changed your mind would make the choice meaningless.
    const a = encounterFor(42, 2, 'L2-1-0', forest.typeBias);
    const b = encounterFor(42, 2, 'L2-1-0', forest.typeBias);
    expect(a.id).toBe(b.id);
  });

  it('shows different nodes different encounters', () => {
    const ids = new Set(
      ['a', 'b', 'c', 'd', 'e', 'f'].map((n) => encounterFor(7, 4, n, forest.typeBias).id),
    );
    expect(ids.size).toBeGreaterThan(1);
  });
});

describe('what the effects do', () => {
  it('gives money, and takes it without pushing a run into debt', () => {
    const run = runWith(['Charmander']);
    expect(apply(run, { kind: 'money', amount: 8 }).run.money).toBe(run.money + 8);
    expect(apply(run, { kind: 'money', amount: -999 }).run.money).toBe(0);
  });

  it('adds balls to the bag', () => {
    const run = runWith(['Charmander']);
    const after = apply(run, { kind: 'balls', tier: 'Great', count: 2 }).run;
    expect(after.balls.Great).toBe(run.balls.Great + 2);
  });

  it('grants EXP that arrives unspent', () => {
    const run = runWith(['Charmander', 'Squirtle']);
    const after = apply(run, { kind: 'exp', points: 3 }).run;

    for (const mon of after.lineUp) expect(mon.exp).toBe(3);
    // Stats unchanged until the player spends the points.
    expect(statsOf(after.lineUp[0]!)).toEqual(statsOf(run.lineUp[0]!));
  });

  it('puts a gift in the line-up when there is room, and the Box when there is not', () => {
    const room = apply(runWith(['Charmander']), { kind: 'gift', tierOffset: 0 }).run;
    expect(room.lineUp).toHaveLength(2);

    const full = runWith(Array.from({ length: MAX_PARTY_SIZE }, () => 'Charmander'));
    const after = apply(full, { kind: 'gift', tierOffset: 0 }).run;
    expect(after.lineUp).toHaveLength(MAX_PARTY_SIZE);
    expect(after.box).toHaveLength(1);
  });

  it('trades away the weakest line-up mon, keeping the line-up the same size', () => {
    const run = runWith(['Charmander', 'Squirtle', 'Pidgey']);
    const after = apply(run, { kind: 'trade', tierOffset: 1 }).run;

    expect(after.lineUp).toHaveLength(3);
    const ids = after.lineUp.map((m) => m.instanceId);
    expect(ids.filter((id) => id.startsWith('trade-'))).toHaveLength(1);
  });

  it('refuses a trade that would strip the last mon', () => {
    const run = runWith(['Charmander']);
    const result = apply(run, { kind: 'trade', tierOffset: 1 });

    expect(result.run.lineUp).toHaveLength(1);
    expect(result.text).toMatch(/nothing to spare/i);
  });

  it('hands back a legendary to fight rather than resolving it in place', () => {
    const run = runWith(['Charmander']);
    const result = apply(run, { kind: 'legendary' });

    expect(result.legendary).not.toBeNull();
    expect(result.legendary).toHaveLength(1);
    expect(result.run).toBe(run);
  });

  it('resolves a gamble to one branch or the other, and says which', () => {
    const run = runWith(['Charmander']);
    const effect: EncounterEffect = {
      kind: 'gamble',
      winChance: 1,
      onWin: { kind: 'money', amount: 10 },
      onLose: { kind: 'nothing' },
    };
    const won = apply(run, effect);
    expect(won.run.money).toBe(run.money + 10);
    expect(won.text).toMatch(/pays off/i);

    const lost = apply(run, { ...effect, winChance: 0 });
    expect(lost.run.money).toBe(run.money);
    expect(lost.text).toMatch(/does not/i);
  });

  it('leaves the run alone when the choice was to walk on', () => {
    const run = runWith(['Charmander']);
    const after = apply(run, { kind: 'nothing' }).run;
    expect(after).toEqual(run);
  });

  it('returns a line of text for every effect kind', () => {
    const run = runWith(['Charmander', 'Squirtle']);
    const effects: EncounterEffect[] = [
      { kind: 'money', amount: 5 },
      { kind: 'balls', tier: 'Poke', count: 1 },
      { kind: 'exp', points: 1 },
      { kind: 'gift', tierOffset: 0 },
      { kind: 'trade', tierOffset: 0 },
      { kind: 'legendary' },
      { kind: 'nothing' },
      { kind: 'gamble', winChance: 0.5, onWin: { kind: 'nothing' }, onLose: { kind: 'nothing' } },
    ];
    for (const effect of effects) {
      expect(apply(run, effect).text.length, effect.kind).toBeGreaterThan(0);
    }
  });

  it('never leaves the total ball count negative', () => {
    const run = runWith(['Charmander']);
    expect(totalBalls(apply(run, { kind: 'balls', tier: 'Poke', count: 1 }).run.balls))
      .toBeGreaterThan(0);
  });
});

describe('Locations and their art', () => {
  it('gives every Location a region backdrop', () => {
    for (const loc of LOCATIONS) {
      expect(REGION_ART, loc.name).toContain(loc.region);
    }
  });

  it('uses each backdrop once, so no two Locations look the same', () => {
    const regions = LOCATIONS.map((l) => l.region);
    expect(new Set(regions).size).toBe(regions.length);
  });
});

describe('Encounter nodes on the map', () => {
  it('appear in generated maps', () => {
    let found = 0;
    for (let seed = 1; seed <= 40; seed++) {
      if (generateLocationMap(seed, 0).nodes.some((n) => n.type === 'Encounter')) found++;
    }
    expect(found).toBeGreaterThan(10);
  });

  it('never stack two in one layer, which would make the layer a non-choice', () => {
    for (let seed = 1; seed <= 40; seed++) {
      const perLayer = new Map<number, number>();
      for (const node of generateLocationMap(seed, seed % 8).nodes) {
        if (node.type === 'Encounter') {
          perLayer.set(node.layer, (perLayer.get(node.layer) ?? 0) + 1);
        }
      }
      for (const [layer, count] of perLayer) {
        expect(count, `seed ${seed} layer ${layer}`).toBeLessThanOrEqual(1);
      }
    }
  });

  it('never replace the Gym or an entry node', () => {
    for (let seed = 1; seed <= 40; seed++) {
      const map = generateLocationMap(seed, 0);
      expect(map.nodes.find((n) => n.id === map.gymId)!.type).toBe('Gym');
      for (const id of map.entryIds) {
        expect(map.nodes.find((n) => n.id === id)!.type).toBe('Wild');
      }
    }
  });
});

describe('mystery trainers', () => {
  it('field a team', () => {
    const node = { id: 'n', type: 'MysteryTrainer' as const, layer: 3, label: 'x' };
    expect(opponentsFor(1, 2, node, 3).length).toBeGreaterThanOrEqual(2);
  });

  it('are untyped, since another player would not follow the local theme', () => {
    const node = { id: 'n', type: 'MysteryTrainer' as const, layer: 3, label: 'x' };
    const volcano = LOCATIONS.find((l) => l.region === 'volcano')!;
    const team = opponentsFor(1, 6, node, 3, volcano);

    // A Gym in the same place is on-theme; a trainer is not.
    const onTheme = team.filter((m) =>
      speciesOf(m.speciesId)!.types.some((t) => volcano.gymTheme.includes(t)),
    );
    expect(onTheme.length).toBeLessThan(team.length);
  });

  it('pay better than a wild fight, because money is the only reward on offer', () => {
    // Their Pokémon belong to someone, so they cannot be caught.
    expect(moneyForWin('MysteryTrainer')).toBeGreaterThan(moneyForWin('Wild'));
  });

  it('are deterministic for a node', () => {
    const node = { id: 'n', type: 'MysteryTrainer' as const, layer: 2, label: 'x' };
    const a = opponentsFor(9, 1, node, 3).map((m) => m.speciesId);
    const b = opponentsFor(9, 1, node, 3).map((m) => m.speciesId);
    expect(a).toEqual(b);
  });
});
