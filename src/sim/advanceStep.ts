/**
 * The Step loop. Ported from Unity's `BattleSimulator.cs`; the ordering is specified in
 * `battle-sim-spec.md` §3 and small deviations produce a game that looks the same and plays
 * completely differently.
 *
 * Both runners call `advanceStepMutable` — the Step logic is never duplicated between them.
 * The exported `advanceStep` is the immutable wrapper the UI should use.
 */

import {
  DEFAULT_CHARGE_CONFIG,
  type ChargeConfig,
  DEFAULT_STEP_DURATION_MS,
  PARALYZED_CHARGE_MULTIPLIER,
  SUDDEN_DEATH_DAMAGE_PER_STEP,
  SUDDEN_DEATH_STEP,
  MAX_LIFESTEAL_PERCENT,
} from './config.js';
import { applyDamage, applyHeal } from './damage.js';
import type { Rng } from './rng.js';
import {
  cloneBattleState,
  isAlive,
  lineUpOf,
  opposing,
  type BattleOutcome,
  type BattleState,
  type Combatant,
  type EffectDefinition,
  type Side,
  type StatusType,
  type StepEvent,
  type TargetSelector,
} from './types.js';

/**
 * Advances exactly one Step, leaving the caller's state untouched.
 *
 * The clone is what makes replay and React state updates work (REACT_REBUILD_REFERENCE.md §5.1).
 * Line-ups are at most a handful of mons, so the copy is far cheaper than the bugs it prevents.
 */
export function advanceStep(
  state: BattleState,
  rng: Rng,
  charge: ChargeConfig = DEFAULT_CHARGE_CONFIG,
): { state: BattleState; events: StepEvent[] } {
  const next = cloneBattleState(state);
  const events = advanceStepMutable(next, rng, charge);
  return { state: next, events };
}

/**
 * The in-place Step. Mutates `state` exactly as the Unity implementation does, which is what
 * keeps the two byte-comparable against the shared golden fixtures.
 */
