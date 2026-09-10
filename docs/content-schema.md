# Content Schema

Status: **not yet written** — this is a Phase 0 placeholder.

This will document the data shape for creatures/abilities/tiers/bot rosters (PLAN.md §2.3), kept
in sync with the actual Unity ScriptableObject fields and their JSON export format. Write this
alongside the Phase 1 data model work (Creature/Ability/Tier ScriptableObjects + JSON export).

Sections to fill in during Phase 1:
- Creature fields (name, tier, attack, health, sprite/placeholder color, ability bindings)
- Ability composition: trigger + effect vocabulary and how they combine
- Tier/pool definitions (which creatures are available at which shop tier)
- Bot roster format (serialized team snapshot — see PLAN.md §2.2, reused later for PvP snapshots)
- JSON export schema (field names/types as they'll appear on disk, for future server validation)
