# ADR 0005: EXP Is a Small Counter, Stats Are Derived From It, and Evolution Runs Off It

**Status:** Accepted
**Date:** 2026-09-12

## Context

EXP existed but did nothing anyone could see. `ExperienceResolver` carried a levels-and-curve model
borrowed from nowhere in particular — 100 EXP to level 2, ×1.5 per level after, +10% to each stat on
level-up — and the awards were sized to match it: 20 EXP for a won PvE node, 60 for a Gym, 30 for a
Camp. Five node fights bought one level and roughly one point of Attack. Evolution, the thing EXP is
ultimately *for* (design doc §12.3), wasn't implemented at all, and couldn't have been: not one
species had `EvolvesInto` set.

Three problems underneath that:

1. **The growth was invisible and unexplainable.** A percentage of a placeholder stat, rounded to an
   int, with a minimum of 1. Nobody could look at a mon and say why it had the numbers it had.
2. **Stats were accumulated, not derived.** Each level-up added onto the running total, so a mon's
   stats depended on the order things had happened in, couldn't be re-derived from its EXP, and left
   no way to apply an evolution's change of base stats without double-counting.
3. **Nothing could evolve.** `EvolvesInto` is a reference to another species asset, and the roster
   sheet has no evolution column. The links had never been authored.

There was also no way to spend a duplicate. Catching produces dupes by design (design doc §12.1) and
§12.3 answers them with "combine 2 of the same mon", but the Team screen only knew how to reorder
and release.

## Decision

**1. EXP is a small counter, and a flat one.**

One point per battle won, two per duplicate combined in. Every point is worth
`ExperienceResolver.StatGainPerExp` (10) on Attack, Health *and* Speed — no curve, no per-stat
weighting. `Level` and `ExpToNextLevel` are gone from `PokemonInstance`: with a flat gain, a level
was a second name for the EXP count. The numbers are explicit placeholders in exactly the sense the
base stats themselves are (design doc §8), and a model this blunt is the one that stays legible
while they're tuned.

**2. Stats are derived from species + EXP, never accumulated into.**

`ExperienceResolver.Recompute` rebuilds `CurrentStats` as `species base + StatGainPerExp * Exp`, so
that expression is always the whole story: the same total EXP gives the same stats however it
arrived, an evolution's new base stats apply by simply recomputing, and a future save layer can
rebuild a loaded mon rather than trusting stored numbers.

The cost is that anything written to `CurrentStats` that doesn't follow from species + EXP is
transient — the next EXP grant overwrites it. A permanent modifier (an item) therefore has to become
an input to the calculation rather than a one-off addition. This is written on the field itself,
because it is exactly the kind of thing that would otherwise be discovered as a bug. It already was
one, in a PlayMode test that made its mon unbeatable by writing 9999s straight onto `CurrentStats`
and then lost every fight after the first.

**3. Evolution every 3 EXP, counted per instance.**

`ExperienceResolver.ExpPerEvolution` (3), against `PokemonInstance.TimesEvolved` — so a fresh mon
evolves at 3 EXP, its next form at 6, and a three-stage line doesn't resolve end to end the moment it
first hits 3. Counting per instance rather than from the species' chain depth matters for the four
curated base forms whose real pre-evolution isn't in the roster (Pikachu, Clefairy, Jigglypuff,
Snorlax): they're stage 1 in the real chain but have evolved zero times, and should reach Raichu on
the first threshold like anything else.

There is deliberately **no per-species EXP threshold**. Nothing in the roster sheet or PokeAPI
supplies one, and an always-zero field that looks authoritative is worse than no field — so the old
`EvolutionExpThreshold` was removed rather than left unused.

**4. The evolution chains come from PokeAPI, cached, like the rest of the content.**

`tools/fetch_evolution_chains.py` writes `docs/roster_evolution_chains.json`; the roster importer
fills each species' `EvolvesInto` and a new `EvolutionStage` from it. 92 of the 183 species now
evolve into another roster species. `EvolutionStage` is the chain depth, and it finally gives
`MagnitudeByStage` (content-schema.md §1, §3) the index it was always for — a passive's magnitude
now actually scales when its owner evolves.

**Branching lines are left unresolved on purpose.** `EvolvesInto` is one reference and cannot
express "Eevee becomes one of seven", so Eevee, Tyrogue and Nincada don't evolve at all. The cache
records them under `branching` rather than silently picking a winner; a branch picker is its own
feature.

**5. Every mon in the line-up is paid, and a Gym pays the same as anything else.**

Both were otherwise before. At one point per win there's no room to express "more" for a Gym without
making it worth an instant evolution, and paying only the survivors meant a Reserve behind a Lead
that never fainted could never grow. Camp was rescaled from 30 EXP to 1 for the same reason — carried
over unchanged it would have been +300 to every stat and ten evolutions in one click.

**6. Combining is a drag onto a duplicate, and the screen asks what it meant.**

Dropping a mon onto another of the **same species** is ambiguous: it's how you'd reorder two
Bulbasaur so the grown one leads, and it's also the combine gesture. The Team screen puts the choice
to the player (Combine / Swap / Cancel) rather than guessing. The mon dropped onto survives and gains
`CombineResolver.ExpGranted` (2); the dragged one is consumed, and its own EXP is *not* carried over
— a combine is a dupe sink, not a way to launder a second mon's growth. A combine that would empty
the line-up is refused by the same rule releasing obeys, and says so.

## Consequences

- **Three wins is an evolution**, and a run's fights are a handful of nodes long, so a line-up now
  visibly changes shape within a single Location. That is the intended feel, but it is fast, and it
  is the first number to look at when balance starts (PLAN.md §10).
- **+10 to every stat per point is large** against base stats that run 20–150. A mon with 6 EXP has
  +60 on all three — comparable to its own base. Deliberate for now (the user asked for numbers that
  make the system visible before it's tuned), and the reason `StatGainPerExp` is a named constant.
- **Speed grows with everything else**, which feeds the charge meter (battle-sim-spec.md §3–§4), so
  passives fire progressively sooner as a run goes on. Nothing accounts for that yet.
- HP tops back up on every EXP grant, because nothing carries damage between fights (ADR 0003). When
  damage does persist, `Recompute` has to preserve damage taken rather than the HP value, or growing
  will quietly heal — noted on the method.
- The wild encounter pools are still unfiltered (ADR 0004), so a Legendary can still turn up in the
  Forest — and now it can evolve too, where it has anywhere to go. Unchanged by this work; still
  PLAN.md §11's next item.
- Eevee is in Character Select's starter-eligible slice and can never evolve, which a player will
  notice before they notice the branching-evolution gap that explains it.