export function advanceStepMutable(
  state: BattleState,
  _rng: Rng,
  charge: ChargeConfig = DEFAULT_CHARGE_CONFIG,
): StepEvent[] {
  state.stepNumber++;
  const step = state.stepNumber;
  const events: StepEvent[] = [];

  // Captured once at the top of the Step. Promotion happens only in beat 4, so these references
  // stay stable for the whole Step even if a mon's HP drops to 0 in beat 1 or 3 — the spec
  // defers all faint handling to one place, since a passive can itself cause a faint.
  const leadA = state.lineUpA[0] ?? null;
  const supportA = state.lineUpA[1] ?? null;
  const leadB = state.lineUpB[0] ?? null;
  const supportB = state.lineUpB[1] ?? null;

  // --- 1. Attack exchange ------------------------------------------------------------------
  // Simultaneous, flat attack-vs-attack, gated only by both Leads existing. Speed plays no part.
  if (leadA !== null && leadB !== null) {
    const dmgToB = leadA.currentStats.attack;
    const dmgToA = leadB.currentStats.attack;
    applyDamage(leadB, 'B', dmgToB, leadA, 'A', events, step);
    applyDamage(leadA, 'A', dmgToA, leadB, 'B', events, step);
  }

  // --- 2. Charge accumulation --------------------------------------------------------------
  // All currently-active mons, regardless of HP having dropped to 0 in beat 1.
  accrueCharge(leadA, 'A', events, step, charge.perSpeedPoint);
  accrueCharge(supportA, 'A', events, step, charge.perSpeedPoint);
  accrueCharge(leadB, 'B', events, step, charge.perSpeedPoint);
  accrueCharge(supportB, 'B', events, step, charge.perSpeedPoint);

  // --- 3. Passive resolution ---------------------------------------------------------------
  // The trigger set is fixed from this Step's charge values and does not grow mid-resolution,
  // even if an earlier passive speeds up a later mon's charge rate.
  const triggering: Triggerer[] = [];
  addIfTriggering(leadA, 'A', 0, triggering, charge.threshold);
  addIfTriggering(supportA, 'A', 1, triggering, charge.threshold);
  addIfTriggering(leadB, 'B', 0, triggering, charge.threshold);
  addIfTriggering(supportB, 'B', 1, triggering, charge.threshold);
  triggering.sort(compareTriggerOrder);

  for (const t of triggering) {
    // A fast mon can bank several thresholds in one Step and fires once per threshold, with the
    // remainder carried rather than discarded. At 100 Speed that is three activations per
    // attack, which is what the charge scale is calibrated to; discarding the overshoot instead
    // would silently cap every mon at one activation and make Speed above the threshold worthless.
    const activations = Math.floor(t.combatant.charge / charge.threshold);
    t.combatant.charge -= activations * charge.threshold;

    for (let n = 0; n < activations; n++) {
      events.push({
        step,
        kind: 'PassiveTriggered',
        sourceSide: t.side,
        sourceInstanceId: t.combatant.instanceId,
      });
      if (t.combatant.passive !== null) {
        for (const effect of t.combatant.passive.effects) {
          applyEffect(effect, t.combatant, t.side, state, events, step);
        }
      }
    }
  }

  // --- 3.5. Status ticks -------------------------------------------------------------------
  // Bypasses shield and damage reduction, and never procs lifesteal: it's self-inflicted damage,
  // not an attack. A status applied by a passive earlier this Step already ticks this Step.
  applyStatusTick(leadA, 'A', events, step);
  applyStatusTick(supportA, 'A', events, step);
  applyStatusTick(leadB, 'B', events, step);
  applyStatusTick(supportB, 'B', events, step);

  // --- 3.6. Sudden death -------------------------------------------------------------------
  // Escalating true damage to both Leads once a fight plainly isn't resolving itself. Bypasses
  // everything for the same reason a status tick does: it isn't an attack, it's the clock
  // running out, and anything that could mitigate it is exactly what caused the stalemate.
  applySuddenDeath(leadA, 'A', events, step);
  applySuddenDeath(leadB, 'B', events, step);

  // --- 3.7. Held items -----------------------------------------------------------------------
  // After all damage for the Step, before anyone is removed: regeneration should be able to save
  // a mon that would otherwise fall, and a below-half heal should read the health it actually
  // ends the Step on rather than a value some later beat undoes.
  applyItemUpkeep(leadA, 'A', events, step);
  applyItemUpkeep(supportA, 'A', events, step);
  applyItemUpkeep(leadB, 'B', events, step);
  applyItemUpkeep(supportB, 'B', events, step);

  // --- 4. Faint check and promotion --------------------------------------------------------
  // The only point removal and promotion happen, batching every faint this Step caused.
  resolveFaintsAndPromotions(state, 'A', events, step);
  resolveFaintsAndPromotions(state, 'B', events, step);

  return events;
}

// --- trigger ordering ------------------------------------------------------------------------

interface Triggerer {
  combatant: Combatant;
  side: Side;
  /** 0 = Lead, 1 = Support. */
  role: number;
}

function addIfTriggering(
  combatant: Combatant | null,
  side: Side,
  role: number,
  list: Triggerer[],
  threshold: number,
): void {
  if (combatant !== null && combatant.charge >= threshold) {
    list.push({ combatant, side, role });
  }
}

/**
 * Same-Step tie-break (battle-sim-spec.md §6, flagged there as provisional pending design
 * confirmation): attacking Leads before waiting Supports, then higher Speed first, then Side A
 * before Side B. `same-step-tiebreak-shield.json` locks this in with a fixture that fails under
 * the wrong order, so changing it means changing the fixture and the spec together.
 */
function compareTriggerOrder(x: Triggerer, y: Triggerer): number {
  if (x.role !== y.role) return x.role - y.role;
  if (x.combatant.currentStats.speed !== y.combatant.currentStats.speed) {
    return y.combatant.currentStats.speed - x.combatant.currentStats.speed;
  }
  return x.side === y.side ? 0 : x.side === 'A' ? -1 : 1;
}

