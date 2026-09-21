/**
 * Core battle-simulation data shapes.
 *
 * Ported from the Unity `Pets.Simulation` namespace (BattleCombatant.cs, BattleState.cs,
 * StepEvent.cs, and the small enum files). Behaviour is specified by `docs/battle-sim-spec.md`
 * in the Unity repo; this module is the TypeScript expression of the same contract.
 *
 * Nothing in /src/sim may import React. See REACT_REBUILD_REFERENCE.md §5.1.
 */

/** The 18 types, in the Unity enum's declaration order. */
export const POKEMON_TYPES = [
  'Normal',
  'Fire',
  'Water',
  'Electric',
  'Grass',
  'Ice',
  'Fighting',
  'Poison',
  'Ground',
  'Flying',
  'Psychic',
  'Bug',
  'Rock',
  'Ghost',
  'Dragon',
  'Dark',
  'Steel',
  'Fairy',
] as const;

export type PokemonType = (typeof POKEMON_TYPES)[number];

/**
 * Ordinal of a type, matching the C# enum's integer value.
 *
 * Load-bearing: TeamSynergy emits its `TypeSynergy` announcement events in ascending type order
 * (battle-sim-spec.md §8), so this ordering is part of the observable event stream, not an
 * internal detail.
 */
export const TYPE_ORDINAL: Readonly<Record<PokemonType, number>> = Object.freeze(
  Object.fromEntries(POKEMON_TYPES.map((t, i) => [t, i])) as Record<PokemonType, number>,
);

export type StatusType = 'Poisoned' | 'Burned' | 'Paralyzed' | 'Asleep';

export type Side = 'A' | 'B';

export type BattleOutcome = 'SideAWins' | 'SideBWins' | 'Draw';

/**
 * Effect vocabulary (content-schema.md §4). Extend this list rather than special-casing a
 * species when a type-flavour seed can't be expressed.
 */
export type EffectType =
  | 'DealDamage'
  | 'Heal'
  | 'Shield'
  | 'ApplyStatus'
  | 'ClearStatus'
  | 'BuffAttack'
  | 'BuffSpeed'
  | 'ModifyChargeRate'
  | 'DamageReduction'
  | 'Lifesteal';

/** Target selectors (content-schema.md §5). Only two mons per side are ever active. */
export type TargetSelector = 'Self' | 'Ally' | 'EnemyLead' | 'EnemySupport';

export interface EffectDefinition {
  readonly type: EffectType;
  readonly target: TargetSelector;
  /**
   * When true, `amount` is ignored and the acting mon's Special is used instead.
   *
   * This is what makes Special mean anything: the default ability is "deal damage equal to your
   * Special", and it is expressed as a normal DealDamage effect carrying this flag rather than as
   * a special case in the Step loop. An ability that shields, heals or inflicts a status simply
   * does not set it, and so *overrides* the default rather than adding to it.
   */
  readonly scalesWithSpecial?: boolean;
  /**
   * Base magnitude. Ignored by effects that don't take one (ClearStatus, and ApplyStatus for
   * Paralyzed/Asleep). For ApplyStatus with Poisoned/Burned this is the tick damage.
   * For ModifyChargeRate it is a percentage delta (50 => x1.5). For Lifesteal, a percentage.
   */
  readonly amount: number;
  /** Only meaningful when `type` is 'ApplyStatus'. */
  readonly status?: StatusType;
}

/**
 * A passive already resolved to its final numbers for one specific mon (content-schema.md §3).
 * The sim never resolves a passive id itself.
 */
export interface PassiveDefinition {
  readonly id: string;
  readonly displayName?: string;
  readonly typeFlavor?: PokemonType;
  readonly effects: readonly EffectDefinition[];
}

export interface Stats {
  attack: number;
  health: number;
  speed: number;
  /**
   * The magnitude of the mon's ability: what it does when its charge bar fills.
   *
   * Separate from Attack because the two fire on different clocks. Attack lands every Step and is
   * always damage; Special lands once a charge meter fills and may not be damage at all — a mon
   * whose ability shields or heals spends its Special on that instead. Keeping them apart is what
   * lets a frail special attacker and a plain bruiser be different mons rather than the same mon
   * with different numbers.
   */
  special: number;
}

/**
 * One Pokemon's state *inside a single battle*.
 *
 * Distinct from the run-level record (a `PokemonInstance`, in the meta layer) because the two
 * have different lifetimes. In Unity, before this split existed, running a battle wrote shields,
 * poison stacks and damage permanently onto the player's roster.
 */
/**
 * What a held item does *during* a battle, already resolved to numbers.
 *
 * The simulator never learns what an item is, the same way it never learns what a Pokémon is — it
 * is handed the behaviour and applies it. Keeping it resolved here means a new item is a content
 * change, not a Step-loop change, unless it genuinely needs a new kind of behaviour.
 */
export interface HeldItemEffects {
  /** Health restored at the end of every Step. */
  readonly regenPerStep?: number;
  /** Health restored once, the first time the holder drops below half. */
  readonly healBelowHalf?: number;
  /** Clears a status the moment it lands, once. */
  readonly curesStatus?: boolean;
}

