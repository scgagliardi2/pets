/**
 * Held items: the catalogue, what they do to a mon's stats and typing, their in-battle
 * behaviour, and the bag.
 */

import { describe, expect, it } from 'vitest';

import { ITEMS, itemById, plateItems, shopItemPool, statEffectsOf } from '../src/content/items.js';
import { POKEMON_TYPES, makeCombatant, runPrecomputed } from '../src/sim/index.js';
import {
  createInstance,
  heldItemEffectsOf,
  statsOf,

  typesOf,
} from '../src/content/factory.js';
import {
  addItem,
  bagContents,
  createRun,
  equipItem,
  unequipItem,
} from '../src/meta/runState.js';
import { applyItemTraining, itemExpBonus } from '../src/meta/experience.js';

const run = () => createRun(1, [createInstance('Charmander', { instanceId: 'a' })]);

describe('the catalogue', () => {
  it('has unique ids and a blurb for everything', () => {
    expect(new Set(ITEMS.map((i) => i.id)).size).toBe(ITEMS.length);
    for (const item of ITEMS) {
      expect(item.name.length, item.id).toBeGreaterThan(0);
      expect(item.blurb.length, item.id).toBeGreaterThan(0);
      expect(item.cost, item.id).toBeGreaterThan(0);
    }
  });

  it('covers every type with a type-setting item', () => {
    const covered = new Set(plateItems().map((i) => i.type));
    for (const type of POKEMON_TYPES) expect(covered.has(type), type).toBe(true);
    expect(plateItems()).toHaveLength(POKEMON_TYPES.length);
  });

  it('has one flat-stat and one training item per stat', () => {
    for (const kind of ['flatStat', 'training'] as const) {
      const stats = ITEMS.filter((i) => i.kind === kind).map((i) => i.stat);
      expect(new Set(stats).size, kind).toBe(4);
    }
  });

  it('keeps Plates out of the general shop pool, so they cannot flood a shelf', () => {
    // Eighteen Plates against a dozen of everything else would leave most shops selling typing.
    expect(shopItemPool().some((i) => i.kind === 'setType')).toBe(false);
    expect(shopItemPool().length).toBeGreaterThan(0);
  });

  it('resolves an unknown id to nothing rather than throwing', () => {
    expect(itemById('not-an-item')).toBeNull();
    expect(itemById(null)).toBeNull();
  });
});

describe('what an item does to a mon', () => {
  it('adds its flat stat', () => {
    const plain = createInstance('Charmander', { instanceId: 'a' });
    const buffed = createInstance('Charmander', { instanceId: 'b', heldItemId: 'protein' });
    const amount = itemById('protein')!.amount!;

    expect(statsOf(buffed).attack).toBe(statsOf(plain).attack + amount);
    expect(statsOf(buffed).health).toBe(statsOf(plain).health);
  });

  it('replaces typing entirely rather than adding to it', () => {
    // A Plate is a way to buy into a synergy, so it has to override — adding a type would make
    // dual-typed mons strictly better holders.
    const mon = createInstance('Charmander', { instanceId: 'a', heldItemId: 'plate-water' });
    expect(typesOf(mon)).toEqual(['Water']);
  });

  it('leaves typing alone without a Plate', () => {
    const mon = createInstance('Charmander', { instanceId: 'a' });
    expect(typesOf(mon)).toEqual(['Fire']);
  });

  it('resolves only the battle-relevant items for the simulator', () => {
    expect(heldItemEffectsOf(createInstance('Charmander', { instanceId: 'a', heldItemId: 'leftovers' })))
      .toEqual({ regenPerStep: 2 });
    expect(heldItemEffectsOf(createInstance('Charmander', { instanceId: 'b', heldItemId: 'lum-berry' })))
      .toEqual({ curesStatus: true });
    // Flat stats and typing are already in the stat line; the sim needs to know nothing.
    expect(heldItemEffectsOf(createInstance('Charmander', { instanceId: 'c', heldItemId: 'protein' })))
      .toBeNull();
  });

  it('reports no stat effects for an empty hand', () => {
    expect(statEffectsOf(null).overrideTypes).toBeNull();
  });
});

