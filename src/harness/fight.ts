/**
 * Text-mode fight harness.
 *
 * Prints a fight's event log to the terminal, Step by Step. Suggested by the build order
 * (REACT_REBUILD_REFERENCE.md §7 stage 2) as "enormously useful and it costs nothing", and it
 * is: it's how you sanity-check a passive or a synergy before any of the battle screen exists.
 *
 *   npm run fight                       a demo fight
 *   npm run fight -- --seed 7           a different seed
 *   npm run fight -- --scenario drain   the lifesteal stalemate sudden death exists to end
 *
 * Scenarios live in `scenarios` below; add to it freely, it is a scratchpad rather than content.
 */

import {
  activeSynergies,
  applyOpeningMutable,
  advanceStepMutable,
  createRandom,
  determineOutcome,
  makeBattleState,
  EVENT_CAP,
  STEP_CAP,
  type Combatant,
  type StepEvent,
} from '../sim/index.js';
import { createInstance, resetInstanceIds, toLineUp } from '../content/factory.js';
import { speciesNamed } from '../content/index.js';

// --- scenarios -------------------------------------------------------------------------------

type Scenario = () => { a: Combatant[]; b: Combatant[]; note: string };

/** A line-up from species names, each optionally with EXP and evolutions: "Charizard:24:2". */
function team(...specs: string[]): Combatant[] {
  return toLineUp(
    specs.map((spec) => {
      const [name, exp, evolutions] = spec.split(':');
      if (speciesNamed(name!) === null) {
        throw new Error(`No species named "${name}" in the roster`);
      }
      return createInstance(name!, {
        exp: exp === undefined ? 0 : Number.parseInt(exp, 10),
        timesEvolved: evolutions === undefined ? 0 : Number.parseInt(evolutions, 10),
      });
    }),
  );
}

const scenarios: Record<string, Scenario> = {
  demo: () => ({
    note: 'All tier 1, straight off the roster — roughly a starting party.',
    a: team('Charmander', 'Pidgey', 'Tyrogue'),
    b: team('Squirtle', 'Geodude', 'Oddish'),
  }),

  midrun: () => ({
    note: 'Roughly where a party sits three Locations in: some EXP, one line evolved.',
    a: team('Charmeleon:14:1', 'Pidgey:8', 'Machop:10'),
    b: team('Wartortle:14:1', 'Graveler:12:1', 'Gloom:9:1'),
  }),

  drain: () => ({
    // Six Grass-types stack Vine Drain's synergy to +60% lifesteal before a single passive fires;
    // the passives then push it to the cap. This is the shape of fight sudden death exists for.
    // Note that ADR 0014's own example — two Bulbasaurs, stalling from Step 12 — no longer
    // reproduces against current content: real Bulbasaurs resolve in three Steps.
    note: 'Stacked lifesteal: a fight that stalls until sudden death intervenes around Step 30.',
    a: team('Bulbasaur:20', 'Oddish:20', 'Bellsprout:20', 'Exeggcute:20', 'Chikorita:20', 'Treecko:20'),
    b: team('Bulbasaur:20', 'Oddish:20', 'Bellsprout:20', 'Exeggcute:20', 'Chikorita:20', 'Treecko:20'),
  }),

  status: () => ({
    note: "Poison and burn against a Fairy ward and a Steel line's damage reduction.",
    a: team('Zubat', 'Charmander'),
    b: team('Magnemite', 'Clefairy'),
  }),

  legends: () => ({
    // Tier 6 spends 58 points, and Attack is capped at just under half of whatever Speed leaves —
    // so Mewtwo is 27/28 and one blow takes all but a point. Base tier lines are a ~2-Step trade
    // at every tier; it's EXP growth, not tier, that lengthens fights.
    note: 'Tier 6 mirror: 27 Attack into 28 Health, so it ends as fast as a tier-1 fight.',
    a: team('Mewtwo'),
    b: team('Rayquaza'),
  }),

  swarm: () => ({
    note: 'Five Bug-types: Swarm Scurry at x5 is +1 Speed to everyone.',
    a: team('Caterpie', 'Weedle', 'Ledyba', 'Nincada', 'Shedinja'),
    b: team('Charmander', 'Squirtle', 'Bulbasaur'),
  }),
};

// --- formatting ------------------------------------------------------------------------------

const pad = (s: string, n: number) => s.padEnd(n);