export interface Combatant {
  readonly instanceId: string;
  /** Opaque back-reference to the run-level record. The sim never reads through it. */
  readonly sourceId?: string;
  currentStats: Stats;
  currentHP: number;
  status: StatusType | null;
  readonly passive: PassiveDefinition | null;
  /** Species types, read by the synergy opening and nothing else. Empty counts for nothing. */
  readonly types: readonly PokemonType[];
  /** Fairy synergy: how many further status applications this mon shrugs off. */
  statusWards: number;
  charge: number;
  shield: number;
  damageReductionFlat: number;
  chargeRateMultiplier: number;
  /** Fraction (0-1) of HP damage dealt that heals this mon back. */
  lifestealPercent: number;
  /** Per-tick damage for the current Poisoned/Burned status. */
  statusTickDamage: number;
  /** The held item's battle behaviour, or null. */
  readonly heldItem: HeldItemEffects | null;
  /**
   * Whether the item's one-shot effects have fired this battle.
   *
   * Battle-local on purpose: combatants are rebuilt for every fight, so "refreshes at the end of
   * a battle" needs no reset step anywhere — a spent berry is simply unspent next time, because
   * this object no longer exists.
   */
  usedStatusCure: boolean;
  usedLastStand: boolean;
  /** Poison ticks since last application; poison damage is tick * stacks and grows each tick. */
  poisonStacks: number;
}

/**
 * Two ordered line-ups. Position 0 is the Lead, position 1 the Support, the rest dormant.
 * Throwaway per-battle state.
 */
export interface BattleState {
  lineUpA: Combatant[];
  lineUpB: Combatant[];
  stepNumber: number;
}

export type StepEventKind =
  | 'Damage'
  | 'Heal'
  | 'Shield'
  | 'ShieldAbsorbed'
  | 'StatusApplied'
  | 'StatusCleared'
  | 'StatusTick'
  | 'ChargeGained'
  | 'StatusBlocked'
  | 'TypeSynergy'
  | 'SuddenDeath'
  | 'BuffAttack'
  | 'BuffSpeed'
  | 'ChargeRateModified'
  | 'DamageReductionApplied'
  | 'Lifesteal'
  | 'LifestealHeal'
  | 'PassiveTriggered'
  | 'Faint'
  | 'BallThrown'
  | 'Caught'
  | 'Promotion'
  | 'BattleEnd';

/**
 * One entry in a battle's event stream: what golden fixtures assert against and what the
 * animation layer consumes. Fields are left undefined where not applicable to a kind, rather
 * than having a subtype per kind, to keep this an easily-comparable plain shape.
 */
export interface StepEvent {
  step: number;
  kind: StepEventKind;
  sourceSide?: Side;
  sourceInstanceId?: string;
  targetSide?: Side;
  targetInstanceId?: string;
  amount?: number;
  status?: StatusType;
  outcome?: BattleOutcome;
  /** Only set on a TypeSynergy event. */
  synergyType?: PokemonType;
}

/** The whole result of a precomputed fight. */
export interface StepLog {
  events: StepEvent[];
  outcome: BattleOutcome;
  /**
   * The combatants as the last Step left them. The line-ups here hold only the mons still
   * standing; the fainted ones were removed as they fell, in the order the Faint events record.
   */
  finalState: BattleState;
}

// --- small helpers over the shapes above ---------------------------------------------------

export const isAlive = (c: Combatant): boolean => c.currentHP > 0;

export const lineUpOf = (state: BattleState, side: Side): Combatant[] =>
  side === 'A' ? state.lineUpA : state.lineUpB;

export const leadOf = (state: BattleState, side: Side): Combatant | null =>
  lineUpOf(state, side)[0] ?? null;

export const supportOf = (state: BattleState, side: Side): Combatant | null =>
  lineUpOf(state, side)[1] ?? null;

export const opposing = (side: Side): Side => (side === 'A' ? 'B' : 'A');

/** Options accepted when building a combatant; everything transient starts at its default. */
export interface CombatantSpec {
  instanceId: string;
  sourceId?: string;
  attack: number;
  health: number;
  speed: number;
  /** Defaults to 0 — a combatant built without one simply has no Special-scaled ability. */
  special?: number;
  passive?: PassiveDefinition | null;
  types?: readonly PokemonType[];
  /** Starting HP, when a mon arrives already damaged. Defaults to full health. */
  currentHP?: number;
  heldItem?: HeldItemEffects | null;
}

/** A fresh combatant at a known-clean slate. Mirrors `BattleCombatant.FromInstance`. */
export function makeCombatant(spec: CombatantSpec): Combatant {
  return {
    instanceId: spec.instanceId,
    sourceId: spec.sourceId,
    currentStats: {
      attack: spec.attack,
      health: spec.health,
      speed: spec.speed,
      special: spec.special ?? 0,
    },
    currentHP: spec.currentHP ?? spec.health,
    status: null,
    passive: spec.passive ?? null,
    types: spec.types ?? [],
    statusWards: 0,
    charge: 0,
    shield: 0,
    damageReductionFlat: 0,
    chargeRateMultiplier: 1,
    lifestealPercent: 0,
    statusTickDamage: 0,
    poisonStacks: 0,
    heldItem: spec.heldItem ?? null,
    usedStatusCure: false,
    usedLastStand: false,
  };
}

export function makeBattleState(lineUpA: Combatant[], lineUpB: Combatant[]): BattleState {
  return { lineUpA, lineUpB, stepNumber: 0 };
}

/** Deep copy of one combatant. */
export function cloneCombatant(c: Combatant): Combatant {
  return {
    ...c,
    currentStats: { ...c.currentStats },
  };
}

/**
 * Deep copy of a whole battle state.
 *
 * The sim's public entry points clone before mutating, so callers keep the state they passed in
 * (REACT_REBUILD_REFERENCE.md §5.1 — immutable updates, and replay becomes trivial). Internally
 * the Step logic still mutates, exactly as the Unity version does, which is what keeps the two
 * implementations byte-comparable against the shared golden fixtures.
 */
export function cloneBattleState(state: BattleState): BattleState {
  return {
    lineUpA: state.lineUpA.map(cloneCombatant),
    lineUpB: state.lineUpB.map(cloneCombatant),
    stepNumber: state.stepNumber,
  };
}
