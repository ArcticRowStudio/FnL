"""Splits the painted VTOL boss into rig parts so they can move on their own.

The boss is one painted image with no layered source, so this is a prototype cut:
  hull       - everything left over, same canvas and pivot as the original body sprite
  duct_l     - far-side ducted fan + its exhaust flame; sits BEHIND the hull (wing bar and
               nose cover it), so the parts the hull hides are inpainted on the duct
  duct_r     - near-side ducted fan + flame; sits IN FRONT of the hull, so the tail boom it
               hid is rebuilt on the hull by repeating the visible boom
  pod_l/pod_r- missile pods on the spine
  cannon     - chin cannon (already separated from the hull by transparent gaps)

Each part is cropped to its bounds with its sprite pivot on its joint (duct axle, pod mount,
cannon ball joint) so it can rotate in place. Writes Assets/Art/Sprites/Boss/Vtol/Parts/
Vtol_<part>_P<1|2>.png + .meta and prints every joint's offset from the body pivot for
VtolBossAI.

    python Tools/split-vtol-parts.py            # build
    python Tools/split-vtol-parts.py --preview  # rest pose + swung pose sheet in TEMP
"""
import math
import os
import sys
import uuid

import cv2
import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, "Assets", "Art", "Sprites", "Boss", "Vtol")
OUT = os.path.join(ART, "Parts")
PPU = 130
BODY_PIVOT = (0.5, 0.45)

# Outlines in body-image pixels (y down), traced on a 10 px grid.
DUCT_L = [(5, 195), (12, 168), (30, 143), (55, 122), (85, 110), (120, 106), (150, 112), (170, 128),
          (185, 155), (195, 190), (198, 230), (197, 265), (182, 290), (167, 318), (156, 345), (152, 400),
          (149, 442), (98, 442), (96, 380), (95, 360), (70, 340), (35, 292), (18, 250), (5, 215)]
# Hull pieces drawn over the far duct.
WING_BAR_L = [(117, 226), (200, 211), (262, 197), (262, 233), (200, 248), (117, 259)]
NOSE_L = [(168, 316), (200, 298), (262, 288), (262, 445), (138, 445), (143, 372)]

DUCT_R = [(718, 232), (725, 205), (745, 175), (775, 150), (810, 136), (850, 130), (885, 137),
          (905, 160), (922, 205), (938, 250), (941, 320), (935, 348), (962, 400), (980, 470),
          (980, 524), (895, 524), (850, 450), (835, 405), (830, 390), (790, 356), (755, 310),
          (735, 272), (720, 250)]
# Tail boom the near duct hides: rows and a clean stretch of boom to repeat across.
BOOM_ROWS = (156, 199)
BOOM_SOURCE_X = (906, 936)
BOOM_FILL_X = (722, 906)

POD_L = [(325, 12), (335, 5), (410, 5), (440, 20), (480, 45), (525, 70), (525, 100), (505, 112),
         (485, 122), (410, 128), (330, 125), (322, 115)]
POD_R = [(572, 12), (582, 5), (660, 5), (700, 25), (740, 45), (768, 60), (770, 130), (740, 135),
         (660, 130), (580, 128), (570, 118)]

CANNON_TOP = 515  # cannon = everything below this row except the hull's support arm

JOINTS = {
    "duct_l": (150.0, 236.0),   # where the wing bar enters the far nacelle
    "duct_r": (745.0, 262.0),   # where the wing bar enters the near nacelle
    "pod_l": (465.0, 105.0),    # mount ring
    "pod_r": (705.0, 105.0),
    "cannon": (628.0, 551.0),   # ball joint under the support arm
}
# Extra points riders need, in body-image pixels.
POINTS = {
    "nozzle_l": (125.0, 362.0),
    "nozzle_r": (880.0, 402.0),
    "muzzle": (198.0, 588.0),
    "pod_l_face": (368.0, 66.0),
    "pod_r_face": (617.0, 66.0),
}


def poly_mask(shape, pts):
    m = Image.new("L", (shape[1], shape[0]), 0)
    ImageDraw.Draw(m).polygon(pts, fill=255)
    return np.asarray(m) > 0


