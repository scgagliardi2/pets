# ADR 0002: Shell-First Build Order, and the Retirement of the Location Hub

**Status:** Accepted (describes what happened). Its decisions 2 and 3 — the milestone and the
orphaned controllers' clock — were carried out the same day by
[ADR 0003](0003-node-resolution-on-the-battle-scene.md), which also settles the Camp-vs-Pokémon
Center question raised below. Everything else here still stands.
**Date:** 2026-09-12

## Context

PLAN.md's phase order says: prove the battle simulator, build one hand-authored Location, make a
PvE fight playable end to end, then add the rest of the run loop (Phase 1), then breadth, then a
backend. Phase 0 was completed roughly as written — the sim was rewritten to
`docs/battle-sim-spec.md`, curated content landed, golden fixtures passed, and a Forest Location
was playable through a tabbed Location Hub scene (`Game.unity`, per design doc §5.2: Team
Management / Location Map / Shop / Pokémon Center).

The work that followed did not continue down that order. Over several days it went into the game's
*shell* instead:

- Character Select as its own scene, with a filterable/sortable roster grid.
- A branching Location-map **generator** and a walkable map scene, replacing the hand-authored
  linear node list as the map the player actually sees.
- `Home` → `CharacterSelect` → map navigation, an in-run menu, `Team`, `History`, `Credits`, and a
  dev roster screen — each a separate scene, all wired through one `SceneNavigator`, all generated
  from `Assets/Editor/*SceneBuilder.cs` and covered by PlayMode tests that click the real buttons.
- Real UI craft: shared uGUI prefabs, a theme, a `PokemonCardBuilder` used by three screens, type
  icons, cached PokeAPI sprites for all 183 roster species, drag-to-rearrange and drag-to-release
  gestures on Team.

In the process, `Game.unity` — the Location Hub — was converted into the Ingame Menu rather than
kept alongside. That removed the only scene that resolved a map node or ran a battle. The
controllers behind those flows (`LocationFlowController`, `PvEClashController`,
`MapPanelController`, `CampPanelController`, `ResourceBarController`, `LocationHubController`, and
`Prefabs/UI/CampOverlay.prefab`) still compile and still have EditMode-tested logic behind them,
but they are attached to no scene.

Net effect: the project has a polished shell wrapped around a run that doesn't run. The plan
described a playable Forest; the build has a playable menu system and an unplayable Location. The
docs did not say so — PLAN.md §6 simultaneously claimed "the Forest Location is playable end to
end" and, three bullets later, that the scene making it playable had been retired.

## Decision

1. **Accept the shell-first order; don't undo it.** The shell is real, tested work that the game
   needed regardless, and the UI vocabulary it established (prefabs, theme, card builder, scene
   builders, `SceneNavigator`) is what makes every subsequent screen cheap. Rebuilding the run loop
   on top of it is less work than the reverse.
2. **Treat "a battle is playable in-game again" as the next milestone**, ahead of any further shell
   or content work. Concretely: collapse the two map models onto one (`RunState`'s linear
   `Nodes`/`CurrentNodeIndex` versus `RegionMapTraversal`'s branching walk), then resolve nodes on
   arrival and re-land `PvEClashController` on the map scene. PLAN.md §11 items 1–2.
3. **The orphaned controllers are kept, but on a clock.** They're retained for the node-resolution
   work they'll be reused for, and the retired hub scene is recoverable from git history. If
   item 2 lands and they still don't fit the shape of the new map/run state, delete them and
   rebuild rather than contorting the new code to match them.
4. **The Location Hub (design doc §5.2) is superseded by separate scenes off the in-run menu.**
   A tabbed Team/Map/Shop/Center screen is not what got built and, on the evidence of playing the
   shell, separate scenes read better. This is a real deviation from the design doc, recorded here
   rather than silently absorbed — the design doc's §5.2 should be updated when Shop and Pokémon
   Center actually land and the shape is settled, not before.
5. **PLAN.md §6 Status is the account of record for what exists.** It is rewritten to describe the
   build honestly, including what's orphaned and what's merely planned, and CLAUDE.md points at it
   first. The phase list below it is intent, not status.

## Consequences

- The phase numbering no longer tracks the build. Phase 0 is "done except the thing it existed to
  prove"; Phase 1 is partly done out of order. Phases are milestones, not a schedule — but anyone
  reading the phase list alone will be misled, hence decision 5.
- Two pieces of naming debt are now baked in and should be paid off deliberately:
  - **`RegionMap*` is the Location map.** `Meta/RegionMap.cs`, `RegionMapNode`,
    `RegionMapGenerator`, `RegionMapTraversal`, `RegionMapController`, `RegionMapSceneBuilder`,
    `RegionMap.unity` and `SceneNames.Map` all implement design doc §5's *Location* node-map. The
    *Region* — the pool of candidate Locations and the Region Hub that offers three of them
    (§4, §5.2) — isn't built at all. Rename to `LocationMap*` before building the Region tier, or
    the two will be permanently confusable.
  - **Camp vs. Pokémon Center.** `RegionMapController` labels `NodeType.Camp` as "Pokémon Center",
    conflating §5.1's Camp node (EXP + a temporary pre-battle buff) with §5.2's Center (adoption).
    Both are supposed to exist and they do different things. Pick one meaning per surface when
    node resolution lands.
- Character Select is simpler than design doc §3 (whole roster in a grid, versus a fixed/chosen
  starter plus a narrowed 3-option secondary plus cosmetics). Open, not wrong — §3 parity may or
  may not matter in play.
- The absence of a save layer is now the shell's most visible hole: "Continue Run" can only resume
  a run still in memory, and History has nothing to list. Save/load is listed as Phase 2 but
  gates more of the existing shell than anything else in that phase.
- Two things PLAN.md previously described as existing do not: the **content-import pipeline**
  (the 28 curated species were hand-authored) and any **save/load layer**. Both are now marked as
  such in PLAN.md §8 and §6.
