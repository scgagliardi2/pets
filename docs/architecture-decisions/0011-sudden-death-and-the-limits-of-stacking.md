# ADR 0011: Sudden Death, and the Limits of Stacking

**Status:** Accepted
**Date:** 2026-09-13
**Amends:** `battle-sim-spec.md` §9 (battle end) and §12's rule that standing modifiers accumulate
without bound. The accumulation rule itself stands; two of the five modifiers are now bounded.

## Context

A fight could reach a state in which **neither side was able to lose**, and when it did the game
looked hung.

Standing modifiers accumulate across repeated triggers for the whole battle — that is spec §12, and
it is deliberate. But `Lifesteal` accumulates as a *percentage of damage dealt*, and nothing capped
it. Bulbasaur's Vine Drain grants 30% a trigger; at Speed 1 a passive triggers every third Step, so:

| Step | Lifesteal |
|------|-----------|
| 3    | 30%       |
| 6    | 60%       |
| 9    | 90%       |
| 12   | **120%**  |

From Step 12 a mon heals back at least as much as any blow takes off it. Two of them — an entirely
ordinary pairing, since Bulbasaur is a starter and appears in Forest encounter pools — each restore
exactly what the other removes, every Step, forever. Measured at the 200-Step cap the pair had
stacked to **1980%** and had traded 400 points of damage and 400 points of healing.

`DamageReductionFlat` had the same shape by a different route: it is a flat subtraction with no
floor, so once it outgrew the other Lead's Attack, `Max(0, attack - reduction)` was zero for the
rest of the battle.

The spec's safety caps did their job — the fight ended as a Draw at Step 200 — but that is not what
the player saw. What they saw was the HP bars freeze while the Step counter kept climbing, for 190
Steps, and then a result panel saying "Draw - Step limit". It was reported as *"the battle stopped
progressing after 18-20 steps"*, which is about where the sustain crosses parity.

Two measurements framed the fix. Across 1,200 generated wild and Gym fights and 1,500 dev random
battles, **no fight real content produces runs longer than 17 Steps** — and node fights come in at a
median of 5. So a fight still going at Step 30 is not a long fight; it is a broken one.

## Decision

**1. Sudden death, from Step 30.**

At the end of every Step from `BattleConfig.SuddenDeathStep`, both Leads take escalating true damage
— 1 on the first such Step, 2 on the next, and so on. It sits in beat 3.6, after status ticks and
before the faint check, and like a status tick it bypasses `DamageReductionFlat`, `Shield` and
`Lifesteal`. That exemption is the whole point: it isn't an attack, it's the clock running out, and
everything that could mitigate it is exactly what causes the fights it exists to end. Because the
escalation is unbounded it must eventually outrun any amount of stacked sustain.

Only the Leads are hit. The Supports are dormant (design doc §7), and clearing the whole board at
once would take the result out of the player's hands entirely.

**2. A connecting attack always takes at least 1 HP** (`MinimumAttackDamage`), however far
`DamageReductionFlat` has climbed. A mon with no Attack still deals nothing — the floor is on a blow
that connects, not a free hit for a mon that can't hit.

**3. Accumulated `Lifesteal` stops at 100%** (`MaxLifestealPercent`). Draining back more life than
the blow actually took is meaningless, so this is a definition rather than a balance number.

2 and 3 are narrowings of §12, not reversals: the modifiers still stack, they just stop at the point
where stacking further would stop meaning anything.

## Consequences

- **A fight always ends with a real result.** The Step and event caps stay, and still force a Draw,
  but they are a genuine last resort now rather than the thing that ended a stalled fight. A fixture
  that reaches them is a bug in the sim, not in the content.
- **Sudden death is invisible in normal play** and should stay that way. If a balance change ever
  makes ordinary fights run past 25 Steps, `SuddenDeathStep` needs raising in the same change —
  otherwise it stops being a backstop and starts being a mechanic, deciding fights that were still
  being fought.
- **One golden fixture moved by 1 HP.** `poison-stacking-and-damage-reduction.json` had a Step in
  which reduction fully nullified the incoming hit; it now takes the minimum 1.
  `sudden-death-breaks-a-stalemate.json` is the new fixture for the Vine Drain pair above.
- **Aron's Iron Hide and Dratini's Coil Up are slightly weaker**, and Vine Drain is capped. None of
  them were reachable in a real fight long enough to matter — see the 17-Step measurement — so this
  is a correctness bound, not a balance pass.
- **The stall was only visible through the UI**, and nothing tested the UI's path to the end of a
  fight except Skip. `BattleScenePlayModeTests` now drives a Step to completion; the gap that hid
  this was that the autoplay and Step paths to the result panel had no coverage at all.
- **Not addressed: the sustain passives are still unbalanced.** A 30%-a-trigger Lifesteal reaching
  100% within nine Steps is a strong effect that happens to be defused by fights being short. The
  passive numbers deserve their own pass; this ADR only stops them breaking the game.