def inpaint_rgba(rgba, hole, radius=5):
    """Fills `hole` from its surroundings (colour and alpha) with OpenCV's Telea inpainting."""
    out = rgba.copy()
    if not hole.any():
        return out
    mask = hole.astype(np.uint8) * 255
    bgr = cv2.cvtColor(np.ascontiguousarray(rgba[..., :3]), cv2.COLOR_RGB2BGR)
    filled = cv2.inpaint(bgr, mask, radius, cv2.INPAINT_TELEA)
    out[..., :3][hole] = cv2.cvtColor(filled, cv2.COLOR_BGR2RGB)[hole]
    out[..., 3][hole] = 255
    return out


def split(img):
    rgba = np.asarray(img.convert("RGBA")).copy()
    h, w = rgba.shape[:2]
    alpha = rgba[..., 3] > 0
    ys = np.arange(h)[:, None].repeat(w, 1)

    # Cannon: the support arm is the solid blob that reaches down into the cannon rows from above.
    solid = (rgba[..., 3] >= 128).astype(np.uint8)
    n, lab, stats, _ = cv2.connectedComponentsWithStats(solid, connectivity=4)
    arm = np.zeros_like(alpha)
    for i in range(1, n):
        x, y, bw, bh, area = stats[i]
        if y < CANNON_TOP <= y + bh and area < 20000:
            arm |= lab == i
    arm = cv2.dilate(arm.astype(np.uint8), np.ones((5, 5), np.uint8)) > 0
    cannon = alpha & (ys >= CANNON_TOP) & ~arm

    # The traced outlines sit just inside each duct's dark ink line; grow them over that line
    # (dark pixels only) or a ghost of the rim stays on the hull once the duct swings away.
    luminance = rgba[..., :3].astype(np.int32).sum(axis=2) / 3

    # The art's edges also carry a soft half-transparent fringe outside the ink.
    fringe = rgba[..., 3] < 250

    def with_ink(mask, grow=8):
        grown = cv2.dilate(mask.astype(np.uint8), np.ones((2 * grow + 1, 2 * grow + 1), np.uint8)) > 0
        return mask | (grown & alpha & ((luminance < 90) | fringe))

    occluders_l = poly_mask(alpha.shape, WING_BAR_L) | poly_mask(alpha.shape, NOSE_L)
    duct_l_area = poly_mask(alpha.shape, DUCT_L)
    duct_l = with_ink(duct_l_area & alpha) & ~occluders_l
    duct_r = with_ink(poly_mask(alpha.shape, DUCT_R) & alpha) & ~cannon
    pod_l = poly_mask(alpha.shape, POD_L) & alpha
    pod_r = poly_mask(alpha.shape, POD_R) & alpha

    def layer(mask):
        out = np.zeros_like(rgba)
        out[mask] = rgba[mask]
        return out

    parts = {
        "cannon": layer(cannon),
        "duct_r": layer(duct_r),
        "pod_l": layer(pod_l),
        "pod_r": layer(pod_r),
    }

    # The far duct: paint in what the wing bar and nose were covering, inside its outline.
    dl = layer(duct_l)
    parts["duct_l"] = inpaint_rgba(dl, duct_l_area & occluders_l & ~cannon, 6)

    hull = rgba.copy()
    for m in (cannon, duct_l, duct_r, pod_l, pod_r):
        hull[m] = 0

    # Crumbs of duct outline that strayed outside the trace: small loose specks near a duct.
    near_duct = cv2.dilate((duct_l | duct_r).astype(np.uint8), np.ones((31, 31), np.uint8)) > 0
    n, lab, stats, _ = cv2.connectedComponentsWithStats((hull[..., 3] > 0).astype(np.uint8), connectivity=8)
    for i in range(1, n):
        if stats[i][4] < 60:
            speck = lab == i
            if (speck & near_duct).any():
                hull[speck] = 0

    # Tail boom behind the near duct: repeat a clean stretch of the visible boom leftward.
    y0, y1 = BOOM_ROWS
    sx0, sx1 = BOOM_SOURCE_X
    period = sx1 - sx0
    for x in range(BOOM_FILL_X[0], BOOM_FILL_X[1]):
        src_x = sx0 + (x - BOOM_FILL_X[0]) % period
        col = rgba[y0:y1, src_x]
        tgt = hull[y0:y1, x]
        empty = tgt[:, 3] == 0
        tgt[empty] = col[empty]
        hull[y0:y1, x] = tgt

    # Spine under the pods: the pods sat on the hull's top edge, so close the gap they leave.
    hull_alpha = hull[..., 3] > 0
    near_hull = cv2.dilate(hull_alpha.astype(np.uint8), np.ones((9, 9), np.uint8)) > 0
    pod_gap = (pod_l | pod_r) & (ys >= 108) & near_hull & ~hull_alpha
    hull = inpaint_rgba(hull, pod_gap, 4)

    parts["hull"] = hull
    return parts


