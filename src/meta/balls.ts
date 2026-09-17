/**
 * Pokeballs: the tiers, what each is worth, and what the player is carrying.
 *
 * Every number here is a placeholder in the same sense as the rest of the balance values — a
 * first pass chosen to make the loop legible (weaken it, give it a status, then throw), not a
 * tuned curve.
 */

/** Ordered weakest to strongest. The order is meaningful: a tray draws them in it. */
export const BALL_TIERS = ['Poke', 'Great', 'Ultra'] as const;
export type BallTier = (typeof BALL_TIERS)[number];

export const ballName = (tier: BallTier): string =>
  tier === 'Great' ? 'Great Ball' : tier === 'Ultra' ? 'Ultra Ball' : 'Poké Ball';

/** Catch chance against a full-health, status-free target. The floor the other factors build on. */
export function baseChance(tier: BallTier): number {
  switch (tier) {
    case 'Great':
      return 0.2;
    case 'Ultra':
      return 0.35;
    default:
      return 0.1;
  }
}

/**
 * The most EXP a mon caught with this tier keeps.
 *
 * A low-tier ball can still succeed against a strong target, but yields an under-grown catch;
 * only an Ultra Ball guarantees the mon at its true strength. Expressed in EXP because EXP *is*
 * this game's level — stats are derived from it and there is no separate level field to cap.
 *
 * Null means no cap.
 */
export function expCap(tier: BallTier): number | null {
  switch (tier) {
    case 'Ultra':
      return null;
    case 'Great':
      return 12;
    default:
      return 6;
  }
}

export type BallInventory = Readonly<Record<BallTier, number>>;

export const STARTING_BALLS: BallInventory = { Poke: 4, Great: 2, Ultra: 1 };

export const emptyInventory = (): BallInventory => ({ Poke: 0, Great: 0, Ultra: 0 });

export const totalBalls = (inv: BallInventory): number =>
  BALL_TIERS.reduce((sum, t) => sum + inv[t], 0);

export const hasBall = (inv: BallInventory, tier: BallTier): boolean => inv[tier] > 0;

/** Spends one ball. Returns the inventory unchanged if there wasn't one to spend. */
export function spendBall(inv: BallInventory, tier: BallTier): BallInventory {
  if (inv[tier] <= 0) return inv;
  return { ...inv, [tier]: inv[tier] - 1 };
}

export const addBalls = (inv: BallInventory, tier: BallTier, amount: number): BallInventory => ({
  ...inv,
  [tier]: Math.max(0, inv[tier] + amount),
});
