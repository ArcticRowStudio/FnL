"""Cut the new helipad control tower out of Control_tower_1.psd, plus the walkway slab it stands on.

    python -P -E Tools/build-control-tower.py <Control_tower_1.psd> <Helipad_ControlTower.png> <out_dir>

PSD layers (bottom to top): 0 배경 (paper, skipped), 1-6 the cab (two small lamps, the white radome,
the dish, the antenna mast, the cab body), 7 the stair tower.

  Tower_Stem.png       layer 7, the lattice leg and zigzag stairs. Row STEM_LANDING_ROW is the top
                       landing, which goes at walkway level.
  Tower_Dish.png       layer 4 without its pole; turns about DISH_PIVOT in game
  Tower_Cab_Front.png  layer 6, the cab body
                       These two share the whole cab's box (bottom row on the walkway).
  Tower_Cab_Lamp.png, Tower_Cab_RadarUnit.png, Tower_Cab_WhiteRadar.png, Tower_Cab_Mast.png
  (layers 1, 2, 3, 5) and Tower_Dish_Pole.png (the dish's support, cut from layer 4): the roof gear,
                       each trimmed to itself, with a manifest pivot where it stands on the roof.
                       All of it sits behind the dish, which sits behind the body, as in the PSD.
  Tower_Walkway.png  rows WALKWAY_ROWS of the old tower sprite: just the steel slab bridging the gap
                     between the radome deck and the helideck (the old cab and railing are left out).
  control_tower_manifest.json  each sprite's box in its source image, for placement
"""
import json
import os
import sys

import numpy as np
from PIL import Image
from psd_tools import PSDImage

STEM_LANDING_ROW = 497
WALKWAY_ROWS = (527, 547)
DISH_POLE_BOX = (1056, 548, 1094, 712)   # x0, y0, x1, y1 of the dish's support pole in layer 4
DISH_PIVOT = (1075, 545)                 # top of the pole, where the dish turns
GEAR_LAYERS = {"Tower_Cab_Lamp": 1, "Tower_Cab_RadarUnit": 2, "Tower_Cab_WhiteRadar": 3, "Tower_Cab_Mast": 5}
GEAR_FOOT = 40                           # rows above the roof searched for the item's footing


def trim(rgba):
    ys, xs = np.nonzero(rgba[:, :, 3] > 0)
    x0, y0, x1, y1 = int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1
    return rgba[y0:y1, x0:x1], [x0, y0, x1, y1]


def main(psd_path, old_tower_path, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    psd = PSDImage.open(psd_path)
    layers = [np.array(psd[i].composite(viewport=psd.viewbox).convert("RGBA")) for i in range(len(psd))]
    manifest = {"canvas": list(psd.size), "stemLandingRow": STEM_LANDING_ROW}

    stem, box = trim(layers[7])
    Image.fromarray(stem, "RGBA").save(os.path.join(out_dir, "Tower_Stem.png"))
    manifest["Tower_Stem"] = box

    # The cab in three slices so the dish can turn: back (lamps, radar, white radome, mast, and the
    # dish's own support pole), the dish, and the cab body in front. All share the whole cab's box,
    # so they stack back into the PSD exactly.
    cab = Image.new("RGBA", psd.size)
    for i in range(1, 7):
        cab.alpha_composite(Image.fromarray(layers[i], "RGBA"))
    _, box = trim(np.array(cab))
    x0, y0, x1, y1 = box

    dish = layers[4].copy()
    px0, py0, px1, py1 = DISH_POLE_BOX
    pole = np.zeros_like(dish)
    pole[py0:py1, px0:px1] = dish[py0:py1, px0:px1]
    dish[py0:py1, px0:px1, 3] = 0

    slices = {
        "Tower_Dish": dish,
        "Tower_Cab_Front": layers[6],
    }
    for name, img in slices.items():
        Image.fromarray(np.ascontiguousarray(img[y0:y1, x0:x1]), "RGBA").save(os.path.join(out_dir, name + ".png"))
    manifest["Tower_Cab"] = box
    manifest["dishPivot"] = list(DISH_PIVOT)

    # Roof gear, each trimmed to itself with a pivot where it stands on the cab roof, so each can be
    # scaled on its own and stay planted: x = the column the item meets the roof with (its densest
    # column in the last GEAR_FOOT rows above the roof), y = the roof's top row there.
    roof_alpha = layers[6][..., 3]
    items = {name: layers[i] for name, i in GEAR_LAYERS.items()}
    items["Tower_Dish_Pole"] = pole
    for name, img in items.items():
        part, pbox = trim(img)
        a = img[..., 3] > 128
        cx = (pbox[0] + pbox[2]) // 2
        roof = int(np.argmax(roof_alpha[:, cx] > 128))
        foot = a[max(pbox[1], roof - GEAR_FOOT):roof, pbox[0]:pbox[2]].sum(axis=0)
        px = pbox[0] + int(np.argmax(foot)) if foot.max() > 0 else cx
        roof = int(np.argmax(roof_alpha[:, px] > 128))
        Image.fromarray(part, "RGBA").save(os.path.join(out_dir, name + ".png"))
        manifest[name] = {"box": pbox, "pivot": [px, roof]}

    old = np.array(Image.open(old_tower_path).convert("RGBA"))
    r0, r1 = WALKWAY_ROWS
    band = np.zeros_like(old)
    band[r0:r1] = old[r0:r1]
    walkway, box = trim(band)
    Image.fromarray(walkway, "RGBA").save(os.path.join(out_dir, "Tower_Walkway.png"))
    manifest["Tower_Walkway"] = box

    with open(os.path.join(out_dir, "control_tower_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)
    print(json.dumps(manifest))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2], sys.argv[3])
