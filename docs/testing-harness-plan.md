# Testing Harness Plan

Status: **Partially implemented**, updated 2026-09-11 against the post-pivot Phase 0 codebase
(Lead/Support/Step battle model, Pokémon-themed content, `Meta`/`Gameplay` node-map run loop — see
[ADR 0001](architecture-decisions/0001-pivot-to-pokemon-roguelite.md)). This doc originally
targeted the pre-pivot shop-drafting auto-battler; that entire codebase (`ShopEconomy`,
`CreatureLibrary`, the old `Simulation`) was deleted, not kept alongside, so this revision
rewrites every concrete detail against the current architecture. The core *idea* — a harness that
plays complete runs, not just isolated units, since nothing else does — is unchanged. Complements
PLAN.md §7 (Testing Strategy) and CLAUDE.md's testing conventions.

**What's done, from §7's build order below:**
- §3 (content validation/linting) — `client/Assets/Scripts/Tests/ContentValidationTests.cs`.
- §2.1-2.3 (full-run harness, hard assertions, PR-gating) —
  `client/Assets/Scripts/Tests/FullRunHarnessTests.cs`, two catch policies × 50 seeds by default.
- §2.4 (metrics collection) and §5.2 (extended nightly sweep) — the harness writes
  `client/TestResults/full-run-metrics-<policy>.json`; a scheduled CI job
  (`full-run-harness-nightly` in `.github/workflows/ci.yml`) reruns it with
  `PETS_HARNESS_SEED_COUNT=500` and uploads the output as a build artifact.
- §6 (playtest checklist) — `docs/PLAYTEST-CHECKLIST.md`.
- §2.5's old RNG-seeding concern doesn't apply to the new architecture at all — see §2.5 below.

**What's still blocked on you, not implemented here:**
- §5.1 (fix the CI Unity license gate) — needs a real `UNITY_LICENSE` (+ email/password or
  serial) added as repo secrets, which only you can create. Both Unity CI jobs still have
  `continue-on-error: true` until that exists; nothing here can safely flip that off.
- §4 (content snapshot regression) — still not separately built; `PokemonContentTests.cs`'s
  `FullPvEFight_ResolvesDeterministically_AgainstRealCuratedContent` test already does a live,
  in-test version of this (replays the same seed against real content and asserts the event log
  matches exactly), which covers most of what a checked-in snapshot would buy — see §4.
- §2.4's balance-drift *comparison* tooling — still explicitly deferred until there's baseline
  metrics data to compare against.
- The new/rewritten tests haven't been run inside the Unity Editor (no Unity available in the
  environment that wrote them) — matched closely against the existing suite's exact types/call
  patterns (`PvEClashController`/`LocationFlowController`'s real logic, read in full before
  porting), but verify they compile and pass before relying on them.
