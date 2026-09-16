# ADR 0016: The opening is on the board before Step 1, and a shield is a bubble

**Status:** Accepted
**Date:** 2026-09-15
**Amends:** battle-sim-spec.md §8 (the on-demand runner's opening is taken by the screen as it
opens, not inside the first `NextStep`).

## Context

ADR 0015 applies each side's team type synergies once, as the battle opens, and §8 deliberately had
the on-demand runner do it "at the start of its first `NextStep`" so that a screen drawing the
line-ups before the fight would show the teams *as they arrived*, unmodified.

That reasoning held for the stat bonuses — a mon that starts with +2 Health simply has a longer bar
— but it hid everything else, and it hid Shell Guard completely:

- The opening's defences are 1–3 points. One Water mon in a party raises **one** point of Shield.
- Synergies and Step 1's exchange were applied in the same call, and the screen drew only once it
  returned. So a 1-point shield was raised and spent between two frames. **There was no board state
  in which it existed** — not a timing problem, an absence.
- The same went for an opening poisoning, the opening damage, and the charge debt Permafrost and
  Sand Tomb leave: all of them arrived already resolved, or already spent.

A shield had a second problem independent of timing: nothing on the battlefield drew it. Attack,
Health and Speed move the numbers in a stat box and a status is written on the mon, but an absorb
pool changed nothing that was drawn, so the first the player knew of a shield was a hit landing for
less than the HP bar said it should.

## Decision

- **`OnDemandStepRunner.ApplyOpening()`** applies the opening if it hasn't been applied and returns
  its events; `OpeningApplied` reports whether it has. `NextStep` runs through the same guard, so a
  caller that takes the opening first gets an identical fight — same events, same order, same
  outcome (`TeamSynergyTests.TakingTheOpeningSeparately_PlaysOutTheSameFight`). The opening draws no
  RNG, so taking it earlier doesn't shift the Steps that follow.
- **The battle screen takes it in `Start`**, before the first draw. The resting board — the one the
  player looks at before pressing anything — already carries its shields, charge, lifesteal, wards
  and opening blows. An autoplaying fight holds that board for `OpeningHoldSeconds` (0.9s) before
  taking Step 1, so autoplay doesn't wipe a shield off before it has been read.
- **A shield is drawn as a bubble around the mon** (`UI/ShieldBubbleView`), sized to the mon's own
  sprite, with the amount it will absorb on a small dark chip at its upper-left rim. It is read off
  `BattleCombatant.Shield` every redraw, so it drains hit by hit and pops when the pool is spent,
  and it is drawn for *any* shield — a Shell Guard or Stone Guard passive fills the same pool
  mid-fight. The Shield badge left `EffectBadgeRowView` in the same change: two things saying the
  same number is worse than one saying it well.

## Consequences

- §8's original intent is reversed on purpose: the board before Step 1 is **not** the teams as they
  arrived, it is the teams as the fight starts. The chip rows over the field already name the
  synergies that did it, and the Synergies panel spells out the numbers.
- A fight can now be over before a Step is taken (a Dark opening on a lone mon at 1 HP), so the
  screen checks `IsBattleOver` after applying and shows the result panel.
- Tests that asserted the pre-Step board had to be retold: it now carries the synergy's Health, and
  the badges are up at load rather than after the first Step. `StepButton_DrainsBothLeadsHp_…`,
  failing since `583f8bd`, was fixed in the same change — it asserted one frame after the click
  (the lunge is drawn first) against HP numbers that predate synergies.
- Untouched: the precomputed runner, which already applied the opening before its loop, and the
  golden fixtures, which are unchanged.
