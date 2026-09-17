/**
 * The one shared damage path. Everything that deals damage goes through here so the mitigation
 * rules can't be implemented twice and drift apart — the synergy opening's Fire burst uses the
 * same `takeHit` the attack exchange does.
 *
 * Order (battle-sim-spec.md §12): flat damage reduction, then shield absorption, then HP.
 * Lifesteal heals off the amount that actually reached HP, not the raw pre-mitigation amount.
 */

import { MINIMUM_ATTACK_DAMAGE } from './config.js';
import type { Combatant, Side, StepEvent } from './types.js';

export interface HitResult {
  hpDamage: number;
  shieldAbsorbed: number;
}

/**
 * The mitigation half of an attack: reduction, then shield, then HP. Raises no events and
 * applies no lifesteal, so callers that aren't attacks (the Fire opening) can share it.
 */
export function takeHit(target: Combatant, rawAmount: number): HitResult {
  // Floored rather than allowed to reach zero: damageReductionFlat accumulates for the whole
  // battle, so without this floor a mon whose reduction has stacked past the other's Attack can
  // never be hurt by it again, and two such mons can never finish a fight. A blow that connects
  // always costs something.
  const afterReduction =
    rawAmount <= 0
      ? 0
      : Math.max(MINIMUM_ATTACK_DAMAGE, rawAmount - target.damageReductionFlat);

  const shieldAbsorbed = Math.min(target.shield, afterReduction);
  target.shield -= shieldAbsorbed;

  const hpDamage = afterReduction - shieldAbsorbed;
  target.currentHP -= hpDamage;

  return { hpDamage, shieldAbsorbed };
}

/**
 * C#'s `Math.Round(x, MidpointRounding.AwayFromZero)`. JavaScript's `Math.round` breaks ties
 * toward +Infinity, which differs for negative values; lifesteal is never negative today, but
 * matching the Unity semantics exactly costs nothing and removes a latent discrepancy.
 */
export function roundHalfAwayFromZero(value: number): number {
  return value < 0 ? -Math.round(-value) : Math.round(value);
}

/** A full attack: mitigation, the events it raises, and the attacker's lifesteal response. */
export function applyDamage(
  target: Combatant,
  targetSide: Side,
  rawAmount: number,
  attacker: Combatant,
  attackerSide: Side,
  events: StepEvent[],
  step: number,
): void {
  const { hpDamage, shieldAbsorbed } = takeHit(target, rawAmount);

  events.push({
    step,
    kind: 'Damage',
    sourceSide: attackerSide,
    sourceInstanceId: attacker.instanceId,
    targetSide,
    targetInstanceId: target.instanceId,
    amount: hpDamage,
  });

  if (shieldAbsorbed > 0) {
    events.push({
      step,
      kind: 'ShieldAbsorbed',
      sourceSide: targetSide,
      sourceInstanceId: target.instanceId,
      targetSide,
      targetInstanceId: target.instanceId,
      amount: shieldAbsorbed,
    });
  }

  if (attacker.lifestealPercent > 0 && hpDamage > 0) {
    const healAmount = roundHalfAwayFromZero(hpDamage * attacker.lifestealPercent);
    const actualHeal = Math.min(healAmount, attacker.currentStats.health - attacker.currentHP);
    if (actualHeal > 0) {
      attacker.currentHP += actualHeal;
      events.push({
        step,
        kind: 'LifestealHeal',
        sourceSide: attackerSide,
        sourceInstanceId: attacker.instanceId,
        targetSide: attackerSide,
        targetInstanceId: attacker.instanceId,
        amount: actualHeal,
      });
    }
  }
}

/** Healing, capped at the target's max HP. Raises no event when it would heal nothing. */
export function applyHeal(
  target: Combatant,
  targetSide: Side,
  amount: number,
  source: Combatant,
  sourceSide: Side,
  events: StepEvent[],
  step: number,
): void {
  const healAmount = Math.min(amount, target.currentStats.health - target.currentHP);
  if (healAmount <= 0) return;

  target.currentHP += healAmount;
  events.push({
    step,
    kind: 'Heal',
    sourceSide: sourceSide,
    sourceInstanceId: source.instanceId,
    targetSide,
    targetInstanceId: target.instanceId,
    amount: healAmount,
  });
}
