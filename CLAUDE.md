# CLAUDE.md

Guidance for working in this repo. See [PLAN.md](PLAN.md) for the condensed plan and phase
roadmap, and [`docs/pokemon-roguelite-autobattler-design-doc.md`](docs/pokemon-roguelite-autobattler-design-doc.md)
for the full design — read both before starting non-trivial work if you haven't already.
[`docs/architecture-decisions/0001-pivot-to-pokemon-roguelite.md`](docs/architecture-decisions/0001-pivot-to-pokemon-roguelite.md)
explains why the project looks the way it does; [`0002-shell-first-deviation.md`](docs/architecture-decisions/0002-shell-first-deviation.md)
explains why what's built doesn't match the plan's order; and
[`0003-node-resolution-on-the-battle-scene.md`](docs/architecture-decisions/0003-node-resolution-on-the-battle-scene.md)
covers how a map node turns into a fight, and which simplifications the run loop deliberately
carries; [`0004-full-roster-import-and-pokedex.md`](docs/architecture-decisions/0004-full-roster-import-and-pokedex.md)
covers the jump from 28 hand-authored species to all 183, the content-import pipeline that did it,
and what that expansion left unfinished. Between them they list the deviations from the design doc
that are still open questions.

## Project snapshot

A single-player roguelite (Slay the Spire–style meta-layer) wrapped around a Super Auto
Pets–style Lead/Support auto-battler, themed with Pokémon species/types/assets.
- Client: Unity (C#), `/client`.
- Backend: Node.js/TypeScript + PostgreSQL, `/server` — **scaffolding only** (folder structure,
  `package.json`, `tsconfig.json`; every source dir is an empty `.gitkeep`). Introduced for real
  once the solo roguelite loop is solid (see PLAN.md §6, Phase 3). Don't add backend networking
  code to the client, or real routes/services to `/server`, before then.
- Shared specs/fixtures: `/shared`, `/docs`.

**Current state: read PLAN.md §6's Status block first.** It is the only accurate account of what
exists — the phase list under it describes intent, and the build has deviated from that order
(ADR 0002). The short version, as of 2026-09-12:
- Everything in the tree is **post-pivot**. The old 5-slot code and `Gameplay/ShopEconomy` were
  deleted, not kept; `Scripts/Simulation` matches `docs/battle-sim-spec.md` and is the code to
  extend, not replace.
- The game's **shell** is built and playable (Home → Character Select → a walkable Location map,
  plus an in-run menu, Team, History, Credits, Settings, a Pokédex, a dev roster screen), and the
  **core run loop inside it now works**: arriving at a map node resolves it (ADR 0003). Battle/Gym nodes hand
  an encounter to `Battle.unity` through `PendingBattle` and it writes the result back to the run
  (Morale, EXP, the stubbed catch, Location complete); the Pokémon Center and the Event/PvP stubs
  resolve as modals on the map. Team's dev button still opens the old throwaway random battle,
  which strips passives and costs the run nothing — don't mistake one for the other.