// --- beats -----------------------------------------------------------------------------------

/**
 * Accrues a mon's charge and says so.
 *
 * The event exists for the renderer. Charge used to change silently, which meant the ability arcs
 * could only be redrawn at a Step boundary — they snapped from one value to the next instead of
 * filling as the Step played, and the moment a bar reached full was invisible. Emitting it makes
 * charge part of the same ordered stream as everything else the player watches.
 *
 * Nothing in the simulation reads the event, so the two implementations still agree on outcomes;
 * it is additive to the stream and no fixture asserts on stream contents.
 */
function accrueCharge(
  combatant: Combatant | null,
  side: Side,
  events: StepEvent[],
  step: number,
  perSpeedPoint: number,
): void {
  if (combatant === null) return;
  if (combatant.status === 'Asleep') return;

  let multiplier = combatant.chargeRateMultiplier;
  if (combatant.status === 'Paralyzed') {
    multiplier *= PARALYZED_CHARGE_MULTIPLIER;
  }
  // Truncated toward zero, matching the C# int cast. This is why Ice and Ground are a starting
  // charge deficit rather than a rate multiplier: at Speed 1 any slowdown truncates to zero.
  const gained = Math.trunc(
    combatant.currentStats.speed * perSpeedPoint * DEFAULT_STEP_DURATION_MS * multiplier,
  );
  if (gained === 0) return;

  combatant.charge += gained;
  events.push({
    step,
    kind: 'ChargeGained',
    sourceSide: side,
    sourceInstanceId: combatant.instanceId,
    targetSide: side,
    targetInstanceId: combatant.instanceId,
    amount: gained,
  });
}

function applyStatusTick(
  combatant: Combatant | null,
  side: Side,
  events: StepEvent[],
  step: number,
): void {
  if (combatant === null || combatant.status === null) return;

  if (combatant.status === 'Poisoned') {
    const dmg = combatant.statusTickDamage * Math.max(1, combatant.poisonStacks);
    combatant.currentHP -= dmg;
    events.push({
      step,
      kind: 'StatusTick',
      sourceSide: side,
      sourceInstanceId: combatant.instanceId,
      targetSide: side,
      targetInstanceId: combatant.instanceId,
      amount: dmg,
      status: 'Poisoned',
    });
    combatant.poisonStacks++;
  } else if (combatant.status === 'Burned') {
    const dmg = combatant.statusTickDamage;
    combatant.currentHP -= dmg;
    events.push({
      step,
      kind: 'StatusTick',
      sourceSide: side,
      sourceInstanceId: combatant.instanceId,
      targetSide: side,
      targetInstanceId: combatant.instanceId,
      amount: dmg,
      status: 'Burned',
    });
  }
  // Paralyzed and Asleep have no tick damage; their whole effect is on charge accrual.
}

/**
 * A held item's end-of-Step behaviour: regeneration, then the one-shot below-half heal.
 *
 * Both are capped at the holder's maximum, and neither fires on a mon already at zero — an item
 * pulling someone back from a fatal blow in the same Step it landed would make the faint rule
 * ambiguous, and "heals when low" is a cushion, not a revival.
 */
function applyItemUpkeep(
  combatant: Combatant | null,
  side: Side,
  events: StepEvent[],
  step: number,
): void {
  if (combatant === null || combatant.heldItem === null) return;
  if (combatant.currentHP <= 0) return;

  const max = combatant.currentStats.health;
  const heal = (amount: number): void => {
    const actual = Math.min(amount, max - combatant.currentHP);
    if (actual <= 0) return;
    combatant.currentHP += actual;
    events.push({
      step,
      kind: 'Heal',
      sourceSide: side,
      sourceInstanceId: combatant.instanceId,
      targetSide: side,
      targetInstanceId: combatant.instanceId,
      amount: actual,
    });
  };

  const regen = combatant.heldItem.regenPerStep ?? 0;
  if (regen > 0) heal(regen);

  const lastStand = combatant.heldItem.healBelowHalf ?? 0;
  if (lastStand > 0 && !combatant.usedLastStand && combatant.currentHP * 2 < max) {
    combatant.usedLastStand = true;
    heal(lastStand);
  }
}

