# Testing Harness Plan

Status: **Proposal.** Written 2026-09-10 against the Phase 2 codebase (14 creatures, 12-round bot
roster, shop-phase triggers live). Complements PLAN.md §7 (Testing Strategy) and CLAUDE.md's
testing conventions — this document goes one level deeper on *how* to build the piece that's
currently missing: a harness that plays complete runs, not just isolated units.

## 1. Where things stand today

Assessed by reading the actual test suite, not assumed:

| Layer | Exists? | Coverage |
|---|---|---|
| `Simulation` unit tests (EditMode) | Yes — `BattleSimulatorTests.cs` | Strong: attack exchange, empty team, `OnFaint`/`OnBattleStart` chains, simultaneous double-faint, fainted-target skip, summon-on-faint, heal cap, round-cap draw, full 5v5 board |
| Golden fixtures | Yes — `GoldenFixtureTests.cs` + 5 fixtures | Checks outcome, faint order, *and* survivor stats per fixture — good contract for a future server port |
| Shop economy unit tests (EditMode) | Yes — `ShopEconomyTests.cs` (279 lines) | Buy/sell/reroll/freeze/combine, trigger firing |
| Save/load round-trip | Yes — `SaveSystemTests.cs` | |
| Content smoke test | Yes — `BotRosterContentTests.cs` | Converts + runs every real bot-roster round once (mirrored vs. itself), asserts it completes — but only checks outcome *type*, not the actual log |
| PlayMode integration | Yes — `RunControllerPlayModeTests.cs` (279 lines) | |
| **Full-run harness** (many seeds, many shop strategies, whole game start→finish) | **No** | — |
| CI actually gating merges | **No** — Unity job has `continue-on-error: true` | Client tests currently can't block a bad PR |
| Determinism of shop-phase RNG | **No** — `ShopEconomy.RefreshShop`/`Reroll` use `UnityEngine.Random.Range`, unseeded | Blocks reproducing a harness-found bug from its seed |
| Server tests | N/A — Phase 3 not started, stub `npm test` is intentional | |

So the unit/fixture layer is already solid. The two real gaps are: **(1)** nothing plays a
complete run the way a player would, across enough random variation to catch emergent bugs
(softlocks, crashes on specific content combos, degenerate economy), and **(2)** the safety net
that should catch regressions in CI is switched off. This plan addresses both, plus a few smaller
gaps found along the way.

## 2. The centerpiece: full-run regression harness

This is "the testing harness to run the game through." Goal: play hundreds of complete runs
(shop → battle → shop → ... → win/loss) headlessly, using the real content and real `ShopEconomy`/
`BattleSimulator` code, and catch anything a human clicking through the UI would eventually hit by
luck — just faster and reproducibly.

### 2.1 Where it lives

As an EditMode test class (`client/Assets/Scripts/Tests/FullRunHarnessTests.cs`), not a separate
project. Reasoning: `ShopEconomy` and content (`CreatureLibrary`, `BotRosterLibrary`) are
ScriptableObject-based and need `AssetDatabase`/Editor context to load, the same way
`BotRosterContentTests` already does — a standalone .NET console app could only cover the pure
`Simulation` slice, not the shop economy that actually drives a "full run." Runs via the same
`game-ci/unity-test-runner` batchmode invocation already wired into CI (§5).

### 2.2 Shop AI policies

A full run needs shop decisions; there's no human in CI. Implement 2-3 small, deterministic
policies as plain C# (not abilities/content — these are test infrastructure):

- **GreedyPolicy** — buy the cheapest affordable creature into an open board slot each turn until
  gold runs low, reroll once if all shop slots are unaffordable, never sells.
- **UpgradePolicy** — prioritizes buying toward 3-of-a-kind combines (checks shop for a match to
  an existing board creature before buying blind), sells weakest board creature when board is full
  and a combine isn't available.
- **RandomPolicy** — uniformly picks a legal action (buy/sell/reroll/freeze/skip) each turn from
  whatever's currently valid, seeded by the run seed. This is the one most likely to find genuine
  bugs, precisely because it doesn't play "sensibly."