function describeEvent(e: StepEvent): string {
  const src = e.sourceInstanceId ?? '-';
  const tgt = e.targetInstanceId ?? '-';
  const amt = e.amount ?? 0;

  switch (e.kind) {
    case 'TypeSynergy':
      return `${e.sourceSide}: ${e.synergyType} x${amt}`;
    case 'Damage':
      return `${src} hits ${tgt} for ${amt}`;
    case 'ShieldAbsorbed':
      return `${tgt}'s shield soaks ${amt}`;
    case 'Heal':
      return `${src} heals ${tgt} for ${amt}`;
    case 'LifestealHeal':
      return `${src} drains back ${amt}`;
    case 'Shield':
      return `${src} shields ${tgt} for ${amt}`;
    case 'StatusApplied':
      return `${tgt} is ${String(e.status).toLowerCase()}`;
    case 'StatusBlocked':
      return `${tgt} wards off ${String(e.status).toLowerCase()}`;
    case 'StatusCleared':
      return `${tgt} is cured of ${String(e.status).toLowerCase()}`;
    case 'StatusTick':
      return `${tgt} takes ${amt} from ${String(e.status).toLowerCase()}`;
    case 'SuddenDeath':
      return `SUDDEN DEATH: ${tgt} takes ${amt}`;
    case 'BuffAttack':
      return `${tgt} +${amt} Attack`;
    case 'BuffSpeed':
      return `${tgt} +${amt} Speed`;
    case 'ChargeRateModified':
      return `${tgt} charge rate ${amt > 0 ? '+' : ''}${amt}%`;
    case 'DamageReductionApplied':
      return `${tgt} +${amt} damage reduction`;
    case 'Lifesteal':
      return `${tgt} +${amt}% lifesteal`;
    case 'PassiveTriggered':
      return `${src} fires its passive`;
    case 'Faint':
      return `${src} faints`;
    case 'ChargeGained':
      return `${tgt} charges +${amt}`;
    case 'BallThrown':
      return `a ball is thrown at ${tgt} (${amt}%)`;
    case 'Caught':
      return `${src} is caught`;
    case 'Promotion':
      return `${src} steps up`;
    case 'BattleEnd':
      return `battle over: ${e.outcome}`;
  }
}

function board(lineUp: Combatant[]): string {
  if (lineUp.length === 0) return '    (empty)';
  return lineUp
    .map((c, i) => {
      const role = i === 0 ? 'Lead   ' : i === 1 ? 'Support' : 'dormant';
      const bits = [
        `HP ${c.currentHP}/${c.currentStats.health}`,
        `ATK ${c.currentStats.attack}`,
        `SP ${c.currentStats.special}`,
        `SPD ${c.currentStats.speed}`,
        `chg ${c.charge}`,
      ];
      if (c.shield > 0) bits.push(`shield ${c.shield}`);
      if (c.damageReductionFlat > 0) bits.push(`DR ${c.damageReductionFlat}`);
      if (c.lifestealPercent > 0) bits.push(`drain ${Math.round(c.lifestealPercent * 100)}%`);
      if (c.statusWards > 0) bits.push(`wards ${c.statusWards}`);
      if (c.status !== null) bits.push(c.status.toLowerCase());
      return `    ${role} ${pad(c.instanceId, 14)} ${bits.join('  ')}`;
    })
    .join('\n');
}

// --- main ------------------------------------------------------------------------------------

function parseArgs(argv: string[]) {
  const get = (flag: string, fallback: string) => {
    const i = argv.indexOf(flag);
    return i >= 0 && argv[i + 1] !== undefined ? argv[i + 1]! : fallback;
  };
  return {
    seed: Number.parseInt(get('--seed', '1'), 10),
    scenario: get('--scenario', 'demo'),
    quiet: argv.includes('--quiet'),
  };
}

function main(): void {
  const { seed, scenario, quiet } = parseArgs(process.argv.slice(2));

  const build = scenarios[scenario];
  if (build === undefined) {
    console.error(`Unknown scenario "${scenario}". Try: ${Object.keys(scenarios).join(', ')}`);
    process.exitCode = 1;
    return;
  }

  // Reset so instance ids — and therefore the EXP growth draws keyed to them — are the same on
  // every run of the harness.
  resetInstanceIds();
  const { a, b, note } = build();
  const state = makeBattleState(a, b);
  const rng = createRandom(seed);

  console.log(`\n${'='.repeat(78)}`);
  console.log(`  ${scenario}  (seed ${seed})`);
  console.log(`  ${note}`);
  console.log('='.repeat(78));

  for (const [side, lineUp] of [
    ['A', state.lineUpA],
    ['B', state.lineUpB],
  ] as const) {
    const synergies = activeSynergies(lineUp);
    if (synergies.length > 0) {
      console.log(`\n  Side ${side} synergies:`);
      for (const s of synergies) console.log(`    ${pad(s.title, 22)} ${s.effect}`);
    }
  }

  // The opening is its own beat, before Step 1 — see ADR 0016.
  const openingEvents = applyOpeningMutable(state);
  console.log('\n--- Opening (Step 0) ---');
  for (const e of openingEvents) {
    if (e.kind === 'TypeSynergy') continue; // already listed above
    console.log(`    ${describeEvent(e)}`);
  }
  if (!quiet) {
    console.log('\n  Side A');
    console.log(board(state.lineUpA));
    console.log('  Side B');
    console.log(board(state.lineUpB));
  }

  let eventCount = openingEvents.length;
  while (state.lineUpA.length > 0 && state.lineUpB.length > 0) {
    if (state.stepNumber >= STEP_CAP || eventCount >= EVENT_CAP) {
      console.log('\n!! safety cap hit — this is a bug in the sim, not the content');
      break;
    }

    const events = advanceStepMutable(state, rng);
    eventCount += events.length;

    console.log(`\n--- Step ${state.stepNumber} ---`);
    for (const e of events) console.log(`    ${describeEvent(e)}`);

    if (!quiet) {
      console.log('\n  Side A');
      console.log(board(state.lineUpA));
      console.log('  Side B');
      console.log(board(state.lineUpB));
    }
  }

  const outcome = determineOutcome(state);
  console.log(`\n${'='.repeat(78)}`);
  console.log(`  ${outcome} after ${state.stepNumber} Step(s), ${eventCount} events`);
  console.log(`${'='.repeat(78)}\n`);
}

main();
