# Type passives

Two different things in this game are keyed to a Pokémon's type, and both get called a "type
passive" in conversation. This is the table for each, with the numbers as they stand today.

- **Team type synergies** — a per-type bonus applied to a whole side once, as a battle opens, scaled
  by how many mons in that line-up carry the type. Code constants in
  [`Simulation/TeamSynergy`](../client/Assets/Scripts/Simulation/TeamSynergy.cs); see
  [ADR 0015](architecture-decisions/0015-team-type-synergies.md) and `battle-sim-spec.md` §8.
- **Species passives** — a mon's own passive, fired by its charge meter during the fight. Content
  assets under `client/Assets/Content/Passives`; see `content-schema.md` §3. Only 17 species have a
  bespoke one; the other 155 share one placeholder per primary type, which is what makes these read
  as "the Water passive", "the Steel passive" and so on.

To change a synergy number, change the constant — every screen that quotes one reads it from there.
To change a species passive, edit its asset (the numbers below are the asset's, not the code's).

## 1. Team type synergies

Counted over the **whole line-up**, dormant mons included, once, as the fight opens — a mon that
faints later doesn't switch its side's synergy off. **A mon counts toward each of its types**, so a
Water/Ground mon adds one to both rows. Both sides get their own.

| Type | Name | Per mon of the type | At x3 | Scales with | Constant |
| --- | --- | --- | --- | --- | --- |
| Normal | Steady Growth | +1 Health to every mon | +3 Health each | count | `NormalHealthPerType` |
| Fire | Ember Burst | 1 damage to the foe's Lead as the fight opens | 3 damage | count | `FireDamagePerType` |
| Water | Shell Guard | +1 Shield to 1 more mon from the front | front 3 mons, +3 Shield each | count, **both ways** | `WaterShieldPerType` |
| Electric | Static Shock | +2 starting charge on the Lead | +6, capped to 3 | count, capped at the charge threshold | `ElectricLeadChargePerType` |
| Grass | Vine Drain | +10% Lifesteal to every mon | +30% | count, capped at 100% | `GrassLifestealPercentPerType` |
| Ice | Permafrost | -1 starting charge on the foe's Lead **and** Support | -3 each | count, uncapped (charge goes negative) | `IceChargePenaltyPerType` |
| Fighting | Power Surge | +1 Attack to every mon | +3 Attack each | count | `FightingAttackPerType` |
| Poison | Poison Sting | the foe's Lead opens Poisoned, 1 damage a tick | 3 a tick | count | `PoisonTickPerType` |
| Ground | Sand Tomb | -2 starting charge on the foe's Lead only | -6 | count, uncapped | `GroundLeadChargePenaltyPerType` |
| Flying | Tailwind | +1 Speed to every mon per **2** Flying | +1 Speed each | count / 2, Speed capped at 3 | `FlyingTypesPerSpeed`, `FlyingSpeedCap` |
| Psychic | Quick Focus | +1 starting charge on the Lead and Support | +3 each | count, capped at the charge threshold | `PsychicChargePerType` |
| Bug | Swarm Scurry | +1 Speed to every mon per **3** Bug | +1 Speed each | count / 3, uncapped | `BugTypesPerSpeed` |
| Rock | Stone Guard | +10% of the Lead's Health (at least +1) | +30% Health on the Lead | count | `RockLeadHealthPercentPerType` |
| Ghost | Curse | the Lead pays 2 HP; every other mon +1 Attack and +1 Health | Lead -6 HP, the rest +3/+3 | count | `GhostLeadHpCostPerType`, `GhostTeamBoostPerType` |
| Dragon | Intimidate | -1 Attack to every enemy mon | -3 Attack each | count, never below 1 | `DragonEnemyAttackPerType` |
| Dark | Night Ambush | 1 **true** damage to the foe's Lead as the fight opens | 3 true damage | count | `DarkDamagePerType` |
| Steel | Iron Hide | the Lead blocks 1 damage a hit | blocks 3 a hit | count, a hit still takes ≥1 HP | `SteelLeadReductionPerType` |
| Fairy | Moonlight Glow | 1 more mon from the **back** blocks one status | the back 3 each block one | count | `FairyWardedMonsPerType` |

Notes that aren't in the table:

- **Water scales twice.** The count decides both how many mons from the front are shielded *and*
  how much each gets, so it is the steepest curve here: x1 is one mon with 1, x4 is four mons with 4.
