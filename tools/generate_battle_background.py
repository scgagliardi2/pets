#!/usr/bin/env python3
"""Generates the placeholder battle backdrop at client/Assets/Art/Backgrounds/BattleForest.png.

A 640x360 pixel-art forest clearing, drawn at half the battle canvas's 1280x720 reference so
BackgroundImportProcessor's Point filter scales every source pixel to a crisp 2x2 block. It
stands in for real Location art until there is some. The layout is keyed to where
BattleSceneBuilder puts the mons: the horizon sits high, as in the battle mockup, so the enemy
pair can stand on the far clearing (feet at y ~150-160) clear of the player's stat boxes, and the
player's pair stands on the patch at the bottom left, which the party strip partly covers.

Deterministic (fixed seed), so re-running it doesn't churn the PNG.

Usage: python3 tools/generate_battle_background.py
"""

import os
import random
from PIL import Image, ImageDraw

W, H = 640, 360
OUT = os.path.join(os.path.dirname(__file__), "..", "client", "Assets", "Art", "Backgrounds", "BattleForest.png")

HORIZON = 116  # base of the far tree line; everything below it is ground

rng = random.Random(7)
img = Image.new("RGB", (W, H))
d = ImageDraw.Draw(img)


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def bands(y0, y1, top, bottom, count):
    """Flat colour bands rather than a smooth gradient, to keep the pixel-art look."""
    for i in range(count):
        ya = y0 + (y1 - y0) * i // count
        yb = y0 + (y1 - y0) * (i + 1) // count
        d.rectangle([0, ya, W, yb], fill=lerp(top, bottom, i / max(1, count - 1)))


def ridge(base_y, peaks, colour):
    points = [(0, H), (0, base_y)] + [(x, base_y - h) for x, h in peaks] + [(W, base_y), (W, H)]
    d.polygon(points, fill=colour)


def spruce(x, base, height, dark, light):
    half = height * 0.42
    d.rectangle([x - 1, base - 2, x + 1, base + 3], fill=(76, 54, 36))
    for tier in range(3):
        top = base - height + tier * height * 0.22
        width = half * (0.55 + tier * 0.22)
        bottom = top + height * 0.5
        d.polygon([(x, top), (x - width, bottom), (x + width, bottom)], fill=dark)
        d.polygon([(x, top), (x - width * 0.45, bottom - height * 0.12), (x, bottom - height * 0.18)], fill=light)


# Sky and clouds.
bands(0, HORIZON, (92, 160, 228), (196, 228, 248), 10)
clouds = [(80, 30, 1.1), (250, 18, 0.8), (455, 36, 1.2), (590, 16, 0.7)]
puffs = [(-14, 4, 14, 8), (0, 0, 18, 11), (16, 3, 15, 9), (30, 7, 10, 6), (-26, 8, 9, 5)]
for shade, lift in (((206, 222, 238), 3), ((250, 252, 255), 0)):
    for cx, cy, s in clouds:
        for dx, dy, rx, ry in puffs:
            x, y = cx + dx * s, cy + dy * s + lift
            d.ellipse([x - rx * s, y - ry * s, x + rx * s, y + ry * s], fill=shade)

# Mountains, far then near.
ridge(HORIZON - 12, [(40, 22), (110, 46), (170, 30), (250, 62), (330, 36), (400, 68), (470, 40), (540, 56), (610, 26)], (138, 156, 188))
ridge(HORIZON - 6, [(0, 14), (70, 30), (140, 18), (220, 40), (300, 20), (380, 44), (450, 22), (520, 36), (600, 18), (640, 12)], (104, 126, 160))

# Grass, the far tree line, then the lake in front of it.
bands(HORIZON - 10, H, (122, 182, 80), (88, 150, 56), 12)
for x in range(-6, W + 12, 9):
    spruce(x + rng.randint(-2, 2), HORIZON, rng.randint(18, 28), (32, 84, 60), (52, 112, 76))
d.ellipse([20, HORIZON - 6, 360, HORIZON + 24], fill=(58, 128, 190))
d.ellipse([50, HORIZON - 2, 330, HORIZON + 20], fill=(78, 154, 212))
for _ in range(16):
    x, y = rng.randint(70, 310), rng.randint(HORIZON + 2, HORIZON + 16)
    d.line([x, y, x + rng.randint(6, 16), y], fill=(172, 216, 242))

# Grass tufts, denser toward the viewer.
for _ in range(1000):
    y = int(HORIZON + 24 + (H - HORIZON - 24) * (rng.random() ** 0.7))
    x = rng.randint(0, W)
    colour = (70, 126, 46) if rng.random() < 0.6 else (156, 204, 100)
    d.line([x, y, x + 1, y - 2], fill=colour)
    d.line([x + 2, y, x + 2, y - 3], fill=colour)

# The enemy's clearing (sprites' feet at y ~150-160) and the player's patch.
d.ellipse([318, 130, 638, 182], fill=(164, 138, 98))
d.ellipse([330, 134, 626, 178], fill=(204, 178, 128))
d.ellipse([-40, 256, 380, 350], fill=(164, 138, 98))
d.ellipse([-30, 262, 368, 344], fill=(204, 178, 128))
for _ in range(80):
    x, y = rng.randint(345, 610), rng.randint(140, 172)
    d.point([x, y], fill=(176, 150, 106))
    x, y = rng.randint(10, 340), rng.randint(272, 336)
    d.point([x, y], fill=(176, 150, 106))

# Rocks and flowers.
for x, y, r in [(300, 196, 10), (618, 214, 14), (24, 200, 12), (520, 250, 9)]:
    d.ellipse([x - r, y - r * 0.7, x + r, y + r * 0.7], fill=(118, 122, 130))
    d.ellipse([x - r * 0.7, y - r * 0.7, x + r * 0.2, y], fill=(156, 160, 168))
for _ in range(70):
    x, y = rng.randint(0, W), rng.randint(HORIZON + 30, H)
    d.point([x, y], fill=(250, 250, 240) if rng.random() < 0.5 else (250, 214, 90))

# Framing trees at both edges.
spruce(14, 240, 210, (26, 70, 50), (44, 98, 66))
spruce(60, 200, 140, (30, 78, 56), (50, 108, 72))
spruce(628, 226, 200, (26, 70, 50), (44, 98, 66))
spruce(596, 150, 110, (30, 78, 56), (50, 108, 72))

os.makedirs(os.path.dirname(OUT), exist_ok=True)
img.save(os.path.normpath(OUT))
print(f"wrote {os.path.normpath(OUT)} ({W}x{H})")
