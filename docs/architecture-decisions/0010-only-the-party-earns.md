# ADR 0010: Only the Party Earns, and a Win Says What It Bought

**Status:** Accepted
**Date:** 2026-09-13
**Amends:** ADR 0008 decision 8 (catch-up), and the half of ADR 0007's catch-up rule it carried
forward. Everything else about growth — a lifetime EXP count, one point a win, a point buying
Attack *or* Health, twelve points to an evolution — is ADR 0008 + ADR 0009 and is unchanged.

## Context

`ApplyCatchUp` raised **every mon the run owned**, line-up and Box alike, to within
`CatchUpExpGap` of the most-experienced. It ran after every win (`BattleRewardResolver`) and every
rest (`CampResolver`), which meant that in practice a mon in the Box earned the same EXP as a mon
that fought — a fight or two behind, and no further.

Three things follow from that, and all three are bad:

1. **Who you field doesn't matter.** A six-mon party and a one-mon party grow the same roster at the
   same rate. The line-up is supposed to be the run's central recurring decision (design doc §7);
   the growth model quietly refunded it.
2. **Catching is free.** A mon caught at the first badge and never used is, at the eighth, within two
   points of the Lead that won all eight — so there is no cost to hoarding and no reason to commit.
3. **The screen was lying about it.** The win panel said "the team gains 1 EXP" while silently
   paying mons that weren't there, and — separately — the panel it said it on could only physically
   show one line (below).

The second half of this ADR is that the result of a win had become unreadable. ADR 0009 made a point
of EXP buy Attack *or* Health on a draw the player doesn't control, so **which stat it bought is the
only part of a win that isn't known before the fight starts** — and it was the part nobody could
see. The Battle scene's result panel was 460x220 with a single 48pt `Text` in a 100px box: Unity's
`Text` truncates what doesn't fit its rect with no warning, so "Victory!" rendered and every reward
line appended after it was silently dropped.

## Decision

**1. Catch-up raises the line-up only.**

`ExperienceResolver.ApplyCatchUp` iterates `state.LineUp` and stops there. A Box mon keeps the EXP it
had when it was benched. The floor itself (`CatchUpExp`) is still measured across the Box as well as
the line-up — benching the run's best mon shouldn't lower the bar a newcomer joins at, and that same
number is what `CatchResolver.Catch` starts a caught mon on.

The fairness rule ADR 0008 wanted survives where it was actually needed: **a mon moved into the party
is brought back within two points by the next grant.** Nothing in the Box is ruined by having been
left there — it just doesn't grow while it sits.

**2. A won fight lists what each party mon gained.**

`GrowthReport.GainLines()` returns one line per mon — `Charmander  3/4/1 → 3/5/1  (+1 Health)` —
followed by one line per evolution. The Battle scene's result panel grew a second `Text`
(`RewardText`, body-sized, under the headline) and the panel grew to 560x420 to hold it; the
headline `Text` now carries the headline and nothing else. The compact one-line
`GrowthReport.Describe()` stays, for the Pokémon Center overlay, which has two lines to spare.

**3. Team is reachable from the map.**

A Team button in the Location Map's title bar, beside Menu. "Can this line-up take that node" is a
question asked between two nodes, and it was two screens away from where it's asked. Team's Back
button returns to whichever screen opened it (`SceneNavigator.GoToTeam` /`ReturnFromTeam`, the same
shape Settings already used), so checking the party mid-walk doesn't cost a detour through the menu
to get back onto the map.

Decisions 2 and 3 are in the same ADR as 1 deliberately: the party choice only becomes a decision
once it costs something (1), and it can only be made once the player can both see the party (3) and
see what fielding it earned (2).

## Consequences

- **Benching is now a real cost**, which is the point. A run that swaps its line-up around every
  Location will have a flatter, broader roster than one that commits — and the committed one will be
  ahead where it counts.
- **The Box is a graveyard until the Team screen is used.** There is no in-run way to train a mon
  except to field it, which is correct for now but leans on decision 3 to be usable at all.
- **A caught mon is still immediately usable** — `CatchResolver` starts it at the run's catch-up EXP,
  unchanged. What's gone is it continuing to earn afterwards from storage.
- **The Pokémon Center pays the line-up only**, on the same rule. It always did grant to the line-up;
  what changed is that its catch-up no longer spills into the Box.
- **The result panel is much taller** (560x420, offset lower so the catch row still clears the top of
  the screen). A six-mon party plus an evolution is eight lines; beyond that Unity truncates, silently
  as ever. If line-ups get bigger or rewards get wordier this needs a scroll view, not a bigger panel.
- **Not addressed: the Team screen doesn't say how far behind a Box mon is.** A player can read two
  stat lines and compare, but there's no "this mon is 9 points behind your Lead" readout, which is
  exactly the thing decision 1 makes worth knowing.
