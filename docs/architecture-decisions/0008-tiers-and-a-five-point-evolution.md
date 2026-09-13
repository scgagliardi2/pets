# ADR 0008: Species Sit in Tiers, EXP Is a Count of Wins, and Five Points Is an Evolution

**Status:** Accepted; decisions 3 and 4 (growth of +1 Attack *and* +1 Health, five points to an
evolution, an evolution rebasing onto the new species' tier line) and the 5-point tier-1 budget
superseded by ADR 0009. The tier structure, the lifetime one-point-a-win EXP count, the flat rewards,
the two-dial enemy scaling and the charge threshold all stand.
**Date:** 2026-09-13
**Supersedes:** ADR 0007 decisions 2, 3, 4, 5 and 7 (the level curve, proportional growth with a
Health multiplier, evolution at Lv 8/17, catch-up in levels, and rewards scaled by foe level), and
what remained of ADR 0005 decision 1. ADR 0005's derived-stats rule (decision 2) and the combine
gesture (decision 6) still stand, as does ADR 0007's eight-badge run and Region Hub (decision 1) and
its "enemies scale with progress, not with the player" rule (decision 6).

## Context

ADR 0007 gave growth a shape that held up in a Monte Carlo model: levels on a rising curve, stats as
a percentage of a species' real base stats, Health tripled, evolution at Lv 8 and 17, rewards scaled
by the level of what was beaten. It worked, and nobody could read it.

The numbers a player saw were the roster's real Pokémon stats run through three transformations. A
Charmander was 52/117/65. Whether that was good depended on knowing the roster's distribution;
whether a win had helped depended on comparing two four-digit numbers. Five constants
(`GrowthPercentPerLevel`, `FlatGainPerLevel`, `HealthMultiplier`, `BaseExpPerLevel`,
`ExpPerLevelIncrease`) stood between "I won a fight" and "my Pokémon is stronger", and every one of
them had been fitted rather than chosen — which is another way of saying none of them could be
explained to a player, or held in a designer's head while authoring content.

The request was the Super Auto Pets shape the game is modelled on in the first place: small integers,
a stat line you can read at a glance, and growth you can count on your fingers.

## Decision

**1. Every species sits in a tier, and every species in a tier spends the same points.**

Tier 1 has 5 points to spend across Attack, Health and Speed; each tier above has 10 more, up to
tier 6 at 55. A species' three stats are how it spends its tier's budget, and the tier is how strong
it is. `Data/SpeciesTier` owns the whole rule; `PokemonSpeciesDefinitionAsset.Tier` carries the
answer, and `ContentIntegrityTests` fails any species whose stats don't add up to its tier's total —
a tier whose members don't cost the same is not a tier.

**2. The tier table is derived from the roster sheet, not authored.**

`docs/pokemon_stats_unique.xlsx` keeps the real Pokémon stats and stays the source of truth. The
importer reads a species' real base-stat total, buckets it into a band
(`SpeciesTier.BaseStatTotalBands` — 165/205/265/295/325 against a roster running 75–350), and splits
that tier's points: Speed from the species' real Speed (2 at 80, 3 at 110, and never above what the
tier can afford), then whatever is left divided between Attack and Health in the ratio of the
species' own real Attack and Health.

The rule reproduces the two stat lines the design was specified against, exactly and without
tuning — Charmander 2/2/1, Charmeleon 7/6/2 — which is why it was kept rather than a hand-authored
tier column. `RosterImportTests` pins both.

**One hand-applied rule on top: an evolution always lands at least one tier above what it came
from.** Ten chains need it, all of them cases where the middle stage is genuinely the weaker Pokémon
(Metapod has worse real stats than the Caterpie it comes from; so do Kirlia, Kakuna, Togetic,
Dusclops, Lombre, Gloom). Left alone they would evolve into a *downgrade*, because a tier is exactly
what five EXP buys.

**3. EXP is a count of wins, and a point is +1 Attack and +1 Health.**

That is the entire growth model (`Data/StatGrowth`). A 2/2/1 Charmander wins a fight and is 3/3/1.
No curve, no percentage, no Health multiplier, no level — `LevelOf`, `ExpToReachLevel`,
`ExpForLevelUp` and `StatGrowth.AtLevel` are gone rather than left as synonyms for the EXP count.

**Speed never grows.** It changes only when a mon evolves into a species the tier table gives more
Speed to, which is rare on purpose. This settles the question ADR 0006 raised and ADR 0007 left
open: Speed drives the charge meter against a fixed threshold, so Speed that crept up with growth
would have every mon firing its passive every Step by mid-run.

**4. Five points is an evolution, and EXP is lifetime.**

`ExperienceResolver.ExpPerEvolution` is 5. What resets on an evolution is not the counter but the
growth applied on top of it: `Exp` is the lifetime total, `TimesEvolved` records how many evolutions
have each charged 5 against it, and `ExpSinceEvolution` is the difference — the EXP the *current*
species has grown on. A maxed 7/7/1 Charmander becomes a 7/6/2 Charmeleon and starts again from
there.

Because a tier is worth exactly one evolution cycle, evolving one tier up is stat-neutral at the
moment it lands and everything past one tier is a real jump. Magikarp (tier 1) becoming Gyarados
(tier 5) is four tiers of jump, which is how it should feel.

Evolution is no longer gated on `TimesEvolved` against a table of levels — the species chain alone
decides, so a curated base form whose real pre-evolution isn't in the roster (Pikachu) still evolves
on its first five points.

**5. EXP never caps.** A final form, and the branching lines the content layer leaves unresolved
(Eevee, Tyrogue, Nincada), keep gaining +1/+1 for every win. A Charizard is still worth fielding at
the eighth badge.

**6. Everything pays one point.** A wild win, a Gym, a Pokémon Center rest and a combine are all
worth exactly 1 (`BattleRewardResolver.ExpPerWin`). ADR 0007 scaled rewards by the foe's level to
keep a rising cost curve moving; with no curve to feed, a flat reward is what makes "five wins is an
evolution" a sentence a player can act on. What rises across a run is the opposition, not the payout.

A combine is therefore worth exactly one win, and the duplicate's own EXP is still not carried over
(ADR 0005 decision 6, unchanged).

**7. Enemies scale on two dials: tier and EXP.**

`Meta/RunProgression` pitches a Location from the badge count, never from the player's mons (ADR
0007 decision 6, unchanged):

