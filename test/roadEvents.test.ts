/**
 * Encounters: what they roll, what each branch does, and the promise the node makes.
 *
 * The load-bearing test here is the last one in the first block. An Encounter costs you the fight
 * that node would have been, so it has to be worth taking — the design promise is that *every*
 * encounter has at least one branch that cannot leave the run worse off. That is a property of
 * the whole content set rather than of any one kind, so it is checked across all nine rather than
 * spot-checked, and it will fail the moment a new encounter is written without a safe branch.
 */

import { describe, expect, it } from 'vitest';

import { createInstance, type PokemonInstance } from '../src/content/factory.js';
import { speciesOf } from '../src/content/index.js';
import { totalBalls } from '../src/meta/balls.js';
import { LOCATIONS, locationFor } from '../src/meta/locations.js';
import {
  ALL_EVENT_KINDS,
  DAYCARE_FEE,
  LOOSE_COINS,
  PROFESSOR_EXP,
  PROFESSOR_STIPEND,
  SHRINE_EXP,
  SHRINE_OFFERING,
  STASH_GREAT_BALLS,
  STASH_POKE_BALLS,
  STASH_SALE,
  STRAY_GIFT,
  TRADER_DIRECTIONS,
  buildRoadEvent,
  describeBounty,
  eventSeedFor,
  grantBounty,
  leastGrown,
  pickKind,
  resolveRoadEvent,
  rollRoadEvent,
  toll,
  type RoadEventKind,
} from '../src/meta/roadEvents.js';
import { createRun, type MapNode, type RunState } from '../src/meta/runState.js';

const location = locationFor(0);

const party = (): PokemonInstance[] => [
  createInstance('Charmander', { instanceId: 'a', exp: 6 }),
  createInstance('Pidgey', { instanceId: 'b', exp: 2 }),
];

/** A run with room to take any branch: money for every fee, and a team with someone behind. */
const richRun = (seed = 1): RunState => ({ ...createRun(seed, party()), money: 20 });

const totalExp = (run: RunState): number =>
  [...run.lineUp, ...run.box].reduce((sum, m) => sum + m.exp, 0);

const build = (kind: RoadEventKind, run = richRun(), seed = 7) =>
  buildRoadEvent(kind, run, location, seed);

describe('every encounter', () => {
  it('rolls a scene with a title, a body and at least two choices', () => {
    for (const kind of ALL_EVENT_KINDS) {
      const event = build(kind);
      expect(event.title.length, kind).toBeGreaterThan(0);
      expect(event.body.length, kind).toBeGreaterThan(0);
      expect(event.choices.length, kind).toBeGreaterThanOrEqual(2);
    }
  });

  it('spells out what every choice costs before it is clicked', () => {
    for (const kind of ALL_EVENT_KINDS) {
      for (const choice of build(kind).choices) {
        expect(choice.label.length, kind).toBeGreaterThan(0);
        expect(choice.detail.length, `${kind}: ${choice.label}`).toBeGreaterThan(0);
      }
    }
  });

  it('always leaves at least one choice takeable', () => {
    // A run with nothing: no money, one mon. Every encounter still has to offer a way out.
    const broke: RunState = { ...createRun(3, [createInstance('Rattata', { instanceId: 'a' })]), money: 0 };
    for (const kind of ALL_EVENT_KINDS) {
      const event = buildRoadEvent(kind, broke, location, 11);
      expect(event.choices.some((c) => c.available), kind).toBe(true);
    }
  });

  it('offers a branch that cannot leave the run worse off — the reason the node is worth taking', () => {
    // Taking an Encounter already costs the EXP and money the fight in that slot would have paid.
    // Charging for it twice would make it a node nobody takes, and a node nobody takes is not a
    // choice. So somewhere in every encounter there is a branch that only ever adds.
    for (const kind of ALL_EVENT_KINDS) {
      const run = richRun();
      const event = build(kind, run);

      const safe = event.choices.some((_, index) => {
        const result = resolveRoadEvent(event, index, run);
        if (result === null) return false;
        return (
          result.run.money >= run.money &&
          totalBalls(result.run.balls) >= totalBalls(run.balls) &&
          result.run.morale >= run.morale &&
          totalExp(result.run) >= totalExp(run) &&
          result.run.lineUp.length + result.run.box.length >= run.lineUp.length + run.box.length
        );
      });

      expect(safe, `${kind} has no branch that is safe to take`).toBe(true);
    }
  });

  it('never spends morale at the moment of choosing — only a fight it starts can cost that', () => {
    for (const kind of ALL_EVENT_KINDS) {
      const run = richRun();
      const event = build(kind, run);
      for (let i = 0; i < event.choices.length; i++) {
        const result = resolveRoadEvent(event, i, run);
        if (result !== null) expect(result.run.morale, `${kind}#${i}`).toBe(run.morale);
      }
    }
  });

  it('refuses a choice that does not exist or is not available, rather than half-applying it', () => {
    const run = richRun();
    const event = build('GameCorner', run);
    expect(resolveRoadEvent(event, 9, run)).toBeNull();
    expect(resolveRoadEvent(event, -1, run)).toBeNull();

    const broke: RunState = { ...run, money: 0 };
    const cannotPlay = build('GameCorner', broke);
    expect(cannotPlay.choices[0]!.available).toBe(false);
    expect(resolveRoadEvent(cannotPlay, 0, broke)).toBeNull();
  });
});