- **Fire is an attack, Dark is not.** Ember Burst goes through DamageReduction and Shield (and so
  can be eaten by the foe's own Shell Guard); Night Ambush ignores both. Neither heals through
  Lifesteal — the opening isn't the Lead's blow.
- **Ghost is skipped when the Lead has nobody to give to**, so a one-mon line-up doesn't pay the HP.
- **Charge penalties can go below zero.** That's the point: the mon accrues as normal but starts in
  debt, so its first passive is further off than its Speed suggests. A deficit rather than a rate
  cut, because charge accrues as an int of Speed and at Speed 1 any percentage slowdown truncates
  to nothing at all.
- Order of application is fixed (own stats → Intimidate → defences → charge → openings → faint
  check), so results are deterministic and an opening that KOs a Lead promotes before Step 1.

### On screen

**The opening is already applied when the board is first drawn** (ADR 0016), before a Step is taken:
the numbers above are small and the first exchange spends the defensive ones, so a board drawn only
after the Step returned never showed them. Both sides' live synergies are also named in a chip row over the field, and the
Synergies button opens a panel spelling out the resolved numbers. What a synergy *put on a mon* shows on the mon: a
[shield bubble](../client/Assets/Scripts/UI/ShieldBubbleView.cs) around it with the amount it will
absorb, and badges above it for blocked damage, Lifesteal, a status ward, a status and charge owed
([`EffectBadgeRowView`](../client/Assets/Scripts/UI/EffectBadgeRowView.cs)). Attack, Health and
Speed bonuses need no badge — they move the numbers in the stat box.

## 2. Species passives

Fired by the mon's own charge meter: it accrues its Speed each Step and triggers at
`BattleConfig.ChargeThreshold` (3), so **Speed 3 fires every Step, Speed 2 every other, Speed 1 every
third** — Paralysis halves accrual, and an Ice/Ground synergy delays the first trigger. Effects apply
in list order to the target named below, and all standing modifiers (Attack, Speed, DamageReduction,
Lifesteal) stack for the rest of the battle.

Magnitude scales by **evolution stage**, through the asset's `MagnitudeByStage` table — index 0 is
the base form, index 1 the first evolution, and so on. Every passive in the game today is a
single-entry `[1]`, so **nothing scales with stage yet**; a passive is worth the same at Blastoise as
at Squirtle.

| Passive | Type flavour | Effect | Target | Importer default for |
| --- | --- | --- | --- | --- |
| Adaptability | Normal | +3 Attack | self | primary type Normal |
| Ember Burst | Fire | Burned, 3 damage a tick | foe's Lead | primary type Fire |
| Shell Guard | Water | +6 Shield | self | primary type Water |
| Static Shock | Electric | Paralyzed, then +5 Speed | foe's Lead, self | primary type Electric |
| Vine Drain | Grass | +30% Lifesteal | self | primary type Grass |
| Permafrost | Ice | Asleep | foe's Lead | primary type Ice |
| Power Surge | Fighting | +4 Attack | self | primary type Fighting |
| Bite | Poison | 3 damage | foe's Lead | primary type Poison |
| Sand Tomb | Ground | 4 damage | foe's Lead | primary type Ground |
| Tailwind | Flying | +2 Attack | self | primary type Flying |
| Quick Focus | Psychic | +20% charge rate | self | primary type Psychic |
| Swarm Scurry | Bug | +5 Speed | self | primary type Bug |
| Stone Guard | Rock | +8 Shield | self | primary type Rock |
| Cursed Touch | Ghost | +4% Lifesteal | foe's Lead | primary type Ghost |
| Coil Up | Dragon | +3 DamageReduction | self | primary type Dragon |
| Night Ambush | Dark | 3 damage, then +2 Attack | foe's Lead, self | primary type Dark |
| Iron Hide | Steel | +4 DamageReduction | self | primary type Steel |
| Moonlight Glow | Fairy | Heal 6 | self | primary type Fairy |
| Spore Cloud | Grass | Poisoned, 2 damage a tick | foe's Lead | — (Oddish's own) |
| Poison Sting | Bug | Poisoned, 1 damage a tick | foe's Lead | — (Weedle's own) |

"Importer default for" is the importer's table
([`SpeciesRosterImporter.DefaultPassiveIdByType`](../client/Assets/Editor/SpeciesRosterImporter.cs)):
a newly imported species gets the passive for its **primary** type, and hand-authored assignments on
the original curated 28 are left alone — which is why Oddish keeps Spore Cloud rather than taking
Grass's Vine Drain, and Weedle keeps Poison Sting rather than Bug's Swarm Scurry. Bespoke passives for the remaining 155 species are Phase 2
content work (PLAN.md §11).

One row does not match its own description, and is flagged here rather than quietly fixed:
**Cursed Touch** grants its Lifesteal to the *enemy* Lead (`Lifesteal 4 -> EnemyLead`), while the
asset's description says it drains the enemy Lead to heal itself — which would be `-> Self`. As
written, a Ghost's passive hands the foe 4% Lifesteal. Worth deciding before Phase 2's content pass —
it is a balance change to 20-odd species, so it wants its own change rather than a drive-by edit.

A smaller oddity, deliberate as far as anyone knows: Poison Sting is Bug-flavoured, while the
importer's default for a Poison primary is Bite.
