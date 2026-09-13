# ADR 0009: A Point of EXP Buys One Stat, Not Two, and an Evolution Takes Most of a Run

**Status:** Accepted
**Date:** 2026-09-13
**Supersedes:** ADR 0008 decisions 3, 4 and the parts of 1 and 2 that set the tier budget (growth of
+1 Attack *and* +1 Health, five points to an evolution, an evolution rebasing onto the new species'
tier line, and the 5-point tier-1 budget). ADR 0008's structure stands: species still sit in six
tiers derived from the roster sheet, EXP is still a lifetime count of wins at one point each, and
stats are still derived rather than accumulated.

## Context

ADR 0008 landed the tier model and immediately showed two things it had got wrong, both called out
in its own Consequences section:

1. **Fights were decided in a single exchange, for the whole run.** A point of EXP added +1 to
   Attack *and* +1 to Health, so a mon's Attack and Health stayed locked together forever. Two even
   mons always killed each other on the first Step, whatever their EXP, which left nothing for a
   passive or a Speed advantage to decide.
2. **A three-stage line was fully evolved by the third of eight badges.** Five points to an
   evolution against about four points a Location meant both thresholds landed in the first third of
   a run, and the remaining five Locations had no shape changes left in them.

The tier-1 budget of five points was also simply too tight to express anything: with Speed taking
one, a species had four points to split, so most of tier 1 was some rounding of 2/2.

## Decision

**1. A point of EXP buys +1 Attack *or* +1 Health, never both.**

Which one is a draw against the species' own `HealthGrowthPercent` (`Data/StatGrowth`). This is what
breaks the lockstep: a Metapod banks 86% of its points into Health and pulls away from its own
Attack, a Charmander 72%, so fights lengthen as a run goes on instead of staying at one Step.

**2. Every species carries a growth value, derived from the sheet like everything else.**

`SpeciesTier.HealthGrowthPercentFor` maps a species' real Health share of Health+Attack onto the half
of the range above a 50% floor, so every mon favours Health at least evenly and the ordering is the
species' real bulk. The floor is the point: a mon that spent its growth on Attack would walk straight
back into problem 1.

Flooring the raw share at 50 was the obvious reading and was rejected on the data — the roster's real
Health share runs 1%–72% with a median of 47%, so a hard floor would have pinned 135 of the 183
species to exactly 50 and the value would have said nothing about most of them. Rescaled, it spans
51%–86% over 22 distinct values.

**Shedinja is the one exception and never gains Health at all** (0%), matching the single hit point
the real games give it. Expressed as "a species whose real Health is 1" rather than as a name, so it
stays a property of the sheet.

**3. The draw is deterministic, and keyed to the mon.**

A true dice roll per win can't be stored — stats are rebuilt from scratch on every EXP grant (ADR
0005), and always will be. So the draw is a pure hash of the mon's instance id and which point of EXP
it is. It behaves like luck (a 70% mon really can take Attack three times running; two Charmanders in
the same party grow into different stat lines) while a mon's whole history stays rebuildable from its
EXP count alone.

**4. An evolution costs twelve points, and gives a flat +3 Attack and +3 Health.**

`ExperienceResolver.ExpPerEvolution` is 12, against about four points a Location — so a three-stage
line evolves in the third Location and reaches its final form in the sixth, which is the mainline
rhythm and what ADR 0007 originally aimed at.

**The species evolved into contributes nothing to a mon's stats.** A mon grows from the tier line of
the species it *started* as for its whole life, plus a flat bonus per evolution. A Charizard is a
Charmander with EXP and two evolutions behind it. `ExperienceResolver.BaseFormOf` is what finds that
starting species, and `CreateAtExp` now builds every mon from the root of its chain and evolves it
forward — so a Charmeleon that was caught and one that was raised are the same mon, which they were
not when the evolved form's own stats were the basis.

The visible cost is that an evolved species' tier line is now **only a Pokédex entry**. Nothing in a
run reads Charizard's 12/14/2. That is a real loss of meaning in the content, accepted because the
alternative — a mon's stats jumping to whatever the new species happened to be worth — is exactly the
rebasing this decision removes.

**5. Tier budgets go up, and Health always beats Attack.**

Tier 1 spends 8 points rather than 5, with the same +10 a tier (8/18/28/38/48/58). Within a tier, the
Attack:Health split is now capped so **Health is strictly greater than Attack for every species in
the roster** — a mon that starts able to kill its own mirror in one exchange leaves nothing for the
rest of the systems to decide. A species' real ratio still sets how much larger.

Charmander comes out at 3/4/1 and Magikarp at 2/5/1.

**6. Speed doesn't change on evolution, and so doesn't change at all.**

It follows from decision 4: if the new species' stats are ignored, that includes its Speed. A mon's
Speed is the one its base form was drawn with, for the whole run.

## Consequences

- **Speed 2 and Speed 3 are currently unreachable in play.** Wild and Gym teams are drawn from base
  forms and grown under the same rules, so only the 20 base forms that are already fast field
  anything above Speed 1 — Alakazam's 3, Raichu's 3 and the rest exist only in the Pokédex. **This is
  a known gap with a planned fix: Speed is to come from somewhere else** (an item, a Location reward,
  a passive), which is its own piece of work. Until it exists, the charge meter is close to a fixed
  three-Step cadence for almost every mon.
- **Fights should lengthen across a run, which is the point of decision 1** — but by how much is
  untested in play. A 50% grower stays close to its mirror; an 86% grower pulls away fast. This is
  now the interesting balance question rather than the flat one ADR 0008 left.
- **A tier's line and a mon's stats diverge immediately.** After twelve points a Charmander is
  somewhere around 6/13/1 while the Charmeleon asset says 7/9/2. Any screen that reports a species'
  tier line is describing where a mon *starts*, not what one is worth now.
- **Growth is per mon, so duplicates are no longer interchangeable.** Two Charmanders caught in the
  same run will have different Attack and Health by mid-run. That makes which one you combine *into*
  an actual decision, and it makes the Team screen's stat lines worth reading.
- **The combine dialog had to start quoting the outcome**, because "+1 EXP" no longer tells a player
  what they get — the point could go either way, and it could be the one that evolves the mon.
- **`StatGrowth.AtExp` needs the mon, not just the species** (instance id, lifetime EXP, evolutions
  behind it). That is a wider signature than a pure species lookup, and it is what makes the draw
  reproducible; a caller that has only a species asset can't ask the question any more.
- **Pacing is unchanged in shape**: `RunProgression` still moves the opposition four points a badge,
  and `RunProgressionTests` now pins the third-and-sixth-Location evolution rhythm instead of the
  second-and-third.
- **Character Select still offers tier 1**, now 8-point mons rather than 5-point ones.
- **Unchanged:** Eevee, Tyrogue and Nincada still can't evolve; everything still pays exactly one
  point; catch-up is still within 2 points of the most-experienced mon; HP still doesn't carry
  between fights.