Each policy is a pure function `(RunState, ShopConfig, CreatureLibrary, seed) -> shop actions`,
tested against the real `ShopEconomy` static methods — no UI, no MonoBehaviours involved.

### 2.3 What the harness runs and asserts

`[TestCase]` matrix: {GreedyPolicy, UpgradePolicy, RandomPolicy} × N seeds (start with ~50 in the
PR-gating CI job for speed, ~1000 in a nightly/scheduled job — see §5). For each (policy, seed):

1. Drive `RunController`'s underlying state machine (or call `ShopEconomy`/`RunState`/
   `BattleSimulator` directly if `RunController` has too much scene/UI coupling — worth checking
   when implementing) through shop phases and battles until win, loss, or a round cap, exactly like
   a real playthrough.
2. **Hard assertions** (any failure is a bug, not a balance question):
   - No unhandled exception anywhere in the loop.
   - The run terminates within a bounded number of rounds (catches a shop-side infinite loop the
     battle sim's own round/event caps don't cover).
   - `Gold` and `Lives` never go negative.
   - `Board.Count` never exceeds `ShopConfig.BoardMaxSize`.
   - Every `BoardCreature.Definition` reference is non-null and resolves through `CreatureLibrary`.
   - Save/load round-trips cleanly at least once mid-run (ties into `SaveSystemTests`, but under
     harness-generated non-trivial state instead of hand-built fixtures).
3. **Soft assertions / recorded metrics** (not pass/fail, just tracked — see §2.4):
   win rate by round, average gold spent per turn, average board size at each battle, which
   creatures/abilities never got bought or never fired (dead content — useful signal even before
   "balance" is a real concern).

### 2.4 Metrics output and balance-drift detection

Write aggregate metrics per CI run to a JSON artifact (e.g.
`client/TestResults/full-run-metrics.json`): win rate per round per policy, average run length,
per-creature pick rate. Two uses:

- **Immediate**: eyeball it after any content change (new creature, rebalanced stats) instead of
  guessing whether it broke something.
- **Later, once there's a baseline** (after a few weeks of real data): a small script compares the
  new run's metrics to the previous baseline and flags/warns (not hard-fails — balance is a
  judgment call, correctness bugs are not) if e.g. round-8 win rate swings more than N percentage
  points. This is a "nice to have once the numbers are stable enough to be meaningful," not a v1
  requirement — don't build the comparison tooling before there's baseline data to compare against.

### 2.5 Prerequisite: seed the shop-phase RNG

