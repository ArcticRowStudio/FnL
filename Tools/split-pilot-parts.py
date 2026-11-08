"""Splits the boss pilot (the tiger in the flight helmet) into rig parts for a procedural run.

The source is one painted 3/4 standing pose on a white background (ArtSource/Pilot/
Pilot_Tiger_source.jpg), so this is a cut-out rig like the VTOL's:
  arm_back  - his left arm (image right), behind the torso, swings from the shoulder
  leg_back  - his left leg (image right)
  torso     - head, vest and the pants down to the crotch. The pants band under the belt is
              kept on the torso as well as on the legs, so a swinging thigh never opens a hole
  leg_front - his right leg (image left); the hand covered its thigh, which is inpainted
  arm_front - his right arm (image left), below the shoulder pauldron, in front of the legs
  pauldron  - the shoulder dome, drawn over the front arm's top; rides the torso

He faces image-right, so the rig runs right as drawn and mirrors to run left.
Parts are cut at full resolution, then each is downscaled once to PPU (character ~2.5 units
tall) with a hard alpha cut, as the other pixel art is. Each part's pivot sits on its joint.
Writes Assets/Art/Sprites/Boss/Pilot/Resources/BossPilot/Pilot_<part>.png + .meta and prints the joint offsets
from the torso pivot for BossPilotEscape.

    python Tools/split-pilot-parts.py            # build
    python Tools/split-pilot-parts.py --preview  # rest pose + two stride poses in TEMP
"""
import os
import sys
import uuid

import cv2
import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "ArtSource", "Pilot", "Pilot_Tiger_source.jpg")
# A Resources folder: BossPilotEscape loads the parts by name, so no boss needs them wired.
OUT = os.path.join(ROOT, "Assets", "Art", "Sprites", "Boss", "Pilot", "Resources", "BossPilot")

HEIGHT_UNITS = 2.5          # helmet top to claw tips in world units
PPU = 130                   # the player's and the VTOL's density
SOURCE_TOP, SOURCE_BOTTOM = 56, 1365
SCALE = HEIGHT_UNITS * PPU / (SOURCE_BOTTOM - SOURCE_TOP)

# Outlines in source pixels (y down).
ARM_FRONT = [(30, 455), (330, 455), (312, 500), (306, 560), (300, 640), (292, 720), (300, 770),
             (330, 790), (352, 830), (356, 900), (348, 948), (318, 952), (288, 928), (250, 912),
             (212, 892), (188, 850), (150, 760), (60, 640), (30, 560)]
PAULDRON = [(70, 470), (85, 360), (125, 295), (200, 262), (285, 268), (335, 305), (350, 380),
            (338, 470)]
ARM_BACK = [(582, 330), (625, 330), (645, 420), (655, 520), (705, 600), (740, 720), (742, 830),
            (725, 895), (650, 900), (612, 875), (612, 860), (606, 820), (598, 780), (588, 740),
            (580, 700), (575, 660), (568, 620), (563, 560), (566, 450), (578, 400)]
LEG_TOP = 720               # legs start under the belt
# The front thigh's pants where the hand hangs over them (left edge guessed from above and below).
THIGH_UNDER_HAND = [(296, 720), (415, 720), (415, 930), (236, 930), (258, 850), (285, 770)]
PELVIS_KEEP = 820           # torso keeps the pants down to here as backing
SEAM_UPPER, SEAM_LOWER, SEAM_SPLIT_Y = 415, 432, 925

JOINTS = {
    "torso": (420.0, 730.0),      # pelvis; the root of the rig
    "pauldron": (420.0, 730.0),   # same as the torso: it rides it
    "arm_front": (200.0, 445.0),  # shoulder under the pauldron
    "arm_back": (592.0, 380.0),
    "leg_front": (320.0, 735.0),  # hips
    "leg_back": (518.0, 735.0),
}
POINTS = {
    "feet": (420.0, float(SOURCE_BOTTOM)),   # ground line under the pelvis
}


def poly_mask(shape, pts):
    m = Image.new("L", (shape[1], shape[0]), 0)
    ImageDraw.Draw(m).polygon(pts, fill=255)
    return np.asarray(m) > 0


def inpaint_rgba(rgba, hole, radius=6):
    out = rgba.copy()
    if not hole.any():
        return out
    mask = hole.astype(np.uint8) * 255
    # Transparent pixels are black; give them the layer's median colour first, or the fill
    # bleeds black in from the empty side.
    src = rgba[..., :3].copy()
    solid = rgba[..., 3] > 0
    src[~solid] = np.median(src[solid & ~hole], axis=0).astype(np.uint8)
    bgr = cv2.cvtColor(np.ascontiguousarray(src), cv2.COLOR_RGB2BGR)
    filled = cv2.inpaint(bgr, mask, radius, cv2.INPAINT_TELEA)
    out[..., :3][hole] = cv2.cvtColor(filled, cv2.COLOR_BGR2RGB)[hole]
    out[..., 3][hole] = 255
    return out


def matte(rgb):
    """Alpha from the white background: near-white pixels connected to the border. White fur
    inside the ink outline is not connected, so it stays."""
    h, w = rgb.shape[:2]
    white = (rgb.min(axis=2) > 225).astype(np.uint8)
    n, lab = cv2.connectedComponents(white, connectivity=4)
    border = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    bg = np.isin(lab, list(border))
    # Grow a pixel into the JPEG halo around the ink.
    bg = cv2.dilate(bg.astype(np.uint8), np.ones((3, 3), np.uint8)) > 0
    alpha = np.where(bg, 0, 255).astype(np.uint8)
    return np.dstack([rgb, alpha])