/**
 * How much true damage sudden death deals on a given Step: nothing before SUDDEN_DEATH_STEP,
 * then one increment more every Step after. Exported so a test can state the schedule rather
 * than restate the arithmetic.
 */
export function suddenDeathDamageAt(step: number): number {
  const stepsIn = step - SUDDEN_DEATH_STEP + 1;
  return stepsIn <= 0 ? 0 : stepsIn * SUDDEN_DEATH_DAMAGE_PER_STEP;
}

/**
 * Only the Leads, never the Supports: the Supports are dormant, and a Step that cleared the
 * whole board at once would take the fight's result out of the player's hands.
 */
function applySuddenDeath(
  combatant: Combatant | null,
  side: Side,
  events: StepEvent[],
  step: number,
): void {
  const damage = suddenDeathDamageAt(step);
  if (combatant === null || damage <= 0) return;

  combatant.currentHP -= damage;
  events.push({
    step,
    kind: 'SuddenDeath',
    sourceSide: side,
    sourceInstanceId: combatant.instanceId,
    targetSide: side,
    targetInstanceId: combatant.instanceId,
    amount: damage,
  });
}

/**
 * Removes anyone at or below 0 HP and promotes behind them. Exported because the synergy opening
 * runs it too: an opening that KOs a Lead must promote before Step 1's exchange, rather than
 * leaving a fainted Lead to swing.
 */
export function resolveFaintsAndPromotions(
  state: BattleState,
  side: Side,
  events: StepEvent[],
  step: number,
): void {
  const lineUp = lineUpOf(state, side);
  const oldLead = lineUp[0] ?? null;
  const oldSupport = lineUp[1] ?? null;

  // Support first, so the Lead's index is still valid when it is removed.
  if (oldSupport !== null && oldSupport.currentHP <= 0) {
    removeFrom(lineUp, oldSupport);
    events.push({
      step,
      kind: 'Faint',
      sourceSide: side,
      sourceInstanceId: oldSupport.instanceId,
    });
  }
  if (oldLead !== null && oldLead.currentHP <= 0) {
    removeFrom(lineUp, oldLead);
    events.push({
      step,
      kind: 'Faint',
      sourceSide: side,
      sourceInstanceId: oldLead.instanceId,
    });
  }

  const newLead = lineUp[0] ?? null;
  const newSupport = lineUp[1] ?? null;

  if (newLead !== null && newLead !== oldLead) {
    events.push({
      step,
      kind: 'Promotion',
      sourceSide: side,
      sourceInstanceId: newLead.instanceId,
    });
  }
  if (newSupport !== null && newSupport !== oldSupport) {
    events.push({
      step,
      kind: 'Promotion',
      sourceSide: side,
      sourceInstanceId: newSupport.instanceId,
    });
  }
}

function removeFrom(lineUp: Combatant[], combatant: Combatant): void {
  const index = lineUp.indexOf(combatant);
  if (index >= 0) lineUp.splice(index, 1);
}

// --- catching --------------------------------------------------------------------------------

/**
 * Takes a caught mon out of the fight at a Step boundary: the Support steps up, or the fight
 * ends if that was the last mon.
 *
 * Lives here rather than in the catching layer because line-up mutation and the promotions that
 * follow are the simulator's rules, and a second place that edits a line-up is how two copies of
 * those rules drift apart. The simulator still has no concept of a ball or a catch roll — it is
 * told a combatant is leaving and applies the same consequences a faint would, differing only in
 * raising a `Caught` event.
 *
 * That distinction matters downstream: the post-fight "pick one from defeated" path reads `Faint`
 * events, and a caught mon is already in the Box.
 *
 * No-ops on a combatant that isn't in the line-up, so a stale throw can't remove someone twice.
 */
