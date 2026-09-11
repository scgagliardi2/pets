# PLAN.md — Pokémon Roguelite Autobattler

> **Pivot note (2026-09-10):** This plan supersedes the earlier "generic shop-drafting
> auto-battler" plan. See [`docs/architecture-decisions/0001-pivot-to-pokemon-roguelite.md`](docs/architecture-decisions/0001-pivot-to-pokemon-roguelite.md)
> for why, and for what happens to the Phase 0–2 code already built against the old design (short
> version: kept for reference, not the source of truth). The full, detailed design lives in
> [`docs/pokemon-roguelite-autobattler-design-doc.md`](docs/pokemon-roguelite-autobattler-design-doc.md)
> — this file is the condensed plan + roadmap; go to the design doc for exhaustive mechanics
> detail (exact node types, catch-chance formula, screen inventory, etc.).

## 1. Overview & Vision

A single-player roguelite (Slay the Spire–style meta-layer) wrapped around a collect-and-auto-
battle combat layer (Super Auto Pets–style Lead/Support Steps), themed with Pokémon species,
types, and assets. Playable solo, with an asynchronous PvP node inside each run.

**Working title:** TBD (design doc suggests "Astromon" as a placeholder codename — Pokémon
mechanics, no Pokémon name, for anywhere a non-Pokémon-branded string is useful in code/assets).

