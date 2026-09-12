# Battle Simulation Spec

Status: **Implemented (2026-09-10) — Phase 0's battle-sim rework, matching this spec.** See
`docs/architecture-decisions/0001-pivot-to-pokemon-roguelite.md`. Source of truth for Step
timing, charge-meter mechanics, and tie-breaking (PLAN.md §3, CLAUDE.md). This is the Unity/C#
implementation spec for the combat model described narratively in
`docs/pokemon-roguelite-autobattler-design-doc.md` §10 — read that section first for the
"why"; this doc is the "exactly how, in code." Update this file *before or alongside* simulation
code changes, not after.

**This supersedes the previous version of this spec**, which documented a 5-slot, turn-based,
`OnBattleStart`/`OnHurt`/`OnFaint`-triggered model. `client/Assets/Scripts/Simulation` now
implements the model below — see `client/Assets/Scripts/Tests/StepSimulatorTests.cs` and
`GoldenFixtureTests.cs` for the test coverage backing it, and §12 below for a few
implementation-driven clarifications this doc didn't originally spell out.

## 1. Scope

The battle simulator resolves a fight between two ordered line-ups, one Step at a time, given a
seed. It has no knowledge of the meta-layer (Region/Location/node-map), the Trailblazer minigame,
the Shop, or Pokémon Center adoption — those are `Meta`/`Gameplay`-layer concerns that produce the
line-ups the simulator consumes. **Catching** (dragging a Pokéball onto the enemy Lead — design
doc §12.1) is also out of scope of the simulator itself: it's a separate interaction layer that
calls into the on-demand runner (§7 below) at Step boundaries and can remove a mon from the enemy
line-up between Steps, but the catch-chance formula and drag-and-drop handling live in
`Gameplay`, not here.

## 2. Formation model

- A **line-up** is an ordered list of `BattleCombatant`s — one mon's state *inside this battle*,
  copied from the run's `PokemonInstance`s at battle start by `BattleCombatant.FromLineUp`. The
  simulator only ever sees combatants, so nothing it does reaches the run's roster; see
  content-schema.md §8 for the split and why it exists. Position 0 is the **Lead**, position 1 is
  the **Support**. Everyone else is dormant.
- **Only the Lead and Support have live stats, a charge meter, and an active passive.** A dormant
  mon has none of these computed — don't allocate charge/passive state for it until it's promoted.
- When the Lead's `currentHP` reaches 0, it's removed: the Support is promoted to Lead, and the
  next dormant mon (if any) is promoted to Support. A line-up with only one mon left has a Lead
  and no Support; a line-up with zero mons left has lost.
- A mon's effective stats when it becomes active (Lead or Support) are the `currentStats` copied
  onto its combatant — already leveled — plus any active team-synergy bonus (§8 below) and any
  equipped-item modifiers. There is no additional per-Step stat recomputation beyond what
  buffs/statuses apply (§5).
- Anything applied **at line-up assembly** (team synergy, a Camp buff, item modifiers) is written
  onto the combatants, never onto the `PokemonInstance`s — otherwise a one-fight buff becomes
  permanent and compounds every battle. Build the combatants yourself and pass them to the
  runner's combatant overload when a fight needs any of this.

## 3. The Step loop

A battle is a sequence of discrete **Steps**. Each Step has a fixed **pacing window**,
`stepDurationMs` (a balance constant, not stat-driven — design doc §10.2 is explicit that this
window's *length* doesn't depend on Speed; only how fast each mon's charge meter fills during it
does).

Within one Step, in this order:

1. **Attack exchange.** The current Lead on each side deals damage to the other side's current
   Lead **simultaneously** — both damage applications happen before either side's on-hurt
   reactions (if any — see §5 on statuses) resolve. Speed does not factor into this exchange at
   all; it is a flat `attack` vs. `attack` trade, gated only by whether a Lead exists on each side.
