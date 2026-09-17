/**
 * Fusion: what may be combined, what survives, and what comes out.
 *
 * All of it is pure, so none of these tests need a store or a component — the team screens only
 * decide *when* a fusion happens, never what it is worth.
 */

import { describe, expect, it } from 'vitest';

import { createInstance, statsOf } from '../src/content/factory.js';
import { speciesNamed } from '../src/content/index.js';
import {
  FUSION_EXP_BONUS,
  FUSION_STAT_BONUS,
  canCombine,
  combine,
  familyOf,
  partnersFor,
} from '../src/meta/fusion.js';
import { EXP_PER_EVOLUTION } from '../src/meta/experience.js';
import { combineMons, createRun } from '../src/meta/runState.js';

const charmander = (id: string, exp = 0) => createInstance('Charmander', { instanceId: id, exp });
const charmeleon = (id: string, exp = 0) =>
  createInstance('Charmeleon', { instanceId: id, exp, timesEvolved: 1 });

describe('what may be combined', () => {
  it('pairs a mon with its own species', () => {
    expect(canCombine(charmander('a'), charmander('b'))).toBe(true);
  });

  it('pairs a mon with an evolved form of itself, which is the whole point', () => {
    expect(canCombine(charmander('a'), charmeleon('b'))).toBe(true);
    expect(
      canCombine(charmeleon('a'), createInstance('Charizard', { instanceId: 'b', timesEvolved: 2 })),
    ).toBe(true);
  });

  it('refuses two mons that merely share an elemental type', () => {
    const growlithe = speciesNamed('Growlithe');
    if (growlithe === null) return; // roster is curated; skip if this one isn't in it
    expect(growlithe.types).toContain('Fire');
    expect(canCombine(charmander('a'), createInstance(growlithe, { instanceId: 'b' }))).toBe(false);
  });

  it('refuses a mon and itself', () => {
    const mon = charmander('a');
    expect(canCombine(mon, mon)).toBe(false);
  });

  it('matches on the base form, whichever stage each mon is at', () => {
    expect(familyOf(charmeleon('a')).name).toBe('Charmander');
    expect(familyOf(charmander('b')).name).toBe('Charmander');
  });

  it('lists only the partners in a pool', () => {
    const pool = [charmander('a'), charmeleon('b'), createInstance('Squirtle', { instanceId: 'c' })];
    expect(partnersFor(pool[0]!, pool).map((m) => m.instanceId)).toEqual(['b']);
  });
});

