# shared

Language-agnostic specs and fixtures that both the Unity client and (from Phase 3+) the Node
backend test against, so a single source of truth exists for battle-sim behavior even though the
two implementations can't literally share code.

- `fixtures/` — golden battle-sim test cases: `(teamA, teamB, seed) -> expected battle log`, as
  JSON. Added starting in Phase 1, once the sim and its data schema exist. Update/add cases here
  whenever battle-sim rules change, and make sure the Unity EditMode suite passes against them
  before considering a sim change done (see CLAUDE.md).
