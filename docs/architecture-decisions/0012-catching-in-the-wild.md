# ADR 0012: Catching in the Wild

**Status:** Accepted
**Date:** 2026-09-13
**Implements:** design doc §12.1 (catching in the wild), replacing the Phase 0 "pick 1 from
defeated" stub as the *primary* acquisition path. The stub is not removed — see Consequences.

## Context

Catching was the last unbuilt piece of the PvE node's loop. The scaffolding had been waiting for it
for some time: `OnDemandStepRunner` exists specifically so "the catching interaction layer can
mutate the enemy line-up at a Step boundary", and `Battle.unity` has carried a drawn-but-disabled
Throw button. What stood in the way was that design doc §12.1 assumes things this build doesn't
have — Pokéballs bought at a Shop, and a *level* to be under-levelled relative to.

## Decisions

### 1. Balls are a run-level inventory, stocked at run start

`RunState.Balls` (`Meta/BallInventory`) holds a count per tier. §12.1 buys them at the Shop and
there is no Shop, so `RunBootstrapper` grants `BallInventory.GrantStartingStock` instead. This is a
stand-in, marked as one in both the code and `PLAN.md`: when the Shop lands it buys into this same
inventory and the starting stock shrinks or goes away. The alternative — building a Shop first —
would have blocked a mechanic whose scaffolding was already in place on one that isn't started.

The stock leans toward Poké Balls (5/2/1) so that the EXP cap below is a constraint the player
actually meets rather than a rule they never see.

### 2. "Under-levelled catch" is an EXP cap, because EXP *is* level here

§12.1: a low-tier ball can still succeed on a high-level Pokémon "but yields an under-levelled
catch; only higher-tier balls guarantee the target's true level". There is no level field to cap —
ADR 0008 made EXP the single growth number and stats derive from it. So `BallCatalog.ExpCap` caps
the EXP a catch keeps: 6 for a Poké Ball, 12 for a Great Ball, uncapped for an Ultra Ball.

The cap is applied *after* the run's catch-up floor (`ExperienceResolver.CatchUpExp`), so it
genuinely binds: a Poké Ball yields a capped mon even in a run whose floor is higher. Applying it
before would have let the floor silently undo the tier difference, which is the whole reason to
carry better balls.

### 3. The odds formula weights HP heavily and status flatly

`Meta/CatchOdds`: `base(tier) + 0.45 × (1 − hpFraction) + 0.15 if statused`, capped at 0.95.

- HP is the big lever because "weaken it first" should be the loop, not a suggestion. At full
  health a Poké Ball is 10% and an Ultra Ball 35%; at 1 HP they are 55% and 80%.
- Status is a flat bonus, equal across all four conditions. §12.1 asks for "a meaningful bonus" and
  says nothing about ranking them; making sleep better than poison is a balance decision that wants
  playtesting, not a guess baked into the formula now.
- The 0.95 cap means no throw is ever certain — the "will it break free" beat needs the possibility
  of breaking free.

Every number here is a placeholder in the same sense as the rest of the game's balance values.

### 4. A catch draws from the battle's own RNG

`OnDemandStepRunner.Rng` is exposed and `CatchOdds.Roll` draws from it, rather than the catch having
its own generator. A run is meant to be reproducible end to end from its seed (design doc §10.5),
and a catch is part of what happened in the fight. The consequence — that throwing a ball shifts
every subsequent Step, so a replay only reproduces if the same throws are made at the same
boundaries — is correct, and is part of why PvE uses the on-demand runner rather than a precomputed
log.

### 5. A catch raises `Caught`, not `Faint`, and removal lives in the simulator

§12.1 says a caught mon leaves "exactly as if it had fainted". Mechanically that is true — the
Support steps up, and taking the last one ends the fight — so `BattleSimulator.RemoveCaught` applies
the same consequences as a faint, and lives in the simulator because line-up mutation and its
promotion events are the simulator's rules. A second place that edits a line-up is how two copies of
those rules drift apart.

But it raises a distinct `Caught` event kind, because the two mean opposite things downstream: the
post-fight stub reads `Faint` events to decide what to offer, and a caught mon is already in the
Box. The simulator still has no concept of a ball or a catch roll — it is told a combatant is
leaving and applies the consequences.

### 6. The Throw button is the trigger; the ball column is selection plus a drag source

The controls are a column of three ball rows — one per tier, each showing its count and its **live
odds against the current enemy Lead** — sitting immediately right of the Throw button in the party
strip, so the tiers and the button that throws them read as one control. Three ways in, all
resolving identically:

- **Throw button** — throws the selected tier. The main trigger.
- **Tap a row** — selects that tier. It deliberately does *not* throw: a stray tap on a 46-pixel row
  shouldn't spend a ball.
- **Drag a ball onto the enemy Lead** — throws that tier directly.

Selection defaults to the weakest stocked tier, so Throw can never quietly spend an Ultra Ball the
player was saving, and re-defaults when the selected tier runs out.

