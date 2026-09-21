/**
 * Team type synergies: for every type present in a side's line-up, a small bonus that scales
 * with how many of that side's mons carry it.
 *
 * Applied once, when a battle opens, by both runners — **never inside `advanceStep`**. Ported
 * from Unity's `TeamSynergy.cs` (battle-sim-spec.md §8, ADR 0015 and 0016).
 *
 * A mon counts toward each of its types, so a Water/Ground mon adds one to both. The count
 * covers the whole line-up, dormant mons included — that's the team the player built — and it is
 * taken once: a mon that faints or is caught doesn't switch its side's synergy off mid-fight.
 *
 * Deliberately small. Stats are single digits and Speed runs 1-3 against a charge threshold of
 * three, so a synergy is worth roughly one EXP point per mon of the type, and Speed — where +1
 * can double how often a passive fires — is only granted per two or three mons.
 *
 * A combatant with no types contributes nothing, so a line-up built without them plays exactly
 * as it would if synergies didn't exist.
 */

import { applyStatus, resolveFaintsAndPromotions } from './advanceStep.js';
import {
  CHARGE_THRESHOLD,
  DEFAULT_CHARGE_CONFIG,
  MAX_LIFESTEAL_PERCENT,
  type ChargeConfig,
} from './config.js';
import { takeHit } from './damage.js';
import {
  POKEMON_TYPES,
  isAlive,
  lineUpOf,
  type BattleState,
  type Combatant,
  type PokemonType,
  type Side,
  type StepEvent,
} from './types.js';

// --- tuning constants ------------------------------------------------------------------------

/** Normal, Steady Growth: +Health to every mon, per Normal-type. */
export const NORMAL_HEALTH_PER_TYPE = 1;
/** Fire, Ember Burst: opening damage to the enemy Lead. Goes through reduction and shield. */
export const FIRE_DAMAGE_PER_TYPE = 1;
/** Water, Shell Guard: this many mons from the front each start with this much Shield. */
export const WATER_SHIELD_PER_TYPE = 1;
/** Electric, Static Shock: starting charge on the Lead, never past the threshold. */
export const ELECTRIC_LEAD_CHARGE_PER_TYPE = 2;
/** Grass, Vine Drain: lifesteal percent for every mon, still capped at 100%. */
export const GRASS_LIFESTEAL_PERCENT_PER_TYPE = 10;
/** Ice, Permafrost: charge taken off the enemy Lead and Support. Charge may go negative. */
export const ICE_CHARGE_PENALTY_PER_TYPE = 1;
/** Fighting, Power Surge: +Attack to every mon. */
export const FIGHTING_ATTACK_PER_TYPE = 1;
/** Poison, Poison Sting: the enemy Lead opens Poisoned with this tick damage. */
export const POISON_TICK_PER_TYPE = 1;
/** Ground, Sand Tomb: charge off the enemy Lead alone — narrower than Ice, and deeper. */
export const GROUND_LEAD_CHARGE_PENALTY_PER_TYPE = 2;
/** Flying, Tailwind: +1 Speed to every mon per this many Flying-types, capped. */
export const FLYING_TYPES_PER_SPEED = 2;
export const FLYING_SPEED_CAP = 3;
/** Psychic, Quick Focus: starting charge on the Lead and Support. */
export const PSYCHIC_CHARGE_PER_TYPE = 1;
/** Bug, Swarm Scurry: +1 Speed to every mon per this many Bug-types, uncapped. */
export const BUG_TYPES_PER_SPEED = 3;
/** Rock, Stone Guard: the Lead gains this percent of its Health, at least 1 per Rock-type. */
export const ROCK_LEAD_HEALTH_PERCENT_PER_TYPE = 10;
/** Ghost, Curse: the Lead gives up HP (never below 1) and everyone behind it gains. */
export const GHOST_LEAD_HP_COST_PER_TYPE = 2;
export const GHOST_TEAM_BOOST_PER_TYPE = 1;
/** Dragon, Intimidate: Attack off every enemy mon, never below 1. */
export const DRAGON_ENEMY_ATTACK_PER_TYPE = 1;
/** Dark, Night Ambush: opening true damage to the enemy Lead. Ignores shield and reduction. */
export const DARK_DAMAGE_PER_TYPE = 1;
/** Steel, Iron Hide: damage reduction on the Lead. */
export const STEEL_LEAD_REDUCTION_PER_TYPE = 1;
/** Fairy, Moonlight Glow: this many mons from the back each block the next status applied. */
export const FAIRY_WARDED_MONS_PER_TYPE = 1;

