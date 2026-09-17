/**
 * How difficulty scales across a run. Ported from Unity's `RunProgression.cs` (ADR 0007, retuned
 * by ADR 0008).
 *
 * **Two dials, not one.** A Location raises the *tier* its opposition is drawn from — which
 * species turn up at all — and the *EXP* those mons carry on top. Tier is the step change, EXP is
 * the slope between steps. A single "level" number had to be both at once and was worse at each.
 *
 * **Enemies scale with progress, never with the player.** Both dials come from the badge count
 * and how deep into a Location a node sits; neither ever looks at the player's own team.
 * Rubber-banding would make EXP worthless — a player who fights more should be ahead, and one who
 * dodges fights should feel it.
 */

/** Badges to win the run. Beating the eighth Gym ends it as a victory. */
export const BADGES_TO_WIN = 8;

/**
 * EXP a Location is worth to a mon that fights through it: two or three wild wins plus the Gym,
 * at one point each. The Pokemon Center is a shop and pays none.
 *
 * It is therefore also how far the *next* Location's opposition is pitched forward — the two have
 * to climb at the same rate, and a test fails if they drift apart.
 */
export const EXP_PER_BADGE = 4;

/** EXP earned per win, by every mon that was in the line-up. */
export const EXP_PER_WIN = 1;

/** How far a Location's first wild encounters sit below its baseline; they catch up deeper in. */
export const WILD_EXP_BELOW_BASELINE = 1;

/** How much EXP a Gym Leader carries above its Location's baseline. */
export const GYM_EXP_ABOVE_BASELINE = 2;

/** The smallest a Gym team is, before it grows with badges and matches the player's line-up. */
export const MIN_GYM_TEAM_SIZE = 2;

/**
 * Highest species tier the pool may draw from in the first Location, and how much that rises per
 * badge. Base forms are mostly tier 1-2, so this is mainly what keeps Snorlax out of the opening.
 */
export const FIRST_LOCATION_MAX_TIER = 1;
export const MAX_TIER_INCREASE_PER_BADGE = 1;

export const MAX_PARTY_SIZE = 6;

export const STARTING_MORALE = 3;
export const STARTING_MONEY = 5;

export const MONEY_PER_WILD_WIN = 2;
export const MONEY_PER_GYM_WIN = 5;

/** The EXP a Location is pitched at: nothing for the first, rising per badge already earned. */
export const baselineExp = (badges: number): number => EXP_PER_BADGE * Math.max(0, badges);

/** A wild encounter's EXP at map layer `layer` (1 is the first choice layer). */
export const wildExp = (badges: number, layer: number): number =>
  Math.max(0, baselineExp(badges) - WILD_EXP_BELOW_BASELINE + Math.floor(Math.max(0, layer) / 2));

export const gymExp = (badges: number): number => baselineExp(badges) + GYM_EXP_ABOVE_BASELINE;

/**
 * How many wild mons an encounter fields: one in the first Location, while the player has only
 * their starting pair; two until the fourth badge; three after.
 */
export const wildEncounterSize = (badges: number): number =>
  badges <= 0 ? 1 : badges < 4 ? 2 : 3;

/**
 * A Gym Leader fields at least as many mons as the player brings — the line-up is a train, so a
 * longer one is simply more health to chew through — and grows with badges regardless.
 */
export const gymTeamSize = (badges: number, lineUpCount: number): number =>
  Math.min(MAX_PARTY_SIZE, Math.max(lineUpCount, MIN_GYM_TEAM_SIZE + Math.floor(Math.max(0, badges) / 2)));

/** The highest species tier an encounter may draw, or null for no cap (the final Location). */
export function maxTier(badges: number): number | null {
  if (badges >= BADGES_TO_WIN - 1) return null;
  return FIRST_LOCATION_MAX_TIER + MAX_TIER_INCREASE_PER_BADGE * Math.max(0, badges);
}

export const moneyForWin = (isGym: boolean): number =>
  isGym ? MONEY_PER_GYM_WIN : MONEY_PER_WILD_WIN;