def crop_with_pivot(arr, joint):
    a = arr[..., 3] > 0
    yy, xx = np.nonzero(a)
    x0, x1 = xx.min(), xx.max() + 1
    y0, y1 = yy.min(), yy.max() + 1
    # Make sure the joint is inside the crop (the pivot may sit on a transparent pixel).
    jx, jy = joint
    x0, x1 = min(x0, int(jx) - 1), max(x1, int(jx) + 2)
    y0, y1 = min(y0, int(jy) - 1), max(y1, int(jy) + 2)
    crop = arr[y0:y1, x0:x1]
    pw, ph = x1 - x0, y1 - y0
    pivot = ((jx - x0) / pw, 1.0 - (jy - y0) / ph)
    return Image.fromarray(crop, "RGBA"), pivot


def body_local(px, size):
    pivot_px = (size[0] * BODY_PIVOT[0], size[1] * (1 - BODY_PIVOT[1]))
    return ((px[0] - pivot_px[0]) / PPU, (pivot_px[1] - px[1]) / PPU)


META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 9
  spritePivot: {{x: {px:.6f}, y: {py:.6f}}}
  spritePixelsToUnits: {ppu}
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 8
  textureShape: 1
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def write_sprite(img, pivot, path):
    img.save(path)
    meta = path + ".meta"
    guid = uuid.uuid4().hex
    if os.path.exists(meta):  # keep the GUID, refresh the pivot
        for line in open(meta):
            if line.startswith("guid:"):
                guid = line.split()[1]
    with open(meta, "w", newline="\n") as f:
        f.write(META.format(guid=guid, px=pivot[0], py=pivot[1], ppu=PPU))


def preview(parts_by_phase, size):
    """Rest pose (should match the original) and a swung pose, side by side."""
    import copy

    def compose(parts, ang_duct, ang_pod, ang_cannon):
        canvas = Image.new("RGBA", size, (40, 44, 52, 255))
        order = [("duct_l", ang_duct), ("hull", 0), ("cannon", ang_cannon), ("pod_l", ang_pod),
                 ("pod_r", ang_pod), ("duct_r", ang_duct)]
        for name, ang in order:
            layer = Image.fromarray(parts[name], "RGBA")
            if ang and name in JOINTS:
                layer = layer.rotate(ang, resample=Image.NEAREST, center=JOINTS[name])
            canvas.alpha_composite(layer)
        return canvas

    p = parts_by_phase[1]
    shots = [compose(p, 0, 0, 0), compose(p, 30, 10, -15), compose(p, -30, -8, 15)]
    sheet = Image.new("RGBA", (size[0] * 3, size[1]), (0, 0, 0, 255))
    for i, s in enumerate(shots):
        sheet.paste(s, (i * size[0], 0))
    path = os.path.join(os.environ.get("TEMP", "."), "vtol_parts_preview.png")
    sheet.save(path)
    print(path)


def main():
    os.makedirs(OUT, exist_ok=True)
    by_phase = {}
    size = None
    for phase in (1, 2):
        body = Image.open(os.path.join(ART, f"VtolBoss_Phase{phase}.png"))
        size = body.size
        by_phase[phase] = split(body)

    if "--preview" in sys.argv:
        preview(by_phase, size)
        return

    for phase, parts in by_phase.items():
        for name, arr in parts.items():
            if name == "hull":
                img, pivot = Image.fromarray(arr, "RGBA"), BODY_PIVOT
            else:
                img, pivot = crop_with_pivot(arr, JOINTS[name])
            write_sprite(img, pivot, os.path.join(OUT, f"Vtol_{name}_P{phase}.png"))

    print("Joint offsets from the body pivot (units):")
    for name, j in JOINTS.items():
        x, y = body_local(j, size)
        print(f"  {name}: ({x:.3f}f, {y:.3f}f)")
    for name, pt in POINTS.items():
        x, y = body_local(pt, size)
        print(f"  {name}: ({x:.3f}f, {y:.3f}f)")


if __name__ == "__main__":
    main()