- **A design question the harness logic surfaced just from reading the code, not from running
  it** — flagging because it's the kind of thing this harness exists to catch: a lost PvE fight
  doesn't clear its node, so the player retries the same encounter (`LocationFlowController.
  OnPvEResolved`). But nothing about the player's line-up or the wild encounter changes between
  retries in Phase 0 (no reordering, no shop, the Camp attack buff is consumed on the first
  attempt) — the same seed feeds both `EncounterGenerator` and `PrecomputedStepLogRunner` on every
  retry. That means a loss is deterministic: if the fixed starting Lead/Support pair can't beat a
  given node once, it can't ever beat it, and the run just burns down Morale (3, no recovery) to 0
  over three identical retries. Worth confirming once the harness actually runs — its per-seed
  `outcome`/`pveLosses` metrics should make this visible immediately if it's happening often.

## 1. Where things stand today

Assessed by reading the actual test suite, not assumed:

| Layer | Exists? | Coverage |
|---|---|---|
| Step-sim unit tests (EditMode) | Yes — `StepSimulatorTests.cs` | Strong: simultaneous Lead exchange, charge accrual/threshold, all three statuses (Paralyzed halves charge, Asleep zeroes it, Poison stacks, Burn doesn't), shields, damage reduction, lifesteal, same-Step tie-breaking (role, speed, side), Lead/Support promotion on faint, dual-runner (precomputed vs. on-demand) parity, determinism, stalemate/draw |
| Golden fixtures | Yes — `GoldenFixtureTests.cs` + fixtures in `/shared/fixtures` | Rewritten for the Lead/Support fixture shape (team-of-5 → ordered line-up doesn't carry forward, per PLAN.md §7) |
| Real content | Yes — `PokemonContentTests.cs` | ≥13 curated species each resolve to a real passive, stat conversion round-trips, a full PvE fight against real content resolves and replays identically on the same seed, on-demand/precomputed runner parity |
| Meta layer (run/encounter/EXP/camp/catch) | Yes — `RunMetaTests.cs` | Node advancement, encounter-generation bias/determinism/fallback, EXP/level-up (single and multi-level), Camp EXP+buff grant, battle-reset transient-state clearing, catch resolution |
| PlayMode / scene wiring | Yes — `ForestScenePlayModeTests.cs` | Scene bootstraps a 2-mon line-up + 5-node map; clicking the Map tab then Go on a PvE node plays a battle and returns to the map; Team tab shows the starting line-up |
| **Full-run harness** (many seeds, whole run start→finish) | **No** (until this PR) | — |
| CI actually gating merges | **No** — Unity job has `continue-on-error: true` | Client tests currently can't block a bad PR |
| Determinism | **Yes, already** — every relevant call (`EncounterGenerator.GenerateWildLineUp`, `PrecomputedStepLogRunner.Run`) takes an explicit `int seed` and uses a local `DeterministicRandom`, not `UnityEngine.Random` global state | Unlike the old `ShopEconomy`, nothing here needs a harness-side seeding fix — see §2.5 |
| Server tests | N/A — Phase 3 not started | |

So — same conclusion as before the pivot, reached independently against the new code: the unit/
fixture/content layer is already solid, arguably more thorough than the pre-pivot suite was. The
gap is still that nothing plays a **complete run** (Map → node → node → ... → forest cleared or
out of Morale) across enough seed variation to catch emergent bugs, and the CI safety net is still
switched off.

## 2. The centerpiece: full-run regression harness

Goal: play many complete runs headlessly, using the real content and the real
`Meta`/`Simulation` resolution logic, and catch anything a human clicking through the Forest hub
would eventually hit by luck — just faster and reproducibly.

Phase 0's Forest is a **fixed, linear 5-node sequence** (PvE/PvE/Camp/PvE/PvE, `Forest
LocationFactory`) with no branching, no shop economy, and no line-up reordering yet (all Phase 1).
That's a much smaller decision space than the old 12-round bot roster + shop economy had — so this
harness is deliberately smaller in scope than the original plan's was, matched to what actually
exists right now rather than over-building ahead of Phase 1.

### 2.1 Where it lives

`client/Assets/Scripts/Tests/FullRunHarnessTests.cs`, an EditMode test class — same reasoning as
before: content (`PokemonSpeciesLibrary`) needs `AssetDatabase`/Editor context to load, the same
way `PokemonContentTests.cs` already does. It's a direct, UI-free port of the real per-node
resolution logic already checked in — `Gameplay/PvEClashController.cs`'s `Begin`/`Resolve` for PvE
nodes, and `Gameplay/LocationFlowController.cs`'s node-advancement/Morale/terminal-state handling
for both node types — calling straight into `Meta.CampResolver`/`CatchResolver`/
`EncounterGenerator` and `Simulation.PrecomputedStepLogRunner`, not through the MonoBehaviours
(those already have PlayMode coverage in `ForestScenePlayModeTests.cs`).

### 2.2 Catch policies

The only real per-run player decision in Phase 0 is whether to catch a defeated wild mon after a
PvE win (`CatchResolver`) — there's no shop, no reordering, nothing else to vary yet. So instead of
the richer buy/sell/reroll policy set the old economy-driven harness needed, there are just two:

- **CatchAll** — catches every defeated wild mon after every PvE win.
- **CatchNone** — never catches.

Both exist mainly to exercise `RunState.Box` growing across a whole run (untested combinatorics),
not because catching is expected to meaningfully change battle outcomes yet (a caught mon doesn't
enter the active line-up in Phase 0 — no reordering exists). Revisit this policy set once Phase 1
adds a shop/reordering — that's when a richer decision space (and therefore richer policies) will
actually exist to test.

### 2.3 What the harness runs and asserts

Two `[Test]` methods (CatchAll, CatchNone) × N seeds (50 by default, overridable via
`PETS_HARNESS_SEED_COUNT` — see §5.2). For each (policy, seed):

1. Build a fresh `RunState` with the fixed starting Lead/Support pair (`AllSpecies[0]`/`[1]`,
   matching `RunBootstrapper`'s own fallback) and the Forest's 5 nodes, then resolve nodes in
   sequence — PvE fights via the real `PrecomputedStepLogRunner`, Camp via the real
   `CampResolver` — until the run reaches a terminal state (forest fully cleared, or
   `RunState.IsRunOver` from Morale hitting 0), or a 50-attempt safety cap (generous — real play
   needs at most ~8 node attempts total; see the code comment for the exact bound).
2. **Hard assertions** (any failure is a bug):
   - No unhandled exception anywhere in the loop.
   - The run reaches a terminal state within the safety cap.
   - `Morale` never goes negative.
   - `LineUp` always has exactly 2 mons (Phase 0 never adds/removes from it mid-run).
   - Every mon in `LineUp`/`Box` resolves back to a real species via `PokemonSpeciesLibrary.
     GetById` (no orphaned/unresolvable `SpeciesId`).
3. **Recorded metrics** (not pass/fail, just tracked — see §2.4): outcome (cleared vs. out of
   Morale), nodes cleared, PvE win/loss counts, mons caught, final Morale.

### 2.4 Metrics output

Same mechanism as originally planned: each policy writes
`client/TestResults/full-run-metrics-<policy>.json` (per-seed outcome/win-loss/catch/Morale data).
Useful immediately for eyeballing after a content or balance change; a comparison-against-baseline
step is still explicitly deferred until there's real accumulated data (same reasoning as before —
don't build comparison tooling before there's a baseline worth comparing against). This is also
where the "is a loss actually a permanent trap" question flagged at the top of this doc becomes
answerable from real numbers instead of code-reading.

### 2.5 RNG determinism — no fix needed this time

The old plan's §2.5 was a prerequisite fix (`ShopEconomy` used unseeded global `UnityEngine.
Random`). The new architecture doesn't have that problem: `EncounterGenerator.GenerateWildLineUp`
and `PrecomputedStepLogRunner.Run` both take an explicit `int seed` and construct their own local
`Simulation.DeterministicRandom` — no global RNG state anywhere in the path this harness drives.
A harness-found failure reproduces exactly from `(policy, seed)` with no extra work. Worth noting
as a real improvement over the pre-pivot code, not just a non-issue.

## 3. Content validation / linting layer

`PokemonContentTests.cs` already checks a lot directly (≥13 species each with a resolvable,
non-empty passive; stat conversion; a real fight resolving). `ContentValidationTests.cs` adds what
that file doesn't, without duplicating it:

- Species ids are unique and positive (`PokemonSpeciesLibrary.GetById` does a silent
  `FirstOrDefault` — a duplicate id would silently shadow another species rather than error).
- Passive ids are unique and non-empty, same reasoning for `PassiveLibrary.GetById`.
- Every species has positive Attack/Health/Speed (zero Health enters battle already fainted; zero
  Speed never accrues charge — both are authoring-mistake signals, not intentional design space).
- A species with `HasSecondType` set doesn't have `Type2 == Type1` (a copy-paste mistake the
  schema itself can't prevent).

Cheap (EditMode, no battle simulation), and catches the same class of "fat-fingered a field while
adding content" bug the old version targeted, adapted to what's actually authorable now (no more
`SummonTemplate`-style dangling references — the new `EffectType` vocabulary doesn't have an
effect that references another asset).

## 4. Snapshot/regression testing for real content

The original plan for this layer was: check in a full expected Step-log for real content and diff
future runs against it, since `PokemonContentTests.cs`'s ancestor (`BotRosterContentTests`) only
checked outcome type. That's less necessary now — `PokemonContentTests.cs`'s
`FullPvEFight_ResolvesDeterministically_AgainstRealCuratedContent` already builds a real matchup,
runs it, and **replays the identical seed a second time, asserting every event in the log matches
exactly** — which proves determinism against real content in-test, without a checked-in baseline
to maintain. What it doesn't catch is an *unintentional* change to real content's behavior between
commits (a checked-in snapshot would diff against yesterday's log, not just today's replay against
itself). Still deliberately not building that separately: doing so blind (without a live Unity run
to generate a verified starting baseline) risks checking in a "golden" log that was never actually
correct. Revisit if/when a content or sim-rule change is suspected of silently changing real-fight
behavior and the in-test replay check isn't enough to catch it.

## 5. CI wiring

Unchanged from the original plan — the pivot didn't touch `.github/workflows/ci.yml`:

1. **Still urgent — the safety net is disabled.** The Unity job has `continue-on-error: true`
   because `UNITY_LICENSE` isn't set up. No client test failure can block a merge right now,
   including the strong suite that already exists. Get a Unity Personal license activated in CI
   (`game-ci` documents the manual activation-file flow — free, no paid seat needed) and remove
   `continue-on-error` once verified green. Still not something this doc/PR can do — needs your
   secrets.
2. **The harness has its own CI job**, `full-run-harness-nightly`, separate from the PR-gating
   EditMode job:
   - **PR-gating**: the harness runs as part of the normal EditMode suite at the default 50
     seeds/policy (fast).
   - **Scheduled** (`schedule`/`workflow_dispatch` trigger, added to `ci.yml`'s top-level `on:`):
     reruns just `FullRunHarnessTests` at `PETS_HARNESS_SEED_COUNT=500`, uploads
     `client/TestResults/full-run-metrics-*.json` as a build artifact.

## 6. What stays manual (and how to keep it lightweight)

Same reasoning as before, updated for the current UI: does the Forest hub *feel* good, is
placeholder content legible, does a run feel fun to play through by hand — not worth automating at
this stage. CLAUDE.md already requires pressing Play and clicking through the affected flow before
calling UI work done. `docs/PLAYTEST-CHECKLIST.md` gives that a short, repeatable shape so it
doesn't rely on memory — see that file directly; it's been updated to match the current
Map/Team/Shop/Center hub and PvE/Camp/RunOver overlays rather than the old shop-phase screens.

## 7. Build order

1. Fix CI's Unity license gate (§5.1) — makes every existing test actually matter. Still blocked
   on you.
2. ~~Seed the RNG~~ — not needed; the new architecture is deterministic by construction (§2.5).
3. Content validation/linting tests (§3) — done, `ContentValidationTests.cs`.
4. Full-run harness with hard assertions (§2.1-2.3) — done, `FullRunHarnessTests.cs`.
5. Metrics collection + nightly/extended job (§2.4, §5.2) — done.
6. Content snapshot regression (§4) — deliberately not built; `PokemonContentTests.cs`'s in-test
   replay check covers most of the need for now.
7. Playtest checklist doc (§6) — done, updated for the current UI.
8. Balance-drift comparison tooling (§2.4, deferred half) — only once there's real baseline data
   to compare against.

Everything here targets the client, matching the project's actual phase — no server-side work
implied; PLAN.md §7's plan for server tests (Vitest + Supertest against a real Postgres,
shared-fixture parity) still holds once Phase 3 starts.
