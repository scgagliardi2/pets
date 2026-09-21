/**
 * The Pokemon Center: a shop, not a heal.
 *
 * Damage already resets after every fight, so there is nothing to restore — what a Center sells
 * is the resource a run actually runs out of. It pays no EXP and no money, so taking one is a
 * real trade: you give up the fight that node would have been.
 *
 * Prices are a first pass. The measured problem they exist to address is that balls were not a
 * constraint at all — a free restock of one a Location won runs as often as three — so the
 * inventory only becomes a decision if it has to be bought against everything else money is for.
 */

import { BALL_TIERS, addBalls, ballName, type BallInventory, type BallTier } from './balls.js';

export interface ShopItem {
  readonly id: string;
  readonly name: string;
  readonly cost: number;
  readonly blurb: string;
  readonly tier: BallTier;
  /** How many of that ball the purchase grants. */
  readonly quantity: number;
}

/**
 * What a Center stocks.
 *
 * Priced so a Location's takings — roughly two money a wild win and five for a Gym — buy a couple
 * of cheap balls or one good one, not both. An Ultra Ball should feel like a decision.
 */
export const SHOP_STOCK: readonly ShopItem[] = [
  {
    id: 'poke-2',
    name: 'Poké Ball ×2',
    cost: 4,
    blurb: '10% base. Enough for a target you have already worn down.',
    tier: 'Poke',
    quantity: 2,
  },
  {
    id: 'great-1',
    name: 'Great Ball',
    cost: 6,
    blurb: '20% base, and it lets a catch keep more of its growth.',
    tier: 'Great',
    quantity: 1,
  },
  {
    id: 'ultra-1',
    name: 'Ultra Ball',
    cost: 12,
    blurb: '35% base, and the catch arrives at full strength.',
    tier: 'Ultra',
    quantity: 1,
  },
];

export const canAfford = (money: number, item: ShopItem): boolean => money >= item.cost;

/** Applies a purchase. Returns nulls when it can't be afforded, so the caller can't half-apply it. */
export function buy(
  money: number,
  balls: BallInventory,
  item: ShopItem,
): { money: number; balls: BallInventory } | null {
  if (!canAfford(money, item)) return null;
  return {
    money: money - item.cost,
    balls: addBalls(balls, item.tier, item.quantity),
  };
}

/** A one-line summary of what the player is carrying, for the shop header. */
export const describeInventory = (balls: BallInventory): string =>
  BALL_TIERS.filter((t) => balls[t] > 0)
    .map((t) => `${ballName(t)} ×${balls[t]}`)
    .join(', ') || 'no balls';


// --- adoption ------------------------------------------------------------------------------------

/**
 * What a Center charges to adopt a Pokémon, by tier.
 *
 * Priced against a Location's takings — roughly eleven money for a clean sweep — so adopting is a
 * real alternative to stocking up on balls rather than something you do as well. A tier above the
 * local cap costs disproportionately more, because that is the thing that keeps you on the curve
 * and catching is supposed to stay the cheaper route to it.
 */
export const adoptionCost = (tier: number): number => 3 + Math.max(0, tier - 1) * 4;

/** What it costs to see a different five. */
export const REROLL_COST = 2;

/** How many Pokémon a Center offers at once. */
export const ADOPTION_SLOTS = 5;

export const canReroll = (money: number): boolean => money >= REROLL_COST;