| | Rule |
|---|---|
| Baseline EXP | 4 × badges |
| Wild encounter EXP | baseline − 1 + (map layer ÷ 2) |
| Gym Leader EXP | baseline + 2 |
| Pool tier cap | 1 + badges, lifted for the final Location |
| Wild encounter size | 1 in the first Location, 2 until the fourth badge, 3 after |
| Gym Leader team size | at least the player's line-up, and at least 2 + badges ÷ 2 (max 6) |

Tier is the step change — which species turn up at all — and EXP is the slope between steps. ADR
0007's single "level" had to be both at once, which is why it needed a base-stat-total cap bolted
alongside it; the cap is now simply the tier.

**8. Catch-up is in EXP.** `ApplyCatchUp` keeps every mon the run owns within 2 points of its
most-experienced (`CatchUpExpGap`), comparing lifetime EXP rather than power. A Caterpie caught late
gets the same EXP as the Charizard beside it and is still a Caterpie — levelling power instead would
have erased the tiers this ADR exists to create.

**9. The charge threshold drops from 100 to 3.**

Speed is now a number between 1 and 3, and a mon accrues its Speed in charge each Step, so
`BattleConfig.ChargeThreshold` reads directly as "Steps per trigger at Speed 1": Speed 1 fires on the
third Step, Speed 2 on the second, Speed 3 every Step. Against the old 100 a Speed-1 mon would have
needed a hundred-Step fight to use its passive once. The golden fixtures were rescaled from their
synthetic Speed 100 to 3 — behaviourally identical, since both mean "fires every Step" — and
`paralyzed-and-asleep-charge.json`'s expected charge moved from 50 to 1 for the same reason.

