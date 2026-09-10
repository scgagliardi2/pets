# pets

A mobile auto-battler in the spirit of *Super Auto Pets*: draft creatures in a shop phase, build
a team, and watch turn-based battles resolve automatically.

Solo hobby project, no fixed deadline. Client is Unity (C#); a Node/TypeScript + PostgreSQL
backend is introduced later for accounts and async PvP.

- **[PLAN.md](PLAN.md)** — full project plan: game design, architecture, tech stack, and the
  phased roadmap.
- **[CLAUDE.md](CLAUDE.md)** — working conventions and hard rules for development in this repo.

## Status

Pre-scaffolding — no `/client` or `/server` code yet. See PLAN.md §6 (Development Phases) and
§11 (Next Steps) for what's currently in progress.

## Getting started

1. Install [Unity Hub](https://unity.com/download), then use it to install the current Unity
   **LTS** editor version, including the iOS and Android Build Support modules.
2. Once `/client` exists, open it as a project through Unity Hub (not by opening the folder in a
   generic editor).
3. The `/server` backend (Node.js/TypeScript + PostgreSQL) isn't needed until Phase 3 — nothing
   to install for it yet.
