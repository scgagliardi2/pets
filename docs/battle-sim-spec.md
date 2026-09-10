# Battle Simulation Spec

Status: **Phase 2 in progress.** Source of truth for trigger ordering, tie-breaking, and stat
formulas (PLAN.md §3, CLAUDE.md). Implemented by `client/Assets/Scripts/Simulation`. Update this
file *before or alongside* simulation code changes, not after.

## 1. Scope

The battle simulator resolves the **battle phase** only: given two fixed teams and a seed, it
produces a deterministic sequence of events with no further input. The **shop phase** (buying,
selling, rerolling, freezing, leveling up creatures) is a separate Gameplay-layer concern that
runs before a battle and produces the `TeamState` the simulator consumes — it is not implemented
yet (see "Out of scope" below) and the simulator has no knowledge of gold, shop odds, or player
choices.

## 2. Board model

- A team is an **ordered list of up to 5 creature slots**. Slot 0 is the "front" — the only slot
  that ever fights. When the front creature faints (and isn't immediately replaced by a `Summon`
  effect), the next slot shifts forward.
- A creature's effective stats are computed once when it enters the simulator:
  - `effectiveAttack = baseAttack + (level - 1) * levelAttackBonus`
  - `effectiveHealth = baseHealth + (level - 1) * levelHealthBonus`
  - `level` defaults to 1; the shop-phase combine-3-to-upgrade mechanic (not yet implemented) is
    what would produce level 2/3 creatures going into a battle.
- Buffs from abilities stack additively on top of effective stats for the remainder of the battle
  (no percentage/multiplicative buffs in the MVP vocabulary).

## 3. Trigger vocabulary

Full vocabulary per PLAN.md §2.3: `OnBattleStart`, `OnHurt`, `OnFaint`, `OnLevelUp`, `OnBuy`,
`OnSell`, `OnTurnStart`.

**Implemented by the battle simulator (this phase):** `OnBattleStart`, `OnHurt`, `OnFaint`.

**Shop-phase, resolved outside the battle simulator (Phase 2):** `OnLevelUp`, `OnBuy`, `OnSell`,
`OnTurnStart` are now live, but fired by `Gameplay/ShopEconomy.cs`, not `BattleSimulator` — the
battle simulator itself still only ever fires `OnBattleStart`/`OnHurt`/`OnFaint` and will never
select an ability with one of these triggers, since a battle only ever asks a creature for its
`OnBattleStart`/`OnHurt`/`OnFaint` abilities in the first place. See content-schema.md §8 for
firing order, target-selector semantics, and effect semantics in the shop context.

## 4. Effect vocabulary

- `DealDamage(amount, target)` — reduces target's health by `amount`.
- `Heal(amount, target)` — increases target's health by `amount`, capped at the creature's
  `effectiveHealth` (max health; healing cannot exceed the creature's max).
- `BuffAttack(amount, target)` / `BuffHealth(amount, target)` — additive, permanent for the
  battle.
- `Summon(creatureTemplate, target)` — only valid as an `OnFaint` effect; inserts a new creature
  (from `creatureTemplate`, a fixed stat block, not the shop pool) into the fainted creature's now
  empty front slot. `target` is ignored for `Summon` (always fills the fainting creature's own
  slot).

## 5. Target selectors

- `Self` — the creature whose ability is firing.
- `RandomAlly` — a uniformly random *other* living creature on the same team (excludes the
  triggering creature; if none exist, the effect is skipped).
- `RandomEnemy` — a uniformly random living creature on the opposing team.
- `FrontEnemy` — the opposing team's slot-0 creature (the one currently fighting).

Random selectors draw from the battle's single seeded PRNG (§7), consuming exactly one draw per
selection, in the order effects are resolved.

Every selector — including `FrontEnemy` — treats a creature at `health <= 0` as an invalid
target, even if it hasn't been formally removed from its team's slots yet (this matters during
the simultaneous-double-faint case in §6.3: one side's `OnFaint` effects can resolve while the
other fainted front creature is still physically in its slot). If a selector has no valid target
(e.g. `RandomAlly` with no other living allies, or `FrontEnemy` when the enemy front just died),
the effect is skipped entirely.

## 6. Turn structure, firing order, and tie-breaking

All of this must be deterministic given `(teamA, teamB, seed)` — this is the whole point of the
simulator, and is exactly what the golden fixtures in `/shared/fixtures` pin down.

### 6.1 The damage-resolution pipeline (used everywhere damage happens)

`OnHurt` and faints aren't special-cased to the attack exchange — **any** damage, whether from
the attack exchange or from an ability's `DealDamage` effect, resolves through the same pipeline,
applied to one target at a time:

1. Apply the damage (`health -= amount`), and log it.
2. If `amount > 0`, fire the target's `OnHurt` effects immediately (regardless of whether the hit
   was lethal — a creature can retaliate as it dies).
