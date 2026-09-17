/**
 * Display state — what the board looks like *partway through* a Step.
 *
 * The simulator hands back the state after a whole Step, but a Step is several beats and the
 * point of animating is to show them in order. So the renderer keeps its own copy of the board and
 * walks the event log forward one event at a time, applying each event's visible consequence.
 *
 * Two rules keep this from drifting out of sync with the real simulation:
 *
 * 1. This is **display only**. Nothing here feeds back into the sim. A bug here makes the
 *    animation wrong, never the fight.
 * 2. At the end of every Step the renderer **resyncs to the authoritative post-Step state**
 *    (`syncTo` below). Anything this missed is corrected at the boundary rather than accumulating.
 *
 * Charge is the one thing not driven by an event at all — accrual is silent, so `chargePlan`
 * derives the arcs' movement from the boards either side of the Step instead.
 */

import type { BattleState, Combatant, StepEvent } from '../sim/index.js';
import { cloneBattleState, CHARGE_THRESHOLD } from '../sim/index.js';

/** Transient flourishes a mon is showing right now, cleared as the Step moves on. */
export interface MonFlash {
  /** Damage number floating off the mon. */
  damage?: number;
  /** Healing number. */
  heal?: number;
  /** The mon is mid-lunge for its attack. */
  attacking?: boolean;
  /** Its passive just fired. */
  firing?: boolean;
  /** Shield just absorbed a blow. */
  absorbed?: number;
  /** Its arc has just landed full and its passive is about to fire. */
  charged?: boolean;
}

export interface DisplayState {
  board: BattleState;
  /** Keyed by instanceId. */
  flashes: Record<string, MonFlash>;
  /** Mons that have fallen and should be drawn fading out rather than removed instantly. */
  fainting: string[];
  /** The most recent event, for a caption line under the field. */
  lastEvent: StepEvent | null;
}

export function initialDisplay(board: BattleState): DisplayState {
  return { board: cloneBattleState(board), flashes: {}, fainting: [], lastEvent: null };
}

function find(board: BattleState, instanceId: string | undefined): Combatant | null {
  if (instanceId === undefined) return null;
  return (
    board.lineUpA.find((c) => c.instanceId === instanceId) ??
    board.lineUpB.find((c) => c.instanceId === instanceId) ??
    null
  );
}

/** A copy of `display` with `event` applied. Never mutates the input. */
export function applyEvent(display: DisplayState, event: StepEvent): DisplayState {
  const board = cloneBattleState(display.board);
  const flashes: Record<string, MonFlash> = {};
  const fainting = [...display.fainting];

  const target = find(board, event.targetInstanceId);
  const source = find(board, event.sourceInstanceId);

  const flash = (id: string | undefined, f: MonFlash): void => {
    if (id === undefined) return;
    flashes[id] = { ...flashes[id], ...f };
  };

  switch (event.kind) {
    case 'Damage':
      if (target !== null) target.currentHP -= event.amount ?? 0;
      // A 0-amount Damage event is real — the exchange fires even for a 0-Attack Lead — so the
      // lunge plays but no number floats off.
      flash(event.sourceInstanceId, { attacking: true });
      if ((event.amount ?? 0) > 0) flash(event.targetInstanceId, { damage: event.amount });
      break;

    case 'ShieldAbsorbed':
      if (target !== null) target.shield = Math.max(0, target.shield - (event.amount ?? 0));
      flash(event.targetInstanceId, { absorbed: event.amount });
      break;

    case 'Heal':
    case 'LifestealHeal':
      if (target !== null) target.currentHP += event.amount ?? 0;
      flash(event.targetInstanceId, { heal: event.amount });
      break;

    case 'StatusTick':
    case 'SuddenDeath':
      if (target !== null) target.currentHP -= event.amount ?? 0;
      flash(event.targetInstanceId, { damage: event.amount });
      break;

    case 'Shield':
      if (target !== null) target.shield += event.amount ?? 0;
      break;

    case 'StatusApplied':
      if (target !== null && event.status !== undefined) target.status = event.status;
      break;

    case 'StatusCleared':
      if (target !== null) target.status = null;
      break;

    case 'StatusBlocked':
      if (target !== null) target.statusWards = Math.max(0, target.statusWards - 1);
      break;

    case 'BuffAttack':
      if (target !== null) target.currentStats.attack += event.amount ?? 0;
      break;

    case 'BuffSpeed':
      if (target !== null) target.currentStats.speed += event.amount ?? 0;
      break;

    case 'DamageReductionApplied':
      if (target !== null) target.damageReductionFlat += event.amount ?? 0;
      break;

    case 'Lifesteal':
      if (target !== null) {
        target.lifestealPercent = Math.min(1, target.lifestealPercent + (event.amount ?? 0) / 100);
      }
      break;

    case 'PassiveTriggered':
      // Charge empties visibly at the moment the passive fires, which is the whole point of
      // drawing the arc.
      if (source !== null) source.charge = 0;
      flash(event.sourceInstanceId, { firing: true });
      break;

    case 'BallThrown':
      break;

    case 'Faint':
    case 'Caught':
      if (event.sourceInstanceId !== undefined) fainting.push(event.sourceInstanceId);
      break;

    case 'Promotion':
    case 'TypeSynergy':
    case 'BattleEnd':
    case 'ChargeRateModified':
      break;
  }

  return { board, flashes, fainting, lastEvent: event };
}

