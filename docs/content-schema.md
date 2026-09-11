# Content Schema

Status: **Partially implemented (2026-09-10) — Phase 0's slice built.** See
`docs/architecture-decisions/0001-pivot-to-pokemon-roguelite.md`. Documents the data shape for
species/passives/items/team-synergy/locations (PLAN.md §8), kept in sync with the actual Unity
ScriptableObject fields (`client/Assets/Scripts/Data`) and their JSON export format. §2-§3 and §8
are built and content-authored for 13 curated species (`client/Assets/Content`); §6 (team synergy)
and §7 (items) are Phase 1+ and not yet implemented; §9 (Location/Gym content) is Phase 1+/2 and
not yet implemented. The old `Data/` code (`CreatureDefinition`, `AbilityDefinition`,
`BotTeamDefinition` etc.) implemented a different game (see ADR 0001) and has been removed — this
schema was never a migration of that one.

**Implementation note:** to avoid a same-namespace name collision between the pure
`Pets.Simulation.PassiveDefinition` (§3's resolved, battle-ready shape) and the authoring
ScriptableObject, the ScriptableObjects below are named `PokemonSpeciesDefinitionAsset` and
`PassiveDefinitionAsset` in code (`Pets.Data`) rather than the bare names used in this doc's
prose — same fields, same intent, just disambiguated for the compiler.

## 1. Design principle

Same principle as before, restated for the new model: content is **data, not code**. A Pokémon's
identity is a `PokemonSpeciesDefinition` asset; its passive is a `PassiveDefinition` asset
referenced by id; a new Pokémon is authored entirely as data, never a new C# class. The passive
effect vocabulary (§4) is deliberately non-polymorphic for the same `JsonUtility`-round-tripping
reason as the old schema — one flat `EffectData` shape, not a discriminated union.

One rule carried over verbatim from the design doc (§8) because it materially shapes the schema:
**a passive persists through a Pokémon's evolutions — only its magnitude scales by stage, never a
different passive per stage.** This is why `PassiveDefinition` has a magnitude-scaling table (§3)
instead of evolution stages each getting their own passive reference.

## 2. `PokemonSpeciesDefinition` (ScriptableObject)

Mirrors the design doc's `PokemonSpecies` interface (§9), authored in Unity:

| Field | Type | Notes |
|---|---|---|
| `id` | `int` | PokeAPI id, reused directly — see design doc §9. |
| `displayName` | `string` | Real Pokémon name (this project uses actual Pokémon names/species — see PLAN.md §9 scope note). |
| `types` | `PokemonType` + optional second `PokemonType` | One or two of the 18 types (design doc §9's `PokemonType` union). |
| `baseAttack` / `baseHealth` / `baseSpeed` | `int` | Per-evolution-stage stats, sourced directly from `docs/pokemon_stats_unique.xlsx` — **explicit placeholders**, not derived from a formula (design doc §8). |
| `passive` | `PassiveDefinition` reference | Empty/none for most of the 183 species today — the source sheet's Ability column is blank. Fill in as passives are hand-authored (PLAN.md §8). |
| `evolvesInto` | `PokemonSpeciesDefinition` reference (nullable) | Object reference in the authoring asset, not a raw id (artist-friendly, same pattern the old schema used for `Summon`). |
| `evolutionExpThreshold` | `int` | Only meaningful if `evolvesInto` is set. |
| `spriteSource` | `string` (PokeAPI sprite URL or local cache path) | Resolved by the content-import pipeline (PLAN.md §8), not hand-entered per species. |
| `isLegendary` | `bool` | Manually flagged for the 7 folded-in Legendaries (Mew, Mewtwo, Rayquaza, Ho-Oh, Lugia, Kyogre, Groudon) per PLAN.md §8 — the source sheet carries no rarity flag, so this is set by hand at import time, not derived. |

## 3. `PassiveDefinition` (ScriptableObject)

Every Pokémon — Lead or Support, no distinction — triggers its passive on exactly one condition:
**its own charge meter filling** (`docs/battle-sim-spec.md` §3–§4). There is no other trigger in
this game (no on-hurt/on-faint/on-buy — those belonged to the old design).

| Field | Type | Notes |
|---|---|---|
| `id` | `string` | Stable identifier, referenced by `PokemonSpeciesDefinition.passive` and possibly item-granted overrides. |
| `displayName` | `string` | E.g. "Ember Burst". |
| `description` | `string` | Player-facing text. |
| `typeFlavor` | `PokemonType` | Which type's flavor this passive expresses — see design doc §11 for the seed list (Fire→burn, Water→shield/heal, Electric→paralyze/speed, etc.). Doesn't have to match the owning species' actual type exactly, but usually does. |
| `effects` | `EffectDefinition[]` | Applied in list order when the trigger fires (§4). |
| `magnitudeByStage` | `int[]` (or a single `int` if the species has no evolution line) | The "same passive, scaling magnitude" rule from §1 — index 0 is base-stage magnitude, index 1 is first evolution, etc. `EffectDefinition.amount` fields are interpreted as multiplied by (or substituted from) this table at battle-instance time, not hand-duplicated per stage. |

`EffectDefinition` (plain `[Serializable]` struct):

| Field | Type | Notes |
|---|---|---|
| `type` | `EffectType` enum | See §4. |
| `target` | `TargetSelector` enum | See §5. |
| `amount` | `int` | Base magnitude before `magnitudeByStage` scaling. Ignored for effects that don't take a magnitude (e.g. `ClearStatus`). |
| `status` | `StatusType` enum (nullable) | Only for `ApplyStatus`/`ClearStatus`. |

A `PassiveDefinition` asset is reusable across species (e.g. a shared "burn on charge fill" asset
used by several Fire-types with only `magnitudeByStage` differing) — same pattern as the old
schema's reusable `AbilityDefinition`.

## 4. Effect vocabulary (`EffectType`)

Extending the old vocabulary's shape to cover the Type-flavor seeds in design doc §11:

- `DealDamage` — reduces target's `currentHP` by `amount` (post-`magnitudeByStage` scaling).
- `Heal` — increases target's `currentHP`, capped at its max HP.
- `Shield` — grants a temporary absorb-shield of `amount`, consumed by incoming damage before HP
  (Rock's "shield-per-hit" seed).
- `ApplyStatus` — sets target's `status` field (`docs/battle-sim-spec.md` §5) to the given
  `StatusType`, overwriting any existing status.
- `ClearStatus` — clears target's `status` field (Fairy's cleanse seed).
- `BuffAttack` / `BuffSpeed` — additive, for the remainder of the battle.
- `ModifyChargeRate` — additive/multiplicative modifier to how fast the target's charge meter
  fills for the remainder of the battle (Electric's speed-up seed, Ice's slow seed, Psychic's
  head-start seed can be expressed as an instantaneous one-time bump via this same effect at
  battle start).
- `DamageReduction` — flat reduction applied to incoming damage for the remainder of the battle
  (Steel's seed).
- `Lifesteal` — heals the effect's owner by a percentage of damage it deals for the remainder of
  the battle (Grass's seed) — implemented as a standing modifier flag rather than a one-shot
  effect, since it needs to apply to *future* attack-exchange damage, not just this passive's own
  `DealDamage`.

This list is expected to grow — per §1, if a Type-flavor seed genuinely can't be expressed by the
current vocabulary, add an `EffectType`, don't special-case a species.

## 5. Target selectors (`TargetSelector`)

Only two mons per side are ever active, so the selector list is narrower than the old 5-slot
model's:

- `Self` — the mon whose passive is firing.
- `Ally` — the *other* currently-active mon on the same side (Lead's passive targets its Support,
  or vice versa). No-op if there is no Support yet (a lone Lead with nothing behind it).
- `EnemyLead` — the opposing side's current Lead.
- `EnemySupport` — the opposing side's current Support. No-op if the enemy has no Support.

Every selector treats a target at `currentHP <= 0` as invalid (mirrors the old schema's rule,
still correct here) — if a selector has no valid target, the effect is skipped.

## 6. Team synergy (`TeamSynergyDefinition`, ScriptableObject)

Design doc §11's TFT-style type-count bonuses — **not** charge-triggered, computed once at
line-up assembly (`docs/battle-sim-spec.md` §8):

| Field | Type | Notes |
|---|---|---|
| `type` | `PokemonType` | Which type this synergy tracks. |
| `thresholds` | `SynergyTier[]` | Usually 3 tiers (2/4/6 mons of this type in the line-up); Dragon uses 2/3 per its own rarity-adjusted note in design doc §11. |

`SynergyTier`:

| Field | Type | Notes |
|---|---|---|
| `countRequired` | `int` | E.g. 2, 4, 6. |
| `effects` | `EffectDefinition[]` | Applied once, flat, to every mon in the active line-up at assembly time — reuses the same `EffectDefinition` shape as passives (§4), though `target` is implicitly "every active mon" here rather than one of the selectors in §5. |

## 7. `ItemDefinition` (ScriptableObject)

Design doc §13 — modifies stats or grants/overrides a passive, equipped onto a specific
`PokemonInstance`:

| Field | Type | Notes |
|---|---|---|
| `id` | `string` | |
| `displayName` | `string` | |
| `statModifiers` | `{ attack?: int; health?: int; speed?: int }` | Flat, additive, applied to the instance's `currentStats`. |
| `passiveOverride` | `PassiveDefinition` reference (nullable) | If set, replaces the equipped mon's `passiveId` for as long as the item is equipped, independent of Lead/Support role (`docs/battle-sim-spec.md` §7 — items fire on their own condition, not the charge-meter rule, unless the granted passive *is* a charge-triggered one). |
| `lockable` | `bool` | Whether this item can be "locked" onto a mon to prevent accidental unequip/sell (design doc §13). |

Equip-slot count per mon is still TBD per design doc §20 — don't hardcode an assumption here; make
the slot count a `RunConfig`-style tunable (see the old schema's `ShopConfig` for the pattern) once
it's implemented.

## 8. `PokemonInstance` (runtime, not a ScriptableObject)

The plain-C# runtime shape a `PokemonSpeciesDefinition` + level/EXP/caught-state produces —
mirrors design doc §9's `PokemonInstance` interface exactly:

```csharp
public class PokemonInstance {
    public string InstanceId;
    public int SpeciesId;
    public string Nickname;          // optional
    public int Level;
    public int Exp;
    public int ExpToNextLevel;
    public Stats CurrentStats;       // attack, health, speed — leveled + synergy + item modifiers folded in at line-up assembly
    public int CurrentHP;
    public StatusType? Status;       // poisoned | burned | paralyzed | asleep — see battle-sim-spec.md §5
    public string PassiveId;         // can differ from species default if item-granted (see ItemDefinition.passiveOverride)
    public List<ItemInstance> EquippedItems;
    public int CaughtWithBallTier;
}
```

Produced from a `PokemonSpeciesDefinition` + save data by a converter (analogous to the old
schema's `TeamStateConverter`) — never authored directly. `Simulation`/`BattleRunner` code (§7 of
`docs/battle-sim-spec.md`) operates on these, never on the ScriptableObject directly, for the same
reason as before: the sim has zero Unity/editor dependencies.

## 9. Location & Gym content (brief — full detail in the design doc)

Locations (design doc §4) and Gyms (§14) are also data, not code, but their schema is lighter and
mostly out of this battle-focused doc's scope:

- `LocationTypeDefinition` — terrain name, biased `PokemonType[]` pool (the table in design doc
  §4), flavor text.
- `GymDefinition` — a fixed opposing line-up (`PokemonInstance[]`, same shape as §8) plus the
  permanent run-wide passive bonus granted on victory (design doc §14) — that bonus reuses the
  `EffectDefinition` shape from §4, applied at the run-state level rather than to a specific mon.

Expand this section once Phase 1/2 (PLAN.md §6) actually builds Location/Gym content generation —
it's listed here now mainly so `EffectDefinition` reuse is established as the pattern from the
start, rather than inventing a second effect shape later.

## 10. JSON export shape

Analogous to the old schema's export, adapted to the new fields. A `PokemonSpeciesDefinition`:

```json
{
  "id": 4,
  "displayName": "Charmander",
  "types": ["Fire"],
  "baseAttack": 6,
  "baseHealth": 8,
  "baseSpeed": 7,
  "passiveId": "ember-burst",
  "evolvesInto": { "speciesId": 5, "expThreshold": 120 },
  "spriteSource": "cached/sprites/4.png",
  "isLegendary": false
}
```

And a `PassiveDefinition`:

```json
{
  "id": "ember-burst",
  "displayName": "Ember Burst",
  "typeFlavor": "Fire",
  "effects": [
    { "type": "ApplyStatus", "target": "EnemyLead", "status": "burned", "amount": 0 }
  ],
  "magnitudeByStage": [1, 2, 3]
}
```

This export exists so `/shared/fixtures` and the future Node backend (PLAN.md §6 Phase 3) have a
real schema to target instead of a guessed one — same rationale as the old schema.

## 11. Runtime registries

Same pattern as before, just renamed for the new content:

- `PokemonSpeciesLibrary` — flat `AllSpecies: PokemonSpeciesDefinition[]`, plus `GetById(id)` and
  `GetByType(type)` (used to build a Location's biased encounter pool, design doc §4).
- `PassiveLibrary` — `GetById(id)`, used to resolve `PokemonInstance.PassiveId` back to behavior
  at battle time, and to resolve an `ItemDefinition.passiveOverride`.

Both populated by a content-import editor tool that reads `docs/pokemon_stats_unique.xlsx` +
cached PokeAPI data (PLAN.md §8) — this replaces the old schema's `ContentSeeder`, since the
source of truth is now an external roster file, not hand-authored assets from scratch.
