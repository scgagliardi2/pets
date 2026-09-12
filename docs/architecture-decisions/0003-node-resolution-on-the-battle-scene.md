# ADR 0003: Node Resolution Runs on the Battle Scene, and a Gym Win Ends the Run

**Status:** Accepted
**Date:** 2026-09-12

## Context

ADR 0002 recorded the state this change starts from: a polished shell around a run that didn't run.
The map was walkable but inert — `RegionMapController.WalkTo` moved the token and stopped — and the
run held two unreconciled map models (`RunState.Nodes`/`CurrentNodeIndex` from the retired linear
Forest, and the branching `LocationMap`/`VisitedMapNodeIds` the map screen actually draws). PLAN.md
§11 ordered the fix: collapse the models, resolve nodes on arrival, then the Gym and the
Morale/win-loss loop. This ADR records two decisions taken while doing that, both of which depart
from what the plan and the design doc describe.

Two things had changed underneath the plan's advice:

1. PLAN.md §11 item 2 said to "re-land the orphaned screens on the map scene," naming
   `PvEClashController` as the thing that makes a battle playable in-game again. But since that
   advice was written, `Battle.unity` was built (ADR 0002's shell work): a real battle screen with
   stat boxes, HP-drain animation, a party strip, playback controls and a result panel, covered by
   PlayMode tests. `PvEClashController` is a plain scrolling text log of Step events. Re-landing it
   would have meant carrying two battle UIs of very different quality, with the worse one being the
   one a real run used.
2. Design doc §14 has a Gym win hand the player a badge and return them to the Region Hub to pick
   the next Location. The Region tier (design doc §4, §5.2) doesn't exist — `RegionMap*` is
   misnamed, it's the *Location* map (PLAN.md §6 "Known naming debt"), and there is no screen that
   offers a choice of Locations.

## Decision

**1. PvE and Gym nodes resolve on `Battle.unity`, and the Location Hub's battle/flow controllers are
deleted.**

Arriving at a PvE or Gym node stashes the encounter in `PendingBattle` (a static hand-off in the
same spirit as `PendingRunSelection`) and transitions to the existing Battle scene, which now runs
two kinds of fight: a *node fight* (passives on, Camp buff spent, result written back to the run)
and the unchanged *dev random battle* off Team's button (passives stripped, costs the run nothing).
`BattleScreenController` owns writing the result back — Morale on a defeat, EXP to the survivors,
the stubbed catch on a PvE win, and the Location completed by beating the Gym.

`LocationFlowController`, `PvEClashController`, `MapPanelController` and `LocationHubController` are
deleted rather than re-landed: all four were shaped around the tabbed hub scene that ADR 0002
retired, and the first is superseded outright. `CampPanelController` + `CampOverlay.prefab` and
`ResourceBarController` *were* re-landed — the first as the Pokémon Center's modal on the map, the
second as the Morale/Money readout in the map's title bar. This closes the "orphaned-controller rot"
risk PLAN.md §10 opened: nothing in `Scripts/Gameplay` is now attached to no scene.

**2. Beating the Gym ends the run and returns to Home.**

Until a Region Hub exists, a Location's finale is the run's finale: the result panel says "Badge
earned — Location complete", and Continue clears `ActiveRun` and goes Home, the same exit a
Morale-broken run takes. This is a stand-in, not a design change — when the Region tier is built, a
Gym win should hand out a badge-as-relic and return to the Region Hub as design doc §14 says.

## Consequences

- A player can now play a whole Location end to end: pick a starter pair, walk a branching map,
  fight real encounters with passives, rest at the Pokémon Center, catch what they defeat, lose
  Morale, and either break or beat the Gym. That is the Phase 1 core loop.
- Two node types resolve as honest stubs — Event and PvP show a modal saying so and move on. They
  block nothing, and neither is a silent no-op.
- Decisions this pass deliberately did **not** make, listed so they aren't mistaken for oversights:
  - **A lost non-Gym fight costs Morale and nothing else.** The old linear Forest kept the player on
    a node until they won it; on a branching graph there's no standing still, so the player walks on
    from wherever they land. The Gym is the exception — it has no forward edges, so losing it offers
    the fight again.
  - **HP does not carry between fights.** Every fight starts at full health, as the dev battle always
    has. Persistent damage needs a healing mechanic to pair with (the Pokémon Center's real job) and
    a rule for a fainted mon, neither of which is built.
  - **The Pokémon Center is the Camp node.** PLAN.md left "Camp or Pokémon Center?" open; the map art
    and caption were already a Center, so that name wins, and design doc §5.1's Camp effect (EXP + a
    next-fight Attack buff) is what resting currently does. Adoption (§5.2) and healing land there
    when they're built.
  - **Catching is still the "pick 1 from defeated" stub**, now offered on the Battle screen's result
    panel. The real drag-a-Pokéball-at-a-Step-boundary system (design doc §12.1) is unchanged work.
  - **`RegionMap*` is still misnamed.** The rename to `LocationMap*` stays its own commit (PLAN.md
    §11 item 6), deliberately not folded into feature work.
