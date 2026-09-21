#!/usr/bin/env python3
"""
Builds src/content/species.json from the Unity repo's source data, using the same derivation
rules the Unity importer uses (SpeciesTier.cs, StatGrowth.cs, SpeciesRosterImporter.cs).

Then **validates every derived value against the 183 Unity species assets on disk**. That check is
the point of doing it this way: it proves the TypeScript content pipeline reproduces the Unity
importer exactly, the same way the golden fixtures prove the sim does. If a number disagrees, the
script fails rather than emitting a roster that silently plays differently.

Usage:
    python3 scripts/build_species.py /path/to/pets-unity-repo

Reads:
    docs/pokemon_stats_unique.xlsx          name, types, real base stats
    docs/roster_pokeapi_ids.json            national dex ids
    docs/roster_evolution_chains.json       stage and evolves_into
    client/Assets/Content/Species/*.asset   validation target
    client/Assets/Content/Passives/*.asset  the 20 passives
"""

import json
import math
import os
import re
import sys
from collections import OrderedDict

try:
    import openpyxl
except ImportError:
    sys.exit("openpyxl is required: pip install openpyxl")

# --- enum orderings, matching the C# declaration order -----------------------------------------

POKEMON_TYPES = [
    "Normal", "Fire", "Water", "Electric", "Grass", "Ice", "Fighting", "Poison", "Ground",
    "Flying", "Psychic", "Bug", "Rock", "Ghost", "Dragon", "Dark", "Steel", "Fairy",
]
EFFECT_TYPES = [
    "DealDamage", "Heal", "Shield", "ApplyStatus", "ClearStatus", "BuffAttack", "BuffSpeed",
    "ModifyChargeRate", "DamageReduction", "Lifesteal",
]
TARGET_SELECTORS = ["Self", "Ally", "EnemyLead", "EnemySupport"]
STATUS_TYPES = ["Poisoned", "Burned", "Paralyzed", "Asleep"]

# --- SpeciesTier.cs ----------------------------------------------------------------------------

TIER_ONE_TOTAL = 8
TOTAL_INCREASE_PER_TIER = 10
MIN_TIER, MAX_TIER = 1, 6
BASE_STAT_TOTAL_BANDS = [165, 205, 265, 295, 325]
SPEED_2_THRESHOLD, SPEED_3_THRESHOLD = 80, 110
MIN_TIER_FOR_SPEED = [1, 2, 3]
MIN_HEALTH_GROWTH_PERCENT = 50

# Special is scaled onto the tier line by the species' own special-to-physical ratio, so a mon
# that is special-leaning in the real games is special-leaning here. Clamped, because an unclamped
# ratio makes Alakazam's ability nearly lethal on its own at a tier where nothing else is.
MIN_SPECIAL_RATIO = 0.5
MAX_SPECIAL_RATIO = 2.0
NO_HEALTH_GROWTH_MAX_REAL_HEALTH = 1


def clamp_tier(tier):
    return max(MIN_TIER, min(MAX_TIER, tier))


def total_for(tier):
    return TIER_ONE_TOTAL + TOTAL_INCREASE_PER_TIER * (clamp_tier(tier) - 1)


def tier_for_base_stat_total(total):
    for i, band in enumerate(BASE_STAT_TOTAL_BANDS):
        if total <= band:
            return i + 1
    return MAX_TIER


def speed_for(real_speed, tier):
    c = clamp_tier(tier)
    speed = 1
    if real_speed >= SPEED_2_THRESHOLD and c >= MIN_TIER_FOR_SPEED[1]:
        speed = 2
    if real_speed >= SPEED_3_THRESHOLD and c >= MIN_TIER_FOR_SPEED[2]:
        speed = 3
    return speed


