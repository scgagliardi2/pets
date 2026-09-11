# shared

Language-agnostic specs and fixtures that both the Unity client and (from PLAN.md §6 Phase 3) the
Node backend test against, so a single source of truth exists for battle-sim behavior even though
the two implementations can't literally share code.

- `fixtures/` — golden battle-sim test cases: `(lineUpA, lineUpB, seed) -> expected Step log`, as
  JSON, per `docs/battle-sim-spec.md`. **Note:** the existing files in this folder
  (`onbattlestart-damage-chain.json`, `summon-on-faint.json`, etc.) are golden fixtures for the
  *old* 5-slot/turn-based model (see `docs/architecture-decisions/0001-pivot-to-pokemon-roguelite.md`)
  and don't carry forward to the new Lead/Support/Step model as-is — the fixture *shape* itself
  changes (an ordered line-up with explicit Lead/Support promotion, not a 5-slot team). New
  fixtures matching the current spec should be added as part of PLAN.md Phase 0's battle-sim
  rework, before that phase is considered done. Update/add cases here whenever battle-sim rules
  change, and make sure the Unity EditMode suite passes against them before considering a sim
  change done (see CLAUDE.md).
