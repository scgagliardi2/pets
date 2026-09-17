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
 *    (`syncTo` below). Anything this missed — charge accrual raises no event, for instance — is
 *    corrected at the boundary rather than accumulating.
 */

import type { BattleState, Combatant, StepEvent } from '../sim/index.js';
import { cloneBattleState } from '../sim/index.js';

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
 * Called at every Step boundary. Charge accrual raises no event, so the arcs only move here — and
 * anything else the per-event walk got wrong is corrected rather than carried forward.
 */
export function syncTo(board: BattleState): DisplayState {
  return { board: cloneBattleState(board), flashes: {}, fainting: [], lastEvent: null };
}
