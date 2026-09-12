#!/usr/bin/env python3
"""Regenerates the 9-sliced UI sprites under client/Assets/Resources/Sprites/UI.

The buttons were originally delivered as ~915x818 renders of chunky pixel art: every
"pixel" was a ~36px block, the blocks didn't land on a consistent grid (detected cell
sizes were 37/35/37/32 across the four files), the colours carried compression noise,
and each sprite sat inside a different amount of transparent padding. None of that can
be 9-sliced correctly - the border values have to include the padding, the stretched
centre swallows part of the chamfered corner, and no single set of numbers works for
all four files.

This script re-authors the same design at its native resolution instead: a 12x12 sprite
with a 5px border, so the slice geometry is exact and identical across the set. The
palette is sampled from the original renders, so the look is unchanged - it is the
geometry that gets fixed, not the art direction.

Shape (per corner, a 3-step 45-degree chamfer):
    ring 0 - dark outline, 1px
    ring 1 - bevel: bright along the top, mid along the sides, dark along the bottom
    inner  - flat fill, plus two specular pixels in the top corners

Usage: python3 tools/generate_ui_sprites.py
"""

from PIL import Image
import os

SIZE = 12          # native sprite is SIZE x SIZE pixels
CHAMFER = 3        # corner cut: pixels with x+y < CHAMFER are outside the shape
BORDER = 5         # 9-slice border, in native pixels (must cover the chamfer + bevel)

# (outline, fill, top bevel, side bevel, bottom bevel), sampled from the original renders.
PALETTES = {
    "ButtonBlue":  ((1, 38, 92),  (34, 125, 252),  (99, 194, 252),  (61, 167, 252), (1, 80, 207)),
    "ButtonGreen": ((3, 53, 52),  (35, 185, 94),   (127, 241, 138), (84, 225, 98),  (11, 139, 72)),
    "ButtonRed":   ((61, 1, 10),  (238, 44, 53),   (251, 122, 126), (250, 84, 89),  (177, 8, 21)),
    # Slate, for Secondary/Disabled. Not in the original delivery - without it those two styles
    # had to reuse the blue art under a grey tint, which muddies the bevel instead of reading as
    # a neutral button. Keyed to the same dark-outline family as the other three.
    "ButtonGray":  ((30, 38, 50),  (146, 157, 171), (205, 213, 223), (178, 188, 200), (99, 110, 126)),
    "TextBox":     ((2, 43, 58),  (252, 240, 219), (232, 213, 180), (208, 183, 144), (208, 183, 144)),
    # Stat bars (Pets.UI.HealthBarView / StatBarView). Same geometry, drawn at half the buttons'
    # scale via Image.pixelsPerUnitMultiplier so a 16-unit bar keeps whole-pixel chamfers. The
    # track's bevel is inverted (dark top, light bottom) so it reads as a recessed groove the fill
    # sits in. HP: an orange pill and the familiar green/yellow/red health bands. SPD: a navy pill
    # and a single blue fill.
    "BarTrack":    ((20, 26, 36),  (52, 60, 74),    (34, 40, 52),    (44, 50, 62),   (76, 86, 102)),
    "HpLabel":     ((74, 30, 4),   (240, 128, 40),  (252, 184, 110), (246, 154, 70), (190, 86, 16)),
    "HpGreen":     ((12, 70, 36),  (72, 200, 88),   (150, 240, 140), (100, 220, 110), (36, 150, 64)),
    "HpYellow":    ((90, 64, 6),   (240, 196, 40),  (252, 236, 130), (248, 214, 80), (196, 146, 16)),
    "HpRed":       ((70, 6, 12),   (226, 58, 52),   (252, 138, 120), (240, 96, 88),  (170, 20, 30)),
    "SpdLabel":    ((10, 20, 48),  (38, 64, 120),   (80, 110, 170),  (58, 86, 146),  (22, 40, 86)),
    "SpdFill":     ((6, 40, 100),  (52, 140, 236),  (130, 200, 252), (88, 170, 248), (20, 96, 196)),
}