def health_growth_percent_for(real_attack, real_health):
    """Real Health share rescaled onto the half of the range above a 50% floor (ADR 0009)."""
    if real_health <= NO_HEALTH_GROWTH_MAX_REAL_HEALTH:
        return 0
    total = max(1, max(0, real_attack) + max(0, real_health))
    share = (200 * real_health + total) // (2 * total)
    return min(100, MIN_HEALTH_GROWTH_PERCENT + (share + 1) // 2)


def special_for(base_attack, real_attack, real_special):
    """Special on the tier line, from the species' real special-to-physical balance.

    Rounds half away from zero rather than using Python's banker's rounding, matching the rest of
    the codebase. It matters at the clamp: Beedrill's 13 Attack at the 0.5 floor is exactly 6.5,
    which banker's rounding sends *down* to 6 and so below the floor the clamp promised.
    """
    ratio = real_special / max(1, real_attack)
    ratio = max(MIN_SPECIAL_RATIO, min(MAX_SPECIAL_RATIO, ratio))
    return max(1, math.floor(base_attack * ratio + 0.5))


def distribute(real_attack, real_health, real_speed, tier):
    """Speed off the threshold, the rest split by real ratio, Health always strictly larger."""
    c = clamp_tier(tier)
    speed = speed_for(real_speed, c)
    rest = total_for(c) - speed
    s = max(1, real_attack + real_health)
    attack = (2 * rest * max(0, real_attack) + s) // (2 * s)
    attack = max(1, min((rest - 1) // 2, attack))
    return {"attack": attack, "health": rest - attack, "speed": speed}


def assign_tiers(roster):
    """Band by real stat total, then force every evolution at least one tier above its source."""
    for row in roster:
        row["tier"] = tier_for_base_stat_total(
            row["realAttack"] + row["realHealth"] + row["realSpeed"]
        )

    index_by_id = {row["id"]: i for i, row in enumerate(roster)}
    stuck = []
    for _ in range(MAX_TIER):
        changed = False
        for row in roster:
            target_id = row.get("evolvesIntoId")
            if not target_id or target_id not in index_by_id:
                continue
            nxt = roster[index_by_id[target_id]]
            if nxt["tier"] > row["tier"]:
                continue
            if row["tier"] >= MAX_TIER:
                stuck.append(f"{row['name']} -> {nxt['name']}")
                continue
            nxt["tier"] = row["tier"] + 1
            changed = True
        if not changed:
            break
    return sorted(set(stuck))


# --- Unity asset parsing -----------------------------------------------------------------------

def parse_asset(path):
    """Unity .asset files are YAML; the fields we want are all flat scalars or simple lists."""
    text = open(path, encoding="utf-8").read()
    out, effects, magnitudes = {}, [], []
    current = None
    for line in text.splitlines():
        m = re.match(r"^  (\w+): ?(.*)$", line)
        if m:
            key, value = m.group(1), m.group(2).strip()
            if key == "Effects":
                current = "effects"
            elif key == "MagnitudeByStage":
                current = "magnitudes"
            else:
                current = None
                out[key] = value
            continue
        m = re.match(r"^  - (\w+): (.+)$", line)
        if m and current == "effects":
            effects.append({m.group(1): int(m.group(2))})
            continue
        m = re.match(r"^    (\w+): (.+)$", line)
        if m and current == "effects" and effects:
            effects[-1][m.group(1)] = int(m.group(2))
            continue
        m = re.match(r"^  - (-?\d+)$", line)
        if m and current == "magnitudes":
            magnitudes.append(int(m.group(1)))
    if effects:
        out["_effects"] = effects
    if magnitudes:
        out["_magnitudes"] = magnitudes
    return out


def guid_of(meta_path):
    for line in open(meta_path, encoding="utf-8"):
        m = re.match(r"^guid: ([0-9a-f]+)$", line.strip())
        if m:
            return m.group(1)
    return None


def ref_guid(field_value):
    m = re.search(r"guid: ([0-9a-f]+)", field_value or "")
    return m.group(1) if m else None


# --- main --------------------------------------------------------------------------------------

def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    repo = sys.argv[1]
    docs = os.path.join(repo, "docs")
    content = os.path.join(repo, "client", "Assets", "Content")

    # 1. The sheet.
    wb = openpyxl.load_workbook(os.path.join(docs, "pokemon_stats_unique.xlsx"))
    ws = wb.worksheets[0]
    sheet = []
    for name, t1, t2, _ability, atk, hp, spd in ws.iter_rows(min_row=2, values_only=True):
        if name is None:
            continue
        sheet.append({
            "name": str(name).strip(),
            "type1": str(t1).strip(),
            "type2": str(t2).strip() if t2 else None,
            "realAttack": int(atk),
            "realHealth": int(hp),
            "realSpeed": int(spd),
        })

    # 2. Ids and evolution chains.
    ids = {e["name"]: e["id"]
           for e in json.load(open(os.path.join(docs, "roster_pokeapi_ids.json")))["species"]}
    chains = {e["name"]: e
              for e in json.load(open(os.path.join(docs, "roster_evolution_chains.json")))["species"]}

    roster = []
    for row in sheet:
        name = row["name"]
        if name not in ids:
            print(f"  ! {name} has no PokeAPI id; skipping")
            continue
        chain = chains.get(name, {})
        row = dict(row)
        row["id"] = ids[name]
        row["stage"] = chain.get("stage", 0)
        row["evolvesIntoId"] = chain.get("evolves_into")
        roster.append(row)

    # 3. Derive.
    special_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "data",
                                "special-attack.json")
    special_data = json.load(open(special_path, encoding="utf-8"))

    stuck = assign_tiers(roster)
    for row in roster:
        row["stats"] = distribute(
            row["realAttack"], row["realHealth"], row["realSpeed"], row["tier"]
        )
        row["healthGrowthPercent"] = health_growth_percent_for(
            row["realAttack"], row["realHealth"]
        )
        row["baseSpecial"] = special_for(
            row["stats"]["attack"],
            special_data["realAttack"].get(str(row["id"]), row["realAttack"]),
            special_data["values"].get(str(row["id"]), row["realAttack"]),
        )

    print(f"Derived {len(roster)} species.")
    if stuck:
        print(f"  {len(stuck)} evolution(s) already at the top tier: {', '.join(stuck)}")

    # 4. Passives, read straight from the Unity assets.
    passives, guid_to_passive_id = OrderedDict(), {}
    passive_dir = os.path.join(content, "Passives")
    for fn in sorted(os.listdir(passive_dir)):
        if not fn.endswith(".asset"):
            continue
        a = parse_asset(os.path.join(passive_dir, fn))
        effects = []
        for e in a.get("_effects", []):
            kind = EFFECT_TYPES[e["Type"]]
            eff = {
                "type": kind,
                "target": TARGET_SELECTORS[e["Target"]],
                "amount": e.get("Amount", 0),
            }
            # Damage aimed at an enemy IS the default ability -- "deal damage equal to your
            # Special" -- so it takes its magnitude from the mon rather than from the authored
            # number. Everything else (shield, heal, status, buffs) keeps its own amount, which is
            # exactly what makes those abilities an override rather than an addition.
            if kind == "DealDamage" and TARGET_SELECTORS[e["Target"]].startswith("Enemy"):
                eff["scalesWithSpecial"] = True
            if kind == "ApplyStatus":
                eff["status"] = STATUS_TYPES[e.get("Status", 0)]
            effects.append(eff)
        pid = a["Id"]
        passives[pid] = {
            "id": pid,
            "displayName": a.get("DisplayName", pid),
            "description": a.get("Description", ""),
            "typeFlavor": POKEMON_TYPES[int(a.get("TypeFlavor", 0))],
            "effects": effects,
            "magnitudeByStage": a.get("_magnitudes", [1]),
        }
        guid = guid_of(os.path.join(passive_dir, fn + ".meta"))
        if guid:
            guid_to_passive_id[guid] = pid
    print(f"Read {len(passives)} passives.")

    # 5. Validate against the Unity species assets, and pick up passive assignments.
    species_dir = os.path.join(content, "Species")
    # Matched by national dex id, not by name: the Unity importer normalises a few display names
    # ("Nidoran (F)" in the sheet becomes "Nidoran-F" on the asset), and the id is unambiguous.
    by_id = {r["id"]: r for r in roster}
    mismatches, checked, legendaries = [], 0, 0

    for fn in sorted(os.listdir(species_dir)):
        if not fn.endswith(".asset"):
            continue
        a = parse_asset(os.path.join(species_dir, fn))
        row = by_id.get(int(a.get("Id", -1)))
        if row is None:
            mismatches.append(f"{a.get('DisplayName')} (id {a.get('Id')}) is an asset with no sheet row")
            continue
        checked += 1

        # The asset's display name wins — it's what the game actually shows.
        row["displayName"] = a.get("DisplayName", row["name"])

        expect = {
            "Tier": row["tier"],
            "BaseAttack": row["stats"]["attack"],
            "BaseHealth": row["stats"]["health"],
            "BaseSpeed": row["stats"]["speed"],
            "HealthGrowthPercent": row["healthGrowthPercent"],
            "EvolutionStage": row["stage"],
        }
        for field, want in expect.items():
            got = int(a.get(field, -1))
            if got != want:
                mismatches.append(f"{row['name']}.{field}: derived {want}, asset has {got}")

        row["passiveId"] = guid_to_passive_id.get(ref_guid(a.get("Passive", "")))
        row["isLegendary"] = a.get("IsLegendary", "0") == "1"
        if row["isLegendary"]:
            legendaries += 1

    print(f"Validated {checked} species against the Unity assets. {legendaries} legendary.")
    if mismatches:
        print(f"\n{len(mismatches)} MISMATCH(ES) — the derivation does not reproduce the importer:\n")
        for m in mismatches[:40]:
            print(f"  - {m}")
        sys.exit(1)
    print("  no mismatches: the derivation reproduces the Unity importer exactly.")

    missing_passive = [r["name"] for r in roster if not r.get("passiveId")]
    if missing_passive:
        print(f"  ! {len(missing_passive)} species have no passive: {missing_passive[:5]}")

    # 6. Emit.
    out_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "content")
    os.makedirs(out_dir, exist_ok=True)

    species_out = []
    for row in sorted(roster, key=lambda r: r["id"]):
        types = [row["type1"]] + ([row["type2"]] if row["type2"] else [])
        species_out.append(OrderedDict([
            ("id", row["id"]),
            ("name", row.get("displayName", row["name"])),
            ("types", types),
            ("tier", row["tier"]),
            ("baseAttack", row["stats"]["attack"]),
            ("baseHealth", row["stats"]["health"]),
            ("baseSpeed", row["stats"]["speed"]),
            ("baseSpecial", row["baseSpecial"]),
            ("healthGrowthPercent", row["healthGrowthPercent"]),
            ("passiveId", row.get("passiveId")),
            ("evolutionStage", row["stage"]),
            ("evolvesIntoId", row.get("evolvesIntoId")),
            ("isLegendary", row.get("isLegendary", False)),
        ]))

    with open(os.path.join(out_dir, "species.json"), "w", encoding="utf-8") as f:
        json.dump(species_out, f, indent=1)
        f.write("\n")

    with open(os.path.join(out_dir, "passives.json"), "w", encoding="utf-8") as f:
        json.dump(list(passives.values()), f, indent=1)
        f.write("\n")

    tiers = {}
    for r in roster:
        tiers[r["tier"]] = tiers.get(r["tier"], 0) + 1
    speeds = {}
    for r in roster:
        speeds[r["stats"]["speed"]] = speeds.get(r["stats"]["speed"], 0) + 1

    print(f"\nWrote species.json ({len(species_out)}) and passives.json ({len(passives)}).")
    print(f"  tier spread:  {dict(sorted(tiers.items()))}")
    print(f"  speed spread: {dict(sorted(speeds.items()))}")
    growths = sorted(r["healthGrowthPercent"] for r in roster)
    print(f"  health growth: {growths[0]}%-{growths[-1]}%, "
          f"{len(set(growths))} distinct values")


if __name__ == "__main__":
    main()