The column and the result callout are built by `BattleSceneBuilder` (the Battle scene is generated,
not hand-authored, so this is the house way to change it) and left empty; `CatchTrayView` fills the
rows at runtime, since counts and odds come from the run and the fight in progress.

**But the controller builds stand-ins when the scene doesn't supply them.** Depending on the scene
alone meant that until someone re-ran *Pets > Build Battle Scene*, both serialized fields were null,
the rows were parented to nothing, and the result was no balls, no callout, no error — and a Throw
button that appeared to do nothing. A feature that only works if someone remembers an editor menu
item isn't finished. The fallback logs a warning naming the menu item, so the baked version still
gets built eventually.

The column is also **140 wide rather than the 170 it started at**. The strip is six 146px party
slots plus gaps plus the 150px Throw button — 1110 of the reference canvas's 1280 before the column
exists at all. At 170 the row came to 1292, overflowed, and hung off the right edge, which looks
exactly like not being drawn. At 140 with a 10px gap the row is 1260. The runtime fallback can't
assume even that much room, since an un-rebuilt scene laid its strip out with no column in mind, so
it measures and drops the column to the *left* of the Throw button when the right doesn't fit. The drop target
is still attached in code, to the enemy *Lead* sprite only — which is what enforces §12.1's rule
that Support and further-back enemies aren't valid targets: the slot is fixed and the mon under it
changes as promotions happen. Attaching it also turns on that Image's `raycastTarget`, which is off
by default because nothing else on the battlefield is interactive.

**Rows are built once and updated in place.** The first implementation destroyed and rebuilt them on
every refresh, and a refresh happens after every Step — which destroyed the very icon a player was
dragging, so the drop had nothing to land on and no catch ever resolved. Refreshes are also skipped
outright while a drag is in progress, so nothing moves under the pointer mid-gesture.

### 7. A throw always shows what it did

A resolved throw holds the fight for a beat on "...", then shows either "Caught Pidgey!" in the
positive colour or "Pidgey broke free!" in the danger colour, then carries on into the next Step
either way. It's a framed panel with a short scale-up as it appears, not bare text, so it reads as
something that just happened rather than a label that was always there — the frame has to be the
panel's parent because a uGUI object can hold only one Graphic, and it's the frame the controller
shows and hides. Without it a successful catch is a mon silently vanishing mid-fight and a failed one is
nothing happening at all — which is indistinguishable from the control being broken.

The target's name is read *before* the throw resolves, because a successful catch removes it from
the line-up and there is then nothing left to name.

## Consequences

- **The "pick 1 from defeated" stub stays.** It is no longer the only way to acquire a mon, but it
  is not made redundant either: it covers wild mons that *fainted*, which catching by definition
  doesn't. The two are complementary. `BattleScreenController` keeps a set of caught instance ids
  as belt-and-braces against the two paths ever both offering the same mon.
- **Catching is PvE-only**, enforced at the screen (`CatchingOffered`) rather than in `Meta`, since
  which kind of node a fight belongs to isn't visible from a `BattleState`. Gym, PvP and the dev
  random battle don't build a tray at all.
- **A throw made mid-animation is queued** to the Step boundary the Step is about to reach, per
  §12.1's "resolves at the next Step boundary"; a throw made while paused resolves immediately,
  because a paused fight is already sitting on one.
- **A run that predates catching gets topped up** when it first reaches a PvE fight
  (`SetUpCatching`), rather than only at run creation. Otherwise an in-flight save is stranded with
  an empty inventory, a dead column and a dead button, with nothing on screen explaining why.
- **Balls are currently finite and unreplenishable within a run.** Once the starting stock is gone,
  catching is over until the next run. That's a direct consequence of there being no Shop, and is
  the main reason the Shop is now the natural next piece of work.

## Note on an RNG property found while testing this

`DeterministicRandom` is xorshift32 seeded directly with the given int, and it diffuses poorly from
a small state: the *first* draw off seeds 1, 2, 3, … runs 369, 738, 1107, 1476 — near-perfectly
linear in the seed. Aggregate uniformity over many seeds is fine (1025 hits where 1000 were expected
across 10,000 seeds), and draws within a single stream are fine, so **this is not a game bug**: live
seeds are `RunSeed`-derived (`Environment.TickCount` XOR node position) and large, where first draws
are properly spread.

It is a trap for *tests*, and it caught this one: a test that threw at 10% odds with
`new DeterministicRandom(seed)` for seed = 1, 2, 3… saw the first two throws both land, because 369
and 738 are both under the 1,000 threshold. Consecutive low seeds are not independent trials. Tests
that need repeated rolls should use one stream, as a real battle does. No change to
`DeterministicRandom` is proposed — the fixed, documented algorithm is a deliberate contract for a
future server-side reimplementation (battle-sim-spec.md §7), and re-seeding it would be a change to
that contract for a problem that only appears in test-only usage.
