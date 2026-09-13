# ADR 0006: Levels on a Run-Scale Curve, an Eight-Badge Run, and the Region Hub

**Status:** Accepted
**Date:** 2026-09-12
**Supersedes:** ADR 0005 decisions 1, 3 and 5 (the flat EXP counter, evolution every 3 EXP, flat
rewards) and ADR 0003 decision 2 (a Gym win ends the run). ADR 0005's derived-stats rule (decision 2)
and the combine gesture (decision 6) stand.

## Context

ADR 0005 made growth visible: 1 EXP per win, +10 to every stat per point, an evolution every 3
points. It said itself that the numbers were placeholders and fast. A review against the real roster
(`docs/pokemon_stats_unique.xlsx`) and the simulator's exchange rule found problems that went past
tuning:

1. **No pacing at run scale.** A Location resolves about five nodes plus a Gym, so a mon earned about
   5 EXP per Location. Every evolution in a run happened inside the first Location, and growth kept
   going with nothing left to unlock.
2. **Flat growth erased the roster.** Base stats run 10–150. Adding the same amount to every species
   converges them, so by mid-run a Caterpie and a Charizard were within a rounding error of each
   other.
3. **Fights were two-Step coin flips.** Damage is flat Attack per Step. The roster's median
   Health:Attack is 0.88, so 63% of Leads one-shot the opposing Lead. A model of 4,000 random 2v2
   fights gave a median of 2 Steps and 21% draws. Adding equal amounts to Attack and Health made that
   worse, not better.
4. **Enemies never scaled.** Wild and Gym teams were level-0 mons from the unfiltered roster. The
   same model had the player winning 97% of fights by the end of the first Location and 100% soon
   after, while that first Location could still roll a Legendary.
5. **Late acquisitions were dead weight.** A mon caught in the fourth Location joined at 0 EXP next
   to a party dozens of points ahead.
6. **The run ended at the first Gym**, because there was no Region Hub to return to (ADR 0003). A
   curve across several Locations couldn't even be observed.

The request was a system that levels Pokémon fairly across a run the length of a mainline game:
eight Gyms, with the Region Hub from the design doc (§5.2) after Character Select and after every Gym.

## Decision

**1. A run is eight badges, with the Region Hub between Locations.**

`RunState` gains `CurrentLocation`, `CompletedLocations` (one badge each), `TravelTo` and
`EarnBadge`. Character Select hands off to `RegionHub.unity`, which offers three Locations. Beating a
Gym earns a badge and returns to the hub. The eighth badge wins the run and ends it at Home. The
Ingame Menu's "Back to Map" lands on the hub while the run is between Locations.

Changes from design doc §4/§5.2, to fit the game as built:
- **One Region per run**, and its Locations are the nine §4 types in a static table
  (`Meta/LocationCatalog`), not `LocationTypeDefinition` assets (content-schema.md §9). Nine fixed
  rows with no art don't need an asset pipeline yet.
- **The hub offers three types**, shuffled from the run seed and badge count, never the one just
  completed. They're derived, not stored, so leaving for the Team screen and coming back shows the
  same three.
- **Difficulty comes from badge count, not from which Location is picked.** The choice is about
  which Pokémon types you'll meet and catch.
- **Travel is instant.** The Trailblazer minigame (§6) isn't built.
- **A badge is a count, not a relic** (§14). It also refills Morale to its starting 3, so Morale is a
  budget of losses per Location rather than one pool that has to last eight Gyms.

**2. EXP buys levels on a rising curve, and the level is derived.**

Going from level L to L+1 costs `2 + 2L`. A mon still stores only `Exp`; `ExperienceResolver.LevelOf`
reads the level off the curve. ADR 0005's rule survives unchanged: stats are derived, never
accumulated.

**3. Stats grow in proportion to the species, and Health is tripled.**

`Data/StatGrowth.AtLevel`: each level adds 10% of the species' base stat plus 2, and Health is then
multiplied by 3. The proportional part keeps each species' shape; the flat part stops the weakest
base forms falling hopelessly behind. Tripling Health moves fights from ~2 Steps to ~5, lets a passive
fire more than once, and cuts draws roughly in half. It uses integer arithmetic, because these numbers
feed the deterministic simulator.

**4. Evolution happens at level 8 and level 17.**

Evolution is still counted per instance (`TimesEvolved`), so Pikachu evolves at the first threshold.
`ExperienceResolver.CreateAtLevel` counts how many roster species come before an evolved species, so a
Charmeleon handed out at level 10 next evolves at 17, not straight away. Every wild mon, Gym member,
catch, starter and dev-added mon is built through it.