2. **Charge accumulation.** All currently-active mons (both sides' Lead and Support — up to 4
   total) accrue charge for the Step's pacing window: `charge += speed * stepDurationMs`. A mon
   promoted mid-Step (because its side's previous Lead fainted in step 1) does **not** get a
   partial share of this Step's charge accrual — promotion happens after charge accrual for this
   Step, so a freshly-promoted mon starts accruing on the *next* Step. (This is a concrete
   resolution of an ambiguity the design doc leaves implicit; revisit if playtesting says
   otherwise.)
3. **Passive resolution.** Any mon whose `charge >= CHARGE_THRESHOLD` (constant, default `100` —
   see §4) triggers its passive now, before the Step concludes and before the next Step's attack
   exchange begins. Its charge resets to `0` (not `charge - CHARGE_THRESHOLD` — no carry-over)
   and begins accruing again next Step. See §6 for ordering when more than one mon triggers in the
   same Step.
3.5. **Status ticks.** Poisoned/Burned mons (§5) take their per-Step tick damage now, after
   passives resolve and before the faint check — so a tick can itself cause a faint this Step, and
   a status applied by a passive earlier in this same Step already ticks this Step too (not first
   next Step). Paralyzed/Asleep have no tick damage here; their effect is entirely on charge
   accrual (step 2).
4. **Faint check & promotion.** After status ticks resolve (since a passive or a status tick can
   deal damage that causes a faint), check every active mon's `currentHP`. Any mon at or below 0 is
   removed from its line-up; its Support (if any) is promoted to Lead, and the next dormant mon is
   promoted to Support. If both sides lose their Lead in the same Step (simultaneous KO with no
   Support to promote on one or both sides), that's a draw for the mon-vs-mon exchange but doesn't
   necessarily end the battle — see §9 for battle-end conditions.

   Steps 1-3.5 above all operate on whichever mons occupied Lead/Support when this Step began,
   even if one of them drops to 0 HP partway through (from step 1's exchange, say) — nothing is
   actually removed or stops acting until this step. The one exception is *targeting*: an effect
   that selects a specific mon (§5 of content-schema.md) treats a 0-HP mon as an invalid target and
   skips, even mid-Step. A mon whose own charge crosses the threshold this Step still triggers its
   passive as normal regardless of its own HP, though — only target validity is HP-gated, not
   trigger eligibility.

The loop repeats until a battle-end condition (§9) is reached.

## 4. Charge meters & the threshold constant

- `CHARGE_THRESHOLD` is a single global balance constant (default `100`), not per-mon or
  per-type. Speed is what varies triggering frequency between mons.
- A charge meter can, in principle, cross the threshold by more than the exact amount in one Step
  (if `speed * stepDurationMs` overshoots). Per §3 step 3, the meter resets to `0` regardless of
  overshoot — the overshoot amount is discarded, not carried into the next cycle. This keeps the
  system simple; revisit only if playtesting shows overshoot loss meaningfully hurts high-Speed
  mons at short `stepDurationMs` values.
- A battle can see the same mon's passive fire more than once — this is intended (design doc
  §10.2).

## 5. Statuses

`PokemonInstance.status` (design doc §9) is a single optional flag: `poisoned | burned |
paralyzed | asleep`. Semantics (tune numbers during Phase 0/1 balancing — these are placeholders
consistent with the Type-flavor seeds in design doc §11):

| Status | Effect | Cleared by |
|---|---|---|
| `poisoned` | Flat damage at the end of every Step this mon is active (stacks in severity the longer it's applied, per the Poison synergy seed) | A cleanse effect (e.g. a Fairy passive), or fainting |
| `burned` | Flat damage at the end of every Step this mon is active (non-stacking, unlike poison) | Same as above |
| `paralyzed` | This mon's charge accrual (§3 step 2) is reduced by a fixed percentage | Same as above |
| `asleep` | This mon's charge accrual is fully zeroed until cleared | Same as above |

Only one status applies at a time — a new status effect overwrites the existing one rather than
stacking multiple statuses on the same mon. This also feeds the catch-chance formula (design doc
§12.1) but that formula itself lives with the catching system, not here.

## 6. Same-Step multi-trigger ordering

**Open question, carried from design doc §20 — the default below is a proposal, not yet
confirmed.** If two or more mons' charge meters cross the threshold within the same Step:

1. Attacking Leads resolve before waiting Supports.
2. Ties within the same role (e.g. both Leads trigger the same Step) are broken by Speed, higher
   first.
3. If Speed also ties, break by line-up side (Team A before Team B) for determinism, then by
   position for full determinism (Lead is checked before Support, already covered by rule 1).

Implement this as the default, write a golden fixture that exercises a same-Step double-trigger,
and flag in the fixture's description that this ordering is provisional pending design
confirmation.

## 7. Runners: precomputed vs. on-demand

Per design doc §10.5, two thin runners sit on top of the per-Step logic in §3 — both call the
same `AdvanceStep(BattleState) -> (BattleState, StepEvent[])` function, never duplicating Step
logic:

- **Precomputed Step-log runner** (Gym, PvP): no catching is possible in these fights, so call
  `AdvanceStep` in a loop until a battle-end condition, collect every `StepEvent[]` into one log,
  and hand the whole thing to the UI to play back (step-through or autoplay, design doc §10.4).
  This is also exactly what a server-authoritative PvP result needs (design doc §16) — the same
  function, called server-side in the TypeScript port once Phase 3 exists.
- **On-demand runner** (PvE): call `AdvanceStep` once per UI advance (a manual step-through click,
  or one tick of autoplay). At any Step boundary — after `AdvanceStep` returns and before the next
  call — the catching interaction layer (`Gameplay`, out of scope here) may mutate the enemy
  line-up (remove the caught mon) before the next `AdvanceStep` call. The simulator itself needs
  no special "catch" concept; it just operates on whatever `BattleState` it's given next. Removing
  a combatant from that line-up is safe precisely because it isn't the run's roster; each
  combatant's `Source` is how the caller gets back to the mon to add to the Box.

Because a battle runs on copies, its **result has to be reported rather than read off the
line-ups** the caller passed in. The precomputed runner's `StepLog` therefore carries
`FinalState` — the combatants as the last Step left them, holding only the mons still standing —
and `Faint` events are the record of who fell and in what order.

## 8. Team synergy (not Step-triggered)

Type-count synergy bonuses (design doc §11) are **not** part of the Step loop — they're computed
once, when a line-up is assembled for battle (at Location-Hub confirm time, or PvE's "current
Team Management order" per design doc §5.1), as a flat modifier applied to `currentStats` before
the mon ever becomes a Lead or Support. Re-deriving them mid-battle isn't needed since the active
line-up's type composition doesn't change mid-fight (catching adds to the player's Box, not their
current line-up, until the player re-arranges outside battle). Implement this as a pre-battle
pass, not inside `AdvanceStep`.

## 9. Battle end

- A side with zero mons remaining (Lead and Support both fainted, no dormant mons left to
  promote) loses.
- If both sides lose their last mon in the same Step, it's a draw.
- **Safety caps** (engineering safeguard, not from the design doc — carried forward from the old
  spec as good practice against pathological content): a **Step cap** (default 200 Steps) and an
  **event cap** (default 10,000 logged `StepEvent`s) both force a draw if hit, to guard against a
  future passive/status combo creating an effectively infinite fight. Tune these once real content
  exists; they shouldn't matter for any sane fight.

## 10. Determinism

- Each battle uses one seeded PRNG instance (a small documented xorshift-style generator, not
  `System.Random`, for the same cross-runtime-consistency reason as before — a documented
  algorithm is what a future TypeScript server reimplementation needs to match exactly).
- Anything probabilistic inside the sim itself (e.g. a passive with a percentage chance to apply
  a status) draws from this generator, in a fixed resolution order matching §6's trigger
  ordering. No wall-clock or `UnityEngine.Random` anywhere in the sim.
- The discrete-Step model is inherently friendlier to determinism than a continuous-time model
  would be (design doc §10.5) — there's no floating-point animation-timing drift to reconcile,
  since `stepDurationMs` is a fixed constant consumed identically every Step.

## 11. Out of scope (battle simulator)

- Catching mechanics and the drag-and-drop interaction (design doc §12.1) — lives in `Gameplay`,
  calls into the on-demand runner (§7) at Step boundaries.
- The Trailblazer minigame, node-map traversal, Shop, Camp, and Pokémon Center — all `Meta`/
  `Gameplay` concerns that produce or consume line-ups but don't participate in Step resolution.
- Exact passive numbers per species — those are content (`docs/content-schema.md`), not sim
  logic. The sim only needs to know each passive's trigger (always: own charge meter fills) and
  its effect, both expressed through the vocabulary content authors use.

## 12. Implementation clarifications (Phase 0)

A few numeric/ordering choices this spec left implicit, pinned down during the Phase 0 rework —
tune the *values* freely, but keep the *shape* of these rules in sync with
`BattleSimulator.cs` if you change them:

- **`stepDurationMs` is a small placeholder scalar, not literal milliseconds.** With the curated
  roster's Speed stats in the ~20-130 range, `BattleConfig.DefaultStepDurationMs = 1` makes
  `charge += speed * stepDurationMs` track Speed directly at a readable 2-5 Steps per trigger.
  Literal real-world milliseconds (e.g. 1000) would cross `ChargeThreshold` every single Step
  regardless of Speed — rescale this constant, not the formula, if pacing needs to change.
- **The same-Step trigger set is fixed once per Step**, computed from charge values after step 2's
  accrual. A passive that speeds up another mon's charge rate (`ModifyChargeRate`) can't cause
  that mon to *also* trigger later in the same Step it was sped up — the effect applies starting
  next Step. `shared/fixtures/same-step-tiebreak-shield.json` locks in the tie-break order itself
  (§6) with a fixture that would fail under the wrong order, not just a different outcome.
- **Standing modifiers (`DamageReduction`, `BuffAttack`, `BuffSpeed`, `ModifyChargeRate`'s
  multiplier, `Lifesteal`'s percent) accumulate across repeated triggers** of the same passive
  over a long fight, the same way `BuffAttack`/`BuffSpeed` obviously do — each trigger adds
  another increment, it doesn't refresh/overwrite a single value. `ApplyStatus` is the one
  exception: re-applying a status (even the same one) resets its tick tracking (severity stacks
  back to 1, tick damage reset to the new `amount`) rather than stacking with the previous
  application — see the Poisoned note below.
- **Damage pipeline order:** `DamageReductionFlat` (flat subtraction) applies first, then `Shield`
  absorbs whatever remains, then the remainder hits `currentHP`. `Lifesteal` heals the attacker
  off the amount that actually hit HP (post-reduction, post-shield), not the raw pre-mitigation
  amount.
- **Poisoned severity resets on re-application**, not on a fixed timer: applying `ApplyStatus`
  with `Poisoned` (even to an already-Poisoned mon) sets its stack count back to 1 and its tick
  damage to the new `amount`; stacks then increment by 1 after every tick from there. Burned never
  stacks — its tick damage is always just the last-applied `amount`.
- **Status ticks bypass `Shield`/`DamageReductionFlat` and never proc `Lifesteal`** — they're
  self-inflicted damage, not an attack, so none of the mitigation/response pipeline in the point
  above applies to them.
