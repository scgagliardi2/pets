# PLAN.md — Pokémon Roguelite Autobattler

> **Pivot note (2026-09-10):** This plan supersedes the earlier "generic shop-drafting
> auto-battler" plan. See [`docs/architecture-decisions/0001-pivot-to-pokemon-roguelite.md`](docs/architecture-decisions/0001-pivot-to-pokemon-roguelite.md)
> for why, and for what happened to the Phase 0–2 code already built against the old design (short
> version: it was deleted, not kept alongside — ADR 0001 said "kept for reference", the working
> decision went the other way). [`0002-shell-first-deviation.md`](docs/architecture-decisions/0002-shell-first-deviation.md)
> records why what's built no longer lines up with the phase order below. The full, detailed design lives in
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
- MVP: one hand-authored Location, a small curated slice of species, no backend. (Reality check:
  the curated slice and the sim exist; the playable Location does not yet — see §6 Status.)

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
| Client engine | Unity 6000.6.0f1 (C#) | 2D, uGUI (not UI Toolkit) for every screen built so far; pinned in `client/ProjectSettings/ProjectVersion.txt` |
| Client testing | Unity Test Framework (NUnit), EditMode for sim logic, PlayMode for integration | |
| Species/type data | PokeAPI, cached locally at build/content-import time | Filtered to the curated Gen 1–3 roster (§8 of the design doc, `docs/pokemon_stats_unique.xlsx`) |
| Backend runtime | Node.js + TypeScript | Introduced once solo loop is solid (design doc Phase 3) |
| Backend framework | Fastify (or Express) | REST JSON API |
| Database | PostgreSQL | Accounts, PvP snapshots, matchmaking |
| Backend testing | Vitest + Supertest, test DB via Docker Compose | |
| CI | GitHub Actions | Lint + test on push/PR for both client and server |
| Source control | Git (this repo), trunk-based on `main` with short-lived feature branches | |
| Art | PokeAPI official-artwork sprites, cached under `client/Assets/Resources/Sprites/Pokemon/` for all 183 roster species; type/node/UI sprites alongside them | Loaded at runtime via `Resources`, see §8 |

## 5. Repository Structure

This is the **actual** layout as built, not an aspiration — the design doc's own suggested layout
(§18) is written for a React/TS frontend, and this is the same conceptual split re-expressed for
Unity. Where a folder the earlier plan named doesn't exist, that's noted inline rather than left
to look like a missing piece.

```
/pets
  PLAN.md  CLAUDE.md  README.md  SETUP.md
  /client                     # Unity project (6000.6.0f1, see client/ProjectSettings/ProjectVersion.txt)
    /Assets
      /Scripts
        /Simulation           # Pure C#, no MonoBehaviour deps — per-Step battle logic (design doc §10).
                              #   Includes BattleRunner.cs (both runners live here; there is no
                              #   separate /BattleRunner folder — the earlier plan named one).
        /Data                 # ScriptableObject authoring assets + runtime registries/factories
        /Meta                 # Pure C# run layer: RunState, map generation/traversal, encounters,
                              #   EXP, camp/catch resolvers
        /Gameplay             # MonoBehaviours: screen controllers, scene navigation, run bootstrap
        /UI                   # Shared view helpers (PokemonCardBuilder, TypeIconView, Theme, UiButton)
        /Tests                # EditMode tests at the root, PlayMode tests under /Tests/PlayMode
      /Content                # ScriptableObject data instances (species, passives, libraries)
      /Editor                 # Scene/prefab builders, texture-import processors, the sprite-atlas
                              #   builder, one-off content migrations, dev capture/diagnostic helpers
      /Prefabs/UI             # Shared uGUI prefabs the scene builders instantiate
      /Art/Pokemon            # Species artwork, referenced directly by the species assets
      /Art/Atlases            # Generated sprite atlases (Pets > Build Sprite Atlases)
      /Resources              # Runtime-loaded-by-path assets only: Sprites/{Types,Nodes,UI}, Fonts
      /Scenes                 # Generated scenes — never hand-edit (see below)
  /server                     # Node/TS backend — folder structure + package.json only, no code
                              #   until Phase 3
    /src/{routes,services,db} # empty (.gitkeep)
    /test                     # empty (.gitkeep)
  /shared
    /fixtures                 # Golden battle-sim cases (JSON), run by GoldenFixtureTests.cs
  /tools                      # One-off content/asset scripts (generate_ui_sprites.py)
  /docs
    pokemon-roguelite-autobattler-design-doc.md   # full design source
    pokemon_stats_unique.xlsx                     # locked-in 183-species roster (stats are placeholders)
    battle-sim-spec.md
    content-schema.md
    architecture-decisions/
```

Folders the earlier version of this plan listed that deliberately **don't** exist:
- `Scripts/BattleRunner` — the two runners are `Simulation/BattleRunner.cs`; splitting them into
  their own assembly folder bought nothing.
- `Scripts/Minigame` — Trailblazer isn't built (Phase 1).
- (`Assets/Art` was empty at the 2026-09-12 re-alignment; it now holds the species sprites — see
  §8 and `client/Assets/Art/README.md` for which art belongs there versus in `Resources`.)

**Scenes are generated from code.** Every scene in `Assets/Scenes` is produced by an
`Assets/Editor/*SceneBuilder.cs`, with `Assets/Editor/SceneCatalog.cs` owning the Build Settings
list and the `Pets > Build All Scenes` menu item. Don't hand-edit a `.unity` file: change the
builder (or the controller's serialized fields) and re-run the builder. Regenerating a scene
reshuffles every fileID in it, so per-scene diffs are always churn — don't try to split a commit
by scene.

## 6. Development Phases

Phases are milestones, not deadlines. They were numbered fresh at the pivot (ADR 0001) because the
combat/content code had to be reworked to the Lead/Support model before any of it counted as
"done" — that rework is finished. The list is **intent, in a sensible order**; it is not a record
of progress, and the build has not followed it strictly (ADR 0002). The Status block immediately
below is the record.

**Status (as of 2026-09-12) — read this before trusting the phase list below.** The last few days
of work went into the **game's shell** (Home, in-run menu, Team, a walkable Location map, Character
Select) rather than down the Phase 0/1 roadmap as written, so "what's built" no longer maps cleanly
onto phase boundaries. ADR 0002 records that deviation and why. The honest summary:

*What you can actually play right now:* `Home` → `CharacterSelect` (pick Starter + Secondary) →
`RegionMap` (walk a branching node map to the Gym), with `IngameMenu` → `Team` (drag to
rearrange/release) / `DevRoster` (stuff mons into the run) hanging off it, plus `History` and
`Credits` off Home. **No battle is playable in any scene.** Arriving at a node does nothing yet;
the simulator only ever runs from tests.

*Verified green as of this writing:* 103 EditMode and 49 PlayMode tests pass (see CLAUDE.md for
the CLI commands).

**Built and covered by tests:**
- `Scripts/Simulation` implements the Lead/Support/Step model per `docs/battle-sim-spec.md` —
  Step loop, charge meters, statuses, both runners — with `StepSimulatorTests.cs` and golden
  fixtures in `/shared/fixtures` (`GoldenFixtureTests.cs`).
- 28 curated species and 17 hand-authored type-flavored passives under `client/Assets/Content`,
  with `PokemonContentTests.cs` running a full real-content fight start to end and
  `ContentIntegrityTests.cs` guarding that every on-disk asset is registered in its library and
  resolves its sprite. All 183 roster sprites are cached under `Assets/Art/Pokemon/{id}.png`, so
  authoring a not-yet-curated species only needs its `Sprite` reference pointed at the matching
  file — no fresh art fetch.
- `Scripts/Meta` (pure C#): `RunState` (line-up/Box/Money/Morale/seed, `MoveMon`, `ReleaseMon`),
  seeded wild-encounter generation, EXP/level-up, Camp's EXP+buff grant, the stubbed
  "pick 1 from defeated" catch, and a branching map generator + traversal model
  (`RegionMapGenerator`, `RegionMapTraversal`) that produces no dead ends, no unreachable nodes
  and no crossing edges. Covered by `RunMetaTests.cs`, `RegionMapGeneratorTests.cs`,
  `RegionMapTraversalTests.cs`.
- Screens, all generated by `Assets/Editor/*SceneBuilder.cs` and driven end-to-end by PlayMode
  tests that click the real `Button`s in the saved scenes:
  - `Home.unity` — Continue Run (only when a run is live in memory) / New Game / History /
    Credits / Quit.
  - `CharacterSelect.unity` — the whole curated roster in a scrollable stat grid (each card laid out
    like a battle panel: name with a sword + attack, type icons, and HP/SPD bars from
    `Prefabs/UI/HealthBar.prefab` / `SpeedBar.prefab`, speed drawn against the 200 cap) with a Type
    filter and Attack/Speed/Health sort toggles; picks Starter then Secondary, hands the pair to
    `RunBootstrapper` via `PendingRunSelection`. Simpler than design doc §3 (which wants a
    fixed/chosen starter, a narrowed 3-option secondary, and cosmetics) — full §3 parity is open.
  - `RegionMap.unity` — the branching map, flowing left to right, with a player token that slides
    between nodes, only forward-reachable nodes clickable, the walked path highlighted, and a
    "New Map" re-roll.
  - `IngameMenu.unity` — Back to Map / Team / Dev: Add Pokemon / Quit to Home. (This scene is the
    old Forest hub `Game.unity`, converted rather than kept alongside.)
  - `Team.unity` — party and Box as six slots each, real mon cards via `UI/PokemonCardBuilder.cs`,
    slot 0 Lead / slot 1 Support / rest Reserve and dimmed (only the front two are ever active,
    design doc §7). Drag a card onto another slot to trade or append; drag onto the bottom bar's
    release zone to release it for good (asks first, irreversible, party can never be emptied).
  - `History.unity` / `Credits.unity` — Credits carries the Pokémon/PokeAPI/font attribution and
    the non-commercial scope note; History is a real screen with an honest empty state, because
    nothing records a finished run yet.
  - `DevRoster.unity` — a dev tool, not a game screen: click a card to drop that species into the
    run's party or Box, duplicates allowed. It ships in the build (per §9 there's no release
    channel to keep it out of) and is *not* the Pokémon Center.
- Plumbing: `Gameplay/SceneNavigator.cs` (one component every menu button is wired to),
  `Gameplay/ActiveRun.cs` (holds `RunState` across scene loads), `Assets/Editor/SceneCatalog.cs`
  (owns the Build Settings list + `Pets > Build All Scenes`), `Assets/Editor/SceneBuilderUtils.cs`
  (shared uGUI construction), `Assets/Prefabs/UI` (Button/TextBox/TypeIcon/HealthBar/SpeedBar prefabs the
  builders and screens instantiate). `UI/HealthBarView` (and `UI/StatBarView` for speed) is built for the battle screen's Lead/Support
  panels (current/max, green→yellow→red) but only Character Select uses it so far — no scene runs
  a battle yet.

**The two big structural gaps** — these are the real Phase 1 work, and everything in "Next Steps"
below flows from them:

1. **No node resolution.** `RegionMapController.WalkTo` moves the token and stops. The screens
   that would resolve a node — `LocationFlowController`, `PvEClashController`, `MapPanelController`,
   `CampPanelController`, `ResourceBarController`, `LocationHubController`, and the
   `Prefabs/UI/CampOverlay.prefab` — still exist in `Scripts/Gameplay` but are **attached to no
   scene**, orphaned when the Forest hub scene became the Ingame Menu. They're kept for the
   Shop/Center/PvE work they'll be reused for; the retired hub scene is recoverable from git
   history if that turns out to be the wrong call.
2. **Two unreconciled map models.** `RunState` now holds *both*: `LocationMap` +
   `VisitedMapNodeIds` (the branching graph the map screen draws and walks, via
   `RegionMapTraversal.ForRun`) and `Nodes`/`CurrentNodeIndex`/`AdvanceToNextNode` (the linear
   Phase 0 Forest sequence from `ForestLocationFactory`, which `RunBootstrapper` still seeds and
   the sceneless `LocationFlowController` still walks). Neither knows about the other.

   The 2026-09-12 structure pass fixed the *ownership* half of this — the walk used to live on
   `RegionMapController`, so leaving the map screen and coming back rerolled the map and reset the
   player's position — but not the duplication. Collapsing the two onto one model is still the
   prerequisite for node resolution.

**Known naming debt** (noted rather than fixed, so nobody assumes the names are meaningful):
- `RegionMap*` (`Meta/RegionMap.cs`, `RegionMapNode`, `RegionMapGenerator`, `RegionMapTraversal`,
  `RegionMapController`, `RegionMapSceneBuilder`, `RegionMap.unity`, `SceneNames.Map`) is really
  the **Location** node-map from design doc §5 — branching PvE/Event/PvP/Camp converging on a
  mandatory Gym. The *Region* in the design doc (§4, §5.2) is the tier above it: the pool of
  candidate Locations and the Region Hub that offers 3 of them. That screen doesn't exist yet, so
  when it's built the current `RegionMap*` names should become `LocationMap*` first, or the two
  tiers will be permanently confusing.
- `RegionMapController` labels `NodeType.Camp` as "Pokémon Center" in its display-name table,
  which conflates design doc §5.1's **Camp** node (EXP + a temporary pre-battle buff) with §5.2's
  **Pokémon Center** hub tab (adoption). Pick one meaning when node resolution lands.
- Design doc §5.2's **Location Hub** (a tabbed Team/Map/Shop/Center screen) is not what got built:
  Team is a standalone scene reached from the Ingame Menu, and the map is its own scene. This may
  well be the better shape for the game — but it's a live deviation from the design doc, not an
  implementation of it. See ADR 0002.

**Also not built:** the real drag-and-drop catching system, evolution, the Trailblazer minigame
(no `Scripts/Minigame` folder — it was never started), Pokémon Center adoption, a real Shop
economy, type synergy bonuses, Gym/Badge flow, Event and PvP node behavior, Region Hub /
Location selection, paging the Box past six slots, and **any save/load layer** — which is why
"Continue Run" only resumes a run still in memory this session, and why History has nothing to
list. Save/load is Phase 2 in the list below but is arguably the thing most blocking the shell
from feeling real.

**Phase 0 — Battle-sim rework + first hand-authored Location (prototype, solo, offline)** — *sim
and content done; the Location it was supposed to prove out is currently unplayable (see Status).*
- ✅ Rework/replace `Simulation` to match `docs/battle-sim-spec.md`: Lead/Support formation, Step
  loop, charge-meter passive triggers, both runners (precomputed + on-demand).
- ✅ Import the first species from `docs/pokemon_stats_unique.xlsx` as content (stats only —
  abilities are blank in the source sheet; type-flavored passives hand-authored per §10.3/§11 of
  the design doc). Since grown to 28 species / 17 passives.
- ⚠️ One hand-authored Location (a Forest), PvE + Camp + Shop nodes, basic Step-based battles
  watchable step-through or autoplay — *was* built against the Forest hub scene, which has since
  been retired in favor of the shell (ADR 0002). The controllers survive, orphaned; no scene runs
  a battle today. Re-landing this on the Location map is Phase 1's first job.
- ✅ Catching stubbed as an end-of-fight "pick 1 from defeated" (`CatchResolver`); Trailblazer
  stubbed as skipped entirely (there's still only one Location, so there's nothing to travel
  between).
- ✅ **Exit criteria:** a full PvE-node fight resolves deterministically via the new Step model and
  is covered by golden fixtures in `/shared/fixtures`.

**Phase 1 — Full run loop** — *in progress, approached shell-first (ADR 0002).*
- Done out of order: Character Select, the branching Location-map generator + walkable map scene,
  the Home/Ingame-menu shell, Team management (drag to rearrange, release a mon).
- Still to do: **node resolution on the map** (make arriving at a node start its fight/event/camp)
  and collapsing the two map models — the two blockers in Status above; then the real Gym/Badge
  flow, Morale/win-loss loop, evolution (via PokeAPI evolution chains, restricted to the curated
  roster), the full drag-and-drop catching system (Step-boundary throws, HP%/status-based odds),
  Pokémon Center adoption, type synergy bonuses, and the real Trailblazer minigame (lane
  obstacle-dodge, Speed/Type-driven per §6 of the design doc).

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
- **Integration/PlayMode tests:** these drive the *saved scenes* — finding real objects by
  `Transform.Find` and firing `Button.onClick.Invoke()` — so they catch a scene that stopped
  matching its controller, which EditMode tests can't. Current suites:
  `CharacterSelectScenePlayModeTests`, `RegionMapScenePlayModeTests`, `NavigationScenePlayModeTests`
  (the Home/menu/Team shell, including the drag-to-rearrange and release gestures),
  `DevRosterScenePlayModeTests`. Still owed, as their features land: catch-chance rolls, Pokémon
  Center adoption, save/load round-trip of a run.
- **Content integrity:** `ContentIntegrityTests` fails if a species/passive asset on disk isn't
  registered in its library or can't resolve its sprite — the failure mode where an asset exists
  but is invisible to the game is otherwise silent.
- **Backend tests (Phase 3+):** Vitest + Supertest against routes, using a disposable Postgres
  (Docker Compose) rather than mocks.
- **CI gate:** PRs must pass client EditMode + PlayMode tests and (once it exists) server tests
  before merge. Note the client job in `.github/workflows/ci.yml` is `continue-on-error: true`
  until `UNITY_LICENSE` secrets exist, so **CI is not currently a real gate** — run the suites
  locally (commands in CLAUDE.md).
- No manual-only testing for simulation logic — if it's not covered by an automated test, assume
  it's broken.

## 8. Data & Content Pipeline

- **Starting roster:** `docs/pokemon_stats_unique.xlsx` is the locked-in source for the 183
  species (id, name, types, Attack/HP/Speed per evolution stage). Stats are explicit placeholders
  — treat every number as a first draft. The sheet's Ability column is empty; passives are
  hand-authored separately, following the Type-flavor seeds in design doc §11.
- **Species/type/evolution/sprite data:** pulled from PokeAPI, filtered to the 183 curated
  species, cached locally rather than hit at runtime. See `docs/content-schema.md` for the exact
  asset shape.
- **The content-import pipeline does not exist yet.** This section has described it as if it did:
  there is no xlsx→ScriptableObject importer under `Assets/Editor` and no PokeAPI fetch script in
  `/tools` (only `generate_ui_sprites.py`, which authors the 9-sliced UI chrome). The 28 curated
  species were **hand-authored** asset by asset, with stats typed in from the sheet. That's been
  fine at 28; it will not be fine at 183, so building the importer is the real prerequisite for the
  Phase 2 roster expansion — write it before hand-authoring the next tranche. Until it exists,
  `ContentIntegrityTests` is the only thing catching a malformed or unregistered asset.
- **The curated slice is base-stage only.** All 28 species are first-stage forms and not one has
  `EvolvesInto` set, so evolution has nothing to act on yet — authoring the evolved forms and
  wiring the chains is part of the evolution work in Phase 1, not a separate content chore.
- **Legendaries:** the 7 folded-in Legendaries (Mew, Mewtwo, Rayquaza, Ho-Oh, Lugia, Kyogre,
  Groudon) currently have no rarity flag in the source sheet; per the design doc's carried-forward
  assumption, treat them as Legendary-tier (ultra-rare, PvE-only, full-party-wipe-risk
  encounters) unless a future decision says otherwise.
- **Art:** PokeAPI official-artwork sprites are cached locally at `client/Assets/Art/Pokemon/{id}.png`
  for all 183 roster species (fetched by name from PokeAPI, resized to 256px) and wired up via each
  curated species' `Sprite` reference + the `Pets.Data.PokemonSprites.Load(...)` accessor — see
  Character Select for the first usage. Species not yet curated already have a cached sprite
  waiting, so authoring their `PokemonSpeciesDefinition` asset only needs that reference set, not a
  fresh art fetch.

  These live under `Art`, not `Resources`, deliberately: everything in a `Resources` folder ships
  whether or not anything references it, so the 155 not-yet-curated species were adding ~13 MB to
  every build. A direct reference means only curated species' art is included. Import settings
  (mipmaps off, block compression on) are applied from code by
  `Assets/Editor/PokemonSpriteImportProcessor.cs` — see `client/Assets/Art/README.md`. Sprites that
  *are* resolved by string path at runtime (UI chrome, type badges, node icons) still live under
  `Assets/Resources/Sprites/`.

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
- ~~**Migration cost from the old code**~~ — resolved. The old 5-slot `Simulation`/`ShopEconomy`
  code was deleted and the sim rewritten against `docs/battle-sim-spec.md`; nothing pre-pivot
  remains in the tree.
- **Orphaned-controller rot (new, 2026-09-12):** six `Scripts/Gameplay` controllers and one prefab
  are attached to no scene (see §6 Status) and therefore have no PlayMode coverage — only whatever
  EditMode tests exercise the Meta logic behind them. The longer they sit detached, the more likely
  they've silently drifted from the `RunState`/map shape they'd need to re-attach to. Either
  re-land them (Next Steps 2) or delete them and rebuild from git history; don't leave them
  indefinitely.
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

The shell is built; the run inside it is not (see §6 Status). The ordering below is deliberate —
items 1 and 2 unblock everything else, and until they land there is no game loop to test any of
the rest against.

1. **Collapse the two map models onto one.** Half done: `RunState` now holds the branching graph
   and the walked path (`LocationMap`, `VisitedMapNodeIds`, bound by `RegionMapTraversal.ForRun`),
   so the map survives leaving and re-entering the screen. What's left is retiring
   `Nodes` + `CurrentNodeIndex` and `ForestLocationFactory`'s linear list (or keeping the latter
   only as a seeded fixture for tests), and pointing `RunBootstrapper` and `LocationFlowController`
   at the branching map instead. EditMode-testable, so do it before touching any screen.
