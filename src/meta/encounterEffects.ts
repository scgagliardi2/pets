/**
 * Turning an encounter choice into a change to the run.
 *
 * Kept apart from the encounter catalogue so that file stays a readable list of fiction and
 * declarations, and so this one place owns every way an encounter can touch a run. An encounter
 * that reached into run state itself would be a second set of rules for gaining a Pokémon.
 *
 * Pure: takes a run and an RNG, returns a new run and a line of text. The store decides when.
 */

import { createInstance, statsOf, type PokemonInstance } from '../content/factory.js';
import { legendaries, speciesOf, type Species } from '../content/index.js';
import { addBalls } from './balls.js';
import { encounterPool } from './encounters.js';
import type { EncounterEffect } from './encountersEvents.js';
import type { Location } from './locations.js';
import { grantWinExp } from './experience.js';
import { MAX_PARTY_SIZE, maxTier } from './progression.js';
import { addCaught, addItem, addMoney, type RunState } from './runState.js';
import { itemById, shopItemPool } from '../content/items.js';
import { createRandom, type Rng } from '../sim/index.js';

export interface EffectResult {
  run: RunState;
  /** One line telling the player what actually happened. */
  text: string;
  /** A line-up to fight, when the effect was a legendary. Null otherwise. */
  legendary: PokemonInstance[] | null;
}

/** The pool an encounter gift or trade draws from: this Location's, shifted by a tier offset. */
function giftPool(run: RunState, location: Location, tierOffset: number): Species[] {
  const cap = maxTier(run.badges);
  const pool = encounterPool(run.badges, location.typeBias);
  if (tierOffset <= 0 || cap === null) return pool;

  // A tier above what is around here. Falls back to the ordinary pool rather than to nothing,
  // because at the top of the tier ladder there is no "one above".
  const better = encounterPool(run.badges + tierOffset, location.typeBias).filter(
    (s) => s.tier > cap,
  );
  return better.length > 0 ? better : pool;
}

export function applyEncounterEffect(
  run: RunState,
  effect: EncounterEffect,
  location: Location,
  rng: Rng,
): EffectResult {
  const plain = (next: RunState, text: string): EffectResult => ({ run: next, text, legendary: null });

  switch (effect.kind) {
    case 'money': {
      // Clamped at zero by addMoney, so a cost you cannot afford costs what you have rather than
      // putting the run into debt.
      const before = run.money;
      const next = addMoney(run, effect.amount);
      const moved = next.money - before;
      return plain(
        next,
        moved >= 0 ? `You gain $${moved}.` : `You hand over $${-moved}.`,
      );
    }

    case 'balls':
      return plain(
        { ...run, balls: addBalls(run.balls, effect.tier, effect.count) },
        `${effect.count} ${effect.tier} Ball${effect.count === 1 ? '' : 's'} added to your bag.`,
      );

    case 'item': {
      const pool = shopItemPool();
      const item =
        effect.itemId !== undefined ? itemById(effect.itemId) : pool[rng.nextInt(pool.length)];
      if (item === null || item === undefined) return plain(run, 'The cart is empty after all.');
      return plain(addItem(run, item.id), `${item.name} goes in your bag.`);
    }

    case 'exp': {
      const { run: next } = grantWinExp(run, effect.points);
      return plain(next, `Your line-up earns ${effect.points} EXP to spend.`);
    }

    case 'gift': {
      const pool = giftPool(run, location, effect.tierOffset);
      const species = pool[rng.nextInt(pool.length)]!;
      const mon = createInstance(species, {
        instanceId: `gift-${run.badges}-${run.visited.length}-${species.id}`,
        exp: run.lineUp[0]?.exp ?? 0,
      });
      const toLineUp = run.lineUp.length < MAX_PARTY_SIZE;
      return plain(
        addCaught(run, mon, toLineUp),
        `${species.name} joins ${toLineUp ? 'your line-up' : 'your Box'}.`,
      );
    }

    case 'trade': {
      // Trades the weakest mon in the line-up, never the Box: the point is to improve the team
      // you are actually fighting with, and picking through storage is the Box screen's job.
      if (run.lineUp.length <= 1) {
        return plain(run, 'You have nothing to spare. The offer stands another day.');
      }
      const weakest = [...run.lineUp].sort((a, b) => {
        const sa = statsOf(a);
        const sb = statsOf(b);
        return sa.attack + sa.health - (sb.attack + sb.health);
      })[0]!;

      const pool = giftPool(run, location, effect.tierOffset);
      const species = pool[rng.nextInt(pool.length)]!;
      const replacement = createInstance(species, {
        instanceId: `trade-${run.badges}-${run.visited.length}-${species.id}`,
        exp: weakest.exp,
        allocation: weakest.allocation,
      });

      const gone = speciesOf(weakest.speciesId)?.name ?? 'your Pokémon';
      return plain(
        {
          ...run,
          lineUp: run.lineUp.map((m) => (m.instanceId === weakest.instanceId ? replacement : m)),
        },
        `${gone} goes with her. ${species.name} takes its place.`,
      );
    }

    case 'legendary': {
      const pool = legendaries();
      const species = pool[rng.nextInt(pool.length)] ?? pool[0];
      if (species === undefined) return plain(run, 'Whatever it was, it is gone.');

      // Pitched above the Location baseline — it should be the hardest thing in the Location —
      // but alone, so a single strong mon rather than a wall of them.
      const mon = createInstance(species, {
        instanceId: `legendary-${run.badges}-${species.id}`,
        exp: (run.lineUp[0]?.exp ?? 0) + 4,
      });
      return { run, text: `${species.name}!`, legendary: [mon] };
    }

    case 'gamble': {
      const won = rng.nextFloat() < effect.winChance;
      const result = applyEncounterEffect(run, won ? effect.onWin : effect.onLose, location, rng);
      return { ...result, text: `${won ? 'It pays off. ' : 'It does not. '}${result.text}` };
    }

    case 'nothing':
      return plain(run, 'You move on.');
  }
}

/** Convenience for callers that only have a seed. */
export const applyWithSeed = (
  run: RunState,
  effect: EncounterEffect,
  location: Location,
  seed: number,
): EffectResult => applyEncounterEffect(run, effect, location, createRandom(seed));
