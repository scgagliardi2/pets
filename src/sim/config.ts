/**
 * Balance constants. Ported from Unity's `BattleConfig.cs` — tune the values freely, but keep
 * them in sync with the Unity side while both implementations exist, since the shared golden
 * fixtures are calibrated against them.
 */

/**
 * A charge meter triggers its passive once it reaches this value. Global, not per-mon — Speed is
 * what varies triggering frequency.
 *
 * Three, because Speed is a number from 1 to 3 rather than a real Pokemon stat in the 20-130
 * range: a mon accrues its Speed in charge each Step, so Speed 1 fires on the third Step, Speed 2
 * on the second, Speed 3 every Step. Against the old threshold of 100, a Speed-1 mon needed a
 * hundred-Step fight to use its passive once.
 */
export const CHARGE_THRESHOLD = 3;

/**
 * Fixed pacing window per Step. Named for milliseconds to match the spec's charge formula
 * (`charge += speed * stepDurationMs`), but the value is a small placeholder scalar, *not*
 * literal milliseconds. At 1, a mon accrues exactly its Speed per Step, which is what lets
 * CHARGE_THRESHOLD read as "Steps per trigger at Speed 1". Rescale this constant, not the
 * formula, if pacing needs to change.
 */
export const DEFAULT_STEP_DURATION_MS = 1;

/**
 * Engineering safeguard against a pathological combo creating an effectively infinite fight —
 * forces a draw if hit. The last resort, not the first: SUDDEN_DEATH_STEP is what actually ends
 * a fight neither side can win, and it does so with a real result.
 */
export const STEP_CAP = 200;

/**
 * The Step from which both Leads start taking escalating true damage.
 *
 * Standing modifiers accumulate for the whole battle, so a long enough fight can reach a state
 * where nobody can lose: two mons whose lifesteal has stacked past 100% each heal back exactly
 * what they take, forever. Two Bulbasaurs with Vine Drain reach that on Step 12. Before this
 * existed, such a fight ground out all 200 Steps of STEP_CAP with the HP bars frozen and then
 * called itself a draw, which on screen is indistinguishable from the game having hung.
 *
 * Thirty, because the longest fight real content produces is 17 Steps.
 */
export const SUDDEN_DEATH_STEP = 30;

/** True damage on sudden death's first Step, growing by this much again every Step after. */
export const SUDDEN_DEATH_DAMAGE_PER_STEP = 1;

/**
 * The least HP damage a connecting attack can do, however much damage reduction has stacked.
 * Blunting a hit is what reduction is for; nullifying every hit is how a fight stops being
 * winnable.
 */
export const MINIMUM_ATTACK_DAMAGE = 1;

/**
 * Ceiling on accumulated lifesteal. Draining back more than the blow took is meaningless — but
 * this is also the thing that kept two Vine Drain mons alive forever at 1980%.
 */
export const MAX_LIFESTEAL_PERCENT = 1;

/** Same safeguard as STEP_CAP, bounding total logged events instead of Steps. */
export const EVENT_CAP = 10000;

/** Charge accrual multiplier while Paralyzed. */
export const PARALYZED_CHARGE_MULTIPLIER = 0.5;
