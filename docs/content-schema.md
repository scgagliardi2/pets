# Content Schema

Status: **Phase 1, in progress.** Documents the data shape for creatures/abilities/tiers/bot
rosters (PLAN.md §2.3), kept in sync with the actual Unity ScriptableObject fields
(`client/Assets/Scripts/Data`) and their JSON export format
(`client/Assets/Scripts/Data/ContentJsonExporter.cs`).

## 1. Design principle

Abilities are **composed from a fixed trigger + effect vocabulary**, not one C# class per
creature (CLAUDE.md hard rule). The vocabulary is deliberately non-polymorphic — every effect is
the same `EffectData` shape (`type`, `target`, `amount`, optional `summonTemplateId`) — so a new
creature is authored entirely as data (ScriptableObject fields in-editor), and the same shape
round-trips through `JsonUtility` without needing a discriminated-union/polymorphic JSON
converter. If a new creature idea genuinely can't be expressed by the existing trigger/effect
vocabulary, that's a signal to add a new `EffectType`/`TriggerType` value — not to special-case a
creature in code.

## 2. `AbilityDefinition` (ScriptableObject)

| Field | Type | Notes |
|---|---|---|
| `trigger` | `TriggerType` enum | See battle-sim-spec.md §3 for which triggers are live. |
| `effects` | `EffectData[]` | Applied in list order when the trigger fires. |

`EffectData` (plain `[Serializable]` struct, shared with `Simulation.AbilityData`):

| Field | Type | Notes |
|---|---|---|
| `type` | `EffectType` enum | `DealDamage`, `Heal`, `BuffAttack`, `BuffHealth`, `Summon`. |
| `target` | `TargetSelector` enum | `Self`, `RandomAlly`, `RandomEnemy`, `FrontEnemy`. Ignored for `Summon`. |
| `amount` | `int` | Damage/heal/buff magnitude. Ignored for `Summon`. |
| `summonTemplateId` | `string` | Only for `Summon` — id of a `CreatureDefinition` used as a fixed stat template (not drawn from the shop pool). |

An `EffectData.summonTemplateId` above is what appears in **JSON export** and the **Simulation
runtime shape** (§6) — a plain string, since `Simulation` cannot reference Unity objects. In the
**editor-authoring** `AbilityDefinition` ScriptableObject itself, `effects` is actually
`EffectDefinition[]` (not `EffectData[]` directly): same `type`/`target`/`amount` fields, but
`summonTemplateId` is instead `summonTemplate: CreatureDefinition` — a real object reference, so
an artist drags a creature asset in the Inspector instead of retyping its id. `Data/TeamStateConverter.cs`
is what turns an `EffectDefinition` into the string/resolved-stats `EffectData` the simulator
consumes, reading `summonTemplate.baseAttack`/`baseHealth`/`id`/`displayName` at conversion time —
so there's exactly one source of truth (the referenced `CreatureDefinition` asset) and no
hand-copied duplicate stats to drift out of sync.

An `AbilityDefinition` asset is reusable — multiple `CreatureDefinition`s can reference the same
one (e.g. a shared "deal 2 to a random enemy on faint" asset).

## 3. `CreatureDefinition` (ScriptableObject)

| Field | Type | Notes |
|---|---|---|
| `id` | `string` | Stable identifier, used in JSON export and bot roster/summon references. |
| `displayName` | `string` | Original name — no Super Auto Pets names (CLAUDE.md hard rule). |
| `tier` | `int` | Shop tier this creature belongs to (1+). Not yet consumed by a shop system. |
| `baseAttack` / `baseHealth` | `int` | Stats at level 1. |
| `levelAttackBonus` / `levelHealthBonus` | `int` | Added per level above 1 — see battle-sim-spec.md §2 for the formula. |
| `placeholderColor` | `Color` | Placeholder-art marker per PLAN.md §8; swapped for real art in Phase 5. |
| `abilities` | `AbilityDefinition[]` | Usually 0 or 1 entry for the starter roster; the shape allows more. |

## 4. `BotTeamDefinition` (ScriptableObject)

The scripted-opponent snapshot format — intentionally the same shape that will later serialize a
real player's team for async PvP (PLAN.md §2.2), so this isn't throwaway.

| Field | Type | Notes |
|---|---|---|
| `round` | `int` | Which round of the bot roster this team is fought on. |
| `slots` | `BotSlot[]` | Ordered front-to-back, max 5. |

`BotSlot`:

| Field | Type | Notes |
|---|---|---|
| `creature` | `CreatureDefinition` reference | |
| `level` | `int` | 1-3. |

A **bot roster** for the starter content is just an ordered set of `BotTeamDefinition` assets, one
per round, living under `Assets/Content/BotRoster/`.

## 5. JSON export shape

`ContentJsonExporter` (`JsonUtility`-based — no Newtonsoft dependency, since the model above is
deliberately non-polymorphic) exports a `CreatureDefinition` as:

```json
{
  "id": "pebblehide",
  "displayName": "Pebblehide",
  "tier": 1,
  "baseAttack": 2,
  "baseHealth": 3,
  "levelAttackBonus": 1,
  "levelHealthBonus": 1,
  "abilities": [
    {
      "trigger": "OnFaint",
      "effects": [
        { "type": "DealDamage", "target": "RandomEnemy", "amount": 1, "summonTemplateId": "" }
      ]
    }
  ]
}
```

And a `BotTeamDefinition` as:

```json
{
  "round": 1,
  "slots": [
    { "creatureId": "pebblehide", "level": 1 },
    { "creatureId": "sparklet", "level": 1 }
  ]
}
```

This export exists from Phase 1 on (unused until Phase 3+ per PLAN.md §8) so the future backend
and `/shared/fixtures` have a real schema to target instead of a guessed one.

## 6. Simulation runtime shapes (not authoring shapes)

`Simulation.CreatureState` / `Simulation.TeamState` are the plain-C# runtime equivalents the
simulator actually operates on — produced from the above by
`Data/TeamStateConverter.cs`, never authored directly. See battle-sim-spec.md for their
semantics; they mirror the fields above minus anything editor-only (`displayName` is kept for
debugging/log readability, `placeholderColor` is dropped — `Simulation` has zero rendering
concerns). `Simulation.EffectData`'s `SummonTemplate` field is a small resolved
`Simulation.CreatureTemplate` (id, display name, attack, health) rather than an id string —
resolved once by the converter, so the simulator never needs to look anything up mid-battle.

## 7. Runtime registries and shop config

Two more ScriptableObjects exist purely so gameplay code can find content at runtime — Editor
`AssetDatabase` lookups (as used by the Editor-only content smoke tests) don't work in a built
player:

- `CreatureLibrary` — a flat `AllCreatures: CreatureDefinition[]`, plus `GetByMaxTier(tier)` (used
  to build the shop pool; a creature at `Tier <= 0`, like the `burr-token` summon template, is
  never shop-eligible) and `GetById(id)` (used to resolve save data back to references).
- `BotRosterLibrary` — an ordered `Rounds: BotTeamDefinition[]`, plus `GetByRound(round)`.

Both are populated by `Editor/ContentSeeder.cs` alongside the creatures/bot teams themselves, so
there's one generator for all of it.

`ShopConfig` holds the tunable run economy (starting gold/lives, shop size, reroll cost, board
size, buy-cost formula, tier-unlock cadence) as Inspector-editable fields rather than code
constants — see `client/Assets/Scripts/Data/ShopConfig.cs` for the current placeholder values.
This is balance data, not creature content, so it doesn't follow the trigger/effect vocabulary
above — it's just a plain settings asset.