describe('in battle', () => {
  const withItem = (id: string, itemId: string | null, health: number) => {
    const c = makeCombatant({ instanceId: id, attack: 2, health, speed: 1 });
    return {
      ...c,
      heldItem: itemId === null ? null : heldItemEffectsOf(
        createInstance('Charmander', { instanceId: id, heldItemId: itemId }),
      ),
    };
  };

  it('regenerates health each Step', () => {
    const holder = withItem('holder', 'leftovers', 60);
    const plain = withItem('plain', null, 60);
    const log = runPrecomputed([holder], [plain], 1);

    const survivor = [...log.finalState.lineUpA, ...log.finalState.lineUpB][0];
    // The regenerating side should outlast an otherwise identical opponent.
    expect(log.outcome).toBe('SideAWins');
    expect(survivor?.instanceId).toBe('holder');
  });

  it('cannot regenerate above its maximum', () => {
    const holder = withItem('holder', 'leftovers', 40);
    const log = runPrecomputed([holder], [withItem('x', null, 40)], 1);
    for (const mon of log.finalState.lineUpA) {
      expect(mon.currentHP).toBeLessThanOrEqual(mon.currentStats.health);
    }
  });

  it('cures a status once, then is spent for the rest of the battle', () => {
    const poisoner = makeCombatant({
      instanceId: 'p',
      attack: 1,
      health: 80,
      speed: 3,
      passive: {
        id: 'toxic',
        effects: [{ type: 'ApplyStatus', target: 'EnemyLead', amount: 3, status: 'Poisoned' }],
      },
    });
    const holder = withItem('holder', 'lum-berry', 80);

    const log = runPrecomputed([poisoner], [holder], 1);
    const cures = log.events.filter(
      (e) => e.kind === 'StatusCleared' && e.targetInstanceId === 'holder',
    );

    // Exactly one cure, however many times poison is reapplied.
    expect(cures).toHaveLength(1);
  });
});

describe('between battles', () => {
  it('grants extra EXP from an EXP item, per mon rather than run-wide', () => {
    const withEgg = createInstance('Charmander', { instanceId: 'a', heldItemId: 'lucky-egg' });
    const without = createInstance('Charmander', { instanceId: 'b' });

    expect(itemExpBonus(withEgg)).toBe(1);
    expect(itemExpBonus(without)).toBe(0);
  });

  it('trains the stat its Power item names, for participants only', () => {
    const state = createRun(1, [
      createInstance('Charmander', { instanceId: 'fought', heldItemId: 'power-bracer' }),
      createInstance('Squirtle', { instanceId: 'benched', heldItemId: 'power-bracer' }),
    ]);
    const after = applyItemTraining(state, ['fought']);

    expect(after.lineUp.find((m) => m.instanceId === 'fought')!.allocation.attack).toBe(1);
    expect(after.lineUp.find((m) => m.instanceId === 'benched')!.allocation.attack).toBe(0);
  });

  it('does nothing for a mon holding something that is not a training item', () => {
    const state = createRun(1, [
      createInstance('Charmander', { instanceId: 'a', heldItemId: 'leftovers' }),
    ]);
    expect(applyItemTraining(state, ['a']).lineUp[0]!.allocation.attack).toBe(0);
  });
});

describe('the bag', () => {
  it('holds items and reports what is in it', () => {
    const state = addItem(addItem(run(), 'leftovers'), 'protein');
    expect(bagContents(state).sort()).toEqual(['leftovers', 'protein']);
  });

  it('moves an item onto a mon and out of the bag', () => {
    const state = equipItem(addItem(run(), 'leftovers'), 'a', 'leftovers');

    expect(state.lineUp[0]!.heldItemId).toBe('leftovers');
    expect(state.bag['leftovers']).toBe(0);
  });

  it('returns the displaced item rather than destroying it', () => {
    // An accidental drop should never cost the player an item.
    let state = addItem(addItem(run(), 'leftovers'), 'protein');
    state = equipItem(state, 'a', 'leftovers');
    state = equipItem(state, 'a', 'protein');

    expect(state.lineUp[0]!.heldItemId).toBe('protein');
    expect(state.bag['leftovers']).toBe(1);
  });

  it('puts an item back when unequipped', () => {
    let state = equipItem(addItem(run(), 'leftovers'), 'a', 'leftovers');
    state = unequipItem(state, 'a');

    expect(state.lineUp[0]!.heldItemId).toBeNull();
    expect(state.bag['leftovers']).toBe(1);
  });

  it('refuses to equip an item the bag does not have', () => {
    const state = run();
    expect(equipItem(state, 'a', 'leftovers')).toBe(state);
  });

  it('refuses to equip onto a mon that does not exist', () => {
    const state = addItem(run(), 'leftovers');
    expect(equipItem(state, 'ghost', 'leftovers')).toBe(state);
  });

  it('does nothing unequipping an empty hand', () => {
    const state = run();
    expect(unequipItem(state, 'a')).toBe(state);
  });

  it('equips a mon in the Box as readily as one in the line-up', () => {
    let state = addItem(run(), 'leftovers');
    state = { ...state, box: [createInstance('Oddish', { instanceId: 'boxed' })] };
    state = equipItem(state, 'boxed', 'leftovers');

    expect(state.box[0]!.heldItemId).toBe('leftovers');
  });
});
