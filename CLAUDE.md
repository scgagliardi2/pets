# CLAUDE.md

Guidance for working in this repo. See [PLAN.md](PLAN.md) for the full project plan, phase
roadmap, and design rationale — read it before starting non-trivial work if you haven't already.

## Project snapshot

Mobile auto-battler (Super Auto Pets–like: draft creatures, auto-resolve battles) built as a
solo hobby project with no fixed deadline.
- Client: Unity (C#), `/client`.
- Backend: Node.js/TypeScript + PostgreSQL, `/server` — **not built yet**, only scaffolded once
  Phase 3 starts (see PLAN.md §6). Don't add backend networking code to the client before then.
- Shared specs/fixtures: `/shared`, `/docs`.

Current phase: check PLAN.md §6 for the active phase and its exit criteria before assuming what
exists. Don't jump ahead to a later phase's work (e.g. PvP, matchmaking, monetization) even if it
seems like a natural extension — the phase boundaries are deliberate scope control.

## Hard rules

- **Original content only.** Do not name creatures/abilities after Super Auto Pets' actual pets,
  copy its specific text/flavor, or reference its assets. Mechanics (drafting, tiers, triggers)
  are fine to draw from; specific expression is not.
- **Battle simulation stays pure.** Code under `client/Assets/Scripts/Simulation` must have zero
  dependency on `UnityEngine.MonoBehaviour`, `GameObject`, or scene state — it's a plain C#
  library of `(teamA, teamB, seed) -> battleLog`. This is what makes it unit-testable and
  eventually portable in spirit to the server. If you find yourself needing `Debug.Log` or a
  Unity type in that folder, the logic belongs in `Gameplay` instead, calling into `Simulation`.
- **Content is data, not code.** New creatures/abilities are ScriptableObject instances composed
  from existing triggers/effects (see `docs/content-schema.md` once it exists). Don't hardcode a
  new C# class per creature — if the existing trigger/effect vocabulary can't express a creature,
  that's a signal to extend the vocabulary, not to special-case it.
- **No new tests-optional logic in the simulator.** Any change to
  `client/Assets/Scripts/Simulation` needs an accompanying EditMode test. This is the one part of
  the codebase where bugs are both easy to introduce and hard to notice by eye.
- **Don't add multiplayer/network code before Phase 3.** Local save is the source of truth for
  the MVP; keep it that way until the backend actually exists.

## Repository layout

```
/client    Unity project (C#)
/server    Node/TS backend — scaffolded in Phase 3, empty/minimal until then
/shared    Golden battle-sim fixtures (JSON) used by both client and (later) server tests
/docs      Design docs: battle-sim-spec.md, content-schema.md, architecture-decisions/
```

## Working conventions

- **Branching:** trunk-based off `main`, short-lived feature branches. This is a solo project —
  keep it simple, but still don't commit directly to `main` for anything non-trivial so history
  stays reviewable.
- **Commits:** small and scoped to one change; explain *why* in the body when the reason isn't
  obvious from the diff.
- **C# style:** standard Unity/.NET conventions (PascalCase for public members/types, camelCase
  for private fields, no Hungarian notation). Prefer plain C# classes/structs over
  MonoBehaviours wherever scene attachment isn't actually needed (this matters most in
  `Simulation` and `Data`).
- **TypeScript style (once `/server` is active):** strict mode on, no implicit `any`, prefer
  explicit types on function boundaries (route handlers, service functions) even where inference
  would work, since these are the API contract.
- **No premature abstraction:** this is explicitly called out in PLAN.md's design philosophy —
  don't build a generic plugin system, config layer, or abstraction for a hypothetical future
  need. Three similar creature abilities expressed as data is fine; don't build a DSL for it until
  the existing trigger/effect vocabulary actually can't express something new.

## Testing & running

- **Client tests:** Unity Test Runner — EditMode tests for everything in `Simulation` (and
  anything else that doesn't need a scene), PlayMode tests for shop/board integration. Run via
  Unity Editor's Test Runner window or `Unity -runTests` in CI.
- **Server tests (Phase 3+):** `vitest` (or the configured runner) against a disposable
  Postgres via Docker Compose — don't mock the database for anything touching real queries.
- **Golden fixtures:** when changing battle-sim rules, update/add cases in `/shared/fixtures` and
  make sure the client test suite passes against them before considering the change done. Once
  the server-side sim exists (Phase 4), it must pass the same fixtures.
- Before reporting simulation or gameplay-logic work as complete, run the relevant automated
  tests — don't rely on "looks right in the editor" for anything with test coverage available.
- For UI changes, actually press play in the Unity editor and click through the shop/board flow
  before calling it done; type/compile success isn't feature success.

## Documentation to keep current

- `docs/battle-sim-spec.md` — the source of truth for trigger ordering, tie-breaking, and stat
  formulas. Update it *before or alongside* simulation code changes, not after — it's what the
  future server implementation will be built from.
- `docs/content-schema.md` — the data shape for creatures/abilities/tiers, kept in sync with the
  actual ScriptableObject fields and the JSON export format.
- Short ADRs in `docs/architecture-decisions/` for decisions worth remembering the reasoning
  behind later (e.g. why a particular determinism approach was chosen) — not required for routine
  work.
