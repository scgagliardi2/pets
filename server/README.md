# server

Node.js/TypeScript backend for the Pokémon roguelite autobattler's async-PvP layer. **Scaffolded
only, not implemented.**

Per PLAN.md §6, this stays empty (structure only) until Phase 3 — Phases 0-2 are a fully offline
Unity client (solo roguelite run, no accounts, no networking). Don't add real routes/services/db
code here before Phase 3 starts.

Current contents: `package.json` (its `test` script is a stub that exits 0, so CI stays green),
`tsconfig.json`, and `src/{routes,services,db}` + `test` as empty `.gitkeep` directories. Note the
client's local-save layer doesn't exist yet either — that's client-side Phase 2 work, not this
folder's. See
`docs/architecture-decisions/0001-pivot-to-pokemon-roguelite.md` for why the phase numbering
resets from what may be referenced in older commit messages.