# Small non-sliced icons, drawn pixel by pixel. They live in the same folder, so the import
# processor gives them a 5px border too — harmless, since they're shown as Image.Type.Simple.
ICON_SIZE = 12
ICONS = {
    # A sword for Attack, tip top-right, pommel bottom-left.
    "AttackIcon": (
        [
            ".........ooo",
            "........owwo",
            ".......owwgo",
            "......owwgo.",
            ".....owwgo..",
            "....owwgo...",
            ".ooowwgo....",
            ".ohhogo.....",
            "..ohho......",
            ".obohho.....",
            "obo.oho.....",
            "oo...o......",
        ],
        {"o": (28, 32, 44), "w": (236, 240, 246), "g": (150, 160, 178), "h": (232, 176, 48), "b": (140, 84, 40)},
    ),
}

# The solid buttons carry two white specular pixels in each top corner, like the original
# art. The text box is a flat parchment panel and deliberately has none.
SPECULAR = {"ButtonBlue", "ButtonGreen", "ButtonRed", "ButtonGray"}

OUT_DIR = os.path.join(os.path.dirname(__file__), "..", "client", "Assets", "Resources", "Sprites", "UI")


def in_shape(x, y, inset):
    """True if (x, y) is inside the octagon inset by `inset` pixels.

    Insetting moves each straight edge in by 1px and each 45-degree chamfer in by one
    diagonal step, which is what keeps every ring exactly 1px thick.
    """
    lo, hi = inset, SIZE - 1 - inset
    if not (lo <= x <= hi and lo <= y <= hi):
        return False
    cut = CHAMFER + inset
    return (x + y >= cut and (SIZE - 1 - x) + y >= cut
            and x + (SIZE - 1 - y) >= cut and (SIZE - 1 - x) + (SIZE - 1 - y) >= cut)


def bevel_colour(x, y, top, side, bottom):
    """Pick which bevel tone a ring-1 pixel gets, from which edge it hugs."""
    if y <= x and y <= SIZE - 1 - x:
        return top
    if y >= x and y >= SIZE - 1 - x:
        return bottom
    return side


def build(name):
    outline, fill, top, side, bottom = PALETTES[name]
    img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    px = img.load()

    for y in range(SIZE):
        for x in range(SIZE):
            if not in_shape(x, y, 0):
                continue
            if not in_shape(x, y, 1):
                px[x, y] = outline + (255,)
            elif not in_shape(x, y, 2):
                px[x, y] = bevel_colour(x, y, top, side, bottom) + (255,)
            else:
                px[x, y] = fill + (255,)

    if name in SPECULAR:
        for x, y in ((2, 3), (3, 2)):
            px[x, y] = (255, 255, 255, 255)
            px[SIZE - 1 - x, y] = (255, 255, 255, 255)

    path = os.path.normpath(os.path.join(OUT_DIR, name + ".png"))
    img.save(path)
    print(f"wrote {path} ({SIZE}x{SIZE}, border {BORDER})")


def build_icon(name):
    rows, colours = ICONS[name]
    assert len(rows) == ICON_SIZE and all(len(r) == ICON_SIZE for r in rows), name
    img = Image.new("RGBA", (ICON_SIZE, ICON_SIZE), (0, 0, 0, 0))
    px = img.load()
    for y, row in enumerate(rows):
        for x, key in enumerate(row):
            if key != ".":
                px[x, y] = colours[key] + (255,)

    path = os.path.normpath(os.path.join(OUT_DIR, name + ".png"))
    img.save(path)
    print(f"wrote {path} ({ICON_SIZE}x{ICON_SIZE} icon)")


if __name__ == "__main__":
    for n in PALETTES:
        build(n)
    for n in ICONS:
        build_icon(n)
