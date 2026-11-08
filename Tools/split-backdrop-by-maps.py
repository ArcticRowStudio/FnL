"""Cut a level backdrop into one sprite per marked map room.

Rooms come from rectangles the artist drew over the backdrop, so each tile is that rectangle
plus a margin - the margin is what pulls the surrounding wall in, and 64 px leaves only 2.3 M
opaque pixels of the sheet unclaimed. Overlaps go to whichever room claims them first so no
pixel is drawn twice. Whatever is still unclaimed becomes one extra low-detail layer.

Scale: the source runs at 100 px per world unit, and its thinnest stroke is 59 px, so there is
no fine detail to protect - halving to 50 px/unit costs nothing visible and quarters the memory.
That also means the old sheet's blurriness was never really about resolution: at 13 px/unit the
thinnest stroke was still 8 px. It was the Bilinear filter softening every edge. These tiles are
meant to be imported Point and uncompressed.
"""
import io, json, os, shutil, sys
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
import numpy as np
from PIL import Image
Image.MAX_IMAGE_PIXELS = None

SRC    = r"Assets/Environment/Sprites/Level 0/backdrop_1.png"
DEST   = r"ArtSource/Separated/Level-0_backdrop/maps"
OFF_X, OFF_Y = 0, 581      # where the marked-up export sits inside the source texture
SRC_PPU = 100.0            # source pixels per world unit
DIV     = 2                # integer downscale -> 50 px/unit
MARGIN  = 64
MIN_X, MAX_Y = 3.20, 47.74 # world position of the sprite content's top-left corner

ROOMS = [
    ("map_01",             2816,   63,  6156, 1925),
    ("map_02",             6248,   63,  9159, 1914),
    ("map_03_left_shaft",    87,   75,  2724, 4500),
    ("map_04",             2816, 2018,  5906, 3177),
    ("map_05",             5986, 2018,  9433, 3189),
    ("map_06",             9513, 2018, 12507, 3189),
    ("map_07",             9501, 3269, 12543, 4512),
    ("map_08",             2816, 3293,  5954, 4476),
    ("map_09_midboss",     6033, 3400,  9385, 5942),
    ("map_10_start",      12969, 4580, 15296, 6490),
    ("map_11",             4591, 4591,  5942, 5965),
    ("map_12",            10907, 4603, 12901, 5989),
    ("map_13",             9453, 4615, 10815, 5942),
    ("map_14",             2542, 4639,  4524, 6049),
    ("map_15_elevator",      51, 4663,   961, 6478),
    ("map_16",             1017, 4687,  2474, 6490),
]
HIDDEN = (9453, 802, 13281, 1938)     # the blue box - recorded, not cut


def world_of(x0, y0, w, h):
    """Centre of a source-pixel rect, in world units."""
    return (MIN_X + (x0 + w / 2.0) / SRC_PPU,
            MAX_Y - (y0 - OFF_Y + h / 2.0) / SRC_PPU)


def save(px, name, x0, y0, rows, div=None):
    div = DIV if div is None else div
    h, w = px.shape[:2]
    img = Image.fromarray(px, "RGBA")
    if div > 1:
        img = img.resize((max(1, w // div), max(1, h // div)), Image.BOX)
    img.save(os.path.join(DEST, name + ".png"))
    wx, wy = world_of(x0, y0, w, h)
    rows.append((name, img.width, img.height, wx, wy, SRC_PPU / div))
    print("  %-18s %5dx%-5d -> %4dx%-4d   world (%7.2f, %7.2f)"
          % (name, w, h, img.width, img.height, wx, wy))
    return img.width * img.height


def main():
    src = Image.open(SRC).convert("RGBA")
    W, H = src.size
    px = np.asarray(src)
    alpha = px[:, :, 3] >= 8
    if os.path.isdir(DEST):
        shutil.rmtree(DEST)
    os.makedirs(DEST)

    claimed = np.zeros((H, W), bool)
    rows, total = [], 0
    for name, x0, y0, x1, y1 in ROOMS:
        rx0 = max(0, x0 + OFF_X - MARGIN); rx1 = min(W - 1, x1 + OFF_X + MARGIN)
        ry0 = max(0, y0 + OFF_Y - MARGIN); ry1 = min(H - 1, y1 + OFF_Y + MARGIN)
        sub = px[ry0:ry1+1, rx0:rx1+1].copy()
        # anything an earlier room already owns is dropped here, so nothing draws twice
        sub[:, :, 3] = np.where(claimed[ry0:ry1+1, rx0:rx1+1], 0, sub[:, :, 3])
        claimed[ry0:ry1+1, rx0:rx1+1] = True
        total += save(sub, name, rx0, ry0, rows)

    # Whatever no room claimed is structure between the rooms: a little ink spread over the
    # whole sheet. As one rectangle that is 25 M mostly-empty pixels, so it goes out on a grid
    # with the empty cells dropped, and at half the rooms' scale - it is all wide flat bars.
    left = alpha & ~claimed
    GX, GY = 16, 8
    tw, th = W // GX, H // GY
    made = 0
    for gy in range(GY):
        for gx in range(GX):
            cell = left[gy*th:(gy+1)*th, gx*tw:(gx+1)*tw]
            if not cell.any():
                continue
            ys, xs = np.where(cell)
            cx0, cx1 = gx*tw + xs.min(), gx*tw + xs.max()
            cy0, cy1 = gy*th + ys.min(), gy*th + ys.max()
            sub = px[cy0:cy1+1, cx0:cx1+1].copy()
            sub[:, :, 3] = np.where(left[cy0:cy1+1, cx0:cx1+1], sub[:, :, 3], 0)
            made += 1
            total += save(sub, "structure_%02d" % made, cx0, cy0, rows, div=DIV*2)
    print("  (%d structure tiles from %d grid cells)" % (made, GX*GY))

    with open(os.path.join(DEST, "placement.csv"), "w", encoding="utf-8") as fh:
        fh.write("file,width,height,world_x,world_y,ppu\n")
        for r in rows:
            fh.write("%s.png,%d,%d,%.4f,%.4f,%.1f\n" % r)
    hx, hy = world_of(HIDDEN[0] + OFF_X, HIDDEN[1] + OFF_Y,
                      HIDDEN[2] - HIDDEN[0], HIDDEN[3] - HIDDEN[1])
    with open(os.path.join(DEST, "notes.json"), "w", encoding="utf-8") as fh:
        json.dump({"source": SRC, "src_ppu": SRC_PPU, "div": DIV, "ppu": SRC_PPU / DIV,
                   "margin": MARGIN, "hidden_room_world_centre": [hx, hy]}, fh, indent=2)

    print("\n%d tiles, %.1f M px total = %.0f MB RGBA32, at %.0f px/unit"
          % (len(rows), total / 1e6, total * 4 / 1e6, SRC_PPU / DIV))
    print("(the single 2048 sheet it replaces was 8 MB at 13 px/unit)")
    print("-> %s" % DEST)


main()
