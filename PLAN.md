# PLAN.md — Auto-Battler Mobile Game

## 1. Overview & Vision

A mobile auto-battler in the spirit of *Super Auto Pets*: players draft creatures into a shop
phase, build and upgrade a small team, then watch automated battles resolve turn by turn.
Progression is driven by economy management (gold, rerolls, upgrades) and team composition
rather than direct combat input.

**Working name:** TBD — use a placeholder codename (e.g. `Critterbrawl`) in code/assets.
**Note on IP:** we are building an original game *inspired by* the genre's mechanics (drafting,
auto-combat, tier/upgrade systems are not protectable expression), but we should use our own
creature roster, names, art style, and UI rather than reproducing Super Auto Pets' specific pets,
names, icons, or text verbatim. Treat this as a design constraint from day one, not a later
cleanup pass.

**Project profile (confirmed):**
- Solo hobby project, no fixed deadline — optimize for low running cost, low maintenance burden,
  and always having a small but *complete and playable* slice rather than a large unfinished one.
- Client: Unity (C#).
- Backend: custom Node.js/TypeScript + PostgreSQL, introduced once single-player is solid.
- MVP battle mode: single-player vs. scripted/data-driven AI opponents (no live multiplayer yet).

## 2. Core Game Design

### 2.1 Loop
1. **Shop phase** (untimed or soft-timed): player has gold to spend. Shop offers N creatures
   drawn from a pool gated by the current "tier" (unlocked by turn number). Actions: buy, sell,
   reroll shop, freeze a slot, reorder/arrange board, combine 3 copies of the same creature at
   the same level to upgrade it.
2. **Battle phase**: player's board auto-battles an opponent's board with no further input.
   Creatures act based on stats (attack/health) and triggered abilities (e.g. on-faint, on-hurt,
   start-of-battle, on-level-up).
3. Loop repeats with increasing gold cap, shop tier, and opponent difficulty until the player
   loses all lives/hearts or reaches a win condition (survive N rounds / beat all opponents).

### 2.2 MVP opponent model
Opponents are **data-driven scripted teams** keyed by round number (a curated "bot roster"),
mirroring the fallback bots the genre uses when no live match is available. This is intentional:
the same data format (a serialized team snapshot) will later double as the format for real
player snapshots in async PvP, so we aren't building throwaway systems.

### 2.3 Content model
All creatures, abilities, tiers, and bot rosters are **data, not code**: defined in Unity
ScriptableObjects (source of truth in-editor) with a JSON export/import path so the same content
can eventually be validated against or served by the backend. Abilities are composed from a small
set of triggers (OnBattleStart, OnHurt, OnFaint, OnLevelUp, OnBuy, OnSell, OnTurnStart) and
effects (buff stat, deal damage, summon, gain gold, etc.) rather than one-off scripts per
creature, so adding content doesn't require new code per pet.

## 3. Architecture

```
+-------------------+          +--------------------------+
|   Unity Client     |          |   Node/TS Backend        |
|  (C#, MVP: fully   |  HTTPS   | (introduced in Phase 3)  |
|   offline capable) | <------> | Auth, cloud save,        |
|                    |  JSON    | leaderboards, later:     |
|  - Shop/board UI   |  API     | matchmaking + snapshot   |
|  - Battle sim      |          | storage for async PvP    |
|  - Battle sim      |          |                          |
|  - Local save      |          | PostgreSQL               |
+-------------------+          +--------------------------+
```

Key principle: the **battle simulator is a pure, deterministic function** of
`(teamA, teamB, rngSeed) -> battleLog`, isolated from Unity's MonoBehaviour/rendering layer. This
is critical for:
- Unit testing without spinning up scenes.
- Replaying a battle log as an animation independent of simulation speed.
- Eventually reimplementing the same algorithm server-side (Phase 5+) for anti-cheat validation
  in PvP, without fighting engine coupling.

Because the client is C# and the eventual server is TypeScript, we cannot literally share code
between them. Mitigation: the battle-sim rules live in one written spec
(`docs/battle-sim-spec.md`) plus a shared set of **golden test fixtures** (JSON: input teams +
seed -> expected battle log) that both the Unity test suite and the future Node test suite run
against, so both implementations are verified against the same ground truth and drift is caught
immediately.

## 4. Tech Stack

| Layer | Choice | Notes |
|---|---|---|
| Client engine | Unity (LTS version, C#) | 2D, uGUI or UI Toolkit for shop/board UI |
| Client testing | Unity Test Framework (NUnit), EditMode for sim logic, PlayMode for integration | |
| Backend runtime | Node.js + TypeScript | Introduced Phase 3 |
| Backend framework | Fastify (or Express) | REST JSON API |
| Database | PostgreSQL | Accounts, cloud saves, later: snapshots/leaderboards |
| Backend testing | Vitest + Supertest, test DB via Docker Compose | |
| CI | GitHub Actions | Lint + test on push/PR for both client and server |
| Source control | Git (this repo), trunk-based on `main` with short-lived feature branches | |
| Placeholder art | Free/CC0 asset packs or primitive sprites, clearly marked as placeholder | Swap later |

## 5. Repository Structure

```
/pets
  PLAN.md
  CLAUDE.md
  README.md
  /client                 # Unity project
    /Assets
      /Scripts
        /Simulation        # Pure C#, no MonoBehaviour deps — the battle sim engine
        /Data              # ScriptableObject definitions (Creature, Ability, Tier, BotRoster)
        /Gameplay           # MonoBehaviours: shop, board, turn flow
        /UI
        /Tests              # EditMode + PlayMode tests
      /Content              # ScriptableObject assets (data instances)
      /Art                  # Placeholder sprites
  /server                  # Node/TS backend (added Phase 3)
    /src
      /routes
      /services
      /db
    /test
  /shared                  # Language-agnostic specs & fixtures both sides test against
    /fixtures               # Golden battle-sim test cases (JSON)
  /docs
    battle-sim-spec.md
    content-schema.md
    architecture-decisions/  # short ADRs for notable decisions
```

## 6. Development Phases

Phases are milestones, not deadlines — move on only when the current phase is genuinely playable
end-to-end. Each phase should end with something you can actually play.

**Status (as of 2026-09-10):** Phase 0 and Phase 1 complete. Phase 2 in progress — Milestone 2A
(shop-phase ability triggers + roster/bot-roster expansion) done; Milestone 2B (home screen, run
history, stats screen) done.

**Phase 0 — Project scaffolding**
- Unity project created, folder structure above, git LFS or `.gitignore` tuned for Unity.
- Empty backend scaffold (not wired up yet) so structure exists but MVP doesn't depend on it.
- GitHub Actions CI skeleton (build/test client, lint/test server) even if server has nothing yet.

**Phase 1 — Core loop, vertical slice (single player, ~5-8 creatures)** — *complete*
- Data model for Creature/Ability/Tier (ScriptableObjects + JSON export).
- Deterministic battle simulator with a handful of triggers/effects, fully unit tested.
- Shop UI: buy/sell/freeze/reroll/upgrade, board arrangement.
- One scripted bot roster spanning ~10 rounds.
- Local save (PlayerPrefs or a save file) — no backend required to play.
- **Exit criteria:** you can play a full run start to (win or lose) with placeholder art.

**Phase 2 — Content & systems depth** — *in progress*
- Expand roster (aim for enough creatures that team-building has real decisions — SAP launched
  with a few dozen; don't block on hitting a specific number, expand until the loop feels good).
  — *done (Milestone 2A): 14 creatures + token, `OnBuy`/`OnSell`/`OnLevelUp`/`OnTurnStart` wired
  up in `ShopEconomy`, `GainGold` effect added.*
- More ability triggers/effects, tier progression tuning, difficulty curve pass on bot rosters.
  — *done (Milestone 2A): bot roster expanded 8 → 12 rounds.*
- Basic meta: run history, simple stats screen. — *done (Milestone 2B): a home screen (Continue /
  New Run / Stats) is now the app's entry point; run outcomes are logged to a cross-run history
  file and shown on a Stats screen reachable from Home.*

**Phase 3 — Gameplay**
- Update gameplay, assets, images, etc to make the game more fun and interesting.

**Phase 4 — Backend introduction**
- Node/TS + Postgres service: account creation (or anonymous device-id accounts), cloud save,
  basic leaderboard (best run length/score).
- Unity client integrates HTTP client for auth + save sync; must still work fully offline
  (local save is the source of truth, cloud sync is best-effort).

**Phase 5 — Polish & release prep**
- Real art pass (replacing placeholders), audio, juice/animation polish.
- Monetization decision (see §9) and store listing assets.
- iOS/Android build pipeline, store submission (App Store, Google Play).

**Phase 6 — Async PvP**
- Team "snapshot" format finalized (reuses the bot-roster data shape from Phase 1).
- Server-side matchmaking pairs snapshots by rank/rating.
- Battle sim reimplemented server-side in TypeScript, validated against the shared golden
  fixtures from `/shared/fixtures` for parity with the client sim.
- Client requests a match, downloads opponent snapshot + battle log (or recomputes locally from
  the snapshot — decide based on anti-cheat needs at the time), plays the animated result.

## 7. Testing Strategy

- **Battle simulation (highest priority):** pure C# unit tests (EditMode) covering individual
  abilities, trigger ordering, edge cases (simultaneous faints, empty board, max board size).
  These are cheap, fast, and where the most subtle bugs will live — invest here first.
- **Golden fixtures:** curated `(teamA, teamB, seed) -> expected log` cases in `/shared/fixtures`,
  run by both the Unity suite and (from Phase 5) the Node suite, to guarantee parity.
- **Integration/PlayMode tests:** shop transactions (buy/sell/gold math), save/load round-trip.
- **Backend tests (Phase 3+):** Vitest + Supertest against routes, using a disposable Postgres
  (Docker Compose) rather than mocks, so schema/query bugs surface in tests.
- **CI gate:** PRs must pass client EditMode tests and (once it exists) server tests before merge.
- No manual-only testing for simulation logic — if it's not covered by an automated test, assume
  it's broken.

## 8. Data & Content Pipeline (placeholder phase)

- Creatures/abilities authored as ScriptableObjects directly in the Unity editor — no external
  tool needed yet.
- Art: solid-color placeholder sprites or a free CC0 icon pack, one visually distinct shape/color
  per creature so playtesting isn't confusing; clearly labeled as temporary in `/Art/README.md`.
- Audio: none/system beeps for MVP.
- A lightweight JSON export of content data should exist from Phase 1 on (even if unused until
  Phase 3+) so the backend and shared fixtures have a real schema to target rather than guessing
  at one later.

## 9. Monetization & Analytics (deferred, decide before Phase 4)

Not needed for a hobby MVP, but flagging now so architecture doesn't paint us into a corner:
- Likely candidates: cosmetic-only IAP (skins), optional rewarded ads, or simply no monetization
  for a personal project. Avoid pay-to-win mechanics (extra rerolls/gold for money) — they
  undermine the design's skill expression.
- Analytics: defer to Phase 4; if added, prefer a lightweight self-hosted or privacy-respecting
  option over heavy SDKs, and gate behind a clear opt-in.

## 10. Risks & Open Questions

- **Determinism across platforms:** confirm Unity's math (float rounding) is consistent enough
  across target devices for battle replays to look identical to what the "server" would compute
  once PvP arrives. Consider fixed-point or integer math for stats if this becomes an issue.
- **Content balance:** with a small team (one person), full economy/ability balancing will be an
  ongoing tuning effort, not a one-time task — budget for it every content phase.
- **Backend cost:** even a minimal always-on Postgres + Node service has hosting cost; pick a
  cheap/free tier (e.g. a small managed Postgres + a low-cost Node host) when Phase 3 starts.
- **Scope creep:** the phase boundaries above exist specifically to prevent building PvP/backend
  infrastructure before the core single-player loop is proven fun. Resist starting Phase 3+ work
  early even if it's tempting.

## 11. Next Steps

Once this plan is approved:
1. Scaffold `/client` Unity project and `/docs` with `battle-sim-spec.md` (first real design doc:
   precise trigger ordering, tie-breaking rules, stat formulas).
2. Define the first ~5 creatures and their abilities as a concrete worked example before writing
   the generic ability system, so the system is designed against real cases, not guesses.
3. Set up GitHub Actions CI skeleton.