/** The synergy's display name, from the type-passive table. */
const DISPLAY_NAMES: Readonly<Record<PokemonType, string>> = {
  Normal: 'Steady Growth',
  Fire: 'Ember Burst',
  Water: 'Shell Guard',
  Electric: 'Static Shock',
  Grass: 'Vine Drain',
  Ice: 'Permafrost',
  Fighting: 'Power Surge',
  Poison: 'Poison Sting',
  Ground: 'Sand Tomb',
  Flying: 'Tailwind',
  Psychic: 'Quick Focus',
  Bug: 'Swarm Scurry',
  Rock: 'Stone Guard',
  Ghost: 'Curse',
  Dragon: 'Intimidate',
  Dark: 'Night Ambush',
  Steel: 'Iron Hide',
  Fairy: 'Moonlight Glow',
};

export const synergyDisplayName = (type: PokemonType): string => DISPLAY_NAMES[type];

// --- counting --------------------------------------------------------------------------------

export type TypeCounts = Record<PokemonType, number>;

function emptyCounts(): TypeCounts {
  return Object.fromEntries(POKEMON_TYPES.map((t) => [t, 0])) as TypeCounts;
}

/** How many mons in a line-up carry each type. A mon listing a type twice still counts once. */
export function countTypes(lineUp: readonly Combatant[]): TypeCounts {
  const counts = emptyCounts();
  for (const mon of lineUp) {
    const seen = new Set<PokemonType>();
    for (const type of mon.types) {
      if (!seen.has(type)) {
        seen.add(type);
        counts[type]++;
      }
    }
  }
  return counts;
}

/** One synergy a side actually has, and how many mons are behind it. */
export interface ActiveSynergy {
  type: PokemonType;
  count: number;
  /** "Ember Burst x2" */
  title: string;
  /** What it is worth at this count, resolved: "2 damage to the foe's Lead". */
  effect: string;
}

/** Every synergy a line-up has, in type order — what a screen lists. */
export function activeSynergies(lineUp: readonly Combatant[]): ActiveSynergy[] {
  const counts = countTypes(lineUp);
  const active: ActiveSynergy[] = [];
  for (const type of POKEMON_TYPES) {
    const count = counts[type];
    if (count > 0) {
      active.push({
        type,
        count,
        title: `${DISPLAY_NAMES[type]} x${count}`,
        effect: effectAtCount(type, count),
      });
    }
  }
  return active;
}

/**
 * Exactly what this synergy does at this count — the resolved numbers, not the per-mon rule, so
 * a screen can show a player what their own team is getting. Reads the same constants `apply`
 * does, so it can't drift from what actually happens.
 */