/**
 * Snap to the authoritative post-Step board and clear the transient flourishes.
 *
 * Called at every Step boundary, so anything the per-event walk got wrong — including the
 * fractional charge the fill leaves the arcs sitting at — is corrected rather than carried forward.
 */
export function syncTo(board: BattleState): DisplayState {
  return { board: cloneBattleState(board), flashes: {}, fainting: [], lastEvent: null };
}

// --- charge ----------------------------------------------------------------------------------

/** Where each mon's arc starts and ends over one Step, and who lands full. */
export interface ChargePlan {
  /** Charge before the Step, keyed by instanceId. */
  from: Record<string, number>;
  /** Charge to fill to, keyed by instanceId. Absent for a mon that leaves the field. */
  to: Record<string, number>;
  /** Who reaches full this Step, in the order their passives fire. */
  full: string[];
}

/**
 * What the arcs should do over one Step, read off the boards either side of it.
 *
 * Charge accrual raises no event, so there is nothing for `applyEvent` to walk — which is why the
 * arcs used to sit still through a Step and then jump at its boundary. The arc is the game's clock
 * (Speed does nothing but fill it), and a clock that only moves between Steps doesn't read as one.
 *
 * A mon whose passive fires ends the Step back at 0, so its post-Step charge is no use as a
 * target: it fills to full instead, and the `PassiveTriggered` beat is what empties it.
 */
export function chargePlan(
  before: BattleState,
  after: BattleState,
  events: StepEvent[],
): ChargePlan {
  const full = events
    .filter((e) => e.kind === 'PassiveTriggered' && e.sourceInstanceId !== undefined)
    .map((e) => e.sourceInstanceId!);
  const fired = new Set(full);

  const afterById = new Map<string, Combatant>();
  for (const c of [...after.lineUpA, ...after.lineUpB]) afterById.set(c.instanceId, c);

  const from: Record<string, number> = {};
  const to: Record<string, number> = {};
  for (const c of [...before.lineUpA, ...before.lineUpB]) {
    from[c.instanceId] = c.charge;
    if (fired.has(c.instanceId)) to[c.instanceId] = CHARGE_THRESHOLD;
    else {
      const later = afterById.get(c.instanceId);
      if (later !== undefined) to[c.instanceId] = later.charge;
    }
  }

  return { from, to, full };
}

/**
 * The arcs partway through a fill, `t` running 0 to 1.
 *
 * Deliberately fractional: charge is an integer of at most 3 against a threshold of 3, so rounding
 * the interpolation would put the arc back to jumping in thirds, which is the thing being fixed.
 */
export function chargeAt(plan: ChargePlan, t: number): Record<string, number> {
  const clamped = Math.max(0, Math.min(1, t));
  const charges: Record<string, number> = {};
  for (const [id, to] of Object.entries(plan.to)) {
    const from = plan.from[id] ?? to;
    charges[id] = from + (to - from) * clamped;
  }
  return charges;
}

/** A copy of `display` with the named mons' charge overwritten. Never mutates the input. */
export function withCharges(
  display: DisplayState,
  charges: Record<string, number>,
  flashes: Record<string, MonFlash> = display.flashes,
): DisplayState {
  const board = cloneBattleState(display.board);
  for (const c of [...board.lineUpA, ...board.lineUpB]) {
    const charge = charges[c.instanceId];
    if (charge !== undefined) c.charge = charge;
  }
  return { ...display, board, flashes };
}
