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

**Design pivot accepted, implementation not yet reworked to match it.** The docs above describe
the current (Pokémon roguelite / Lead-Support) direction. The code currently checked in under
`client/Assets/Scripts/Simulation` and `Gameplay/ShopEconomy` still implements an earlier,
different design (a 5-slot turn-based shop-drafter) and is not the source of truth — see the ADR
linked above. Phase 0 of the new plan (PLAN.md §6) — reworking the battle sim to the new
Lead/Support/Step model and standing up one hand-authored Location — is the next actual work.

## Getting started

1. Install [Unity Hub](https://unity.com/download), then use it to install the Unity editor
   version pinned in `client/ProjectSettings/ProjectVersion.txt`, including the iOS and Android
   Build Support modules.
2. Open `/client` as a project through Unity Hub (not by opening the folder in a generic editor).
3. Open `Assets/Scenes/Game.unity` and press Play — note this currently runs the *old*
   shop-drafter prototype, not the Pokémon roguelite described above (see Status).
4. The `/server` backend (Node.js/TypeScript + PostgreSQL) isn't needed until PLAN.md §6 Phase
   3 — nothing to install for it yet.