## Consequences

- **Fights are about one Step per mon, and stay that way.** Attack and Health grow in lockstep, so
  the ratio between them never changes: a 2/2/1 and a 40/40/1 both fall to an equal opponent in one
  exchange. ADR 0007 tripled Health specifically to escape this; the tier budget doesn't, and
  shouldn't, because the small readable numbers are the point. What it means in practice is that a
  Speed-1 mon gets roughly one passive off per fight and a Speed-2 mon two — Speed and passives are
  now what decide a fight, rather than a war of attrition. **This is the first knob to look at when
  this gets played** (PLAN.md §10), and the honest options if it doesn't feel right are a higher
  charge threshold with more Steps bought some other way, or a tier budget that spends more of
  itself on Health than the species' real ratio asks for.
- **A three-stage line is fully evolved by the third of eight badges.** At one EXP a win, five to an
  evolution, and about four wins a Location, both thresholds land early and the remaining five
  Locations are flat +1/+1. That is arithmetic, not a choice — there is no reward smaller than one
  point to slow it with. `RunProgressionTests.Evolutions_LandInTheSecondAndThirdLocations` pins it so
  a change is a decision rather than a surprise. ADR 0007 aimed for the third and sixth Locations;
  the trade for legibility is that the back half of a run has no evolutions left in it.
- **The numbers came from arithmetic, not from a model.** ADR 0007's constants were fitted by
  simulating whole runs; these were derived by counting (a Location pays about four points, so the
  opposition moves four points a badge). That is defensible because there is far less to fit, but it
  is a weaker guarantee than ADR 0007 had. Expect to retune; `RunProgressionTests` pins the shape,
  not the values.
- **The roster is visibly compressed.** 183 species now hold 6 distinct power levels and stat lines
  built from 5–55 points. Species that were far apart on paper can land in the same tier — a fully
  evolved Butterfree and a Venusaur are both tier 3. That is the cost of a tier system and was
  accepted going in.
- **Tier distribution is uneven**: 54 / 46 / 54 / 17 / 7 / 5. The top three tiers are thin because
  the top of the real roster is thin, and the tier-3 band is wide because it was widened to keep the
  three Kanto starters' final forms together (Charizard's real total is 262 against Venusaur's 242).
- **Speed is scarce**: 128 of 183 species are Speed 1, 43 are Speed 2 and 12 are Speed 3, and 71 of
  the 91 base forms are Speed 1. `RosterImportTests.SpeedStaysScarce_AcrossTheRoster` keeps it that
  way.
- **Character Select now offers tier 1** (54 species) rather than everything under a base-stat total
  of 180 (68 species). Every startable mon is a five-point mon with a whole run of growing to do.
- **The UI stopped saying "Lv".** The Team screen shows a mon's tier and how far it is from its next
  evolution ("T2 Evo in 3"), or its lifetime EXP once it has nowhere left to go; battle stat boxes
  name a mon with its tier; the Region Hub advertises a Location by the tier and EXP of what lives
  there; the Pokédex shows a species' tier.
- **`GrowthReport` reports stat lines, not level-ups.** With a point of EXP worth +1/+1, the new stat
  line *is* the news — "Grew: Charmander 3/3/1" rather than "Level up: Charmander Lv 8".
- **Unchanged:** Eevee, Tyrogue and Nincada still can't evolve; HP still doesn't carry between fights,
  and when it does, `Recompute` has to preserve damage taken rather than the HP value; the
  badge-as-relic, the Line-Up menu before a Gym, and the Trailblazer minigame are still unbuilt.
- Test baseline moves from 178/97 to **182 EditMode, 97 PlayMode**, all passing. The four new tests
  cover the tier derivation, Speed scarcity, per-species tier budgets, and that no evolution is a
  tier downgrade.