export function removeCaught(
  state: BattleState,
  side: Side,
  targetInstanceId: string,
): { state: BattleState; events: StepEvent[] } {
  const next = cloneBattleState(state);
  const events: StepEvent[] = [];
  const lineUp = lineUpOf(next, side);

  const target = lineUp.find((c) => c.instanceId === targetInstanceId);
  if (target === undefined) return { state: next, events };

  const oldLead = lineUp[0] ?? null;
  const oldSupport = lineUp[1] ?? null;

  removeFrom(lineUp, target);
  events.push({
    step: next.stepNumber,
    kind: 'Caught',
    sourceSide: side,
    sourceInstanceId: target.instanceId,
  });

  const newLead = lineUp[0] ?? null;
  const newSupport = lineUp[1] ?? null;
  if (newLead !== null && newLead !== oldLead) {
    events.push({
      step: next.stepNumber,
      kind: 'Promotion',
      sourceSide: side,
      sourceInstanceId: newLead.instanceId,
    });
  }
  if (newSupport !== null && newSupport !== oldSupport) {
    events.push({
      step: next.stepNumber,
      kind: 'Promotion',
      sourceSide: side,
      sourceInstanceId: newSupport.instanceId,
    });
  }

  return { state: next, events };
}

// --- battle end ------------------------------------------------------------------------------

/** Battle-end condition (battle-sim-spec.md §9). */
export function determineOutcome(state: BattleState): BattleOutcome {
  const aEmpty = state.lineUpA.length === 0;
  const bEmpty = state.lineUpB.length === 0;
  if (aEmpty && bEmpty) return 'Draw';
  if (aEmpty) return 'SideBWins';
  if (bEmpty) return 'SideAWins';
  return 'Draw';
}

// --- effects ---------------------------------------------------------------------------------

function applyEffect(
  effect: EffectDefinition,
  self: Combatant,
  selfSide: Side,
  state: BattleState,
  events: StepEvent[],
  step: number,
): void {
  const resolved = resolveTarget(effect.target, self, selfSide, state);
  if (resolved === null) return;
  const { target, side: targetSide } = resolved;

  // Read at the moment the ability fires, not when the passive was authored, so a Special buffed
  // mid-battle is worth more on the next trigger.
  const amount = effect.scalesWithSpecial === true ? self.currentStats.special : effect.amount;

  switch (effect.type) {
    case 'DealDamage':
      applyDamage(target, targetSide, amount, self, selfSide, events, step);
      break;

    case 'Heal':
      applyHeal(target, targetSide, amount, self, selfSide, events, step);
      break;

    case 'Shield':
      target.shield += amount;
      events.push(effectEvent('Shield', self, selfSide, target, targetSide, step, amount));
      break;

    case 'ApplyStatus':
      if (effect.status !== undefined) {
        applyStatus(target, targetSide, effect.status, amount, self, selfSide, events, step);
      }
      break;

    case 'ClearStatus':
      clearStatus(target, targetSide, self, selfSide, events, step);
      break;

    case 'BuffAttack':
      target.currentStats.attack += amount;
      events.push(
        effectEvent('BuffAttack', self, selfSide, target, targetSide, step, amount),
      );
      break;

    case 'BuffSpeed':
      target.currentStats.speed += amount;
      events.push(
        effectEvent('BuffSpeed', self, selfSide, target, targetSide, step, amount),
      );
      break;

    case 'ModifyChargeRate':
      // Amount is a percentage delta (50 => x1.5, -50 => x0.5), floored at 0.
      target.chargeRateMultiplier = Math.max(
        0,
        target.chargeRateMultiplier + amount / 100,
      );
      events.push(
        effectEvent('ChargeRateModified', self, selfSide, target, targetSide, step, amount),
      );
      break;

    case 'DamageReduction':
      target.damageReductionFlat += amount;
      events.push(
        effectEvent(
          'DamageReductionApplied',
          self,
          selfSide,
          target,
          targetSide,
          step,
          amount,
        ),
      );
      break;

    case 'Lifesteal':
      // Accumulates across triggers, but capped: draining back more than the blow took is
      // meaningless, and 100%+ is self-sustaining forever.
      target.lifestealPercent = Math.min(
        MAX_LIFESTEAL_PERCENT,
        target.lifestealPercent + amount / 100,
      );
      events.push(
        effectEvent('Lifesteal', self, selfSide, target, targetSide, step, amount),
      );
      break;
  }
}

