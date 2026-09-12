# ADR 0004: The Full 183-Species Roster, a Stat-Total Cap on Character Select, and a Pokédex Screen

**Status:** Accepted
**Date:** 2026-09-12

## Context

Content stood at 28 hand-authored species out of the 183 the roster sheet
(`docs/pokemon_stats_unique.xlsx`) locks in. PLAN.md §8 was explicit about what that meant: the
content-import pipeline it describes didn't exist, the 28 had been typed in asset by asset, and
"that's been fine at 28; it will not be fine at 183, so building the importer is the real
prerequisite for the Phase 2 roster expansion — write it before hand-authoring the next tranche."
CLAUDE.md carries the same rule.

Importing the rest ran into three things the sheet can't answer on its own:

1. **The sheet has no id column**, and the National Dex id is what everything downstream is keyed by
   — the cached artwork is `Assets/Art/Pokemon/{id}.png`, and `PokemonSpeciesLibrary.GetById` is the
   runtime lookup.
2. **The sheet's Ability column is empty**, but `ContentIntegrityTests` (rightly) fails any species
   the battle sim can't resolve a passive for. 155 new species needed 155 passives that nobody has
   authored.
3. **The roster is not 183 comparable mons.** It's base forms, their evolutions, and seven
   Legendaries all in one list, with no stage or rarity column. Character Select showed "the whole
   curated roster" — at 28 base-stage species that was a reasonable starter pick; at 183 it offers
   to open a run on Groudon.

## Decision

**1. An xlsx → ScriptableObject importer, with a checked-in PokeAPI id cache.**

`Assets/Editor/SpeciesRosterImporter.cs` reads the roster sheet directly (an .xlsx is a zip of XML;
this needs seven columns out of one sheet, which is not worth a dependency) and writes one
`PokemonSpeciesDefinitionAsset` per row, then rebuilds `PokemonSpeciesLibrary` from the folder in
Dex order. `tools/fetch_roster_ids.py` resolves sheet name → Dex id against PokeAPI in a single
request and caches the answer in `docs/roster_pokeapi_ids.json`, so an import never touches the
network — the caching etiquette PLAN.md §9 asks for, and it keeps the import offline and
deterministic.

The importer is **idempotent and non-destructive on purpose**: it writes only the fields the sheet
owns, leaves hand-authored passives and evolution links alone, and never deletes an asset. That's
what let it run over the existing 28 without touching a byte of them (155 created, 0 updated, 28
unchanged), and it's what makes re-running it after a sheet edit safe rather than a gamble.

`RosterImportTests` (EditMode) then compares the sheet to the assets on every test run, which is the
check that catches the failure CLAUDE.md names but nothing previously enforced: stats edited in one
place and never reconciled with the other.

**2. Newly-imported species get a shared passive chosen by their primary type.**

One type-flavored passive per type (design doc §11's Type-flavor seeds), in
`SpeciesRosterImporter.DefaultPassiveIdByType`, with three new hand-authored assets filling the
types the curated 28 never covered (Ground, Dark, Steel). The alternative to a placeholder was
either 155 bespoke passives up front or 155 species the content tests reject.

This is **explicitly a placeholder**, not a design position: bespoke passives are the Phase 2
content work, and the table only applies to a species with no passive yet, so hand-authoring one
later simply overrides it. It's also what the curated 28 already did informally —
`geodude-stone-guard` is shared by Geodude, Diglett, Onix and Snorlax.

**3. Character Select offers only species with a base stat total under 180.**

`CharacterSelectController.MaxStarterStatTotal`. 68 of the 183 pass it today, and they read as the
first-stage-shaped end of the roster. The cap is a screen rule rather than a flag on the asset
because it's about what this screen should offer, not a property of the species — and the sheet
carries no evolution-stage column to gate on instead. Evolution (PLAN.md Phase 1) is what's
eventually meant to make this unnecessary.

This is a **deviation from design doc §3**, which wants a fixed-or-chosen starter plus a narrowed
3-option secondary. It narrows in a different direction than §3 does; §3 parity remains open (PLAN.md
§11 item 8).

**4. A Pokédex screen, reached from Home.**

`Pokedex.unity` — the same grid, filter and sort as Character Select, over the whole roster, with a
card press filling a detail line instead of picking. With the cap above, most of the roster is
otherwise only ever seen as a wild encounter; the detail line is also the only place a species'
passive is readable in-game.

The two screens' shared parts moved into `Gameplay/SpeciesGridView.cs` (the card pool and its
binding) and `Gameplay/SpeciesRosterToolbar.cs` (the filter/sort state and the sort buttons' visuals)
rather than being copied. The design doc's §17 screen inventory didn't list a Pokédex; it does now.

## Consequences

- **Encounters got a lot wilder, and this is not yet handled.** `EncounterGenerator`,
  `GymTeamGenerator` and `RandomBattle` all draw from `library.AllSpecies` with no filter, so the
  Forest's wild pool now includes Tyranitar, Salamence and Groudon alongside Caterpie. `IsLegendary`
  is imported and correct but still read by nothing. Nothing about the *loop* broke — the sim, the
  Morale ledger and the tests are all fine with it — but a run's difficulty is now arbitrary.
  Filtering the encounter pools (by stat total, stage, or Legendary flag, with Legendaries becoming
  the rare full-party-wipe-risk encounters design doc §8 describes) is the obvious next piece of
  work and is listed in PLAN.md §11.
- Stats for all 183 remain the sheet's explicit placeholders, so the tuning surface PLAN.md §10
  flagged is now fully exposed rather than mostly latent.
- Pikachu is 55/35/90 = exactly 180 and therefore *not* startable. That's the cap being a strict
  "under", not an oversight — move the constant if the line should fall elsewhere.
- All 183 sprites are now referenced by a species asset, so the build-size argument in PLAN.md §8
  for keeping them under `Art` rather than `Resources` no longer buys anything in practice. The
  split still holds for the right reason (a wrong reference is visible; a wrong path string is a
  silent null), so it's left alone.
- The curated slice is no longer base-stage-only, so `EvolvesInto` now has real targets to point at
  — wiring the evolution chains is unblocked content work rather than blocked on authoring the
  evolved forms first.
