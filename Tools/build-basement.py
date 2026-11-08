"""Paint the basement structure in exactly the backdrop_1 style.

backdrop_1.png is procedural underneath: every solid pixel is one of a few rules, all on the
global 100 px/unit canvas grid (measured 2026-10-04):
  - fill: a 192 x 384 tile (diamond lattice + seams every 128 rows), globally aligned;
  - top surfaces: 5 rows of 6 px checker (c8dcf0/a0b4cd, phase by column only), 15 rows of
    8 px checker (5a697d/414c5c, phase on the global grid), 8 rows of 161a21;
  - bottom edges: 7 rows of 414c5a; left/right edges: 7 columns of 3c4655.
So a new room drawn from a rectangle layout with the same rules on the same grid continues
the existing art seamlessly. The fill tile and checker phases are read from backdrop_1 itself.

The canvas reaches OVERLAP rows up into the backdrop and is drawn above it. Near the join it
paints over the backdrop's bottom-edge border so there is no seam. Further up it only paints
the void band: under the middle of the level the backdrop's lowest floor sits at y -12.5 with
open air below it down to the sprite edge, which the basement would otherwise expose; that air
(transparent and connected to the sprite's bottom edge) is filled solid.

    python Tools/build-basement.py
"""
import json, os
import numpy as np
from PIL import Image

Image.MAX_IMAGE_PIXELS = None
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "Assets", "Environment", "Sprites", "Level 0", "backdrop_1.png")
LAYOUT = os.path.join(ROOT, "ArtSource", "basement_layout.json")
OUT = os.path.join(ROOT, "Assets", "Environment", "Sprites", "Level 0", "basement_1.png")

PX = 100                 # canvas px per world unit
SPRITE_BOTTOM_T = 7128   # backdrop_1 row (from the top) where the sprite ends = world y -17.64
SEAM_ROWS = 30           # rows above the room always repainted to hide the backdrop's bottom border

TOP_ROWS = 28
EDGE = 7


def hexrgb(h):
    return np.array([(h >> 16) & 255, (h >> 8) & 255, h & 255], np.uint8)


def learn_fill_tile(src):
    """Most common colour per (row % 384, col % 192) over every deep-interior pixel."""
    import cv2
    solid = (src[..., 3] > 250).astype(np.uint8)
    interior = cv2.erode(solid, np.ones((81, 81), np.uint8)).astype(bool)
    ys, xs = np.nonzero(interior)
    col = (src[ys, xs, 0].astype(np.uint32) << 16) | (src[ys, xs, 1].astype(np.uint32) << 8) | src[ys, xs, 2]
    pal, ci = np.unique(col, return_inverse=True)
    cnt = np.zeros((384 * 192, len(pal)), np.int64)
    np.add.at(cnt, ((ys % 384) * 192 + (xs % 192), ci), 1)
    p = pal[cnt.argmax(1)].reshape(384, 192)
    return np.stack([(p >> 16) & 255, (p >> 8) & 255, p & 255], -1).astype(np.uint8)