- The systems hanging off that loop are **not** built: evolution, the real drag-and-drop catching,
  Pokémon Center adoption/healing, a Shop, type synergy, the badge reward, real Event/PvP nodes,
  the Trailblazer minigame, and the Region Hub. See PLAN.md §6 for the deliberate simplifications
  that came with the loop (a lost fight costs only Morale; HP doesn't carry between fights).
- **Content is all 183 roster species** (ADR 0004), imported by
  `Assets/Editor/SpeciesRosterImporter.cs` from `docs/pokemon_stats_unique.xlsx`. Two things that
  expansion left open and that it's easy to mistake for finished: only the original 28 species have
  a bespoke passive (the rest share one placeholder per primary type), and the encounter/Gym/random
  -battle pools still draw from the *whole* library unfiltered, so a Forest wild encounter can be a
  Legendary. See PLAN.md §11 item 9.
- `RegionMap*` is misnamed: it's the **Location** node-map (design doc §5), not the Region tier
  (§4/§5.2), which isn't built. See PLAN.md §6 "Known naming debt" before adding to it.
- There is **no save/load layer** (so "Continue Run" only resumes a run still in memory, and
  History has nothing to list) and **no content-import pipeline** (the 28 curated species were
  hand-authored asset by asset). Both are still in the plan; neither is built.

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
  special-case it. Stats come from `docs/pokemon_stats_unique.xlsx` — never invented, and never
  hand-tuned in the asset without updating the sheet. **Species assets are generated from that
  sheet**: edit the sheet, then re-run `Pets > Content > Import Species From Roster Sheet` (PLAN.md
  §8) rather than editing a species asset's stats or typing by hand — `RosterImportTests` fails if
  the two disagree. The importer is idempotent and leaves hand-authored passives and evolution links
  alone, so re-running it is always safe. Adding or editing a content asset means re-running
  `ContentIntegrityTests` — an asset that isn't registered in its library is invisible to the game
  and silent otherwise.
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
/client    Unity project (C#, 6000.6.0f1)
  Assets/Scripts/Simulation   pure C# per-Step battle logic (incl. both runners in BattleRunner.cs)
  Assets/Scripts/Meta         pure C# run layer: RunState, map generation/traversal, resolvers
  Assets/Scripts/Data         ScriptableObject authoring assets + runtime registries
  Assets/Scripts/Gameplay     MonoBehaviour screen controllers, navigation, run bootstrap
  Assets/Scripts/UI           shared view helpers (PokemonCardBuilder, TypeIconView, Theme)
  Assets/Scripts/Tests        EditMode at the root, PlayMode under Tests/PlayMode
  Assets/Content              species/passive assets + their libraries
  Assets/Editor               scene + prefab builders, dev tooling
  Assets/Art/Pokemon          species artwork, referenced directly by the species assets
  Assets/Resources            loaded-by-path only: Sprites/{Types,Nodes,UI} and Fonts
  Assets/Scenes               GENERATED — never hand-edit (see Working conventions)
/server    Node/TS backend — scaffolding only; introduced once solo loop is solid
/shared    Golden battle-sim fixtures (JSON) used by both client and (later) server tests
/tools     one-off content/asset scripts (generate_ui_sprites.py)
/docs      pokemon-roguelite-autobattler-design-doc.md (full design), pokemon_stats_unique.xlsx
           (roster), battle-sim-spec.md, content-schema.md, architecture-decisions/
```

There is no `Scripts/BattleRunner` or `Scripts/Minigame` — see PLAN.md §5 for why. Art splits two
ways: anything resolved by string path at runtime goes in `Resources` (and therefore ships whether
referenced or not), everything else goes in `Art` behind a direct reference. See
`client/Assets/Art/README.md`.

## Working conventions

- **Branching:** trunk-based off `main`, short-lived feature branches. This is a solo project —
  keep it simple, but still don't commit directly to `main` for anything non-trivial so history
  stays reviewable.
- **Commits:** small and scoped to one change; explain *why* in the body when the reason isn't
  obvious from the diff. A regenerated scene reshuffles every fileID in the file, so a commit that
  touches a builder will carry a large unreadable `.unity` diff — that's expected; say so in the
  body rather than trying to split the commit by scene.
- **Scenes are generated from code — never hand-edit a `.unity` file.** Every scene has an
  `Assets/Editor/*SceneBuilder.cs` that creates it, sharing `SceneBuilderUtils.cs` for uGUI
  construction, with `SceneCatalog.cs` owning the Build Settings list. After changing a builder, a
  controller's serialized fields, `Assets/Prefabs/UI`, or `Pets.UI.Theme`, re-run
  `Pets > Build All Scenes` (or the single builder's `Build`) — a hand-patched scene will be
  silently overwritten by the next build, and an un-rebuilt scene is how a working controller ends
  up wired to nothing.
- **A new scene must go in `SceneCatalog.AllScenePaths` and `Gameplay/SceneNames`**, or
  `SceneManager.LoadScene` won't resolve it and the button that navigates there fails at runtime
  only.
- **Navigate with `ScreenFade.TransitionTo(sceneName)`, not `SceneManager.LoadScene`.** A
  synchronous load stalls the main thread through the next screen's `Start`, which is where these
  screens do their work; the fade covers an async load instead. A PlayMode test that drives a real
  navigation must therefore wait for it — use `SceneTransitionWait`, not a fixed frame count.
- **Texture import settings come from an `AssetPostprocessor`, not the Inspector.** One per art
  folder under `Assets/Editor` (`UiSprite`/`TypeIcon`/`NodeIcon`/`PokemonSprite`). Hand-tuning a
  file's settings is how the species sprites ended up uncompressed with mipmaps on, and the node
  icons at 1312px to draw a 52px node. A new art folder needs a processor and an entry in
  `SpriteAtlasBuilder`; after adding one, run `Pets > Build Sprite Atlases`.
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

- **Client tests:** Unity Test Runner — EditMode for the battle-sim and Meta code (anything that
  doesn't need a scene), PlayMode for the saved scenes and their button wiring. Run from the
  Editor's Test Runner window, or headlessly (the Editor must be **closed** — it holds a project
  lock):

  ```sh
  UNITY=/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity
  $UNITY -batchmode -runTests -testPlatform EditMode -projectPath client \
         -testResults /tmp/edit.xml -logFile /tmp/edit.log
  $UNITY -batchmode -runTests -testPlatform PlayMode -projectPath client \
         -testResults /tmp/play.xml -logFile /tmp/play.log
  ```

  Parse the NUnit XML for pass/fail counts (the exit code alone isn't enough). Baseline as of
  2026-09-12 (after the full-roster import and the Pokédex, ADR 0004): **146 EditMode, 83 PlayMode,
  all passing**. The same binary runs any Editor entry
  point headlessly — `-executeMethod Pets.EditorTools.SceneCatalog.BuildAll` to rebuild scenes,
  and the `DevCaptureUiKit` capture methods with `-captureOutput <path>` to render a screen to a
  PNG, which is the only way to actually look at the UI without opening the Editor.

  Note a batch run that has just exited can leave `client/Temp/UnityLockfile` behind for a few
  seconds; a second run started immediately fails with a bare exit code 1 and an almost empty log.
  Wait for the process to clear rather than debugging the log.
- **CI is not a gate yet:** the client job in `.github/workflows/ci.yml` is `continue-on-error`
  until `UNITY_LICENSE` secrets exist, so a red client suite won't block a merge. Run the suites
  locally before saying work is done.
- **Server tests (Phase 3+):** `vitest` (or the configured runner) against a disposable
  Postgres via Docker Compose — don't mock the database for anything touching real queries.
- **Golden fixtures:** when changing battle-sim rules, update/add cases in `/shared/fixtures` and
  make sure the client test suite passes against them before considering the change done. Once
  the server-side sim exists (Phase 3), it must pass the same fixtures.
- Before reporting simulation or gameplay-logic work as complete, run the relevant automated
  tests — don't rely on "looks right in the editor" for anything with test coverage available.
- For UI changes, actually press play in the Unity editor and click through the affected flow
  (Home/menu shell, Character Select, the Pokédex, the Location map, Team) before calling it done;
  headlessly, `Pets/Dev/Capture *` + `-captureOutput <path>` renders a screen to a PNG, which is the
  closest substitute;
  type/compile success isn't feature success, and neither is a passing PlayMode test — it clicks
  the buttons it knows about, it doesn't look at the screen.
- A PlayMode test is the minimum bar for a **new screen or a new button**: the wiring between a
  generated scene and its controller is exactly what compiles fine and does nothing.

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
- `PLAN.md` §6 Status — **the one doc that goes stale fastest.** It's the account of what exists
  versus what's merely planned, and it's the first thing anyone (human or agent) reads to orient.
  If you finish a screen, retire one, orphan a controller, or discover something the plan claims
  exists but doesn't, update §6 in the same change. Don't describe planned work there as though
  it's built — that's precisely the drift the 2026-09-12 re-alignment had to undo.
- Short ADRs in `docs/architecture-decisions/` for decisions worth remembering the reasoning
  behind later (ADR 0001 — the pivot — and ADR 0002 — the shell-first deviation — are the
  templates) — not required for routine work, but write one when you knowingly depart from the
  plan or the design doc, rather than leaving the next reader to infer it from the diff.

## When code and the design doc disagree

The design doc is the design source of truth, but it was written ahead of the build and parts of
it have been overtaken. When you hit a conflict, don't silently pick a side:
- If the code is **wrong**, fix the code.
- If the code is **better**, update the design doc's section (or add to its §20 Open Questions)
  and note the deviation in PLAN.md §6 — ADR 0002 lists the live ones (Location Hub tabs vs.
  separate scenes, Camp vs. Pokémon Center on the map, Character Select's scope).
- If you can't tell, leave both and write it down. An unrecorded deviation is the expensive
  outcome; a recorded one is just a decision waiting to be made.