export function effectAtCount(type: PokemonType, count: number): string {
  switch (type) {
    case 'Normal':
      return `+${count * NORMAL_HEALTH_PER_TYPE} Health to every mon`;
    case 'Fire':
      return `${count * FIRE_DAMAGE_PER_TYPE} damage to the foe's Lead as the fight opens`;
    case 'Water':
      return `+${count * WATER_SHIELD_PER_TYPE} shield to the front ${count}`;
    case 'Electric':
      return `+${count * ELECTRIC_LEAD_CHARGE_PER_TYPE} starting charge on the Lead (max ${CHARGE_THRESHOLD})`;
    case 'Grass':
      return `+${count * GRASS_LIFESTEAL_PERCENT_PER_TYPE}% lifesteal to every mon`;
    case 'Ice':
      return `-${count * ICE_CHARGE_PENALTY_PER_TYPE} starting charge on the foe's Lead and Support`;
    case 'Fighting':
      return `+${count * FIGHTING_ATTACK_PER_TYPE} Attack to every mon`;
    case 'Poison':
      return `the foe's Lead opens Poisoned, ${count * POISON_TICK_PER_TYPE} a tick`;
    case 'Ground':
      return `-${count * GROUND_LEAD_CHARGE_PENALTY_PER_TYPE} starting charge on the foe's Lead`;
    case 'Flying':
      return Math.trunc(count / FLYING_TYPES_PER_SPEED) > 0
        ? `+${Math.trunc(count / FLYING_TYPES_PER_SPEED)} Speed to every mon (max ${FLYING_SPEED_CAP})`
        : `+1 Speed to every mon at ${FLYING_TYPES_PER_SPEED} Flying`;
    case 'Psychic':
      return `+${count * PSYCHIC_CHARGE_PER_TYPE} starting charge on the Lead and Support`;
    case 'Bug':
      return Math.trunc(count / BUG_TYPES_PER_SPEED) > 0
        ? `+${Math.trunc(count / BUG_TYPES_PER_SPEED)} Speed to every mon`
        : `+1 Speed to every mon at ${BUG_TYPES_PER_SPEED} Bug`;
    case 'Rock':
      return `+${count * ROCK_LEAD_HEALTH_PERCENT_PER_TYPE}% Health on the Lead`;
    case 'Ghost':
      return `the Lead gives up ${count * GHOST_LEAD_HP_COST_PER_TYPE} HP; every other mon +${count * GHOST_TEAM_BOOST_PER_TYPE} Attack and Health`;
    case 'Dragon':
      return `-${count * DRAGON_ENEMY_ATTACK_PER_TYPE} Attack on every enemy mon`;
    case 'Dark':
      return `${count * DARK_DAMAGE_PER_TYPE} true damage to the foe's Lead as the fight opens`;
    case 'Steel':
      return `+${count * STEEL_LEAD_REDUCTION_PER_TYPE} damage reduction on the Lead`;
    case 'Fairy':
      return `the back ${count * FAIRY_WARDED_MONS_PER_TYPE} each shrug off one status`;
  }
}

// --- the opening -----------------------------------------------------------------------------

/**
 * Applies both sides' synergies to a battle that hasn't started, and returns what happened,
 * stamped Step 0.
 *
 * The order is fixed so the result is deterministic and readable: announcements, then each
 * side's own stat bonuses, then Intimidate on the other side, then defences, then charge, then
 * the opening blows — and finally a faint check, so an opening that KOs a Lead promotes the next
 * mon before Step 1's exchange, and one that empties a side ends the battle with no Step taken.
 *
 * Mutates the state it's given; the runners own the cloning.
 */
export function applyOpeningMutable(
  state: BattleState,
  charge: ChargeConfig = DEFAULT_CHARGE_CONFIG,
): StepEvent[] {
  const events: StepEvent[] = [];
  const countsA = countTypes(state.lineUpA);
  const countsB = countTypes(state.lineUpB);

  announce(state.lineUpA, 'A', countsA, events);
  announce(state.lineUpB, 'B', countsB, events);

  applyOwnStats(state.lineUpA, countsA);
  applyOwnStats(state.lineUpB, countsB);
  applyIntimidate(state.lineUpB, countsA.Dragon);
  applyIntimidate(state.lineUpA, countsB.Dragon);

  applyDefenses(state.lineUpA, countsA);
  applyDefenses(state.lineUpB, countsB);

  applyOwnCharge(state.lineUpA, countsA, charge.threshold);
  applyOwnCharge(state.lineUpB, countsB, charge.threshold);
  applyEnemyCharge(state.lineUpB, countsA);
  applyEnemyCharge(state.lineUpA, countsB);

  applyOpeningBlows(state, 'A', countsA, events);
  applyOpeningBlows(state, 'B', countsB, events);

  resolveFaintsAndPromotions(state, 'A', events, 0);
  resolveFaintsAndPromotions(state, 'B', events, 0);

  return events;
}

