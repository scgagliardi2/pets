# ADR 0014: Road Events, Three More Held Items, and a Three-Row Pokémon Center

**Status:** Accepted
**Date:** 2026-09-15
**Amends:** design doc §5.1 (the Event node), §13 (items, the shop's stock), §20 (what Events pay);
ADR 0013's "items never run out" and its one-item Poké Mart grid; `RunProgression.AllowsLegendaries`'s
"Legendaries only on the final Gym", which an Event now bends.

## Context

The map's Event node ("Encounter") was an honest stub: a modal saying Event branches weren't
written. The Pokémon Center sold one item, the Muscle Band, from a 2×2 grid that the scene build
refused to grow past a second item. The request had three parts:
- four Slay the Spire–style encounters, Pokémon themed: fight a Legendary, gain money, and gain or
  lose Pokémon or items;
- three more held items;
- a Center laid out as three rows: 3 random Pokémon, the Poké Balls, and 3 random items.

## Decision

**Four encounters** (`Meta/RoadEvents`), rolled from the node's seed when the node is reached. Each is
a short scene with two or three choices. Every choice is a known trade with at most one coin flip:

| Encounter | Choices |
|---|---|
| **Legendary Sighting** | Challenge it: a real fight on `Battle.unity` against one Legendary at the Location's baseline EXP. A win pays the normal win plus **$15 and a random held item**; a loss costs 1 Morale like any fight. No catching. Or slip away: nothing. |
| **Game Corner** | Play the slots: **$5, 50% to win $20**. Or pocket the loose coins: **+$4**. |
| **Traveling Trader** | Trade the run's **least-grown mon** for a random non-Legendary base form **one tier up** at the **same EXP**. It takes the exact slot, so the line-up never empties, and the traded mon's item goes back to the bag. Or decline. |
| **Team Rocket Ambush** | Pay **half your money** (rounded up); hand over a **random item from the bag** (dead if the bag is empty); or grab their loot and run: **50% a random item, otherwise −1 Morale**. |

The Event overlay grew **three stacked choice buttons** over its Continue button. A choice shows its
outcome with Continue under it. Challenging the Legendary is the one choice that leaves the map: it
hands the fight over through `PendingBattle.SetLegendary`, whose `Bounty` also tells the Battle screen
what kind of fight it is. An encounter that takes the last Morale ends the run at Home, as a lost
fight does. PvP is still the stub, in the same overlay.

**The encounters are plain C#, not content assets.** "Content is data" is written about species,
passives and items. Four encounters don't justify an effect vocabulary and a ScriptableObject type,
and building one now would be the premature DSL CLAUDE.md warns against. Revisit once there are
enough encounters that adding one means copying a method rather than writing one.

**Three held items**, flat stats only, since that's all items support. Each is an asset under
`Content/Items`, registered in `ItemLibrary`, with an icon from `tools/generate_ui_sprites.py`:
- **Assault Vest:** +4 Health, $8.
- **Choice Band:** +5 Attack, −2 Health, $10. The first item with a negative modifier.
  `ExperienceResolver` now floors a held item's result at 1 Health and 0 Attack/Speed, so no item
  can send a mon into a fight already fainted.
- **Quick Claw:** +1 Speed, $10. It's the first way to gain Speed at all (design doc §20). Speed can
  now exceed the 3 the stat bars are drawn against. The sim doesn't care, and the bar just reads full.

**The Center is three full-width rows**, each with a sign on the left: three Pokémon (as before),
one card per ball tier, and **three items rolled from the library per visit**. Both card prefabs
became 360×140 so three fit a row. The Pokémon card nests the battle stats box at three-quarter
scale. The item offers use their own salt on the node's seed, so a bigger library never reshuffles
which Pokémon a shelf shows.

**Each item on offer sells once**, like the Pokémon, and gets a Sold stamp. Balls stay unlimited.
This reverses ADR 0013's "items never run out": with a rolled selection, one purchase each is what
makes the row a choice rather than a menu.

## Consequences

- **A Legendary can now turn up before the final Gym.** It's opt-in, never in the wild pool, and
  never catchable. At the first badge a tier 4–6 Legendary will usually beat a starting pair, and
  the encounter text says so rather than the Legendary being weakened. Whether to pitch it lower
  early is a tuning question.
- **Events pay something now, but not EXP.** Design doc §20's "what should Event nodes pay?" is
  partly answered. A path through an Event still earns a point less than one through a fight, except
  after a won Legendary fight, which pays EXP like any win.
- **Adding an item no longer needs a Center rebuild.** The item row is three rolled cards, not one
  card per library entry. It still needs registering in `ItemLibrary` (`ContentIntegrityTests`).
- **The encounter numbers are first guesses**: $5/$20 slots, a $15 bounty, 50% odds. They are
  untuned, like ADR 0013's prices.
- **Not built:** encounters as data, encounters biased by Location type, more than one roll per
  node, and Location-specific encounter pools.
