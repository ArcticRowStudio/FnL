"""Bend the hand-animated parry wind (Desktop/FNL/New_Sprites/Row/Parry_wind03) into a shockwave.

Laid out as drawn, the strokes hang around the character and never travel, so they read as
decoration rather than force. Here they are mapped into polar space about the blow: source x
becomes distance and source y becomes angle, so the vertical strokes turn into arcs. Two arcs
travel toward the attacker and a smaller one is thrown out behind the body. The source's pink haze
is dropped.

The whole shape cools from white to deep blue with time, which also keeps it apart from the
character's own white and blue. Frames are drawn on the player's dot grid in three flat tones, on a
canvas whose pivot is the blow. One frame is one step at FPS, the prefab's framesPerSecond.

Usage: python Tools/build-parry-wind.py [output folder]
"""
import math
import os
import sys

import numpy as np
from PIL import Image

SOURCE = os.path.expanduser(r"~\Desktop\FNL\New_Sprites\Row\Parry_wind03")
OUTPUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Art", "Pixel", "Vfx", "Parry_Wave")
DRAWINGS = [3, 5, 7, 9, 11, 13, 15, 18, 20, 22]
SOURCE_FPS = 24

FPS = 40
FRAMES = 16
PPU = 64
# Canvas in world units about the blow, facing right.
BEHIND, AHEAD, ABOVE, BELOW = 2.3, 3.3, 3.1, 3.1
SUPERSAMPLE = 2

# (delay, strength): the second arc follows the first.
FRONT_ARCS = ((0.0, 1.0), (0.05, 0.8))
FRONT_LIFE = 0.34
REAR_LIFE = 0.26
COOL_TIME = 0.38 / 1.6

HOT = ((255, 255, 255), (190, 232, 255), (110, 190, 245))
COOL = ((190, 226, 250), (96, 170, 232), (56, 104, 176))
TONE_LEVELS = ((50, 170), (112, 235), (200, 255))  # coverage needed, alpha


def load_strokes():
    """Coverage of the blue-white strokes in each drawing at half size; the pink haze is left out."""
    strokes = []
    for number in DRAWINGS:
        rgba = np.asarray(Image.open(os.path.join(SOURCE, f"Parry_wind_{number:04d}.png")).convert("RGBA"))
        blue_leads = rgba[..., 2].astype(int) >= rgba[..., 0].astype(int)
        coverage = Image.fromarray(np.where(blue_leads, rgba[..., 3], 0).astype(np.uint8), "L")
        strokes.append(np.asarray(coverage.resize((960, 540), Image.BOX), dtype=float))
    return strokes


def ease_out(x):
    return 1 - (1 - min(max(x, 0.0), 1.0)) ** 3


def sample(strokes, drawing, x, y):
    source = strokes[min(max(drawing, 0), len(strokes) - 1)]
    xi, yi = np.rint(x).astype(int), np.rint(y).astype(int)
    inside = (xi >= 0) & (xi < source.shape[1]) & (yi >= 0) & (yi < source.shape[0])
    out = np.zeros(x.shape)
    out[inside] = source[yi[inside], xi[inside]]
    return out


def arc(strokes, drawing, radius, angle, front, thickness, span, power):
    """One band of the source bent into an arc around the blow, tapered at its tips and edges."""
    u = angle / span
    v = (radius - front) / thickness
    coverage = sample(strokes, drawing, 480 + v * 170, 270 + u * 260)
    coverage *= np.clip((1 - np.abs(u) ** power) * 1.6, 0, 1) * (np.abs(u) < 1) * (np.abs(v) < 1)
    return coverage * np.clip((1 - np.abs(v)) * 3, 0, 1)


def lifetime_fade(progress):
    return min(max((1.1 - progress) * 2.5, 0.0), 1.0)


def build(strokes, t, dx, dy, size):
    radius = np.hypot(dx, dy)
    ahead_angle = np.arctan2(dy, dx)
    behind_angle = np.arctan2(dy, -dx)
    drawing = int(t * SOURCE_FPS)

    layers = []
    for index, (delay, strength) in enumerate(FRONT_ARCS):
        local = t - delay
        if local <= 0:
            continue
        progress = local / FRONT_LIFE
        e = ease_out(progress)
        layers.append(strength * lifetime_fade(progress) * arc(
            strokes, int(local * SOURCE_FPS) + index * 3, radius, ahead_angle,
            0.25 + 2.05 * e, 0.3 + 0.3 * e, math.radians(82 - 18 * e), 2.5))

    if t > 0.01:
        progress = (t - 0.01) / REAR_LIFE
        e = ease_out(progress)
        layers.append(0.72 * lifetime_fade(progress) * arc(
            strokes, drawing + 5, radius, behind_angle,
            0.95 + 0.75 * e, 0.2 + 0.16 * e, math.radians(62), 2.0))

    coverage = np.max(layers, axis=0)
    coverage = Image.fromarray(coverage.astype(np.uint8), "L").resize(size, Image.BOX)
    coverage = np.asarray(coverage, dtype=float)

    heat = max(0.0, 1 - t / COOL_TIME)
    out = np.zeros((size[1], size[0], 4), dtype=np.uint8)
    for (level, alpha), hot, cool in zip(TONE_LEVELS, reversed(HOT), reversed(COOL)):
        colour = [round(h * heat + c * (1 - heat)) for h, c in zip(hot, cool)]
        out[coverage >= level] = (*colour, alpha)
    return Image.fromarray(out, "RGBA")


def main():
    folder = sys.argv[1] if len(sys.argv) > 1 else OUTPUT
    os.makedirs(folder, exist_ok=True)
    strokes = load_strokes()

    size = (round((BEHIND + AHEAD) * PPU), round((ABOVE + BELOW) * PPU))
    rows, cols = np.mgrid[0:size[1] * SUPERSAMPLE, 0:size[0] * SUPERSAMPLE]
    dx = (cols + 0.5) / (SUPERSAMPLE * PPU) - BEHIND
    dy = ABOVE - (rows + 0.5) / (SUPERSAMPLE * PPU)

    for index in range(FRAMES):
        t = (index + 0.5) / FPS
        image = build(strokes, t, dx, dy, size)
        image.save(os.path.join(folder, f"Parry_Wave_{index:02d}.png"))
        alpha = np.asarray(image.split()[3])
        clipped = alpha[0].any() or alpha[-1].any() or alpha[:, 0].any() or alpha[:, -1].any()
        print(index, "drawn", int((alpha > 0).sum()), "CLIPPED" if clipped else "")

    print(FRAMES, "frames", size, "pivot", (BEHIND / (BEHIND + AHEAD), BELOW / (ABOVE + BELOW)), "->", os.path.abspath(folder))


if __name__ == "__main__":
    main()
