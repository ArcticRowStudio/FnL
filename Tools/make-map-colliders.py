"""Turn each map tile's opaque pixels into axis-aligned collider boxes.

A PolygonCollider2D built from these sprites traces the alpha outline, which on line art means
thousands of points and self-intersecting paths - expensive and unreliable to stand on. But this
artwork is entirely axis-aligned rectangles (its thinnest stroke is 59 source pixels), so the
solid area decomposes exactly into a handful of boxes: rows are cut into horizontal runs and a
run is extended downwards for as long as its x-extent repeats. 296 boxes cover the whole level
at full pixel precision, no simplification needed.

Boxes are emitted in each tile's local space so they ride along if a tile is moved, and are meant
to be merged by a CompositeCollider2D on the parent, which erases the seams between neighbours
so nothing catches on an internal edge.
"""
import io, os, sys
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
import numpy as np
from PIL import Image

SRC_DIR = r"ArtSource/Separated/Level-0_backdrop/maps"
OUT     = r"Assets/Environment/Sprites/Level 0/maps/colliders.txt"
ALPHA   = 128


def rects_from_mask(m):
    h, w = m.shape
    out, open_runs = [], {}
    for y in range(h):
        d = np.diff(np.concatenate([[0], m[y].view(np.int8), [0]]))
        cur = set(zip(np.where(d == 1)[0], np.where(d == -1)[0]))
        for k in cur:
            open_runs.setdefault(k, y)
        for k in [k for k in open_runs if k not in cur]:
            out.append((k[0], open_runs[k], k[1], y))
            del open_runs[k]
    for k, y0 in open_runs.items():
        out.append((k[0], y0, k[1], h))
    return out


def main():
    ppu = {}
    for line in open(os.path.join(SRC_DIR, "placement.csv"), encoding="utf-8").read().splitlines()[1:]:
        f = line.split(",")
        if len(f) >= 6:
            ppu[f[0]] = float(f[5])

    rows, total = [], 0
    for name in sorted(f for f in os.listdir(SRC_DIR) if f.endswith(".png")):
        a = np.asarray(Image.open(os.path.join(SRC_DIR, name)).convert("RGBA"))[:, :, 3] >= ALPHA
        if not a.any():
            continue
        P = ppu[name]
        H, W = a.shape
        for x0, y0, x1, y1 in rects_from_mask(a):
            # sprite pivot is Center and its y runs up, the mask's y runs down
            ox = (x0 + x1) / 2.0 / P - W / (2.0 * P)
            oy = H / (2.0 * P) - (y0 + y1) / 2.0 / P
            rows.append((os.path.splitext(name)[0], ox, oy, (x1 - x0) / P, (y1 - y0) / P))
        total += 1

    with open(OUT, "w", encoding="utf-8") as fh:
        fh.write("tile,offset_x,offset_y,size_x,size_y\n")
        for r in rows:
            fh.write("%s,%.5f,%.5f,%.5f,%.5f\n" % r)

    areas = sorted(r[3] * r[4] for r in rows)
    print("%d boxes across %d tiles" % (len(rows), total))
    print("box size: smallest %.3f x %.3f world units, median area %.3f, largest area %.1f"
          % (min(r[3] for r in rows), min(r[4] for r in rows),
             areas[len(areas)//2], areas[-1]))
    print("-> %s" % OUT)


main()
