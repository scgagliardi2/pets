# pets

A mobile auto-battler in the spirit of *Super Auto Pets*: draft creatures in a shop phase, build
a team, and watch turn-based battles resolve automatically.

Solo hobby project, no fixed deadline. Client is Unity (C#); a Node/TypeScript + PostgreSQL
backend is introduced later for accounts and async PvP.

- **[PLAN.md](PLAN.md)** — full project plan: game design, architecture, tech stack, and the
  phased roadmap.
- **[CLAUDE.md](CLAUDE.md)** — working conventions and hard rules for development in this repo.

## Status

**Phase 1 complete** — a full run (shop → battle → shop, repeated through the bot roster, win or
lose) is playable end to end with placeholder art and local save. See PLAN.md §6 (Development
Phases) for what Phase 2 adds next.

## Getting started

1. Install [Unity Hub](https://unity.com/download), then use it to install the Unity editor
   version pinned in `client/ProjectSettings/ProjectVersion.txt`, including the iOS and Android
   Build Support modules.
2. Open `/client` as a project through Unity Hub (not by opening the folder in a generic editor).
3. Open `Assets/Scenes/Game.unity` and press Play to try the current build.
4. The `/server` backend (Node.js/TypeScript + PostgreSQL) isn't needed until Phase 3 — nothing
   to install for it yet.
