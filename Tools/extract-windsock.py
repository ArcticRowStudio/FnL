"""Lift the windsock out of the helipad art so it can move, and hollow out its grey cone.

    python -P -E Tools/extract-windsock.py <Helipad_Main source.png> <Helipad_Main out.png> <Windsock out.png>

The windsock is painted into Helipad_Main.png (3840 x 2160, PPU 65). It is the striped sock plus the
cone that joins it to the pole; the pole and its lamp stay in the helipad art.

  - The cone is the triangle from its tip on the pole (CONE_TIP) to the sock's mouth (CONE_TOP to
    CONE_BOTTOM). Only that triangle (grown by EDGE px for its outline) and the sock body
    (columns from SOCK_X0, rows SOCK_ROWS) are taken: the lamp's glow overlaps the cone's bounding
    box above it and must stay behind.
  - Inside the cone, the flat grey fill is cleared, leaving its dark outline as wires.
  - The taken pixels are cleared from the helipad art; nothing is painted behind them.

Prints the windsock's box in the helipad texture and its pivot (the cone tip), y down.
"""
import json
import sys

import numpy as np
from PIL import Image

CONE_TIP = (1899, 482)
CONE_TOP = (1931, 446)
CONE_BOTTOM = (1931, 517)
EDGE = 3
EDGE_DARK = 8           # outline pixels this far outside the cone still belong to it
LAMP_GLOW = (1876, 421, 36)   # centre x, y and radius of the pole lamp's glow, which stays
SOCK_X0, SOCK_X1 = 1926, 2096
SOCK_ROWS = (438, 528)
OUTLINE_MAX = 60        # max channel below this is outline, kept


def inside_triangle(xs, ys, a, b, c, grow):
    def side(p, q, x, y):
        return (q[0] - p[0]) * (y - p[1]) - (q[1] - p[1]) * (x - p[0])
    d1, d2, d3 = side(a, b, xs, ys), side(b, c, xs, ys), side(c, a, xs, ys)
    lens = [np.hypot(q[0] - p[0], q[1] - p[1]) for p, q in ((a, b), (b, c), (c, a))]
    # orientation-independent "inside, allowing grow px outside each edge"
    s = np.sign(side(a, b, *c))
    return (s * d1 >= -grow * lens[0]) & (s * d2 >= -grow * lens[1]) & (s * d3 >= -grow * lens[2])


def main(src, out_helipad, out_sock):
    img = np.array(Image.open(src).convert("RGBA"))
    h, w, _ = img.shape
    ys, xs = np.mgrid[0:h, 0:w]

    cone = inside_triangle(xs, ys, CONE_TIP, CONE_TOP, CONE_BOTTOM, EDGE) & (xs > CONE_TIP[0])
    # The cone's outline runs thicker than EDGE in places; take dark pixels a little further out,
    # but only dark ones, so the lamp's brown glow next to the upper edge stays put
    # (its faint antialiased fringe too, except where that fringe is the glow's own soft edge)
    dark = img[..., :3].max(axis=2) < OUTLINE_MAX
    away_from_glow = np.hypot(xs - LAMP_GLOW[0], ys - LAMP_GLOW[1]) > LAMP_GLOW[2]
    fringe = (img[..., 3] < 200) & away_from_glow
    cone |= inside_triangle(xs, ys, CONE_TIP, CONE_TOP, CONE_BOTTOM, EDGE_DARK) & (xs > CONE_TIP[0]) & (dark | fringe)
    body = (xs >= SOCK_X0) & (xs < SOCK_X1) & (ys >= SOCK_ROWS[0]) & (ys < SOCK_ROWS[1])
    take = (cone | body) & (img[..., 3] > 0)

    sock = np.zeros_like(img)
    sock[take] = img[take]

    # Hollow the cone: inside (not on its edge), anything brighter than the outline goes
    interior = inside_triangle(xs, ys, CONE_TIP, CONE_TOP, CONE_BOTTOM, -2.5) & (xs < SOCK_X0 + 1)
    fill = interior & (sock[..., :3].max(axis=2) >= OUTLINE_MAX)
    sock[fill, 3] = 0

    out = img.copy()
    out[take, 3] = 0
    Image.fromarray(out, "RGBA").save(out_helipad)

    yy, xx = np.nonzero(sock[..., 3] > 0)
    x0, y0, x1, y1 = int(xx.min()), int(yy.min()), int(xx.max()) + 1, int(yy.max()) + 1
    Image.fromarray(np.ascontiguousarray(sock[y0:y1, x0:x1]), "RGBA").save(out_sock)
    print(json.dumps({"box": [x0, y0, x1, y1], "pivot": list(CONE_TIP),
                      "taken": int(take.sum()), "hollowed": int(fill.sum())}))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2], sys.argv[3])