`ShopEconomy.RefreshShop`/`Reroll` currently call `UnityEngine.Random.Range` directly, which is
process-global and unseeded from the harness's point of view. This matters for the harness because
when it finds a bug, "seed 4821 with RandomPolicy" needs to actually reproduce the same shop
offers every time, or the repro is useless. Fix: seed `UnityEngine.Random.InitState(seed)` once at
the start of each harness-driven run (simplest fix, no production code change) — or, better long
term, thread a seeded RNG (mirroring `Simulation.DeterministicRandom`) through `ShopEconomy`
explicitly instead of relying on Unity's global RNG state, which is fragile if anything else in
the process consumes `Random` between harness iterations. Worth a short ADR either way since it's
a determinism decision (CLAUDE.md flags this pattern for `battle-sim-spec.md` §7's rationale).

## 3. Content validation / linting layer

`BotRosterContentTests` today only proves *one* mirrored battle per round completes. Expand into a
general content-linter test class (`ContentValidationTests.cs`) that checks, independent of any
particular battle:

- Every `CreatureDefinition.Id` is unique and non-empty.
- Every `AbilityDefinition.Effects[].SummonTemplate` (when `Type == Summon`) resolves to a real
  creature/template, not a dangling reference.
- Every `BotTeamDefinition` slot's creature is tier-eligible for that round (`Tier <=
  ShopConfig.TierForRound(round)`), or is explicitly a `Tier <= 0` summon-only template — catches
  "oops, put a tier-4 creature in round 2's bot team" before it ships.
- `ContentJsonExporter`'s output round-trips: export then re-parse produces the same shape
  documented in `content-schema.md` §5 — catches schema drift between the doc, the exporter, and
  reality.

This is cheap (all EditMode, no battle simulation needed) and catches the most common class of
"solo dev added a new creature at 11pm and fat-fingered a field" bug before it reaches a battle at
all.

## 4. Snapshot/regression testing for real content

`GoldenFixtureTests` pins exact behavior for small hand-built scenarios. `BotRosterContentTests`
runs real content but only checks outcome type. Add a middle layer: for each real bot-roster round,
snapshot the *full battle log* (not just outcome) from a fixed reference attacker team, check it
into `/shared/fixtures` or a parallel `/shared/snapshots` directory, and diff future runs against
it. This catches "an unrelated trigger-ordering change silently altered round 6's battle" — the
kind of regression that's invisible to "did outcome type stay the same" but exactly what
golden-fixture testing exists to catch (PLAN.md §3). Update snapshots deliberately (like updating
`GoldenFixtureTests` fixtures) whenever a sim-rule change is intentional, never silently.

## 5. CI wiring

Two fixes, ordered by urgency:

1. **Urgent — the safety net is currently disabled.** `.github/workflows/ci.yml`'s Unity job has
   `continue-on-error: true` because `UNITY_LICENSE` isn't set up yet, per its own comment. Right
   now *no client test failure can block a merge*, including the strong suite that already exists.
   Get a Unity Personal license activated in CI (`game-ci` documents the manual activation-file
   flow for a free license — no paid seat needed for a solo/hobby project) and remove
   `continue-on-error` once it's verified green. This isn't part of the harness build-out, but it
   should happen first or in parallel — building more tests that still can't gate merges is lower
   value than fixing the gate.
2. **Add the harness as its own CI job**, separate from the existing fast EditMode job so a slow
   1000-seed sweep doesn't block every PR:
   - **PR-gating**: `testMode: EditMode`, harness `TestCase`s capped to ~50 seeds/policy (couple
     minutes) — catches crashes/hard-assertion failures on every PR.
   - **Scheduled (nightly or weekly `workflow_dispatch`/`schedule` trigger)**: same harness, ~1000+
     seeds/policy — better odds of surfacing a rare edge case, and it's where the metrics artifact
     (§2.4) accumulates the history a future balance-drift check would need.

## 6. What stays manual (and how to keep it lightweight)

Some things genuinely aren't worth automating for a solo hobby project at this stage: does the
shop UI *feel* good, is placeholder art distinguishable enough to play by, does a full run feel
fun. CLAUDE.md already requires pressing Play and clicking through before calling UI work done —
keep that, don't try to replace it with automation. The only addition: a short
`docs/PLAYTEST-CHECKLIST.md` (5-10 bullet points: new-run flow, buy/sell/freeze/reroll each work
visually, a battle animates and resolves, win and loss screens both reachable, stats screen shows
a completed run) to run through after UI changes, so "did I actually check this" doesn't rely on
memory. Not a testing-framework concern — just cheap insurance.

## 7. Build order

Roughly in priority order, each step shippable on its own:

1. Fix CI's Unity license gate (§5.1) — makes every existing test actually matter.
2. Seed the shop-phase RNG (§2.5) — small, and everything after this depends on runs being
   reproducible from a seed.
3. Content validation/linting tests (§3) — cheap, catches a common failure mode immediately.
4. Full-run harness with hard assertions only (§2.1-2.3), PR-gating job at a small seed count.
5. Metrics collection + nightly/extended job (§2.4, §5.2).
6. Content snapshot regression tests (§4).
7. Playtest checklist doc (§6) — whenever there's a natural pause in UI work.
8. Balance-drift comparison tooling (§2.4, deferred half) — only once there's real baseline data
   to compare against, likely a few weeks into Phase 2/3.

Everything here targets the client, matching the project's actual phase — no server-side work
implied; PLAN.md §7's existing plan for server tests (Vitest + Supertest against a real Postgres,
shared-fixture parity) still holds once Phase 3 starts.