2. **Node resolution on arrival.** Hook the end of `RegionMapController.WalkTo` into a per-node-type
   handler and re-land the orphaned screens on the map scene: PvE (`PvEClashController` — this is
   what makes a battle playable in-game again) and Camp (`CampPanelController` +
   `CampOverlay.prefab`) already have logic behind them; Event, PvP and Gym don't yet. Decide at
   this point whether the Camp node is a Camp or a Pokémon Center (see §6 naming debt).
3. **Gym/Badge node:** the mandatory boss at the end of a Location's map — Line-Up menu (scout the
   opponent, reorder before the fight), badge-as-relic reward, and the Morale/win-loss loop that
   makes losing mean something.
4. **Save/load.** Listed under Phase 2 below, but it's what "Continue Run", History, and any
   meta-progression all actually wait on — consider pulling it forward once the loop above exists
   and there's a run state worth persisting.
5. **The real drag-and-drop catching system** (Step-boundary ball throws, HP%/status-based odds,
   design doc §12.1) in place of the "pick 1 from defeated" stub. Depends on 2.
6. **Rename `RegionMap*` → `LocationMap*`** before building the actual Region Hub / Location
   selection tier (§6 naming debt). Still open, and now slightly wider than before —
   `RunState.LocationMap` is already correctly named and holds a `RegionMap`, which reads oddly.
   Mechanical, but touches ~8 files plus a scene regeneration, so do it as its own commit, not
   folded into feature work.
7. Evolution (via PokeAPI evolution chains), the Trailblazer minigame, Pokémon Center adoption,
   and a real Shop economy.
8. Narrow Character Select toward design doc §3's actual flow (fixed/chosen starter + a 3-option
   secondary pick + cosmetics) if that distinction ends up mattering in play.
9. Continue expanding curated content past the current 28 species via the content-import pipeline
   (§8) as more Locations/roster breadth are needed — the sprites for all 183 are already cached.