def split(rgba):
    h, w = rgba.shape[:2]
    alpha = rgba[..., 3] > 0
    ys, xs = np.mgrid[0:h, 0:w]

    arm_front = poly_mask(alpha.shape, ARM_FRONT) & alpha
    arm_back = poly_mask(alpha.shape, ARM_BACK) & alpha & ~arm_front
    pauldron = poly_mask(alpha.shape, PAULDRON) & alpha & ~arm_front
    seam = np.where(ys < SEAM_SPLIT_Y, SEAM_UPPER, SEAM_LOWER)
    legs = alpha & (ys >= LEG_TOP) & ~arm_front & ~arm_back
    leg_front = legs & (xs < seam)
    leg_back = legs & (xs >= seam)

    def layer(mask):
        out = np.zeros_like(rgba)
        out[mask] = rgba[mask]
        return out

    parts = {
        "arm_front": layer(arm_front),
        "arm_back": layer(arm_back),
        "pauldron": layer(pauldron),
        "leg_back": layer(leg_back),
    }

    # The front thigh behind the hand: paint the pants in, inside the leg's outline.
    thigh = poly_mask(alpha.shape, THIGH_UNDER_HAND)
    hole = thigh & arm_front
    parts["leg_front"] = inpaint_rgba(layer(leg_front), hole, 8)

    torso_mask = alpha & ~arm_front & ~arm_back & ~pauldron & ((ys < LEG_TOP) | ((ys < PELVIS_KEEP) & legs))
    torso = layer(torso_mask)
    # The hand also covered the pelvis band the torso keeps as backing.
    torso = inpaint_rgba(torso, thigh & arm_front & (ys < PELVIS_KEEP), 8)
    parts["torso"] = torso
    return parts


def downscale_with_pivot(arr, joint):
    a = arr[..., 3] > 0
    yy, xx = np.nonzero(a)
    jx, jy = joint
    x0, x1 = min(xx.min(), int(jx) - 1), max(xx.max() + 1, int(jx) + 2)
    y0, y1 = min(yy.min(), int(jy) - 1), max(yy.max() + 1, int(jy) + 2)
    crop = Image.fromarray(arr[y0:y1, x0:x1], "RGBA")
    tw, th = max(1, round(crop.width * SCALE)), max(1, round(crop.height * SCALE))
    # Premultiplied resize so the transparent black around the art doesn't darken the edges.
    small = crop.convert("RGBa").resize((tw, th), Image.LANCZOS).convert("RGBA")
    s = np.asarray(small).copy()
    s[..., 3] = np.where(s[..., 3] >= 128, 255, 0)
    s[s[..., 3] == 0] = 0
    pivot = ((jx - x0) / (x1 - x0), 1.0 - (jy - y0) / (y1 - y0))
    return Image.fromarray(s, "RGBA"), pivot


def local(px):
    """Offset from the torso pivot in world units (y up)."""
    tx, ty = JOINTS["torso"]
    return ((px[0] - tx) / (PPU / SCALE), (ty - px[1]) / (PPU / SCALE))


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


ORDER = ["arm_back", "leg_back", "torso", "leg_front", "arm_front", "pauldron"]


def preview(parts, size):
    """Rest pose (should match the source) and both stride extremes."""
    def compose(legs, arms):
        canvas = Image.new("RGBA", size, (60, 66, 76, 255))
        angles = {"leg_front": legs, "leg_back": -legs, "arm_front": arms, "arm_back": -arms}
        for name in ORDER:
            layer = Image.fromarray(parts[name], "RGBA")
            ang = angles.get(name, 0)
            if ang:
                layer = layer.rotate(ang, resample=Image.BICUBIC, center=JOINTS[name])
            canvas.alpha_composite(layer)
        return canvas

    shots = [compose(0, 0), compose(22, -14), compose(-22, 14)]
    sheet = Image.new("RGBA", (size[0] * 3, size[1]), (0, 0, 0, 255))
    for i, s in enumerate(shots):
        sheet.paste(s, (i * size[0], 0))
    path = os.path.join(os.environ.get("TEMP", "."), "pilot_parts_preview.png")
    sheet.resize((sheet.width // 2, sheet.height // 2)).save(path)
    print(path)


def main():
    rgb = np.asarray(Image.open(SRC).convert("RGB"))
    parts = split(matte(rgb))
    size = (rgb.shape[1], rgb.shape[0])

    if "--preview" in sys.argv:
        preview(parts, size)
        return

    os.makedirs(OUT, exist_ok=True)
    for name, arr in parts.items():
        img, pivot = downscale_with_pivot(arr, JOINTS[name])
        write_sprite(img, pivot, os.path.join(OUT, f"Pilot_{name}.png"))
        print(f"  Pilot_{name}.png {img.size}")

    print("Joint offsets from the torso pivot (units):")
    for name, j in JOINTS.items():
        x, y = local(j)
        print(f"  {name}: ({x:.3f}f, {y:.3f}f)")
    for name, pt in POINTS.items():
        x, y = local(pt)
        print(f"  {name}: ({x:.3f}f, {y:.3f}f)")


if __name__ == "__main__":
    main()