function effectEvent(
  kind: StepEvent['kind'],
  self: Combatant,
  selfSide: Side,
  target: Combatant,
  targetSide: Side,
  step: number,
  amount: number,
): StepEvent {
  return {
    step,
    kind,
    sourceSide: selfSide,
    sourceInstanceId: self.instanceId,
    targetSide,
    targetInstanceId: target.instanceId,
    amount,
  };
}

/** Every selector treats a target at 0 HP or below as invalid, and the effect is skipped. */
function resolveTarget(
  selector: TargetSelector,
  self: Combatant,
  selfSide: Side,
  state: BattleState,
): { target: Combatant; side: Side } | null {
  const own = lineUpOf(state, selfSide);
  const enemySide = opposing(selfSide);
  const enemies = lineUpOf(state, enemySide);

  let candidate: Combatant | null = null;
  let side: Side = selfSide;

  switch (selector) {
    case 'Self':
      candidate = self;
      break;
    case 'Ally':
      // The other currently-active mon on this side.
      candidate = (self === own[0] ? own[1] : own[0]) ?? null;
      break;
    case 'EnemyLead':
      candidate = enemies[0] ?? null;
      side = enemySide;
      break;
    case 'EnemySupport':
      candidate = enemies[1] ?? null;
      side = enemySide;
      break;
  }

  if (candidate === null || !isAlive(candidate)) return null;
  return { target: candidate, side };
}

/**
 * Sets a status, overwriting any existing one — only one applies at a time.
 *
 * Exported because the synergy opening's Poison Sting applies through it, so the ward check
 * can't be implemented twice.
 */
export function applyStatus(
  target: Combatant,
  targetSide: Side,
  status: StatusType,
  amount: number,
  source: Combatant,
  sourceSide: Side,
  events: StepEvent[],
  step: number,
): void {
  // A Fairy ward spends itself on the application and leaves any existing status untouched.
  if (target.statusWards > 0) {
    target.statusWards--;
    events.push({
      step,
      kind: 'StatusBlocked',
      sourceSide,
      sourceInstanceId: source.instanceId,
      targetSide,
      targetInstanceId: target.instanceId,
      status,
    });
    return;
  }

  // A cure item spends itself the instant a status lands, so the status never actually takes
  // hold — checked after the Fairy ward, which is free and should be used up first.
  if (target.heldItem?.curesStatus === true && !target.usedStatusCure) {
    target.usedStatusCure = true;
    events.push({
      step,
      kind: 'StatusCleared',
      sourceSide: targetSide,
      sourceInstanceId: target.instanceId,
      targetSide,
      targetInstanceId: target.instanceId,
      status,
    });
    return;
  }

  target.status = status;
  target.statusTickDamage = status === 'Poisoned' || status === 'Burned' ? amount : 0;
  // Re-applying poison resets severity to 1 rather than stacking with the previous application.
  target.poisonStacks = status === 'Poisoned' ? 1 : 0;

  events.push({
    step,
    kind: 'StatusApplied',
    sourceSide,
    sourceInstanceId: source.instanceId,
    targetSide,
    targetInstanceId: target.instanceId,
    status,
  });
}

function clearStatus(
  target: Combatant,
  targetSide: Side,
  source: Combatant,
  sourceSide: Side,
  events: StepEvent[],
  step: number,
): void {
  if (target.status === null) return;

  const cleared = target.status;
  target.status = null;
  target.statusTickDamage = 0;
  target.poisonStacks = 0;

  events.push({
    step,
    kind: 'StatusCleared',
    sourceSide,
    sourceInstanceId: source.instanceId,
    targetSide,
    targetInstanceId: target.instanceId,
    status: cleared,
  });
}