describe('rolling', () => {
  it('is stable for a seed, so looking at a node twice shows the same encounter', () => {
    const run = richRun();
    const a = rollRoadEvent(run, location, 99);
    const b = rollRoadEvent(run, location, 99);
    expect(a.kind).toBe(b.kind);
    expect(a.body).toBe(b.body);
  });

  it('gives two Encounters in one layer different seeds', () => {
    // They differ only in the last character of their id, so a seed built from the layer alone
    // would roll both of them the same encounter.
    const node = (id: string): MapNode => ({ id, type: 'Encounter', layer: 3, label: 'x' });
    expect(eventSeedFor(1, 0, node('L0-2-0'))).not.toBe(eventSeedFor(1, 0, node('L0-2-1')));
  });

  it('reaches every kind across many seeds, so no encounter is unreachable content', () => {
    const seen = new Set<RoadEventKind>();
    for (let seed = 0; seed < 400; seed++) seen.add(pickKind(seed));
    expect([...seen].sort()).toEqual([...ALL_EVENT_KINDS].sort());
  });

  it('builds for every Location without falling back', () => {
    for (const loc of LOCATIONS) {
      for (const kind of ALL_EVENT_KINDS) {
        const event = buildRoadEvent(kind, richRun(), loc, 5);
        expect(event.choices.length, `${loc.name}/${kind}`).toBeGreaterThanOrEqual(2);
      }
    }
  });
});

describe('Legendary Sighting', () => {
  it('sends you against a Legendary, for a bounty', () => {
    const run = richRun();
    const event = build('LegendarySighting', run);
    const result = resolveRoadEvent(event, 0, run)!;

    expect(result.foes).toHaveLength(1);
    expect(speciesOf(result.foes[0]!.speciesId)!.isLegendary).toBe(true);
    expect(result.bounty!.money).toBeGreaterThan(0);
    expect(result.bounty!.ball).toBe('Ultra');
    // The bounty is paid by the battle, not by the choice.
    expect(result.run.money).toBe(run.money);
  });

  it('pays you something for walking away, so declining is never nothing', () => {
    const run = richRun();
    const result = resolveRoadEvent(build('LegendarySighting', run), 1, run)!;
    expect(totalBalls(result.run.balls)).toBe(totalBalls(run.balls) + 1);
    expect(result.foes).toHaveLength(0);
  });

  it('pays the bounty into the run when the fight is won', () => {
    const run = richRun();
    const bounty = { money: 12, ball: 'Ultra' as const, ballCount: 1 };
    const paid = grantBounty(run, bounty);

    expect(paid.money).toBe(run.money + 12);
    expect(paid.balls.Ultra).toBe(run.balls.Ultra + 1);
    expect(describeBounty(bounty)).toContain('Ultra Ball');
  });
});

describe('Traveling Trader', () => {
  it('trades the least-grown mon for one a tier up, in its own slot', () => {
    const run = richRun();
    const event = build('TravelingTrader', run);
    expect(event.tradeAwayId).toBe(leastGrown(run)!.instanceId);

    const result = resolveRoadEvent(event, 0, run)!;
    expect(result.run.lineUp).toHaveLength(run.lineUp.length);
    expect(result.run.lineUp.map((m) => m.instanceId)).toContain(event.offer!.instanceId);
    expect(result.run.lineUp[1]!.instanceId).toBe(event.offer!.instanceId);

    const gave = speciesOf(run.lineUp[1]!.speciesId)!;
    const got = speciesOf(event.offer!.speciesId)!;
    expect(got.tier).toBeGreaterThan(gave.tier);
  });

  it('carries the traded mon EXP across, so a trade is not a reset', () => {
    const run = richRun();
    const event = build('TravelingTrader', run);
    expect(event.offer!.exp).toBe(leastGrown(run)!.exp);
  });

  it('pays for directions when you decline', () => {
    const run = richRun();
    const result = resolveRoadEvent(build('TravelingTrader', run), 1, run)!;
    expect(result.run.money).toBe(run.money + TRADER_DIRECTIONS);
  });
});

