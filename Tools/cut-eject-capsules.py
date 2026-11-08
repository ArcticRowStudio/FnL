"""Cuts the bosses' cockpits out as ejection capsules.

When a boss is beaten the pilot fires the whole cockpit off (EjectionCapsule.cs). For each source
sprite this writes:
  Capsule_<boss>_P<n>.png  - the cockpit block on its own, outlined, pivot at its centre
  <boss>_Ejected_P<n>.png  - the source with a scorched empty socket where the cockpit was;
                             same size, pivot and PPU as the source, so it swaps in place
into Assets/Art/Sprites/Boss/Pilot/Resources/BossPilot (loaded by name at runtime), and prints
each capsule centre in the source sprite's local units (as drawn, before any flip or mirror).

  Vtol    - the canopy block on the nose of Vtol_hull_P1/P2
  Quadpod - the angular housing on the turret's upper rear (Quadpod_UpperTurret / _P2). It sticks
            out of the silhouette, so instead of a socket its space is left empty and the turret
            is scorched where it tore away

    python Tools/cut-eject-capsules.py
"""
import os
import re
import uuid

import cv2
import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BOSS = os.path.join(ROOT, "Assets", "Art", "Sprites", "Boss")
OUT = os.path.join(BOSS, "Pilot", "Resources", "BossPilot")

# Outlines in source pixels (y down).
VTOL_CANOPY = [(200, 262), (222, 232), (255, 218), (300, 205), (345, 198), (390, 200), (405, 215),
               (400, 260), (385, 300), (370, 330), (335, 345), (260, 350), (225, 340), (205, 320)]
QUAD_HOUSING = [(322, 100), (345, 80), (380, 68), (420, 75), (440, 80), (440, 95), (420, 110),
                (395, 130), (370, 145), (330, 145), (320, 125)]

# (boss, phase, source, outline, socket: paint a hollow where it was / just leave it empty)
JOBS = [
    ("Vtol", 1, os.path.join(BOSS, "Vtol", "Parts", "Vtol_hull_P1.png"), VTOL_CANOPY, True),
    ("Vtol", 2, os.path.join(BOSS, "Vtol", "Parts", "Vtol_hull_P2.png"), VTOL_CANOPY, True),
    ("Quadpod", 1, os.path.join(BOSS, "Quadpod_UpperTurret.png"), QUAD_HOUSING, False),
    ("Quadpod", 2, os.path.join(BOSS, "Quadpod_UpperTurret_P2.png"), QUAD_HOUSING, False),
]

SOCKET = (22, 24, 28)
SOCKET_RIM = (74, 77, 84)
EMBER = (255, 140, 40)
OUTLINE = (12, 12, 14)


def read_meta(path):
    text = open(path + ".meta", encoding="utf-8").read()
    ppu = float(re.search(r"spritePixelsToUnits: ([\d.]+)", text).group(1))
    m = re.search(r"spritePivot: \{x: ([\d.\-e]+), y: ([\d.\-e]+)\}", text)
    return ppu, (float(m.group(1)), float(m.group(2)))


def poly_mask(shape, pts):
    m = Image.new("L", (shape[1], shape[0]), 0)
    ImageDraw.Draw(m).polygon(pts, fill=255)
    return np.asarray(m) > 0