**Scope note — read this before adding content or talking about distribution:** this is a
personal/friends fan project using Pokémon (Nintendo/Game Freak/Creatures) characters, types, and
assets, with **no monetization planned**. Treat it as private/non-commercial — don't publish it
widely or monetize it — to stay on the safe side of IP concerns. [PokeAPI](https://pokeapi.co/) is
the intended source for species base data, types, evolution chains, and sprites; check their
fair-use guidelines for attribution/caching etiquette before pulling from it in bulk.

**Project profile (confirmed):**
- Solo hobby project, no fixed deadline — optimize for low running cost, low maintenance burden,
  and always having a small but *complete and playable* slice rather than a large unfinished one.
- **Client: Unity (C#).** (The design doc's own §18 suggests React + TypeScript; we're
  overriding that and staying on Unity — see ADR 0001.)
- Backend: custom Node.js/TypeScript + PostgreSQL, introduced once the solo roguelite loop is
  solid (async PvP needs it; solo play doesn't).
- MVP: one hand-authored Location, a small curated slice of species, no backend.

## 2. Core Game Design

Full detail lives in the design doc; this is the shape of it:

- **Run structure:** Region (procedurally chosen from available Locations) → pick a Location →
  Trailblazer travel minigame → Location's node-map (PvE / Event / PvP / Camp nodes, all
  eventually funneling into a mandatory Gym) → badge → back to Region Hub. Morale is the run's
  life total; hitting 0 ends the run. See design doc §2–§5, §14–§15.
- **Roster & Box:** catch mons in the wild (PvE only, drag-a-Pokéball-onto-the-enemy-Lead), adopt
  them at a Pokémon Center (every Location, luck-independent), grow them via EXP/level, evolve
  them along real Pokémon evolution chains, combine duplicates for EXP. See design doc §7–§8,
  §12.
- **Combat:** Lead/Support formation (only the front two mons per side are ever mechanically
  active); battle proceeds as discrete **Steps** where both sides' Leads trade damage
  simultaneously while all four active mons' passives charge on their own Speed-driven meter and
  fire independently when full. See design doc §10 — this is the part
  `docs/battle-sim-spec.md` implements in detail.
- **Team synergy:** TFT-style type-count bonuses for your active line-up (design doc §11).
- **Async PvP:** deterministic Step-log simulation means a Gym/PvP fight can be computed once,
  server-side or client-side against a signed snapshot, and just played back — no need for both
  players online at once (design doc §16).

## 3. Architecture

```
+----------------------+          +--------------------------+
|   Unity Client        |          |   Node/TS Backend         |
|  (C#, MVP: fully      |  HTTPS   | (introduced once solo     |
|   offline capable)    | <------> | loop is solid)            |
|                        |  JSON    | Auth, cloud save,         |
|  - Region/Location/    |  API     | PvP snapshot storage +    |
|    node-map UI         |          | matchmaking, server-side  |
|  - Trailblazer         |          | battle-sim reimpl. for    |
|    minigame            |          | authoritative PvP         |
|  - Battle sim +        |          |                            |
|    runners             |          | PostgreSQL                |
|  - Local save          |          |                            |
+------------------------+          +---------------------------+
```

Key principle, carried over from the original plan and reinforced by the design doc's own
suggested split (§18): the **battle simulator is a pure, deterministic function** of one Step at
a time — `(battleState) -> nextBattleState`, with two thin runners on top (§10.5 of the design
doc):
- A **precomputed Step-log runner** for Gym/PvP fights (no catching possible, so the whole fight
  can be computed up front as `simulateBattle(leadA, supportA, ..., leadB, supportB, ..., seed)
  -> Step[]` and played back — this is exactly what a server-authoritative async-PvP result
  needs).
- An **on-demand Step runner** for PvE fights (a successful catch changes the board, so Steps are
  generated one at a time as the player advances).

Both runners call the same underlying per-Step logic, isolated from Unity's
MonoBehaviour/rendering layer, for the same reasons as before: unit-testable without spinning up
scenes, replayable as an animation independent of simulation speed, and eventually
reimplementable server-side (TypeScript) for anti-cheat/authoritative PvP without fighting engine
coupling. Because the client is C# and the eventual server is TypeScript, we still can't literally
share code between them — mitigation is the same as before: one written spec
(`docs/battle-sim-spec.md`) plus golden test fixtures in `/shared/fixtures` that both suites run
against.

## 4. Tech Stack

| Layer | Choice | Notes |
|---|---|---|
| Client engine | Unity (LTS version, C#) | 2D, uGUI or UI Toolkit for hub/map/battle UI |
| Client testing | Unity Test Framework (NUnit), EditMode for sim logic, PlayMode for integration | |
| Species/type data | PokeAPI, cached locally at build/content-import time | Filtered to the curated Gen 1–3 roster (§8 of the design doc, `docs/pokemon_stats_unique.xlsx`) |
| Backend runtime | Node.js + TypeScript | Introduced once solo loop is solid (design doc Phase 3) |
| Backend framework | Fastify (or Express) | REST JSON API |
| Database | PostgreSQL | Accounts, PvP snapshots, matchmaking |
| Backend testing | Vitest + Supertest, test DB via Docker Compose | |
| CI | GitHub Actions | Lint + test on push/PR for both client and server |
| Source control | Git (this repo), trunk-based on `main` with short-lived feature branches | |
| Placeholder art | Free/CC0 packs or primitives until real Pokémon-style sprites are sourced (PokeAPI sprites are the eventual real asset source) | |

## 5. Repository Structure

The design doc's own suggested layout (§18) is written for a React/TS frontend; this is the same
conceptual split re-expressed for Unity:

```
/pets
  PLAN.md
  CLAUDE.md
  README.md
  /client                 # Unity project
    /Assets
      /Scripts
        /Simulation        # Pure C#, no MonoBehaviour deps — per-Step battle logic (§10 of the design doc)
        /BattleRunner      # Precomputed Step-log runner (Gym/PvP) + on-demand runner (PvE)
        /Data              # ScriptableObject content: species, passives, items, locations, gyms
        /Meta              # Region/Location generation, run state (roster, map position, morale, money)
        /Minigame          # Trailblazer canvas/lane module — no dependency on Simulation
        /Gameplay          # MonoBehaviours: hub screens, node-map, camp, shop, catching interaction
        /UI
        /Tests             # EditMode + PlayMode tests
      /Content              # ScriptableObject data instances (curated species, passives, items)
      /Art                  # Placeholder + eventual PokeAPI-sourced sprites
  /server                  # Node/TS backend (added once solo loop is solid)
    /src
      /routes
      /services
      /db
    /test
  /shared                  # Language-agnostic specs & fixtures both sides test against
    /fixtures               # Golden battle-sim Step-log test cases (JSON)
  /docs
    pokemon-roguelite-autobattler-design-doc.md   # full design source
    pokemon_stats_unique.xlsx                     # locked-in 183-species roster (stats are placeholders)
    battle-sim-spec.md
    content-schema.md
    architecture-decisions/
```

## 6. Development Phases

Phases are milestones, not deadlines — move on only when the current phase is genuinely playable
end-to-end. Numbering resets from the old plan (see ADR 0001) since the actual combat/content
code needs reworking to match the new Lead/Support model before any of it counts as "done" here.

**Status (as of 2026-09-10):** Design pivot accepted (ADR 0001). Docs (this file, CLAUDE.md,
README.md, battle-sim-spec.md, content-schema.md) now describe the new direction.
`client/Assets/Scripts/Simulation` and `Gameplay/ShopEconomy` still implement the old 5-slot/
turn-based model and are not the source of truth. **Phase 0's battle-sim rework has a first,
runnable implementation** — see `client/Assets/Scripts/Roguelite/` (new folder, doesn't touch or
replace the old `Simulation`/`Gameplay` code, so nothing existing broke): `Simulation` implements
the Lead/Support/Step/charge-meter model from `docs/battle-sim-spec.md`, `Sandbox` has a small
hand-picked slice of the real roster with example passives (real stats from
`docs/pokemon_stats_unique.xlsx`, not invented) so you can run and add matchups without the real
content pipeline existing yet, and `Tests/BattleSandboxTests.cs` is a working, currently-green
EditMode suite (verified compiling and passing outside Unity too, via `mcs`/`nunit-console`, since
this sandbox has no Unity engine dependency at all — see `Roguelite/Sandbox/README.md`). What's
still outstanding for Phase 0's exit criteria: the one hand-authored Location, stubbed
catching/Trailblazer, and folding the sandbox's hand-typed content into the real
ScriptableObject-based content pipeline (§8) instead of hand-typed C#.

**An early, real finding from running the sandbox:** several real-roster matchups (e.g. Charmander
vs. Squirtle) trade a mutual KO on Step 1 — Attack and starting HP are on similar scales in the
placeholder stats, and this combat model has no Defense stat, so a lot of pairs one-shot each
other before any passive gets a chance to charge. Not a bug — a genuine, early balance signal for
the tuning pass this section already calls out as ongoing work, surfaced by having something
runnable to point at real numbers.

**Phase 0 — Battle-sim rework + first hand-authored Location (prototype, solo, offline)**
- Rework/replace `Simulation` to match `docs/battle-sim-spec.md`: Lead/Support formation, Step
  loop, charge-meter passive triggers, both runners (precomputed + on-demand).
- Import the first ~10–15 species from `docs/pokemon_stats_unique.xlsx` as content (stats only —
  abilities are blank in the source sheet; hand-author a handful of type-flavored passives per
  §10.3/§11 of the design doc for this slice).
- One hand-authored Location (e.g. a Forest), PvE + Camp + Shop nodes, basic Step-based battles
  watchable step-through or autoplay. No evolution, no real backend.
- Catching stubbed as a simple end-of-fight "pick 1 from defeated" rather than full drag-and-drop.
  Trailblazer stubbed as an instant auto-roll.
- **Exit criteria:** a full PvE-node fight resolves deterministically via the new Step model and
  is covered by golden fixtures in `/shared/fixtures`.

**Phase 1 — Full run loop**
- Real Gym/Badge flow, Morale/win-loss loop, evolution (via PokeAPI evolution chains, restricted
  to the curated roster), the full drag-and-drop catching system (Step-boundary throws,
  HP%/status-based odds), Pokémon Center adoption, type synergy bonuses, and the real Trailblazer
  minigame (lane obstacle-dodge, Speed/Type-driven per §6 of the design doc).

**Phase 2 — Content & breadth**
- Events (narrative branches), the rest of the Location types (§4 of the design doc) and their
  type-biased encounter pools, procedural Region generation, the rest of the curated 183-species
  roster with hand-authored passives.

**Phase 3 — Backend + async PvP**
- Node/TS + Postgres: accounts (or anonymous device-id), PvP snapshot storage, matchmaking query.
- Server-side battle-sim reimplementation in TypeScript, validated against the shared golden
  fixtures for parity with the Unity sim (design doc §16).

**Phase 4 — Meta-progression & polish**
- Achievements/meta-progression screen, run-finale/capstone decision (one of the Open Questions in
  the design doc §20), real art pass (PokeAPI sprites or commissioned equivalents), balance pass
  on stats/passives/synergy values (all currently placeholders), audio/juice polish.

**Phase 5 — Release prep (if ever)**
- Given the non-commercial scope note in §1, this phase is about *whether* and *how* to share the
  project privately (friends/testers) rather than a store listing — revisit the scope note before
  doing anything resembling a public release.

## 7. Testing Strategy

- **Battle simulation (highest priority):** pure C# unit tests (EditMode) covering the Step loop,
  charge-meter timing, simultaneous-Lead-exchange semantics, passive triggering, Lead/Support
  promotion on faint, and the tie-breaking rule for same-Step meter fills (design doc §10.2, and
  see the open question on that rule in §20 — write the test against whatever we lock in, and
  update it if that question resolves differently).
- **Golden fixtures:** curated `(leadA, supportA, ..., leadB, supportB, ..., seed) -> expected
  Step log` cases in `/shared/fixtures`, run by both the Unity suite and (from Phase 3) the Node
  suite, to guarantee parity. The fixture *shape* changes from the old plan (team-of-5 → ordered
  line-up with explicit Lead/Support) — old fixtures don't carry forward as-is.
- **Integration/PlayMode tests:** node-map traversal, catch-chance rolls, Pokémon Center
  adoption, save/load round-trip of a run.
- **Backend tests (Phase 3+):** Vitest + Supertest against routes, using a disposable Postgres
  (Docker Compose) rather than mocks.
- **CI gate:** PRs must pass client EditMode tests and (once it exists) server tests before merge.
- No manual-only testing for simulation logic — if it's not covered by an automated test, assume
  it's broken.

## 8. Data & Content Pipeline

- **Starting roster:** `docs/pokemon_stats_unique.xlsx` is the locked-in source for the 183
  species (id, name, types, Attack/HP/Speed per evolution stage). Stats are explicit placeholders
  — treat every number as a first draft. The sheet's Ability column is empty; passives are
  hand-authored separately, following the Type-flavor seeds in design doc §11.
- **Species/type/evolution/sprite data:** pulled from PokeAPI, filtered to the 183 curated
  species, cached locally rather than hit at runtime. A content-import step turns the xlsx +
  PokeAPI data into `PokemonSpeciesDefinition` ScriptableObject assets — see
  `docs/content-schema.md` for the exact shape.
- **Legendaries:** the 7 folded-in Legendaries (Mew, Mewtwo, Rayquaza, Ho-Oh, Lugia, Kyogre,
  Groudon) currently have no rarity flag in the source sheet; per the design doc's carried-forward
  assumption, treat them as Legendary-tier (ultra-rare, PvE-only, full-party-wipe-risk
  encounters) unless a future decision says otherwise.
- Art: placeholder sprites until PokeAPI sprites (or a commissioned equivalent) are wired into the
  import pipeline.

## 9. Scope, IP & Distribution

Restating §1's scope note because it affects engineering decisions, not just legal ones:
- No monetization of any kind is planned. Don't build IAP, ads, or store-listing infrastructure.
- Treat this as private/non-commercial — don't publish it widely. If sharing with friends/testers
  ever comes up, revisit this section first.
- PokeAPI is the intended data/sprite source; follow their fair-use/attribution/caching guidance
  when pulling from it, especially in bulk (the content-import pipeline in §8 should cache rather
  than hit PokeAPI at runtime, partly for this reason and partly for offline play).

## 10. Risks & Open Questions

Project-level risks (mechanics-level open questions live in design doc §20 — don't duplicate them
here, go there):
- **Migration cost from the old code:** `Simulation`/`ShopEconomy` implement a materially
  different combat model (5-slot turn-based vs. 2-slot Step-based). Phase 0's rework is a real
  rewrite of the sim core, not a refactor — budget for it as such.
- **Determinism across platforms:** same risk as before — confirm Unity's float math is
  consistent enough across target devices for battle replays to match a future server-computed
  result. The design doc's discrete-Step model (vs. continuous real-time) is actually friendlier
  to this than the old model was (design doc §10.5).
- **Content balance:** 183 species, all-placeholder stats, no abilities yet — this is a large
  tuning surface. Don't try to hand-balance all 183 before Phase 0's exit criteria; balance the
  first slice, ship it, iterate.
- **Backend cost:** unchanged from before — pick a cheap/free tier when Phase 3 starts.
- **Scope creep:** the phase boundaries exist specifically to prevent building PvP/backend
  infrastructure before the core solo loop is proven fun. Resist starting Phase 3+ work early.

## 11. Next Steps

1. Rework `client/Assets/Scripts/Simulation` to match `docs/battle-sim-spec.md` (Lead/Support,
   Steps, charge meters) — Phase 0's main task.
2. Stand up the content-import pipeline (§8) for the first ~10–15 species from
   `docs/pokemon_stats_unique.xlsx`, with hand-authored passives as a concrete worked example
   before touching the rest of the roster.
3. Build the one hand-authored Location (Phase 0) end to end: PvE + Camp + Shop nodes, stubbed
   catching, stubbed Trailblazer.
4. Update `/shared/fixtures` with golden Step-log cases against the reworked sim before calling
   Phase 0 done.