describe('what comes out', () => {
  it('keeps the highest evolution', () => {
    const fusion = combine(charmander('low'), charmeleon('high'));
    expect(fusion?.mon.instanceId).toBe('high');
    expect(fusion?.absorbedId).toBe('low');
    expect(fusion?.mon.speciesId).toBe(speciesNamed('Charmeleon')?.id);
  });

  it('keeps the highest evolution whichever way round it is asked', () => {
    expect(combine(charmeleon('high'), charmander('low'))?.mon.instanceId).toBe('high');
  });

  it('takes the higher Attack and Health of the two, plus one', () => {
    const a = charmander('a', 4);
    const b = charmander('b', 1);
    const [before, other] = [statsOf(a), statsOf(b)];

    const fused = combine(a, b)!;
    const after = statsOf(fused.mon);

    expect(after.attack).toBe(Math.max(before.attack, other.attack) + FUSION_STAT_BONUS);
    expect(after.health).toBe(Math.max(before.health, other.health) + FUSION_STAT_BONUS);
  });

  it('is never a downgrade on either stat', () => {
    const a = charmander('a', 6);
    const b = charmander('b', 6);
    const best = {
      attack: Math.max(statsOf(a).attack, statsOf(b).attack),
      health: Math.max(statsOf(a).health, statsOf(b).health),
    };
    const after = statsOf(combine(a, b)!.mon);

    expect(after.attack).toBeGreaterThan(best.attack - 1);
    expect(after.health).toBeGreaterThan(best.health - 1);
  });

  it('banks a point of EXP, taken from whichever mon had more', () => {
    const fusion = combine(charmander('a', 2), charmander('b', 5))!;
    expect(fusion.mon.exp).toBe(5 + FUSION_EXP_BONUS);
  });

  it('evolves the survivor when that point of EXP is the one that tips it', () => {
    const fusion = combine(
      charmander('a', EXP_PER_EVOLUTION - 1),
      charmander('b', EXP_PER_EVOLUTION - 1),
    )!;

    expect(fusion.evolutions.map((e) => [e.from.name, e.to.name])).toEqual([
      ['Charmander', 'Charmeleon'],
    ]);
    expect(fusion.mon.speciesId).toBe(speciesNamed('Charmeleon')?.id);
    expect(fusion.mon.timesEvolved).toBe(1);
  });

  it('counts how many mons have gone into it, so a fused mon can say so', () => {
    const once = combine(charmander('a'), charmander('b'))!;
    const twice = combine(once.mon, charmander('c'))!;
    expect(once.mon.timesFused).toBe(1);
    expect(twice.mon.timesFused).toBe(2);
  });

  it('compounds, because a fused mon is just a mon with a bonus', () => {
    const once = combine(charmander('a', 3), charmander('b', 3))!;
    const twice = combine(once.mon, charmander('c', 3))!;

    expect(statsOf(twice.mon).attack).toBeGreaterThan(statsOf(once.mon).attack);
    expect(statsOf(twice.mon).health).toBeGreaterThan(statsOf(once.mon).health);
  });

  it('keeps growing normally afterwards — the bonus is added, not a replacement', () => {
    const fused = combine(charmander('a', 2), charmander('b', 2))!.mon;
    const later = { ...fused, exp: fused.exp + 4 };

    const before = statsOf(fused);
    const after = statsOf(later);
    expect(after.attack + after.health).toBe(before.attack + before.health + 4);
  });

  it('leaves Speed alone, the way EXP and evolution do', () => {
    const fused = combine(charmander('a', 3), charmander('b', 3))!.mon;
    expect(statsOf(fused).speed).toBe(statsOf(charmander('a', 3)).speed);
  });

  it('refuses two mons of different families', () => {
    expect(combine(charmander('a'), createInstance('Squirtle', { instanceId: 'b' }))).toBeNull();
  });
});

describe('combining inside a run', () => {
  const runWith = (lineUp = [charmander('a'), charmeleon('b')], box = [charmander('c')]) => ({
    ...createRun(1, lineUp),
    box,
  });

  it('puts the survivor in the line-up, in the earlier of the two slots', () => {
    const run = { ...runWith(), lineUp: [createInstance('Squirtle', { instanceId: 's' }), charmander('a'), charmeleon('b')] };
    const result = combineMons(run, 'b', 'a')!;

    expect(result.run.lineUp.map((m) => m.instanceId)).toEqual(['s', 'b']);
  });

  it('keeps the survivor fighting when only one half was in the line-up', () => {
    const result = combineMons(runWith(), 'a', 'c')!;

    // 'a' was fighting and 'c' was boxed: the merged mon stays in the line-up rather than
    // following the boxed half into storage.
    expect(result.run.lineUp.map((m) => m.instanceId)).toContain('a');
    expect(result.run.box).toHaveLength(0);
  });

  it('leaves a merge of two boxed mons in the Box', () => {
    const run = { ...runWith(), box: [charmander('c'), charmander('d')] };
    const result = combineMons(run, 'c', 'd')!;

    expect(result.run.box.map((m) => m.instanceId)).toEqual(['c']);
    expect(result.run.lineUp).toHaveLength(2);
  });

  it('never leaves the absorbed mon anywhere', () => {
    const result = combineMons(runWith(), 'b', 'a')!;
    const ids = [...result.run.lineUp, ...result.run.box].map((m) => m.instanceId);

    expect(ids).not.toContain('a');
    expect(ids.filter((id) => id === 'b')).toHaveLength(1);
  });

  it('refuses a pair that is not a family, and a mon the run does not own', () => {
    const run = runWith([charmander('a'), createInstance('Squirtle', { instanceId: 's' })], []);
    expect(combineMons(run, 'a', 's')).toBeNull();
    expect(combineMons(run, 'a', 'ghost')).toBeNull();
  });
});