def cut(rgba, outline, seed, socket):
    alpha = rgba[..., 3] > 0
    pod = poly_mask(alpha.shape, outline) & alpha
    k3 = np.ones((3, 3), np.uint8)

    # The capsule: the block plus a one-pixel dark outline so it reads as an object on its own.
    capsule = np.zeros_like(rgba)
    capsule[pod] = rgba[pod]
    ring = (cv2.dilate(pod.astype(np.uint8), k3) > 0) & ~pod
    capsule[ring] = (*OUTLINE, 255)

    hull = rgba.copy()
    if not socket:
        # Torn off: empty space, and a scorched band on what is left along the break.
        hull[pod] = 0
        left = hull[..., 3] > 0
        near = cv2.dilate(pod.astype(np.uint8), k3, iterations=3) > 0
        burn = left & near
        hull[burn, :3] = (hull[burn, :3].astype(np.int32) * 0.35).astype(np.uint8)
        rng = np.random.default_rng(seed)
        embers = burn & (rng.random(burn.shape) < 0.08)
        hull[embers] = (*EMBER, 255)
        return capsule, hull, pod

    # The socket left behind: dark inside, a lighter torn rim, a few embers, inked edge.
    inner = cv2.erode(pod.astype(np.uint8), k3, iterations=4) > 0
    rim = pod & ~inner
    edge = pod & ~(cv2.erode(pod.astype(np.uint8), k3) > 0)
    # Depth: lit from above, darker toward the floor of the socket, with frame ribs across it.
    ys = np.argwhere(pod)[:, 0]
    top, bottom = ys.min(), ys.max()
    rows = np.arange(rgba.shape[0])[:, None].repeat(rgba.shape[1], 1)
    u = np.clip((rows - top) / max(1, bottom - top), 0, 1)
    shade = (np.array([52, 55, 62])[None, None, :] * (1 - u[..., None]) + np.array(SOCKET)[None, None, :] * u[..., None])
    hull[pod, :3] = shade[pod].astype(np.uint8)
    hull[pod, 3] = 255
    ribs = inner & ((rows - top) % 14 < 2)
    hull[ribs] = (*SOCKET_RIM, 255)
    rng = np.random.default_rng(seed)
    torn = rim & (rng.random(rim.shape) < 0.6)
    hull[torn] = (*SOCKET_RIM, 255)
    embers = inner & (rng.random(inner.shape) < 0.004)
    hull[embers] = (*EMBER, 255)
    hull[edge] = (*OUTLINE, 255)
    return capsule, hull, pod


def crop_centred(arr, mask):
    yy, xx = np.nonzero(arr[..., 3] > 0)
    x0, x1, y0, y1 = xx.min(), xx.max() + 1, yy.min(), yy.max() + 1
    cy, cx = np.argwhere(mask).mean(axis=0)
    pivot = ((cx - x0) / (x1 - x0), 1.0 - (cy - y0) / (y1 - y0))
    return Image.fromarray(arr[y0:y1, x0:x1], "RGBA"), pivot, (cx, cy)


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


def write_sprite(img, pivot, ppu, path):
    img.save(path)
    meta = path + ".meta"
    guid = uuid.uuid4().hex
    if os.path.exists(meta):  # keep the GUID, refresh the pivot
        for line in open(meta):
            if line.startswith("guid:"):
                guid = line.split()[1]
    with open(meta, "w", newline="\n") as f:
        f.write(META.format(guid=guid, px=pivot[0], py=pivot[1], ppu=ppu))


def main():
    os.makedirs(OUT, exist_ok=True)
    for boss, phase, src, outline, socket in JOBS:
        ppu, src_pivot = read_meta(src)
        rgba = np.asarray(Image.open(src).convert("RGBA")).copy()
        h, w = rgba.shape[:2]
        capsule, hull, pod = cut(rgba, outline, phase, socket)

        img, pivot, (cx, cy) = crop_centred(capsule, pod)
        write_sprite(img, pivot, ppu, os.path.join(OUT, f"Capsule_{boss}_P{phase}.png"))
        write_sprite(Image.fromarray(hull, "RGBA"), src_pivot, ppu, os.path.join(OUT, f"{boss}_Ejected_P{phase}.png"))

        lx = (cx - src_pivot[0] * w) / ppu
        ly = ((1 - src_pivot[1]) * h - cy) / ppu
        print(f"{boss} P{phase}: capsule {img.size}, centre in source local units ({lx:.3f}f, {ly:.3f}f)")


if __name__ == "__main__":
    main()