describe('growth encounters', () => {
  it('the Professor levels the whole line-up', () => {
    const run = richRun();
    const result = resolveRoadEvent(build('WanderingProfessor', run), 0, run)!;

    for (const mon of result.run.lineUp) {
      const before = run.lineUp.find((m) => m.instanceId === mon.instanceId)!;
      expect(mon.exp).toBeGreaterThanOrEqual(before.exp + PROFESSOR_EXP);
    }
    expect(Object.keys(result.report.gained).length).toBe(run.lineUp.length);
  });

  it('the Professor pays cash if you would rather keep walking', () => {
    const run = richRun();
    const result = resolveRoadEvent(build('WanderingProfessor', run), 1, run)!;
    expect(result.run.money).toBe(run.money + PROFESSOR_STIPEND);
    expect(totalExp(result.run)).toBe(totalExp(run));
  });

  it('the Day Care brings the one that is behind level with the best', () => {
    const run = richRun();
    const event = build('DayCare', run);
    const result = resolveRoadEvent(event, 0, run)!;

    const best = Math.max(...run.lineUp.map((m) => m.exp));
    const raised = result.run.lineUp.find((m) => m.instanceId === event.tradeAwayId)!;
    expect(raised.exp).toBe(best);
    expect(result.run.money).toBe(run.money - DAYCARE_FEE);
  });

  it('the Day Care will not take your money when nothing of yours is behind', () => {
    const level: RunState = {
      ...createRun(1, [
        createInstance('Charmander', { instanceId: 'a', exp: 4 }),
        createInstance('Pidgey', { instanceId: 'b', exp: 4 }),
      ]),
      money: 20,
    };
    expect(build('DayCare', level).choices[0]!.available).toBe(false);
  });

  it('the shrine grows the Lead alone', () => {
    const run = richRun();
    const result = resolveRoadEvent(build('AncientShrine', run), 0, run)!;

    expect(result.run.lineUp[0]!.exp).toBe(run.lineUp[0]!.exp + SHRINE_EXP);
    expect(result.run.lineUp[1]!.exp).toBe(run.lineUp[1]!.exp);
    expect(result.run.money).toBe(run.money - SHRINE_OFFERING);
  });
});

describe('gift encounters', () => {
  it('the stash pays in balls, or in what the balls are worth', () => {
    const run = richRun();
    const kept = resolveRoadEvent(build('FoundStash', run), 0, run)!;
    expect(kept.run.balls.Poke).toBe(run.balls.Poke + STASH_POKE_BALLS);
    expect(kept.run.balls.Great).toBe(run.balls.Great + STASH_GREAT_BALLS);

    const sold = resolveRoadEvent(build('FoundStash', run), 1, run)!;
    expect(sold.run.money).toBe(run.money + STASH_SALE);
  });

  it('the stray joins the line-up while there is room for it', () => {
    const run = richRun();
    const event = build('StrayFriend', run);
    const result = resolveRoadEvent(event, 0, run)!;

    expect(result.run.lineUp).toHaveLength(run.lineUp.length + 1);
    expect(result.run.lineUp.at(-1)!.instanceId).toBe(event.offer!.instanceId);
  });

  it('the stray waits in the Box when the line-up is full', () => {
    const full: RunState = {
      ...createRun(1, Array.from({ length: 6 }, (_, i) =>
        createInstance('Rattata', { instanceId: `m${i}` }),
      )),
      money: 20,
    };
    const event = build('StrayFriend', full);
    const result = resolveRoadEvent(event, 0, full)!;

    expect(result.run.lineUp).toHaveLength(6);
    expect(result.run.box).toHaveLength(1);
  });

  it('the stray leaves you something when you send it home', () => {
    const run = richRun();
    const result = resolveRoadEvent(build('StrayFriend', run), 1, run)!;
    expect(result.run.money).toBe(run.money + STRAY_GIFT);
  });
});

describe('the two that can cost you', () => {
  it('the Game Corner is the same flip every time for a seed', () => {
    const run = richRun();
    const first = resolveRoadEvent(build('GameCorner', run, 21), 0, run)!;
    const second = resolveRoadEvent(build('GameCorner', run, 21), 0, run)!;
    expect(first.run.money).toBe(second.run.money);
    expect(first.message).toBe(second.message);
  });

  it('the Game Corner always has a branch that just hands you coins', () => {
    const run = richRun();
    const result = resolveRoadEvent(build('GameCorner', run), 1, run)!;
    expect(result.run.money).toBe(run.money + LOOSE_COINS);
  });

  it('Team Rocket takes half, rounded up, and nothing else', () => {
    const run: RunState = { ...richRun(), money: 9 };
    expect(toll(run)).toBe(5);

    const result = resolveRoadEvent(build('RocketShakedown', run), 1, run)!;
    expect(result.run.money).toBe(4);
    expect(totalBalls(result.run.balls)).toBe(totalBalls(run.balls));
    expect(totalExp(result.run)).toBe(totalExp(run));
  });

  it('Team Rocket can be fought instead, for the bag', () => {
    const run = richRun();
    const result = resolveRoadEvent(build('RocketShakedown', run), 0, run)!;

    expect(result.foes.length).toBeGreaterThan(1);
    expect(result.bounty!.money).toBeGreaterThan(0);
    expect(result.run.money).toBe(run.money);
  });
});