**5. Nobody falls hopelessly behind.**

After any grant, and whenever a mon joins, `ApplyCatchUp` raises every mon in the line-up and Box to
within 2 levels of the strongest. A catch keeps its own level if that's higher. Combining is worth
exactly one level from anywhere in the survivor's current level, so it stays the way to push a mon
*above* the floor.

**6. Enemies scale with progress, never with the player.**

`Meta/RunProgression` pitches every Location from the badge count:

| | Rule |
|---|---|
| Baseline level | 1 + 3 × badges |
| Wild encounter level | baseline − 1 + (map layer ÷ 2) |
| Wild encounter size | 1 in the first Location, 2 until the fourth badge, 3 after |
| Gym Leader level | baseline + 1 |
| Gym Leader team size | at least the player's line-up, and at least 2 + badges ÷ 2 (max 6) |

Keying enemies to the player's own level would make EXP worthless. `Meta/EncounterPool` filters what
both wild and Gym teams draw:
- **base forms only:** an evolved species appears by levelling up under the player's own rules
- **a base-stat-total cap**, 180 + 20 per badge, lifted for the final Location
- **Legendaries only on the final Gym Leader's team**

A Gym Leader draws from the Location's type bias. The Gym's old +25% Health bonus is gone: against a
team as long as the player's line-up, it made every Gym a coin flip.

**7. Rewards scale with what was beaten.**

| Source | EXP to each line-up mon |
|---|---|
| Won fight | 2 + the foes' average level |
| Gym | double a wild fight at its level |
| Pokémon Center | 2 + the Location's baseline level (about a fight's worth) |
| Combine | exactly one level |

Rewards that scale with the foe are what let a rising cost curve keep a steady pace across all eight
badges.

## How the numbers were chosen

Hand-picked numbers failed twice in the review, so the constants were fitted by simulating whole
runs. The Monte Carlo model used:
- the real 183-species roster and evolution chains
- the simulator's flat simultaneous exchange (no passives)
- the map generator's node mix and a naive path-picker that prefers Battle nodes
- a 30% catch rate
- Morale, retries and catch-up exactly as above

It searched the grid of award, curve and enemy-offset constants, scored against three targets: about
85% wild wins, about 65% first-try Gym wins, and the player a level or two above each Gym.

Three rounds failed in ways worth recording:
- **Paying per foe snowballed.** Team sizes grow with badges, so supply grew quadratically against a
  linear cost; the player hit level ~56 against a level-24 Gym.
- **The first Location was a coin flip.** Fights there were 44% wins, and most runs died in it.
- **A Health-buffed Gym matching the player's line-up was too hard.** Only 2–7% of runs survived.

The chosen constants gave:

| Measure | Result |
|---|---|
| Wild wins, every Location | 84–100% |
| Gym first-try wins, every Location | 58–77% |
| Player's top level vs. each Gym | +1 to +2 |
| Starter evolutions | around Locations 3 and 6 |
| Mon caught in Location 4, at run end | within ~2 levels of the top |
| Runs won by the naive path-picker | ~30% |

`RunProgressionTests` pins the shape of the curve, not its exact numbers: enemies climb every badge,
the evolution levels fall in the third and sixth Locations, and a typical Location's rewards take a
player two to four levels up at every badge.

## Consequences

- **The run is playable end to end:** Home → Character Select → Region Hub → Location → Gym → Region
  Hub, eight times, then Home. History still records nothing (no save layer).
- **The numbers came from a model, not from play.** Passives were off, and real players choose paths
  and catches better than the model does. Expect to retune; the constants are named and the tests
  pin the shape.
- **Speed grows with level**, so passives fire more often late in a run, which nothing accounts for
  yet. Battle speed bars are drawn against `StatGrowth.SpeedCeilingAtLevel` so they don't pin full.
- **Event and PvP nodes are still stubs and pay no EXP**, so a path through them earns less. The
  curve assumes about 2.5 wild wins per Location.
- **Battle screen:** stat boxes show each mon's level.
- **Dev tools:** the dev random battle rolls enemies at the party's top level, and dev-added mons
  join at the catch-up level.
- **Unchanged:** Eevee, Tyrogue and Nincada still can't evolve; the badge-as-relic, the Line-Up menu
  before a Gym, and the Trailblazer minigame are still unbuilt; HP still doesn't carry between
  fights, and when it does, `Recompute` has to preserve damage taken (noted on the method).
- **The `RegionMap*` → `LocationMap*` rename** landed as its own commit first, as PLAN.md §11 asked,
  so "Region" now means the tier this ADR builds.
