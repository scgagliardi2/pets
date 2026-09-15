# ADR 0013: The Pokémon Center Is a Shop

**Status:** Accepted
**Date:** 2026-09-14
**Amends:** design doc §5.1 (the Camp node's effect), §12.2 (adoption), §13 (economy, shop, items);
ADR 0003's resolution of the Camp node as an EXP + buff overlay; ADR 0012's starting ball stock,
which stops being the only way to get balls.

## Context

The map's Pokémon Center node (`NodeType.Camp`) resolved as a modal over the map. It paid every
mon in the line-up one point of EXP and set a +20% Attack buff for the next fight. That was design
doc §5.1's Camp effect, borrowed because the Center's own mechanics (adoption, healing) weren't
built (ADR 0003).

Around it:
- **Money** was on `RunState` and in the title bar, but nothing ever paid any.
- **Balls** came only from ADR 0012's fixed starting stock (5 Poké, 2 Great, 1 Ultra). ADR 0012
  called that a stand-in until there was somewhere to buy them.
- **Items** were a schema entry (content-schema.md §7) with no implementation.

The request was to turn the Center into a shop scene selling items, Poké Balls, and Pokémon matched
to the player's current team. It also asked to let the Team screen put items on mons, and to remove
the Center's stat-giving.

## Decision

**The Center is a scene, not an overlay.** `PokemonCenter.unity` is reached by walking onto the
node. Leave goes straight back to the map, onto the same node (`SceneNavigator.ReturnToMap`), and
Team goes to the Team screen and back. It's built from prefabs (`ShopItemCard`, and `ShopPokemonCard`
nesting `BattleStatsBox`, plus the Button prefab) on two shelves under an awning.

**It pays no EXP and sets no buff.** `CampResolver`, `CampPanelController`, `CampOverlay.prefab`
and `RunState.NextBattleAttackBonusPercent` are deleted.

**What it sells** (`Meta/PokemonCenterShop`):
- Balls of every tier, into the same `BallInventory` the battle screen's tray throws from: Poké $2,
  Great $5, Ultra $9. Unlimited.
- Every item in `ItemLibrary`, at the item's own price. Unlimited.
- Three Pokémon, $10 each, one adoption apiece, sent to the Box (where a catch lands too).

**The Pokémon are matched to the party** in both terms a mon's strength is made of:
- *Tier* is the species' stat total (ADR 0008). Offers are drawn from the tiers the line-up's mons
  *started* at, since stats grow off the base form (ADR 0009).
- *EXP*: offers carry the line-up's average.

Like the wild pool, offers are base forms and never Legendary. If too few species fit the tier
range it widens a tier at a time on both sides. The shelf is rolled from the node's seed when the
node is reached, then kept on `RunState.CenterStock`, so stepping out to Team doesn't reroll it. It
is dropped on travel and on a badge.

**Money is earned per win**: $3 a wild win and $10 a Gym (`BattleRewardResolver.GrantWinMoney`),
from a $5 start. That puts roughly $11 in hand at a first-Location Center: enough for a Pokémon, or
the item and a ball, but not both.

**Items are held, one per mon.** The only item is the Muscle Band (+3 Attack, $8), a
ScriptableObject in `Content/Items` registered in `ItemLibrary`.

The trap was the derived-stats rule: `CurrentStats` is recomputed from species + EXP on every
grant, so a +3 written onto it would vanish. Instead, equipping bakes the item's modifiers onto
`PokemonInstance.HeldItemStats`, the way `ResolvedPassive` is baked, and
`ExperienceResolver.Recompute` adds them on top. So the battle sim needed no change: the bonus is
already in the stats a combatant is copied from.

`Meta/HeldItems` owns every move (equip, unequip, hand over), and an item is always in exactly one
place: `RunState.Items`, the bag, or one mon. Releasing a mon, or consuming it in a combine, returns
its item to the bag.

**Team gains a Bag row.** You drag a chip onto a card to equip it, or drag the card's gold item
badge onto another mon to hand it over (trading if that mon holds one). Dragging the badge back to
the Bag takes it off. The badge is its own drag handle, so pressing it drags the item and pressing
the rest of the card still drags the mon. The slot and release-zone drop handlers check for an item
first.

## Consequences

- **A Center stop costs a fight's EXP.** The Center took a map layer's node and used to pay the
  same point a fight did. Now it pays nothing, so a player who shops is about half a point a
  Location behind the opposition's pace (`RunProgression.ExpPerBadge`) and buys something instead.
  `RunProgressionTests` now models a fight on every layer. Whether an item or a Pokémon is worth
  that point is a tuning question (design doc §20).
- **The starting ball stock now overlaps the shop.** ADR 0012's 5/2/1 is still granted at run start.
  The battle screen also grants it again whenever a wild fight starts with an empty inventory. Both
  are left as they were, but the refill means a player who throws until they're out never needs to
  buy a ball. Shrinking the stock, and dropping the refill, is the obvious next step. That's a
  balance call for catching's owner.
- **Adding an item is content plus a scene rebuild.** Create the asset and register it in
  `ItemLibrary` (`ContentIntegrityTests` fails otherwise), then rebuild the Pokémon Center scene.
  Its supply cards are fixed at build time, one per ball tier and one per item, in a 2×2 grid. That
  holds the three tiers and one item; the scene build throws, rather than overlapping the footer,
  once there's a second.
- **Not built:** item passive overrides and locking (content-schema.md §7), more than one slot, the
  Center healing (HP still doesn't persist, ADR 0003), and Location-type bias on adoptions (§12.2's
  proposal; party-matching was asked for instead).
- `NodeType.Camp` keeps its name. It's only the enum for the Center node now.
