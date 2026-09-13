# ADR 0006: Leveling, Evolution and Difficulty Are Paced Across a Six-Location Run

**Status:** Superseded by ADR 0007 (an eight-badge run); kept for its reasoning
**Date:** 2026-09-13
**Supersedes:** the numbers and growth model in [ADR 0005](0005-exp-as-a-small-counter.md) (its
derived-stats decision survives intact; see below)

## Context

ADR 0005 made EXP visible: 1 point per battle won, 2 per duplicate combined, +10 to every stat per
point, an evolution every 3 points. It said, in its own Consequences, that three wins to an evolution
"is fast, and it is the first number to look at when balance starts". That was right, and it was
sized for the run that existed — one Location, five or six nodes long.

A run is meant to be five or six Locations (design doc §2, §4). Measured against that, every part of
the model breaks in a different direction:

1. **Growth never stops and evolution finishes immediately.** Six Locations pay a mon roughly 30 EXP:
   +300 to every stat, against base stats averaging 57, and ten evolution thresholds crossed against
   a roster whose longest chain is two. Every evolution a run would ever see happened in Location 1,
   and the remaining EXP just kept inflating stats.

2. **A flat gain converges the roster.** +10 to everyone is a rounding error on a Charizard and a
   transformation on a Caterpie. After a dozen points, every mon in a run has approximately the same
   numbers — the growth model erases the differences that make catching things interesting, and it
   does it faster the longer the run goes.

