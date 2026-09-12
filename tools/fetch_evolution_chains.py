#!/usr/bin/env python3
"""Cache each roster species' evolution parent and stage.

`PokemonSpeciesDefinitionAsset.EvolvesInto` is what `Pets.Meta.ExperienceResolver` follows when a
mon crosses its evolution threshold, and `EvolutionStage` is the index into a passive's
`MagnitudeByStage` table (content-schema.md §1/§3). Neither is in the roster sheet — the real
Pokémon evolution chains are, per PLAN.md §8, meant to come from PokeAPI, restricted to the curated
roster.

This resolves "what does X evolve from, and how deep in its chain is it" once and writes
`docs/roster_evolution_chains.json`, which `Assets/Editor/SpeciesRosterImporter.cs` reads. The
importer never touches the network — same caching etiquette as `fetch_roster_ids.py`, which this
script reuses the id cache from.

    python3 tools/fetch_evolution_chains.py

Re-run it only when the roster sheet gains or loses a species. It is one read-only GET per species
(183 today), paced politely.

**Branching evolutions are deliberately left unresolved.** `EvolvesInto` is a single reference and
cannot express "Eevee becomes one of seven". Any species with more than one child inside the roster
is written with `evolves_into: null` and listed under `branching` for the importer to report, so the
data says "unresolved" rather than silently picking a winner. A branch picker is its own feature.
"""

import json
import pathlib
import sys
import time
import urllib.error
import urllib.request

REPO_ROOT = pathlib.Path(__file__).resolve().parent.parent
ID_CACHE = REPO_ROOT / "docs" / "roster_pokeapi_ids.json"
OUT = REPO_ROOT / "docs" / "roster_evolution_chains.json"

API_SPECIES = "https://pokeapi.co/api/v2/pokemon-species/{}"
USER_AGENT = "pets-roster-import/1.0 (personal, non-commercial; see PLAN.md)"
REQUEST_PAUSE_SECONDS = 0.05


def load_roster():
    """[(sheet name, dex id)] from the id cache fetch_roster_ids.py wrote."""
    if not ID_CACHE.is_file():
        print(f"{ID_CACHE} not found — run tools/fetch_roster_ids.py first.", file=sys.stderr)
        sys.exit(1)
    payload = json.loads(ID_CACHE.read_text(encoding="utf-8"))
    return [(entry["name"], entry["id"]) for entry in payload["species"]]


def fetch_species(dex_id):
    request = urllib.request.Request(API_SPECIES.format(dex_id), headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.load(response)


def parent_id(species_json):
    """Dex id of what this species evolves from, or None for a chain root."""
    parent = species_json.get("evolves_from_species")
    if not parent:
        return None
    return int(parent["url"].rstrip("/").rsplit("/", 1)[1])


def main():
    roster = load_roster()
    in_roster = {dex_id for _, dex_id in roster}
    print(f"{len(roster)} roster species")

    # Parents are resolved for every species PokeAPI reports, in roster or not: the stage index has
    # to count real evolution steps, so a mid-line species whose base form isn't curated is still
    # stage 1, not stage 0.
    parents = {}
    for index, (name, dex_id) in enumerate(roster, start=1):
        try:
            parents[dex_id] = parent_id(fetch_species(dex_id))
        except (urllib.error.URLError, KeyError, ValueError) as error:
            print(f"\nFailed on {name} (id {dex_id}): {error}", file=sys.stderr)
            return 1
        if index % 25 == 0 or index == len(roster):
            print(f"  [{index:3}/{len(roster)}]")
        time.sleep(REQUEST_PAUSE_SECONDS)

    def stage_of(dex_id):
        """How many evolution steps deep this species is, following real parents."""
        stage, cursor, guard = 0, parents.get(dex_id), 0
        while cursor is not None and guard < 10:
            stage += 1
            cursor = parents.get(cursor)
            guard += 1
        return stage

    # A parent's children, restricted to the roster — the roster is what the game can evolve into.
    children = {}
    for _, dex_id in roster:
        parent = parents.get(dex_id)
        if parent is not None and parent in in_roster:
            children.setdefault(parent, []).append(dex_id)

    entries, branching, unreachable_parents = [], [], []
    for name, dex_id in roster:
        kids = sorted(children.get(dex_id, []))
        if len(kids) > 1:
            branching.append({"name": name, "id": dex_id, "candidates": kids})
        parent = parents.get(dex_id)
        if parent is not None and parent not in in_roster:
            unreachable_parents.append(name)
        entries.append({
            "name": name,
            "id": dex_id,
            "stage": stage_of(dex_id),
            "evolves_into": kids[0] if len(kids) == 1 else None,
        })

    payload = {"species": entries, "branching": branching}
    OUT.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    evolving = sum(1 for e in entries if e["evolves_into"] is not None)
    print(f"\nWrote {len(entries)} entries to {OUT.relative_to(REPO_ROOT)}")
    print(f"  {evolving} evolve into a single roster species")
    print(f"  {len(branching)} branch and are left unresolved: " +
          ", ".join(b["name"] for b in branching))
    if unreachable_parents:
        print(f"  {len(unreachable_parents)} have a pre-evolution outside the roster (fine, they're "
              f"just never reached by evolving): {', '.join(unreachable_parents)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
