"""
Fetch animated front + back sprites from PokeAPI for the roster in
pokemon_stats_unique.xlsx, plus Magmortar and Pichu, and zip them up.

Gen 4 games never had animated battle sprites - animation was introduced in Gen 5
(Black/White), so that's the default source here. PokeAPI also hosts a separate
"Showdown" animated set (a different, more modern art style, not tied to any one
generation) as an alternative - see SPRITE_SOURCE below to switch.

Meant to be run in YOUR OWN project/environment. It only talks to PokeAPI's public
REST API (pokeapi.co) and its GitHub-hosted sprite files, both provided specifically
for programmatic use like this.

Usage:
    pip install openpyxl
    python fetch_animated_sprites.py pokemon_stats_unique.xlsx

Output:
    animated_sprites/front/<name>.gif
    animated_sprites/back/<name>.gif
    animated_sprites.zip
"""
import sys
import os
import time
import json
import zipfile
import urllib.request
import openpyxl

# "gen5"     -> official Black/White animated battle sprites (only exists for
#               species that existed as of Gen 5, which covers your whole roster)
# "showdown" -> Pokemon Showdown's animated set (different art style, works for
#               every species regardless of generation)
SPRITE_SOURCE = "gen5"

OUT_DIR = "animated_sprites"
EXTRA_NAMES = ["Magmortar", "Pichu"]

# PokeAPI's server returns 403 Forbidden for requests without a normal-looking
# User-Agent - Python's urllib sends a generic one by default, so we override it.
HEADERS = {"User-Agent": "Mozilla/5.0 (compatible; PokemonSpriteFetcher/1.0)"}

NAME_OVERRIDES = {
    "Nidoran♀": "nidoran-f",
    "Nidoran♂": "nidoran-m",
    "Mr. Mime": "mr-mime",
    "Farfetch'd": "farfetchd",
    "Ho-Oh": "ho-oh",
    "Mime Jr.": "mime-jr",
}


def slugify(name: str) -> str:
    if name in NAME_OVERRIDES:
        return NAME_OVERRIDES[name]
    return name.lower().strip().replace(" ", "-").replace("'", "").replace(".", "")


def fetch_json(url: str) -> dict:
    req = urllib.request.Request(url, headers=HEADERS)
    with urllib.request.urlopen(req) as r:
        return json.loads(r.read().decode())


def download(url: str, path: str) -> None:
    req = urllib.request.Request(url, headers=HEADERS)
    with urllib.request.urlopen(req) as r, open(path, "wb") as f:
        f.write(r.read())


def get_animated_urls(data: dict) -> tuple[str | None, str | None]:
    if SPRITE_SOURCE == "showdown":
        s = data["sprites"]["other"]["showdown"]
    else:
        s = data["sprites"]["versions"]["generation-v"]["black-white"]["animated"]
    return s.get("front_default"), s.get("back_default")


def load_roster(xlsx_path: str) -> list[str]:
    wb = openpyxl.load_workbook(xlsx_path, data_only=True)
    ws = wb.active
    names = [row[0] for row in ws.iter_rows(min_row=2, values_only=True) if row[0]]
    seen = {n.lower() for n in names}
    for extra in EXTRA_NAMES:
        if extra.lower() not in seen:
            names.append(extra)
            seen.add(extra.lower())
    return names


def main(xlsx_path: str) -> None:
    names = load_roster(xlsx_path)

    os.makedirs(f"{OUT_DIR}/front", exist_ok=True)
    os.makedirs(f"{OUT_DIR}/back", exist_ok=True)
    missing = []

    for i, name in enumerate(names, start=1):
        slug = slugify(name)
        try:
            data = fetch_json(f"https://pokeapi.co/api/v2/pokemon/{slug}")
            front, back = get_animated_urls(data)
            if front:
                download(front, f"{OUT_DIR}/front/{slug}.gif")
            if back:
                download(back, f"{OUT_DIR}/back/{slug}.gif")
            if not front or not back:
                missing.append(name)
        except Exception as e:
            missing.append(f"{name} ({e})")

        print(f"[{i}/{len(names)}] {name} -> {slug}")
        time.sleep(0.2)  # be polite to the free public API

    with zipfile.ZipFile("animated_sprites.zip", "w") as zf:
        for root, _, files in os.walk(OUT_DIR):
            for f in files:
                fp = os.path.join(root, f)
                zf.write(fp, os.path.relpath(fp, OUT_DIR))

    ok = len(names) - len(missing)
    print(f"\nDone. {ok}/{len(names)} fetched cleanly. Zipped to animated_sprites.zip")
    if missing:
        print("Needs a manual look (name/slug mismatch, or no animated sprite exists):")
        for m in missing:
            print(" -", m)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "pokemon_stats_unique.xlsx")
