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
- **[`docs/architecture-decisions/`](docs/architecture-decisions/)** — ADR 0001: why this project
  changed direction from an earlier original-IP shop-battler design. ADR 0002: why the build order
  then diverged from the plan, and which deviations from the design doc are still open.

## Status

**Playable: the shell. Not playable: the run inside it.** You can go Home → Character Select (pick
a Starter and Secondary from the curated roster) → the Location map and walk it to the Gym, with an
in-run menu, a Team screen (drag mons to rearrange or release them), History, Credits, and a dev
screen for stuffing a run with mons. Arriving at a map node doesn't start anything yet, so **no
battle happens in-game** — the battle simulator is built and tested to the Lead/Support/Step spec,
but it only ever runs from the test suite.

Built and covered: the simulator (`client/Assets/Scripts/Simulation`) with golden fixtures in
`/shared/fixtures`; the pure-C# run layer (`Scripts/Meta`) including a branching map generator;
28 curated species and 17 passives (`client/Assets/Content`); every screen listed above. 103
EditMode and 49 PlayMode tests pass.

Not built: node resolution, Gym/Badge flow, catching, evolution, the Trailblazer minigame, Pokémon
Center adoption, the Shop, type synergy, save/load, and the content-import pipeline. See
**[PLAN.md](PLAN.md) §6 Status** for the honest, detailed version and **§11** for what's next;
[`docs/architecture-decisions/0002-shell-first-deviation.md`](docs/architecture-decisions/0002-shell-first-deviation.md)
explains why the build order diverged from the plan.

## Getting started

1. Install [Unity Hub](https://unity.com/download), then use it to install the editor version
   pinned in `client/ProjectSettings/ProjectVersion.txt` (currently **6000.6.0f1**), including the
   iOS and Android Build Support modules. See [SETUP.md](SETUP.md) for the full toolchain walkthrough.
2. Open `/client` as a project through Unity Hub (not by opening the folder in a generic editor).
3. Press play on `Assets/Scenes/Home.unity` — that's the entry point, and it's first in Build
   Settings, so a built player starts there too.
4. **Scenes are generated from code.** Every scene is produced by an `Assets/Editor/*SceneBuilder.cs`;
   don't hand-edit a `.unity` file. After changing a builder, a controller's serialized fields, or
   the shared UI prefabs, run **Pets > Build All Scenes**.
5. Run the tests via Window > General > Test Runner (both EditMode and PlayMode), or headlessly with
   the Editor closed — commands are in [CLAUDE.md](CLAUDE.md) under "Testing & running".
6. The `/server` backend (Node.js/TypeScript + PostgreSQL) is empty scaffolding until PLAN.md §6
   Phase 3 — nothing to install for it yet.