function announce(
  lineUp: readonly Combatant[],
  side: Side,
  counts: TypeCounts,
  events: StepEvent[],
): void {
  // Ascending type order, matching the C# enum's declaration order. Part of the observable
  // event stream, not an internal detail.
  for (const type of POKEMON_TYPES) {
    if (counts[type] > 0) {
      events.push({
        step: 0,
        kind: 'TypeSynergy',
        sourceSide: side,
        sourceInstanceId: lineUp[0]?.instanceId,
        amount: counts[type],
        synergyType: type,
      });
    }
  }
}

/** Health added to both the ceiling and the current value, so it reads as a real gain. */
function addHealth(mon: Combatant, amount: number): void {
  mon.currentStats.health += amount;
  mon.currentHP += amount;
}

function addCharge(mon: Combatant, amount: number, threshold: number): void {
  if (amount > 0) {
    mon.charge = Math.min(threshold, mon.charge + amount);
  }
}

function applyOwnStats(lineUp: Combatant[], counts: TypeCounts): void {
  const lead = lineUp[0];
  if (lead === undefined) return;

  const normal = counts.Normal;
  const fighting = counts.Fighting;
  const flyingSpeed = Math.trunc(counts.Flying / FLYING_TYPES_PER_SPEED);
  const bugSpeed = Math.trunc(counts.Bug / BUG_TYPES_PER_SPEED);

  for (const mon of lineUp) {
    addHealth(mon, normal * NORMAL_HEALTH_PER_TYPE);
    mon.currentStats.attack += fighting * FIGHTING_ATTACK_PER_TYPE;
    if (flyingSpeed > 0) {
      // Raises toward the cap, but never lowers a mon already past it.
      mon.currentStats.speed = Math.max(
        mon.currentStats.speed,
        Math.min(FLYING_SPEED_CAP, mon.currentStats.speed + flyingSpeed),
      );
    }
    mon.currentStats.speed += bugSpeed;
  }

  const rock = counts.Rock;
  if (rock > 0) {
    // Integer division matching C#, with +50 for round-half-up. At least one point per Rock-type,
    // since 10% of a tier-1 mon rounds to nothing.
    const percentBonus = Math.trunc(
      (lead.currentStats.health * ROCK_LEAD_HEALTH_PERCENT_PER_TYPE * rock + 50) / 100,
    );
    addHealth(lead, Math.max(rock, percentBonus));
  }

  const ghost = counts.Ghost;
  if (ghost > 0 && lineUp.length > 1) {
    lead.currentHP = Math.max(1, lead.currentHP - ghost * GHOST_LEAD_HP_COST_PER_TYPE);
    for (let i = 1; i < lineUp.length; i++) {
      const mon = lineUp[i]!;
      mon.currentStats.attack += ghost * GHOST_TEAM_BOOST_PER_TYPE;
      addHealth(mon, ghost * GHOST_TEAM_BOOST_PER_TYPE);
    }
  }
}

function applyIntimidate(enemies: Combatant[], dragon: number): void {
  if (dragon <= 0) return;
  for (const mon of enemies) {
    const attack = mon.currentStats.attack;
    // Floored at 1 — but a 0-Attack mon isn't lifted to 1 by being intimidated.
    mon.currentStats.attack = Math.max(
      Math.min(attack, 1),
      attack - dragon * DRAGON_ENEMY_ATTACK_PER_TYPE,
    );
  }
}

