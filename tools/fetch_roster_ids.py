#!/usr/bin/env python3
"""Cache the National Dex id of every species in docs/pokemon_stats_unique.xlsx.

The roster sheet is the locked-in source for names/types/stats (PLAN.md §8) but carries no id
column, while everything downstream is keyed by National Dex id: the cached artwork is
``client/Assets/Art/Pokemon/{id}.png`` and ``PokemonSpeciesDefinitionAsset.Id`` is what
``PokemonSpeciesLibrary.GetById`` looks up. This script resolves sheet name -> id once against
PokeAPI and writes the answer to ``docs/roster_pokeapi_ids.json`` so the Unity importer
(``Assets/Editor/SpeciesRosterImporter.cs``) never touches the network — same
cache-rather-than-hit-at-runtime etiquette PLAN.md §9 asks for.

Run it again only when the roster sheet gains or loses a species:

    python3 tools/fetch_roster_ids.py

It is a read-only GET per species (183 today) and it refuses to write a cache that doesn't have
exactly one id per sheet row, so a typo'd name fails loudly here rather than silently importing a
species with no artwork.
"""

import json
import pathlib
import re
import sys
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

REPO_ROOT = pathlib.Path(__file__).resolve().parent.parent
SHEET = REPO_ROOT / "docs" / "pokemon_stats_unique.xlsx"
CACHE = REPO_ROOT / "docs" / "roster_pokeapi_ids.json"
ART_DIR = REPO_ROOT / "client" / "Assets" / "Art" / "Pokemon"

NS = "{http://schemas.openxmlformats.org/spreadsheetml/2006/main}"
# One paginated request for the whole species index rather than 183 individual lookups — the
# politer shape for a bulk pull, and it makes the script a single round trip.
API_INDEX = "https://pokeapi.co/api/v2/pokemon-species?limit=100000"
# PokeAPI rejects urllib's default User-Agent with a 403, so identify the script.
USER_AGENT = "pets-roster-import/1.0 (personal, non-commercial; see PLAN.md)"


def sheet_names():
    """The Pokémon column of the roster sheet, in sheet order."""
    with zipfile.ZipFile(SHEET) as archive:
        sheet = ET.fromstring(archive.read("xl/worksheets/sheet1.xml"))

    names = []
    for row_index, row in enumerate(sheet.find(f"{NS}sheetData")):
        if row_index == 0:  # header
            continue
        for cell in row:
            if not cell.get("r", "").startswith("A"):
                continue
            if cell.get("t") == "inlineStr":
                text = "".join(t.text or "" for t in cell.iter(f"{NS}t"))
            else:
                value = cell.find(f"{NS}v")
                text = value.text if value is not None else ""
            if text.strip():
                names.append(text.strip())
    return names


def api_slug(display_name):
    """Sheet spelling -> PokeAPI species slug.

    Only two shapes occur in this roster: the gendered Nidoran pair, which the sheet writes as
    "Nidoran (M)" / "Nidoran (F)" and PokeAPI as "nidoran-m" / "nidoran-f", and hyphenated names
    like Ho-Oh which already match once lowercased.
    """
    slug = display_name.lower()
    slug = re.sub(r"\s*\((m|f)\)\s*$", r"-\1", slug)
    slug = slug.replace(". ", "-").replace(" ", "-").replace(".", "").replace("'", "")
    return slug


def fetch_species_index():
    """slug -> National Dex id, for every species PokeAPI knows."""
    request = urllib.request.Request(API_INDEX, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=60) as response:
        results = json.load(response)["results"]
    return {entry["name"]: int(entry["url"].rstrip("/").rsplit("/", 1)[1]) for entry in results}


def main():
    names = sheet_names()
    print(f"{len(names)} species in {SHEET.relative_to(REPO_ROOT)}")

    try:
        index = fetch_species_index()
    except (urllib.error.URLError, KeyError, ValueError) as error:
        print(f"Could not read the PokeAPI species index: {error}", file=sys.stderr)
        return 1
    print(f"{len(index)} species in the PokeAPI index")

    ids = {}
    failures = []
    for name in names:
        slug = api_slug(name)
        if slug not in index:
            failures.append(f"{name} (tried '{slug}')")
            continue
        ids[name] = index[slug]

    if failures:
        print("\nFailed to resolve:\n  " + "\n  ".join(failures), file=sys.stderr)
        return 1

    duplicates = {i for i in ids.values() if list(ids.values()).count(i) > 1}
    if duplicates:
        print(f"\nDuplicate ids resolved: {sorted(duplicates)}", file=sys.stderr)
        return 1

    # The cached artwork is the other half of this mapping, so a mismatch here means an import
    # would produce species with no sprite — which ContentIntegrityTests would only catch later.
    if ART_DIR.is_dir():
        on_disk = {int(p.stem) for p in ART_DIR.glob("*.png")}
        missing_art = sorted(set(ids.values()) - on_disk)
        orphan_art = sorted(on_disk - set(ids.values()))
        if missing_art:
            print(f"\nNo cached artwork for ids: {missing_art}", file=sys.stderr)
            return 1
        if orphan_art:
            print(f"\nWarning: artwork with no roster row: {orphan_art}", file=sys.stderr)

    # A {"species": [{name, id}]} array rather than a name -> id object: the Unity importer reads
    # this with JsonUtility, which has no representation for a dictionary.
    payload = {"species": [{"name": name, "id": ids[name]} for name in names]}
    CACHE.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"\nWrote {len(ids)} ids to {CACHE.relative_to(REPO_ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
