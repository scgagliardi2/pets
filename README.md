# pets

A single-player roguelite (Slay the Spire–style meta-layer: Region → Location → node-map → Gym)
wrapped around a collect-and-auto-battle combat layer (Super Auto Pets–style Lead/Support
Steps), themed with Pokémon species, types, and assets. Playable solo, with an asynchronous PvP
node inside each run.

Solo hobby/personal project, no fixed deadline, **non-commercial** — see PLAN.md §9 before
distributing this anywhere. Client is Unity (C#); a Node/TypeScript + PostgreSQL backend is
introduced later for accounts and async PvP.

- **[PLAN.md](PLAN.md)** — condensed project plan: game design summary, architecture, tech
  stack, and the phased roadmap.
- **[`docs/pokemon-roguelite-autobattler-design-doc.md`](docs/pokemon-roguelite-autobattler-design-doc.md)**
  — the full design doc; go here for exhaustive mechanics detail.
- **[`docs/pokemon_stats_unique.xlsx`](docs/pokemon_stats_unique.xlsx)** — the locked-in starting
  roster: 183 species spanning Gen 1–3, stats are placeholders pending balance passes.
- **[CLAUDE.md](CLAUDE.md)** — working conventions and hard rules for development in this repo.
- **[`docs/architecture-decisions/0001-pivot-to-pokemon-roguelite.md`](docs/architecture-decisions/0001-pivot-to-pokemon-roguelite.md)**
  — why this project changed direction from an earlier original-IP shop-battler design, and what
  that means for the existing code.

## Status

**Phase 0 partially built.** The battle simulator (`client/Assets/Scripts/Simulation`) has been
fully reworked to the Pokémon roguelite's Lead/Support/Step model, with EditMode tests and golden
fixtures in `/shared/fixtures`, plus hand-authored content for 13 curated species
(`client/Assets/Content`) exercised end-to-end by a real PvE-fight integration test. The old
5-slot shop-drafter code has been removed rather than kept alongside. **Not yet built:** the
actual Forest Location (node-map, PvE/Camp/Shop screens), stubbed catching/Trailblazer, and any
save/run layer — see PLAN.md §6 for exactly what's left of Phase 0's exit criteria.

## Getting started

1. Install [Unity Hub](https://unity.com/download), then use it to install the Unity editor
   version pinned in `client/ProjectSettings/ProjectVersion.txt`, including the iOS and Android
   Build Support modules.
2. Open `/client` as a project through Unity Hub (not by opening the folder in a generic editor).
3. There's no playable scene yet — `Assets/Scenes/Game.unity` and `SampleScene.unity` predate the
   pivot and aren't wired to the new content/sim. Run the EditMode test suite (Window > General >
   Test Runner > EditMode) to see the battle sim and content working.
4. The `/server` backend (Node.js/TypeScript + PostgreSQL) isn't needed until PLAN.md §6 Phase
   3 — nothing to install for it yet.
