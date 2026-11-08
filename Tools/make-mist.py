"""Mist textures for the topside sea mist.

    python -P -E Tools/make-mist.py <out_dir>

  Sea_Mist.png  1024 x 512, tiles horizontally (32 x 16 units at PPU 32). Dense along the
                bottom, thinning upwards; wisps stretched sideways so it reads as drifting
                sea fog. Scrolled sideways in game by MistDrift, so it has to wrap seamlessly.
  Mist_Puff.png 512 x 192 (16 x 6 units), a single puff faded on every side, for the low mist
                drifting in front of the truss (a tiled band would show hard ends there).

Colour is constant (alpha carries the shape) and near white so it stands out against the sunset.
"""
import os
import sys

import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter

MIST_COLOUR = (242, 232, 228)   # near white: a sunset-coloured mist vanished into the sky


def wisps(h, w, seed, octaves):
    """Fractal noise in 0..1 that wraps along x; each octave is (sigma_y, sigma_x, weight)."""
    rng = np.random.default_rng(seed)
    total = np.zeros((h, w))
    weights = 0.0
    for sy, sx, weight in octaves:
        n = gaussian_filter(rng.standard_normal((h, w)), sigma=(sy, sx), mode=("nearest", "wrap"))
        n = (n - n.mean()) / (n.std() + 1e-9)
        total += weight * n
        weights += weight
    total /= weights
    return np.clip(0.5 + 0.22 * total, 0.0, 1.0)


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def save(path, colour, alpha):
    img = np.zeros(alpha.shape + (4,), np.uint8)
    img[..., :3] = colour
    img[..., 3] = np.round(np.clip(alpha, 0, 1) * 255).astype(np.uint8)
    Image.fromarray(img, "RGBA").save(path)


def main(out_dir):
    os.makedirs(out_dir, exist_ok=True)

    h, w = 512, 1024
    n = wisps(h, w, seed=7, octaves=((40, 260, 1.0), (14, 80, 0.7), (5, 26, 0.35)))
    up = 1.0 - np.arange(h)[:, None] / (h - 1)                 # 0 at the bottom row, 1 at the top
    body = 1.0 - smoothstep(0.35, 1.0, up)                    # dense low down, gone at the top
    edge = smoothstep(0.0, 0.3, up)                           # long fade: a short one read as a line in wide shots
    # A thin veil everywhere in the body plus contrasty wisps on top, so it reads as drifting fog
    wisp = smoothstep(0.42, 0.68, n)
    alpha = body * edge * (0.22 + 0.78 * wisp)
    save(os.path.join(out_dir, "Sea_Mist.png"), MIST_COLOUR, 0.9 * alpha)

    # A loose puff, soft on every side, for the low mist drifting in front of the truss
    h, w = 192, 512
    n = wisps(h, w, seed=23, octaves=((14, 70, 1.0), (6, 26, 0.5)))
    y = (np.arange(h)[:, None] - (h - 1) / 2) / (h / 2)       # -1..1
    x = (np.arange(w)[None, :] - (w - 1) / 2) / (w / 2)       # -1..1
    r = np.sqrt(x ** 2 + y ** 2)
    shape = 1.0 - smoothstep(0.35, 1.0, r)
    alpha = shape * smoothstep(0.3, 0.7, n * (0.6 + 0.4 * shape))
    save(os.path.join(out_dir, "Mist_Puff.png"), MIST_COLOUR, alpha)
    print("written Sea_Mist.png and Mist_Puff.png to", out_dir)


if __name__ == "__main__":
    main(sys.argv[1])