3. If the target's `health <= 0` and it hasn't already been removed from its team this instant,
   resolve its faint: remove it from its team's slots (later slots shift forward), log the faint,
   then fire its `OnFaint` effects. Those effects go through this same pipeline recursively if
   they themselves deal damage — a chain of faints (e.g. an `OnFaint` effect that kills a
   low-health creature elsewhere on the enemy board) resolves fully, in order, before control
   returns to whatever triggered it.

A creature at `health <= 0` is never a valid target for any selector (§5), even before its faint
has been formally resolved — so a chain can't re-target something that's already dead.

### 6.2 Battle start

`OnBattleStart` fires once per creature, in this fixed order: Team A's creatures front-to-back,
then Team B's creatures front-to-back. Each creature's effects apply immediately (not batched),
through the pipeline above — so e.g. a `DealDamage` effect on `OnBattleStart` can kill an enemy
before the first round even begins, chaining into that creature's own `OnFaint`.

### 6.3 Round loop

Repeated until a team is empty or the round cap is hit:

1. If either team has no living creatures, stop — see §6.5 (battle end).
2. **Attack exchange**: Team A's front creature and Team B's front creature deal damage to each
   other **simultaneously** — both `DealDamage` applications (step 1 of the pipeline) happen
   before either side's `OnHurt` fires. Concretely: apply both damages, then run step 2 (`OnHurt`)
   for Team A's creature, then step 2 for Team B's creature, then run step 3 (faint check, which
   may chain) for Team A's creature, then step 3 for Team B's creature. This is the one place
   the pipeline is deliberately split across two creatures instead of run front-to-back per
   target, precisely to make the exchange simultaneous rather than sequential.

### 6.4 Safety cap

Two caps guard against pathological content (not expected from the starter roster, but the
vocabulary doesn't rule out a future infinite `Summon`/`OnFaint` loop):
- **Round cap**: after 50 rounds with both teams still non-empty, stop — **draw**.
- **Event cap**: if total logged events in a single battle exceeds 10,000, stop immediately —
  **draw**. This catches runaway chains within a single round that the round cap wouldn't.

### 6.5 Battle end

Whichever team has 0 living creatures loses; if both do (simultaneous double KO with no
survivors either side), it's a draw.

## 7. Determinism

- Each battle uses one instance of `Simulation.DeterministicRandom`, a small seeded xorshift-style
  generator — not raw `System.Random`. `System.Random`'s output isn't guaranteed identical across
  .NET runtimes/versions, and a from-scratch generator with a documented algorithm gives a fixed
  contract that a future server-side reimplementation (Phase 4) can match exactly. This is a
  partial mitigation of the cross-platform determinism risk in PLAN.md §10 — full parity is only
  proven once the server-side sim exists and runs the same `/shared/fixtures` cases.
- All randomness in a battle (random target selection) draws from this single generator, in the
  fixed resolution order defined above. No other source of nondeterminism (no wall-clock, no
  `UnityEngine.Random`) is permitted anywhere in `Simulation`.

## 8. Out of scope (battle simulator)

- Shop phase gold/pool/odds/buy/sell/reroll/freeze/combine-to-level-up mechanics themselves — those
  live in `Gameplay/ShopEconomy.cs` and are out of scope for this document, which covers the
  battle simulator only (§1). `OnBuy`/`OnSell`/`OnLevelUp`/`OnTurnStart` *ability effect
  execution* is documented in content-schema.md §8, not here, since the battle simulator never
  runs it.
- Run history and a stats screen (PLAN.md §6 Phase 2's "basic meta") — deferred to a later pass.