function applyDefenses(lineUp: Combatant[], counts: TypeCounts): void {
  const lead = lineUp[0];
  if (lead === undefined) return;

  const water = counts.Water;
  const shielded = Math.min(lineUp.length, water * WATER_SHIELD_PER_TYPE);
  for (let i = 0; i < shielded; i++) {
    lineUp[i]!.shield += water * WATER_SHIELD_PER_TYPE;
  }

  lead.damageReductionFlat += counts.Steel * STEEL_LEAD_REDUCTION_PER_TYPE;

  const grass = counts.Grass;
  if (grass > 0) {
    for (const mon of lineUp) {
      mon.lifestealPercent = Math.min(
        MAX_LIFESTEAL_PERCENT,
        mon.lifestealPercent + (grass * GRASS_LIFESTEAL_PERCENT_PER_TYPE) / 100,
      );
    }
  }

  // Wards go to the *back* of the line-up: the mons who'll arrive later, fresh.
  const warded = counts.Fairy * FAIRY_WARDED_MONS_PER_TYPE;
  for (let i = lineUp.length - 1; i >= 0 && i >= lineUp.length - warded; i--) {
    lineUp[i]!.statusWards++;
  }
}

function applyOwnCharge(lineUp: Combatant[], counts: TypeCounts, threshold: number): void {
  const lead = lineUp[0];
  if (lead === undefined) return;

  addCharge(lead, counts.Electric * ELECTRIC_LEAD_CHARGE_PER_TYPE, threshold);
  const psychic = counts.Psychic * PSYCHIC_CHARGE_PER_TYPE;
  addCharge(lead, psychic, threshold);
  const support = lineUp[1];
  if (support !== undefined) addCharge(support, psychic, threshold);
}

function applyEnemyCharge(enemies: Combatant[], counts: TypeCounts): void {
  const lead = enemies[0];
  if (lead === undefined) return;

  // A deficit rather than a rate multiplier: accrual truncates Speed x multiplier to an int, so
  // at Speed 1 any slowdown at all becomes no charge whatsoever. A deficit of D instead delays
  // the first trigger by D / Speed Steps and nothing after.
  const ice = counts.Ice * ICE_CHARGE_PENALTY_PER_TYPE;
  lead.charge -= ice + counts.Ground * GROUND_LEAD_CHARGE_PENALTY_PER_TYPE;
  const support = enemies[1];
  if (support !== undefined) support.charge -= ice;
}

function applyOpeningBlows(
  state: BattleState,
  side: Side,
  counts: TypeCounts,
  events: StepEvent[],
): void {
  const own = lineUpOf(state, side);
  const enemySide: Side = side === 'A' ? 'B' : 'A';
  const enemies = lineUpOf(state, enemySide);

  const source = own[0];
  const enemyLead = enemies[0];
  if (source === undefined || enemyLead === undefined) return;

  const fire = counts.Fire * FIRE_DAMAGE_PER_TYPE;
  if (fire > 0 && isAlive(enemyLead)) {
    // An attack in all but name: reduction and shield blunt it. Never lifesteal, though — the
    // opening isn't the Lead's own blow.
    const { hpDamage, shieldAbsorbed } = takeHit(enemyLead, fire);
    pushDamage(events, side, source, enemySide, enemyLead, hpDamage, shieldAbsorbed);
  }

  const dark = counts.Dark * DARK_DAMAGE_PER_TYPE;
  if (dark > 0 && isAlive(enemyLead)) {
    enemyLead.currentHP -= dark;
    pushDamage(events, side, source, enemySide, enemyLead, dark, 0);
  }

  const poison = counts.Poison * POISON_TICK_PER_TYPE;
  if (poison > 0 && isAlive(enemyLead)) {
    applyStatus(enemyLead, enemySide, 'Poisoned', poison, source, side, events, 0);
  }
}

function pushDamage(
  events: StepEvent[],
  side: Side,
  source: Combatant,
  targetSide: Side,
  target: Combatant,
  hpDamage: number,
  absorbed: number,
): void {
  events.push({
    step: 0,
    kind: 'Damage',
    sourceSide: side,
    sourceInstanceId: source.instanceId,
    targetSide,
    targetInstanceId: target.instanceId,
    amount: hpDamage,
  });
  if (absorbed > 0) {
    events.push({
      step: 0,
      kind: 'ShieldAbsorbed',
      sourceSide: targetSide,
      sourceInstanceId: target.instanceId,
      targetSide,
      targetInstanceId: target.instanceId,
      amount: absorbed,
    });
  }
}