def checker_signs(src):
    """Which parity gets the light colour, read off one known top edge of backdrop_1."""
    t, c0 = 4965, 1100                       # top of the long floor slab
    cs = np.arange(c0, c0 + 600)
    row0 = src[t, cs, :3].astype(np.int32)
    light0 = (row0 == hexrgb(0xc8dcf0)).all(1)
    s6 = bool((light0 == ((cs // 6) % 2 == 0)).mean() > 0.5)
    row7 = src[t + 7, cs, :3].astype(np.int32)
    light7 = (row7 == hexrgb(0x5a697d)).all(1)
    s8 = bool((light7 == ((((cs // 8) + ((t + 7) // 8)) % 2) == 0)).mean() > 0.5)
    return s6, s8


def run_lengths(solid, axis, reverse):
    """Distance (px) from each solid pixel to the nearest empty one along an axis."""
    a = np.moveaxis(solid, axis, 0)
    if reverse:
        a = a[::-1]
    d = np.zeros(a.shape, np.int32)
    run = np.zeros(a.shape[1:], np.int32)
    for i in range(a.shape[0]):
        run = np.where(a[i], run + 1, 0)
        d[i] = run
    if reverse:
        d = d[::-1]
    return np.moveaxis(d, 0, axis) - 1      # 0 = the edge pixel itself


def main():
    layout = json.load(open(LAYOUT, encoding="utf-8"))
    # rows the canvas reaches above the room (the scene builder offsets the sprite by the same);
    # canvas height must stay a multiple of 7.5 for the 13.33 px/unit import to come out exact
    OVERLAP = layout["canvas_overlap_px"]
    CONTEXT = OVERLAP + 200
    src = np.asarray(Image.open(SRC).convert("RGBA"))
    tile = learn_fill_tile(src)
    s6, s8 = checker_signs(src)

    from scipy import ndimage
    w = int(round(layout["size"][0] * PX))
    h = int(round(layout["size"][1] * PX))
    t0 = SPRITE_BOTTOM_T - CONTEXT           # first canvas row, in backdrop_1 rows
    H = CONTEXT + h

    solid = np.zeros((H, w), bool)
    # backdrop above the room, with the shaft hole the room opens into
    solid[:CONTEXT] = src[t0:SPRITE_BOTTOM_T, :w, 3] > 128
    # the backdrop's last row is transparent; without this every surface under it reads as
    # a top edge and gets the checker strip, drawing a line along the join
    solid[CONTEXT - 2:CONTEXT] |= solid[CONTEXT - 3]
    # void band: air under the backdrop's lowest floor, open to the sprite's bottom edge.
    # Found before the shaft hole is cut, or the elevator pit would count as open air too.
    air = ~solid[:CONTEXT]
    lab, _ = ndimage.label(air)
    seeds = np.unique(lab[CONTEXT - 3][air[CONTEXT - 3]])
    band = np.isin(lab, seeds[seeds > 0])
    band[:CONTEXT - OVERLAP] = False
    hx0, hx1 = (int(round(v * PX)) for v in layout["shaft_hole"])
    hole_top = CONTEXT - int(round(0.64 * PX)) - 2     # the pit floor slab, cut by a BackdropPiece
    band[hole_top:CONTEXT, hx0:hx1] = False
    solid[:CONTEXT] |= band
    solid[hole_top:CONTEXT, hx0:hx1] = False
    for x0, y0, x1, y1 in (s["rect"] for s in layout["solids"]):
        solid[CONTEXT + int(round(y0 * PX)):CONTEXT + int(round(y1 * PX)),
              int(round(x0 * PX)):int(round(x1 * PX))] = True

    # beyond the canvas: solid to the left/right/top (the walls and level continue), empty below
    padded = np.pad(solid, ((1, 1), (1, 1)), constant_values=False)
    padded[0, :] = True
    padded[:, 0] = padded[:, -1] = True
    padded[-1, :] = False
    d_top = run_lengths(padded, 0, False)[1:-1, 1:-1]
    d_bot = run_lengths(padded, 0, True)[1:-1, 1:-1]
    d_left = run_lengths(padded, 1, False)[1:-1, 1:-1]
    d_right = run_lengths(padded, 1, True)[1:-1, 1:-1]

    tt, cc = np.mgrid[t0:t0 + H, 0:w]
    out = np.zeros((H, w, 4), np.uint8)
    out[..., :3] = tile[tt % 384, cc % 192]

    side = (d_left < EDGE) | (d_right < EDGE)
    out[side, :3] = hexrgb(0x3c4655)
    bottom = d_bot < EDGE
    out[bottom, :3] = hexrgb(0x414c5a)

    top = d_top < TOP_ROWS
    par6 = ((cc // 6) % 2 == 0) == s6
    par8 = ((((cc // 8) + (tt // 8)) % 2) == 0) == s8
    band0 = top & (d_top < 5)
    band1 = top & (d_top >= 5) & (d_top < 20)
    band2 = top & (d_top >= 20)
    out[band0, :3] = np.where(par6[band0, None], hexrgb(0xc8dcf0), hexrgb(0xa0b4cd))
    out[band1, :3] = np.where(par8[band1, None], hexrgb(0x5a697d), hexrgb(0x414c5c))
    out[band2, :3] = hexrgb(0x161a21)
    # above the seam rows only touch what changed: the band and the backdrop's floor edge on it
    paint = np.ones((H, w), bool)
    paint[:CONTEXT - SEAM_ROWS] = False
    near_band = ndimage.binary_dilation(np.pad(band, ((0, H - CONTEXT), (0, 0))), iterations=TOP_ROWS + 2)
    paint |= near_band
    out[..., 3] = np.where(solid & paint, 255, 0)

    crop = out[CONTEXT - OVERLAP:]
    Image.fromarray(crop, "RGBA").save(OUT)
    print(f"{OUT}: {crop.shape[1]}x{crop.shape[0]} (overlap {OVERLAP} rows, void band {band.sum()} px), "
          f"checker signs {s6},{s8}")


if __name__ == "__main__":
    main()
