# ADR 0015: Team Type Synergies

**Status:** Accepted
**Date:** 2026-09-15
**Amends:** design doc §11 (thresholds → linear per-type scaling, and several type rules);
content-schema.md §6 (`TeamSynergyDefinition` isn't built); battle-sim-spec.md §8 (from a
"pre-battle stat pass" to an opening applied by the runners), §5 (status wards).

## Context

Type synergies were designed (§11) and never built. A table of eighteen team passives came in, one
per type, each scaling "+X per <type>-type" in the team: Steady Growth (Normal), Ember Burst
(Fire), Shell Guard (Water) and so on. The ask was to apply them and keep the effects small.

Three facts about the current build shaped it:

- **Stats are tiny.** A tier-1 mon is 8 points across three stats (Charmander 3/4/1), and Speed is
  1–3 against a charge threshold of 3. +1 Speed is the difference between one passive a fight and
  two, so the table's Speed synergies can't be granted per mon.
- **Charge-rate percentages don't work at Speed 1.** Accrual is `(int)(Speed × multiplier)`, so any
  slowdown below ×1 gives a Speed-1 mon no charge at all, which makes it asleep.
- **Several rules aren't stat modifiers.** Burst damage, an opening poison, a Lead sacrificing HP
  and a status ward all need to target the other side or raise events, so a pure pre-battle stat
  pass (the old spec §8) can't express them.

## Decision

- **One pass, `Simulation/TeamSynergy.Apply`, run by both runners as the battle opens** (Step 0),
  outside `AdvanceStep`. It's pure sim code, deterministic, and emits events: a `TypeSynergy` per
  type present, plus `Damage`/`StatusApplied`/`StatusBlocked`/`Faint`/`Promotion` for the openings.
  The on-demand runner applies it lazily in the first `NextStep` so the battle screen's opening
  render shows the teams as they arrived and the opening animates with Step 1.
- **Linear per-type counts, no thresholds.** The count is the whole line-up (dormant included), and
  a dual-type mon counts for both. Magnitudes are about one EXP point per mon of the type: +1
  Attack, +1 Health, 1 damage, 1 Shield. Speed is +1 per 2 Flying (capped at 3) or per 3 Bug
  (uncapped, since the table says Bug rewards stacking most).
- **Ice and Ground are a starting charge deficit**, not a rate multiplier (see Context). Charge may
  start negative. Electric and Psychic are the mirror image, a starting charge capped at the
  threshold.
- **Types ride on `BattleCombatant.Types`**, set by `Meta/BattleLineUp.Assemble` from the species
  library. `PokemonInstance` stays types-free, because a stored copy would go stale on evolution.
  Node fights (wild, Gym, Legendary) and the dev random battle both assemble this way. The dev
  battle keeps stripping passives — a separate switch, and not the same thing. It started without
  types and got them once it became clear it is the only fight reachable without walking a map: a
  feature that can't be seen on the quickest path into a fight is one nobody checks.
- **Rules as code, not assets.** content-schema §6's `TeamSynergyDefinition` would need a
  targeting vocabulary (front-to-back, back-to-front, the other side, sacrifice) for eighteen
  one-off rules. The numbers are named constants in one file instead. This departs from "content is
  data", and it's the thing to revisit if synergies grow tiers or something else reuses them.

## Consequences

- Both sides get synergies, so wild and Gym teams (which are type-biased by Location) get them too.
  A mono-type Gym is noticeably tougher than before, and stacking a Gym's weak type now pays off
  more than it did.
- The battle screen names them: a chip row per side over the field ("You"/"Foe", a badge and a
  count each), a Synergies button opening a panel with both sides' resolved numbers
  (`TeamSynergy.EffectAtCount`), and per-mon badges for the state a stat box can't show — shield,
  flat damage reduction, lifesteal, status ward, status, and charge owed to Permafrost or Sand Tomb.
  The card screens show a short per-type line (`TeamSynergy.Summary`) under a mon's type badges.
  The wording and the numbers both come from `TeamSynergy`, so a retune can't leave the UI lying.
- The Team screen's cards had no room to grow: its three sections already filled the content area,
  so the slot cell took 14 units and the card's sprite gave up 7 to pay for the new line.
- Fight length: the synergies are one-off or bounded modifiers, so sudden death needs no change.
  The balance of the numbers is unmeasured beyond the test suite.
- `shared/fixtures` gained an optional `types` per mon, and `type-synergy-opening.json` locks in
  the opening's order against shield and reduction.
