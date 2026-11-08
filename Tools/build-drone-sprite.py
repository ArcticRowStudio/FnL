"""Cut the drone out of Drone1.psd and shrink it to a far-off sky sprite for the drone swarm.

    python -P -E Tools/build-drone-sprite.py <Drone1.psd> <Drone.png> [width_px]

Only the visible drone layer is used (the other layers are hidden references). The drone is drawn
nose right; DroneSwarm flips it to face its flight direction. It is shrunk with premultiplied alpha
so the thin rotor blades keep their dark colour instead of picking up the transparent white around
them. At PPU 320 the default 432 px is 1.35 units.
"""
import sys

import numpy as np
from PIL import Image
from psd_tools import PSDImage

DRONE_LAYER = 3


def main(psd_path, out_path, width=144):
    psd = PSDImage.open(psd_path)
    rgba = np.array(psd[DRONE_LAYER].composite(viewport=psd.viewbox).convert("RGBA")).astype(np.float64) / 255
    ys, xs = np.nonzero(rgba[..., 3] > 0)
    rgba = rgba[ys.min():ys.max() + 1, xs.min():xs.max() + 1]

    h = max(1, round(rgba.shape[0] * width / rgba.shape[1]))
    pad = 2                                   # clear border so bilinear filtering doesn't clip the edges
    premul = rgba.copy()
    premul[..., :3] *= premul[..., 3:]
    channels = [np.array(Image.fromarray(premul[..., c].astype(np.float32), "F").resize((width, h), Image.LANCZOS))
                for c in range(4)]
    small = np.clip(np.stack(channels, axis=-1), 0, 1)
    a = small[..., 3:]
    small[..., :3] = np.where(a > 1e-4, small[..., :3] / np.maximum(a, 1e-4), 0)
    out = np.zeros((h + 2 * pad, width + 2 * pad, 4))
    out[pad:pad + h, pad:pad + width] = small
    Image.fromarray(np.round(np.clip(out, 0, 1) * 255).astype(np.uint8), "RGBA").save(out_path)
    print(f"{out_path}: {width + 2 * pad} x {h + 2 * pad}")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2], int(sys.argv[3]) if len(sys.argv) > 3 else 432)
