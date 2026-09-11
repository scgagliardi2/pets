# shared

Language-agnostic specs and fixtures that both the Unity client and (from PLAN.md §6 Phase 3) the
Node backend test against, so a single source of truth exists for battle-sim behavior even though
the two implementations can't literally share code.

- `fixtures/` — golden battle-sim test cases for the Lead/Support/Step model described in
  `docs/battle-sim-spec.md`, run by `client/Assets/Scripts/Tests/GoldenFixtureTests.cs`. Two
  shapes, both JSON objects with `seed`, `lineUpA`, `lineUpB` (each a list of
  `{ instanceId, attack, health, speed, passive? }`, `passive` optionally
  `{ id, effects: [{ type, target, amount, status? }] }`):
  - **Outcome fixtures** (no `steps`, or `steps: 0`): run to completion and assert `expected`
    (`{ outcome, faintOrder, survivors: [{ instanceId, currentHP }] }`) — for scenarios about how
    a fight concludes.
  - **State fixtures** (`steps: N > 0`): run exactly N raw Steps and assert `expectedState`
    (`[{ instanceId, currentHP, shield, charge, status? }]`) — for scenarios about precise
    mid-battle mechanics (same-Step tie-breaking, status stacking, shield/damage-reduction math)
    that a terminal outcome alone wouldn't pin down. `same-step-tiebreak-shield.json` in
    particular locks in the provisional same-Step trigger-ordering rule from
    `docs/battle-sim-spec.md` §6 — if that rule changes, update the fixture and the spec together.

  Update/add cases here whenever battle-sim rules change, and make sure the Unity EditMode suite
  passes against them before considering a sim change done (see CLAUDE.md). Once the server-side
  sim exists (Phase 3), it must pass the same fixtures.
