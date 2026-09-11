# CLAUDE.md

Guidance for working in this repo. See [PLAN.md](PLAN.md) for the condensed plan and phase
roadmap, and [`docs/pokemon-roguelite-autobattler-design-doc.md`](docs/pokemon-roguelite-autobattler-design-doc.md)
for the full design — read both before starting non-trivial work if you haven't already.
[`docs/architecture-decisions/0001-pivot-to-pokemon-roguelite.md`](docs/architecture-decisions/0001-pivot-to-pokemon-roguelite.md)
explains why the project looks the way it does and what's still catching up to the new design.

## Project snapshot

A single-player roguelite (Slay the Spire–style meta-layer) wrapped around a Super Auto
Pets–style Lead/Support auto-battler, themed with Pokémon species/types/assets.
- Client: Unity (C#), `/client`.
- Backend: Node.js/TypeScript + PostgreSQL, `/server` — **not built yet**, introduced once the
  solo roguelite loop is solid (see PLAN.md §6, Phase 3). Don't add backend networking code to the
  client before then.
- Shared specs/fixtures: `/shared`, `/docs`.

Current phase: check PLAN.md §6 for the active phase and its exit criteria before assuming what
exists. **As of the pivot, the code under `client/Assets/Scripts/Simulation` and
`Gameplay/ShopEconomy` implements the *old* design (5-slot board, turn-based rounds,
hurt/faint triggers) and does not match `docs/battle-sim-spec.md`.** Don't extend that code as if
it were current — reworking it to the new Lead/Support/Step model is Phase 0's actual task. If
you're not sure whether something is pre- or post-pivot, check the file/class against
`docs/battle-sim-spec.md` and `docs/content-schema.md` rather than assuming.

## Hard rules

- **Non-commercial scope is a hard constraint, not a later concern.** This project uses
  Nintendo/Game Freak/Creatures IP (Pokémon species, types, names, and eventually PokeAPI sprites).
  No monetization, no wide publishing — see PLAN.md §9. Don't add IAP, ads, or store-listing
  infrastructure. This is the opposite of the old rule ("original content only, no Super Auto
  Pets names") — the old rule no longer applies; this one replaces it.
- **Battle simulation stays pure.** Code implementing the per-Step battle logic (Lead/Support
  formation, simultaneous exchange, charge-meter passive triggers — see `docs/battle-sim-spec.md`)
  must have zero dependency on `UnityEngine.MonoBehaviour`, `GameObject`, or scene state. This is
  what makes it unit-testable and eventually portable in spirit to the server. If you find
  yourself needing `Debug.Log` or a Unity type in that code, the logic belongs in `Gameplay`
  instead, calling into the sim.
- **Content is data, not code.** Species, passives, and items are ScriptableObject instances (see
  `docs/content-schema.md`). Don't hardcode a new C# class per Pokémon or per passive — if the
  existing passive/effect vocabulary can't express something, extend the vocabulary, don't
  special-case it. Stats for the 183-species roster come from `docs/pokemon_stats_unique.xlsx` via
  the content-import pipeline (PLAN.md §8), not hand-typed per species.
- **No new tests-optional logic in the simulator.** Any change to the battle-sim code needs an
  accompanying EditMode test — this is the one part of the codebase where bugs are both easy to
  introduce and hard to notice by eye.
- **Don't add multiplayer/network code before Phase 3.** Local save is the source of truth until
  the backend actually exists.
- **Respect the two-active-slots rule.** Only the Lead and Support (front two of a line-up) are
  ever mechanically active — no stats, charge, or passive for anyone further back until promoted.
  Don't build systems (UI, sim, or otherwise) that assume more than two mons per side are live at
  once; that's the old 5-slot model.

## Repository layout

```
/client    Unity project (C#)
/server    Node/TS backend — not started; introduced once solo loop is solid
/shared    Golden battle-sim fixtures (JSON) used by both client and (later) server tests
/docs      pokemon-roguelite-autobattler-design-doc.md (full design), pokemon_stats_unique.xlsx
           (roster), battle-sim-spec.md, content-schema.md, architecture-decisions/
```

## Working conventions

- **Branching:** trunk-based off `main`, short-lived feature branches. This is a solo project —
  keep it simple, but still don't commit directly to `main` for anything non-trivial so history
  stays reviewable.
- **Commits:** small and scoped to one change; explain *why* in the body when the reason isn't
  obvious from the diff. If a commit reworks something the old design touched, say so explicitly
  (e.g. "Rework Simulation for Lead/Support Steps (supersedes old 5-slot model, PLAN.md Phase 0)")
  so the history stays legible about the pivot.
- **C# style:** standard Unity/.NET conventions (PascalCase for public members/types, camelCase
  for private fields, no Hungarian notation). Prefer plain C# classes/structs over
  MonoBehaviours wherever scene attachment isn't actually needed (this matters most in the
  battle-sim and content code).
- **TypeScript style (once `/server` is active):** strict mode on, no implicit `any`, prefer
  explicit types on function boundaries (route handlers, service functions) even where inference
  would work, since these are the API contract.
- **No premature abstraction:** don't build a generic plugin system, config layer, or abstraction
  for a hypothetical future need. A handful of similar passives expressed as data is fine; don't
  build a DSL for it until the existing vocabulary actually can't express something new.

## Testing & running

- **Client tests:** Unity Test Runner — EditMode tests for the battle-sim code (and anything else
  that doesn't need a scene), PlayMode tests for node-map/hub/battle-screen integration. Run via
  Unity Editor's Test Runner window or `Unity -runTests` in CI.
- **Server tests (Phase 3+):** `vitest` (or the configured runner) against a disposable
  Postgres via Docker Compose — don't mock the database for anything touching real queries.
- **Golden fixtures:** when changing battle-sim rules, update/add cases in `/shared/fixtures` and
  make sure the client test suite passes against them before considering the change done. Once
  the server-side sim exists (Phase 3), it must pass the same fixtures.
- Before reporting simulation or gameplay-logic work as complete, run the relevant automated
  tests — don't rely on "looks right in the editor" for anything with test coverage available.
- For UI changes, actually press play in the Unity editor and click through the affected flow
  (node-map, catching, Trailblazer, hub tabs) before calling it done; type/compile success isn't
  feature success.

## Documentation to keep current

- `docs/pokemon-roguelite-autobattler-design-doc.md` — the design source of truth for mechanics.
  If a design decision changes during implementation, update this doc's relevant section (or its
  Open Questions in §20 if the decision remains unresolved) rather than letting code and doc
  drift apart.
- `docs/battle-sim-spec.md` — the source of truth for the Unity implementation of Step timing,
  charge-meter mechanics, and tie-breaking. Update it *before or alongside* simulation code
  changes, not after.
- `docs/content-schema.md` — the data shape for species/passives/items/locations, kept in sync
  with the actual ScriptableObject fields and JSON export format.
- `docs/pokemon_stats_unique.xlsx` — the roster source. If stats change during balancing, update
  the sheet, don't let hand-edited ScriptableObject values silently diverge from it.
- Short ADRs in `docs/architecture-decisions/` for decisions worth remembering the reasoning
  behind later (ADR 0001 — the pivot itself — is the template for these) — not required for
  routine work.
- `client/Assets/Scripts/Roguelite/Sandbox/README.md` — how to run and extend the battle-sim
  testing sandbox (try a matchup, add a Pokémon, try an ability idea). Keep it in sync with
  `SandboxContent.cs` if the pattern for adding content changes.
