"""Split the flat sunset painting (Sunset1.psd) into a sky layer and a sea layer.

    python -P -E Tools/split-sunset.py <Sunset1.psd> <out_dir>

The PSD has only the paper layer and one painted layer, so the split is found in the pixels:
the sea is a smooth vertical gradient and the sky meets it with a hard colour step. Each
column is scanned up from the bottom until the row-to-row change exceeds STEP; a 31-column
median replaces the few columns where a thin cloud stroke dips into the sea.

  Sunset_Sky.png  the painting unchanged, its own sea included (sea is 15.6% of the height, as in
                  the PSD); where the separate sea layer drifts (parallax) or is masked out, the
                  painted water shows rather than a gap
  Sunset_Sea.png  the sea only, from just above the highest horizon row to the canvas bottom,
                  transparent above the horizon line

Nothing is added or blurred. The scene shows the sky at about 6x (the user's scale), which is
big enough that parallax never brings either image's edge into view, so no extension is needed.
  sunset_manifest.json  canvas size, crop offsets and the per-column horizon (rows, y down)
"""
import json
import os
import sys

import numpy as np
from PIL import Image
from psd_tools import PSDImage
from scipy.ndimage import median_filter

STEP = 12            # max channel change between rows that still counts as sea gradient
SCAN_TOP = 1000      # the horizon is never above this row
OUTLIER = 3          # px from the median that marks a stroke, not the horizon


def main(psd_path, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    psd = PSDImage.open(psd_path)
    painting = np.array(psd[1].composite(viewport=psd.viewbox).convert("RGB"))
    h, w, _ = painting.shape

    rgb = painting.astype(int)
    step = np.abs(rgb[:-1] - rgb[1:]).max(axis=2)   # step[y]: row y vs row y + 1
    horizon = np.empty(w, int)
    for x in range(w):
        y = h - 2
        while y > SCAN_TOP and step[y, x] <= STEP:
            y -= 1
        horizon[x] = y + 1                           # first sea row
    smooth = median_filter(horizon, size=31)
    strokes = np.abs(horizon - smooth) > OUTLIER
    horizon[strokes] = smooth[strokes]
    print("horizon rows", horizon.min(), "..", horizon.max(), "fixed columns", int(strokes.sum()))

    rows = np.arange(h)[:, None]
    is_sea = rows >= horizon[None, :]

    # The sky is the painting as it is, its own sea included, so wherever the separate sea layer
    # drifts off (parallax) or is masked out, the same painted water shows instead of a fill.
    sky = painting
    Image.fromarray(sky, "RGB").save(os.path.join(out_dir, "Sunset_Sky.png"))

    top = int(horizon.min()) - 2
    sea = np.zeros((h - top, w, 4), np.uint8)
    sea[..., :3] = painting[top:]
    sea[..., 3] = np.where(is_sea[top:], 255, 0)
    Image.fromarray(sea, "RGBA").save(os.path.join(out_dir, "Sunset_Sea.png"))

    manifest = {
        "canvas": [w, h],
        "skyHeight": int(sky.shape[0]),
        "seaTop": top,
        "seaHeight": int(sea.shape[0]),
        "horizonMean": float(horizon.mean()),
        "horizonMin": int(horizon.min()),
        "horizonMax": int(horizon.max()),
        "horizon": horizon.tolist(),
    }
    with open(os.path.join(out_dir, "sunset_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f)
    print("sky", (w, sky.shape[0]), "sea", (w, sea.shape[0]), "from row", top, "horizon mean", round(float(horizon.mean()), 1))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
