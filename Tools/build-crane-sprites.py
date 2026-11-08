"""Cut the two cranes in Crane1.psd into the sprites the moving parts need.

    python -P -E Tools/build-crane-sprites.py <Crane1.psd> <out_dir>

Layers (bottom to top): 0 배경 (paper, skipped), 1 gantry frame, 2 gantry trolley + hook,
3 jib crane body, 4 jib crane rigging (hanging cable + block, pendant lines, winch).

Outputs, all at canvas resolution (import at PPU 100, the level-canvas scale):
  Gantry_Frame.png    layer 1
  Gantry_Trolley.png  layer 2 above HOOK_SPLIT_Y (hoist, cage, saddle bar)
  Gantry_Hook.png     layer 2 from HOOK_SPLIT_Y down (cables, block, hook) - sways
  Gantry_Cable.png    vertical tile of the hoist strands, lengthens the hook's cable
  Jib_Body.png        layer 3
  Jib_Rigging.png     layer 4 minus the hanging cable/block (pendant lines + winch stay static)
  Jib_Sheave.png      layer 3 cut around the boom tip, drawn over the swinging cable's top end
  Jib_HookBlock.png   layer 4 block + hook, the swing rope's bottom stopper
  Jib_Cable.png       vertical tile of the two-strand cable for the swing rope's segments
  crane_manifest.json canvas boxes and pivots (canvas px, y down) for the Unity builder
"""
import json
import os
import sys

import numpy as np
from PIL import Image
from psd_tools import PSDImage

HOOK_SPLIT_Y = 546                 # first row below the gantry hoist box
HOOK_PIVOT = (1225, 546)           # where the hook cables leave the hoist
GANTRY_CABLE_BOX = (1190, 550, 1260, 572)  # straight hoist strands, centred on HOOK_PIVOT x
HANG_REGION = (1640, 106, 1750, 1200)   # x0, y0, x1, y1 of the hanging cable + block in layer 4
BLOCK_TOP_Y = 1016                 # rows from here down in HANG_REGION are the hook block
SWING_ANCHOR = (1700, 150)         # swing rope pivot, between the sheave and the boom-tip plate
SHEAVE_BOX = (1648, 84, 1732, 206)  # layer 3 cut that hides the rope's top end
CABLE_TILE_W = 22
CABLE_TILE_H = 32
STRAND_W = 5
CABLE_CORE = (128, 138, 150)       # steel grey, like the sheave's highlight


def layer_rgba(psd, i):
    return np.array(psd[i].composite(viewport=psd.viewbox).convert("RGBA"))


def trim(rgba, origin=(0, 0)):
    ys, xs = np.nonzero(rgba[:, :, 3] > 0)
    x0, y0, x1, y1 = xs.min(), ys.min(), xs.max() + 1, ys.max() + 1
    return rgba[y0:y1, x0:x1], (int(origin[0] + x0), int(origin[1] + y0), int(origin[0] + x1), int(origin[1] + y1))


def save(out_dir, name, rgba):
    Image.fromarray(rgba, "RGBA").save(os.path.join(out_dir, name))


def main(psd_path, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    psd = PSDImage.open(psd_path)
    manifest = {"canvas": list(psd.size), "pixelsPerUnit": 100, "sprites": {}}

    def emit(name, rgba, pivot=None):
        img, box = trim(rgba)
        save(out_dir, name + ".png", img)
        entry = {"box": box}
        if pivot is not None:
            # normalised pivot, Unity convention (origin bottom-left)
            entry["pivot"] = [(pivot[0] - box[0]) / (box[2] - box[0]),
                              (box[3] - pivot[1]) / (box[3] - box[1])]
            entry["pivotCanvas"] = list(pivot)
        manifest["sprites"][name] = entry
        print(name, box)

    frame = layer_rgba(psd, 1)
    emit("Gantry_Frame", frame)

    trolley = layer_rgba(psd, 2)
    top, bottom = trolley.copy(), trolley.copy()
    top[HOOK_SPLIT_Y:, :, 3] = 0
    bottom[:HOOK_SPLIT_Y, :, 3] = 0
    emit("Gantry_Trolley", top)
    emit("Gantry_Hook", bottom, HOOK_PIVOT)
    # Rows 549..573 are the three straight hoist strands; tiled, they lengthen the cable above the hook.
    gx0, gy0, gx1, gy1 = GANTRY_CABLE_BOX
    save(out_dir, "Gantry_Cable.png", trolley[gy0:gy1, gx0:gx1])
    manifest["sprites"]["Gantry_Cable"] = {"box": list(GANTRY_CABLE_BOX)}

    body = layer_rgba(psd, 3)
    emit("Jib_Body", body)

    rigging = layer_rgba(psd, 4)
    x0, y0, x1, y1 = HANG_REGION
    hanging = np.zeros_like(rigging)
    hanging[y0:y1, x0:x1] = rigging[y0:y1, x0:x1]
    static = rigging.copy()
    static[y0:y1, x0:x1, 3] = 0
    emit("Jib_Rigging", static)

    block = hanging.copy()
    block[:BLOCK_TOP_Y, :, 3] = 0
    _, bbox = trim(block)
    emit("Jib_HookBlock", block, ((bbox[0] + bbox[2]) // 2, BLOCK_TOP_Y))
    manifest["swingRopeLength"] = (BLOCK_TOP_Y - SWING_ANCHOR[1]) / 100.0
    manifest["swingAnchorCanvas"] = list(SWING_ANCHOR)

    sx0, sy0, sx1, sy1 = SHEAVE_BOX
    sheave = np.zeros_like(body)
    sheave[sy0:sy1, sx0:sx1] = body[sy0:sy1, sx0:sx1]
    # SwingRope draws its top anchor 0.08 units above the rope pivot
    emit("Jib_Sheave", sheave, (SWING_ANCHOR[0], SWING_ANCHOR[1] - 8))

    # Cable colour: the darkest opaque pixels of the hanging strands
    strands = hanging[y0 + 300:BLOCK_TOP_Y - 40]
    px = strands[strands[:, :, 3] > 200][:, :3]
    lum = px.mean(axis=1)
    colour = np.median(px[lum <= np.percentile(lum, 50)], axis=0).astype(np.uint8)
    # Each strand is outlined in the art's cable colour around a steel core, so the cable
    # still reads when it swings in front of the truss's near-black columns.
    tile = np.zeros((CABLE_TILE_H, CABLE_TILE_W, 4), np.uint8)
    for sx in (0, CABLE_TILE_W - STRAND_W):
        tile[:, sx:sx + STRAND_W, :3] = colour
        tile[:, sx + 1:sx + STRAND_W - 1, :3] = CABLE_CORE
        tile[:, sx:sx + STRAND_W, 3] = 255
    save(out_dir, "Jib_Cable.png", tile)
    manifest["sprites"]["Jib_Cable"] = {"size": [CABLE_TILE_W, CABLE_TILE_H], "colour": colour.tolist()}
    print("Jib_Cable colour", colour.tolist())

    with open(os.path.join(out_dir, "crane_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
