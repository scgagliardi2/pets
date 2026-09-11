# ADR 0001: Pivot to Pokémon Roguelite Autobattler

**Status:** Accepted
**Date:** 2026-09-10

## Context

The repo's original plan (`PLAN.md`, `CLAUDE.md`, `docs/battle-sim-spec.md`,
`docs/content-schema.md`) described an original-IP, Super Auto Pets–style shop-drafting
auto-battler: a 5-slot board, turn-based rounds, `OnBattleStart`/`OnHurt`/`OnFaint` triggers, no
meta-layer beyond "survive the bot roster." Phases 0–2 of that design were implemented in Unity/
C# (`client/Assets/Scripts/Simulation`, `Gameplay/ShopEconomy`, a 14-creature roster, a 12-round
bot roster, home/stats screens) and are tested and playable.

`docs/pokemon-roguelite-autobattler-design-doc.md` and `docs/pokemon_stats_unique.xlsx` were
added afterward, describing a substantially different game: a Slay the Spire–style roguelite
meta-layer (Regions → Locations → node-maps → mandatory Gym) wrapped around a Super Auto
Pets–style **Lead/Support** combat model (two active slots, not five; simultaneous Step-based
exchanges; charge-meter-triggered passives instead of hurt/faint triggers), skinned with Pokémon
species, types, and assets (183 curated species spanning Gen 1–3, non-commercial/personal-project
scope only). That doc's own suggested architecture (§18) proposes a React + TypeScript frontend.

These two designs disagree on genre structure (drafting vs. exploration), combat model (5-slot
turn-based vs. 2-slot Step-based), IP (original vs. Pokémon-skinned), and even client engine
(Unity vs. React).

## Decision

1. **The Pokémon roguelite design is the current direction.** `PLAN.md`, `CLAUDE.md`,
   `README.md`, `docs/battle-sim-spec.md`, and `docs/content-schema.md` are rewritten to describe
   it, keyed off `docs/pokemon-roguelite-autobattler-design-doc.md` as the detailed design source
   and `docs/pokemon_stats_unique.xlsx` as the locked-in starting roster (183 species, Gen 1–3,
   stats are placeholders pending balance passes).
2. **Client engine stays Unity/C#**, overriding the design doc's own React + TypeScript
   suggestion (§18). The design doc's suggested module split (pure battle-sim core, two thin
   runners, data-driven content, serializable run state, UI layer) is preserved conceptually and
   re-expressed in Unity terms (ScriptableObjects for content, plain C# for the sim core,
   MonoBehaviours for screens) — see `docs/battle-sim-spec.md` and `docs/content-schema.md`.
3. **Non-commercial scope is a hard constraint**, not a later concern — see `CLAUDE.md`. This
   project uses Nintendo/Game Freak/Creatures IP (species, types, names) and is not to be
   published widely or monetized.
4. **The Phase 0–2 shop-battler code is superseded, not deleted.** `client/Assets/Scripts/
   Simulation`, `Gameplay/ShopEconomy`, the 14-creature roster, and the golden fixtures in
   `/shared/fixtures` implement the *old* combat model (5-slot board, hurt/faint triggers) and do
   not match the new Lead/Support/charge-meter model in `docs/battle-sim-spec.md`. They are left
   in place for reference (the deterministic-RNG approach and save-system scaffolding are likely
   reusable) but are not the source of truth going forward. Reworking/replacing this code to match
   the new spec is tracked as its own piece of work — see `PLAN.md` §6, Phase 0 (new).
5. **Phase numbering resets.** The "Phase 2 in progress" status on the old design doesn't carry
   forward; the new plan's phases (§19 of the design doc, adapted) start over at Phase 0, since
   the actual combat/content code needs to be reworked to match the new model before any of it can
   be called "done" against the new spec.

## Consequences

- Docs and code will be inconsistent for a while: the docs now describe the roguelite/Lead-Support
  game, but the checked-in Unity code still runs the old shop-battler. This is expected and
  tracked, not an oversight — see `PLAN.md`'s status line.
- Anyone picking up simulation/content work should treat `docs/battle-sim-spec.md` and
  `docs/content-schema.md` (post-pivot versions) as the target, not the current
  `Simulation`/`Data` code.
- The 7 folded-in Legendaries (Mew, Mewtwo, Rayquaza, Ho-Oh, Lugia, Kyogre, Groudon) and the
  183-species roster generally are subject to the same non-commercial scope note as the rest of
  the project.
