"""Turn the ship-hull photo into a seamlessly repeating wall tile.

Two things make a photo tile badly, and both are handled here.

Lighting: a photo carries a broad brightness gradient. Tiled, that gradient turns into a
visible checkerboard of light and dark patches even when the seams themselves are perfect. So
the low-frequency luminance is divided out first and the texture re-centred on a flat mean.

Seams: a plain cross-fade ghosts - rivets turn into double rivets, panel lines smear. Instead
the two strips that will meet are merged along a minimum-error boundary cut (Efros & Freeman
image quilting): a path is found through the overlap that crosses only where the two strips
already agree, and each pixel is taken whole from one side or the other. Rivets stay round.
"""
import io, os, sys
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
import numpy as np
from PIL import Image, ImageFilter

SRC  = r"C:\Users\mokos\Downloads\선박 벽면.jpeg"
OUT  = os.path.join(os.path.dirname(os.path.abspath(__file__)), "wall_out")
CROP = (300, 180, 1280)   # x0, y0, side - even lighting, avoids the dark top-right corner
T, OV = 1024, 256         # tile size and the width of the band the two edges are merged in


def flatten_lighting(rgb, sigma=120):
    lum = Image.fromarray(rgb.astype(np.uint8)).convert("L")
    lo = np.asarray(lum.filter(ImageFilter.GaussianBlur(sigma)), np.float32)
    gain = np.clip(lo.mean() / np.maximum(lo, 1.0), 0.55, 1.8)[:, :, None]
    return np.clip(rgb * gain, 0, 255)


def min_cut_path(err):
    """Cheapest top-to-bottom path through an error surface; returns one column index per row."""
    h, w = err.shape
    cost = err.copy()
    back = np.zeros((h, w), np.int32)
    for y in range(1, h):
        left  = np.r_[np.inf, cost[y-1, :-1]]
        mid   = cost[y-1]
        right = np.r_[cost[y-1, 1:], np.inf]
        stack = np.vstack([left, mid, right])
        pick = np.argmin(stack, axis=0)
        back[y] = np.arange(w) + pick - 1
        cost[y] += stack[pick, np.arange(w)]
    path = np.zeros(h, np.int32)
    path[-1] = int(np.argmin(cost[-1]))
    for y in range(h - 1, 0, -1):
        path[y-1] = back[y, path[y]]
    return path


def merge_band(a, b):
    """Merge two same-sized strips along a vertical min-error cut: left of the cut takes `a`."""
    err = ((a - b) ** 2).sum(axis=2)
    path = min_cut_path(err)
    cols = np.arange(a.shape[1])[None, :]
    take_a = cols < path[:, None]
    # one pixel of feather right on the cut hides JPEG noise without ghosting the structure
    m = take_a.astype(np.float32)
    m = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.0)), np.float32) / 255.0
    return a * m[:, :, None] + b * (1 - m[:, :, None])


def main():
    os.makedirs(OUT, exist_ok=True)
    x0, y0, side = CROP
    src = np.asarray(Image.open(SRC).convert("RGB"), np.float32)[y0:y0+side, x0:x0+side]
    print("crop %dx%d from (%d,%d)" % (side, side, x0, y0))

    s = flatten_lighting(src)
    print("lighting flattened: quarter means %s -> %s"
          % (["%.0f" % v for v in (src[:, :side//4].mean(), src[:, -side//4:].mean(),
                                   src[:side//4].mean(), src[-side//4:].mean())],
             ["%.0f" % v for v in (s[:, :side//4].mean(), s[:, -side//4:].mean(),
                                   s[:side//4].mean(), s[-side//4:].mean())]))

    # Horizontal wrap: in the tiled plane, columns [0,OV) and [T,T+OV) land on top of each
    # other, so they become one band. Which side goes where matters. The band's left edge butts
    # against the tile's own right edge, column T-1, so it must start as the content that
    # naturally follows it - s[:, T:]. Its right edge runs into s[:, OV], so it must end as
    # s[:, :OV]. Feeding those in the other order is what puts a hard seam back in.
    band = merge_band(s[:, T:T+OV], s[:, :OV])
    tile = np.concatenate([band, s[:, OV:T]], axis=1)

    # Vertical wrap: same argument one axis over, so the cut runs left to right.
    band = merge_band(np.swapaxes(tile[T:T+OV], 0, 1), np.swapaxes(tile[:OV], 0, 1))
    tile = np.concatenate([np.swapaxes(band, 0, 1), tile[OV:T]], axis=0)

    tile = np.clip(tile, 0, 255).astype(np.uint8)
    Image.fromarray(tile).save(os.path.join(OUT, "wall_tile_1024.png"))
    Image.fromarray(tile).resize((512, 512), Image.LANCZOS).save(os.path.join(OUT, "wall_tile_512.png"))

    # Honest seam check: how different are the wrapping edges from their neighbours?
    f = tile.astype(np.float32)
    checks = [("left/right", f[:, -1], f[:, 0], np.abs(f[:, 1:] - f[:, :-1]).mean()),
              ("top/bottom", f[-1], f[0], np.abs(f[1:] - f[:-1]).mean())]
    for name, a, b, inner in checks:
        d = np.abs(a - b).mean()
        print("%s seam mean |diff| %5.1f   vs %4.1f between ordinary neighbours   -> %.2fx"
              % (name, d, inner, d / inner))

    grid = np.tile(tile, (3, 3, 1))
    Image.fromarray(grid).resize((1152, 1152), Image.LANCZOS).save(os.path.join(OUT, "wall_tiled_3x3.png"))
    print("\nwrote wall_tile_1024.png, wall_tile_512.png, wall_tiled_3x3.png to %s" % OUT)


main()