3. **Nothing else scaled at all.** `EncounterGenerator` and `GymTeamGenerator` built base-stat
   instances from the whole unfiltered 183-species roster (ADR 0004's open item). Simulated against
   the real roster: two fresh teams win 39% / lose 40% / draw 21%; a player with one Location's
   growth wins **97%**; a player with three Locations' growth wins **100%**. There was no curve to
   be fair against, and the actual difficulty of a Location-1 fight was decided by whether the pool
   rolled a Caterpie or a Rayquaza.

4. **A mon acquired after Location 1 was dead weight.** It starts at 0 EXP while the line-up sits 15
   ahead. Catching and Pokémon Center adoption are two of the three acquisition paths in the design,
   and both stopped being worth using almost immediately.

5. **Fights were too short for any of it to be visible.** Damage is flat Attack per Step and the
   roster's median Health/Attack is 0.88, so **63% of Leads one-shot the opposing Lead**: a median
   fight was 2 Steps, and 21% of even matches ended in a mutual wipe that is neither a win nor a
   loss. Passives fired once, if at all.

6. **There was nowhere for a six-Location curve to happen.** Beating a Gym ended the run at Home
   (ADR 0003) because no Region Hub existed.

## Decision

**1. EXP buys levels; stats follow from the level.**

`LevelCurve` (Meta) owns the pacing: level 1 to a cap of 12, with level *L* costing `6 + L` EXP to
leave. `StatGrowth` (Data) owns what a level is worth. ADR 0005's central decision — stats are
*derived* from `species + growth`, never accumulated into — is kept exactly; only the function
changed.

**2. Growth is proportional to the species' own base: +10% plus a flat +3 per level.**

A mon at the cap is ~2.1x its base, before evolutions. This keeps a strong species strong and a weak
one distinct, which the flat model did not; the flat term keeps the bottom of the roster (Attack 10,
Health 1) from being left behind by a percentage of almost nothing.

**3. Speed does not grow with level.**

It is a species trait, changed only by evolving. Speed drives the charge meter against a fixed
threshold of 100, and the roster's median Speed of 60 already fires a passive every other Step.
Scaling it would end every run with every mon on the board firing every Step — erasing the fast/slow
distinction exactly when a run has the most mons to tell apart. This is the one place the model
deliberately does *less* than "levels make you stronger".

**4. Health carries a flat x3 scalar.**

Applied in the same derived-stat calculation, which puts a median fight at ~5 Steps instead of 2 and
drops draws from 21% to ~11%. It is battle balance riding in the stat layer on purpose: the
simulator stays a pure function of the stats it is handed (`docs/battle-sim-spec.md`), and the golden
fixtures, which carry explicit stats, are untouched by it.

**5. Evolution is gated on level, at `ExperienceResolver.EvolutionLevels` = {4, 9}.**

Counted per instance against `TimesEvolved`, as before (that part of ADR 0005 was right, and is why
Pikachu still evolves on its first threshold). Against the curve, that puts the first evolution early
in Location 2 and the final form at the end of Location 4 — fully evolved for the back third of a
run, which is the mainline games' shape.

**6. Awards depend on the node: PvE 3, PvP 4, Gym 8, Pokémon Center 2.**

A 1-point counter had no room to say "a Gym is a milestone". These rates and the level costs are two
halves of one number: a Location's expected node mix pays ~23 EXP, and six Locations land exactly on
the cap. `GrowthAndEvolutionTests` walks six Locations and asserts that, so the relationship fails a
test rather than drifting the next time an award or a node weight changes.

**7. A run has a floor level, and everything it owns is held at it.**

`RunState.RunExp` tracks what the run has paid out; `FloorLevel` is one level below what that buys;
`RunProgression` holds every mon in the line-up *and the Box* at it. So a mon caught in Location 5
arrives playable (and evolves as far as that level reaches), a Box mon doesn't rot while the line-up
fights, and rotating the team is free. Personal EXP is what puts a mon *above* the floor — and since
the whole line-up is paid equally, **combining duplicates is the only way to get there**, which is
the lever "invest in one mon" now pulls.

The cost: per-mon EXP is mostly cosmetic outside of combining. That is a deliberate trade of
simulationist bookkeeping for "every mon you own is worth playing", and it is reversible — dropping
the floor to a lower trail, or to zero, is a one-line change.

**8. The opposition scales with the run, from a tiered pool (`RegionTier`).**

Enemies are built at the party's level plus a per-node delta (wild -3, trainer -2, Gym -1) through
the same `StatGrowth`, and drawn from a slice of the roster that opens up as the run goes on: base
forms for Locations 1–2, first evolutions for 3–4, everything for 5–6, with Legendaries reserved for
the final Gym. Simulated with those pools, wild fights land at ~85% and Gyms at ~74%, which costs a
full run about three Morale.

**9. Morale starts at 5, and a run is six badges.**

Five, because ~27 fights at those win rates expects about three losses and 3 Morale meant the average
run died to arithmetic. Six badges, because a fixed badge count is the simplest of design doc §20's
three candidate win conditions and the only one the build can express today — this ADR is where that
open question gets an answer.

**10. The Region Hub exists, so a run is six Locations.**

Three candidate Locations, deterministic from the run's seed and its position, each previewing its
type bias and the tier its opposition will be built at. `LocationCatalog` replaces
`ForestLocationFactory` with design doc §4's whole table, since a run is no longer always a Forest.

## Consequences

- **The curve is asserted, not documented.** `GrowthAndEvolutionTests` walks a representative
  six-Location run and fails if it doesn't finish at the cap with the starter fully evolved by
  Location 4. Balance changes now have to be deliberate.
- **The x3 Health scalar moves every existing balance impression**, Gym difficulty included. It is
  the single largest feel change in this ADR and the first thing to revisit if fights now drag.
- **Badges are inert.** Design doc §14 wants each to be a Slay-the-Spire-style run-wide passive; the
  effect vocabulary has no notion of a run-wide effect, and building a DSL for one relic is the
  premature abstraction CLAUDE.md warns against. Counted and displayed; still open.
- **The Trailblazer minigame's slot is now real.** Design doc §6 puts it between choosing a Location
  and arriving; the Hub currently goes straight to the map.
- **Damage still doesn't carry between fights** (ADR 0003), so the longer fights the Health scalar
  buys are still each fought at full HP. When that changes, `ExperienceResolver.Recompute` has to
  preserve damage taken rather than the HP value — noted on the method.
- **Wild encounters are now filtered by evolution stage, which closes PLAN.md §11 item 9** — a
  Legendary can no longer turn up in a Location-1 Forest.
- **The starter cap and the tier pool overlap.** Character Select's stat-total cap (ADR 0004) and
  `RegionTier`'s stage filter are two different ways of saying "base forms only"; they agree today
  but nothing enforces that they keep agreeing.
