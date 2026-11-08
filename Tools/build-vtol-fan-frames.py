"""Cuts the two ducted fans out of the painted VTOL boss and renders spin frames for them.

The boss is one painted image, so the fans are seen at an angle as tilted ellipses. Each frame
un-projects the fan disc to a circle, rotates it, and projects it back, so the blades turn in
perspective. Each frame blends a sharp copy with a short smear for a sense of speed, and the
spinner cone at the hub stays put.

Writes Assets/Art/Sprites/Boss/Vtol/Fans/Vtol_Fan_<L|R>_P<1|2>_<n>.png plus .meta (130 PPU,
point filter, centre pivot, same as the body) and prints each fan's local offset on the body
for VtolBossAI.

    python Tools/build-vtol-fan-frames.py            # build
    python Tools/build-vtol-fan-frames.py --preview  # contact sheet in the system temp folder
"""
import math
import os
import sys
import uuid

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, "Assets", "Art", "Sprites", "Boss", "Vtol")
OUT = os.path.join(ART, "Fans")

PPU = 130
BODY_PIVOT = (0.5, 0.45)  # VtolBoss_Phase*.png import pivot

# Blade disc inside each duct, in body-image pixels: centre (where the spokes meet, under the
# spinner), semi-axes, tilt (deg, y down). "cone" is an image-space ellipse (centre, rx, ry)
# around the spinner, which is left as painted.
FANS = {
    "L": dict(center=(89.0, 172.0), a=56.0, b=25.5, angle=-26.0, cone=((88.0, 160.0), 12.0, 15.0)),
    "R": dict(center=(808.0, 200.0), a=66.0, b=29.5, angle=-26.0, cone=((805.0, 187.0), 13.0, 16.0)),
}
BLADES = 8           # spokes in the art; one blade pitch is a seamless loop
FRAMES = 6
SMEAR_DEG = 4.0      # angular smear blended into each frame
SMEAR_SAMPLES = 5
SHARP_WEIGHT = 0.55


def ellipse_frames(img, fan):
    cx, cy = fan["center"]
    a, b = fan["a"], fan["b"]
    ang = math.radians(fan["angle"])
    ca, sa = math.cos(ang), math.sin(ang)

    pad = 2
    r = int(math.ceil(a)) + pad
    x0, y0 = int(cx) - r, int(cy) - r
    size = 2 * r + 1
    ys, xs = np.mgrid[0:size, 0:size].astype(np.float64)
    dx = xs + x0 - cx
    dy = ys + y0 - cy
    # Into the ellipse's frame, then stretch the minor axis so the disc becomes a circle of radius a.
    u = dx * ca + dy * sa
    v = (-dx * sa + dy * ca) * (a / b)
    rad = np.hypot(u, v)
    inside = rad <= a
    (kx, ky), krx, kry = fan["cone"]
    hub = ((xs + x0 - kx) / krx) ** 2 + ((ys + y0 - ky) / kry) ** 2 <= 1.0

    src = np.asarray(img, dtype=np.float64)
    H, W = src.shape[:2]

    def rotated_source(uu, vv, theta):
        """Image pixel that lands at circle-space (uu, vv) after turning the disc by theta."""
        ct, st = math.cos(theta), math.sin(theta)
        ur = uu * ct + vv * st
        vr = (-uu * st + vv * ct) * (b / a)
        sx = cx + ur * ca - vr * sa
        sy = cy + ur * sa + vr * ca
        return (np.clip(np.rint(sx).astype(int), 0, W - 1), np.clip(np.rint(sy).astype(int), 0, H - 1))

    def in_cone(px, py, grow=1.0):
        return ((px - kx) / (krx * grow)) ** 2 + ((py - ky) / (kry * grow)) ** 2 <= 1.0

    original = src[np.clip(ys.astype(int) + y0, 0, H - 1), np.clip(xs.astype(int) + x0, 0, W - 1)]
    pitch = 2 * math.pi / BLADES

    # The spinner is painted on top of the blades, so a rotated copy would carry a second cone
    # round with it. Paint it out of the source first, filling each pixel with the blade one or
    # more pitches round that is clear of the cone (the blades repeat every pitch).
    clean = src.copy()
    todo = inside & in_cone(xs + x0, ys + y0, 1.3)
    for k in range(1, BLADES):
        if not todo.any():
            break
        sx, sy = rotated_source(u[todo], v[todo], k * pitch)
        ok = ~in_cone(sx, sy, 1.3)
        ty, tx = np.nonzero(todo)
        ty, tx = ty[ok], tx[ok]
        clean[ty + y0, tx + x0] = src[sy[ok], sx[ok]]
        todo[ty, tx] = False

    def sample(theta):
        ix, iy = rotated_source(u, v, theta)
        return clean[iy, ix]
    frames = []
    for f in range(FRAMES):
        theta = pitch * f / FRAMES
        sharp = sample(theta)
        smear = np.zeros_like(sharp)
        for s in range(SMEAR_SAMPLES):
            off = math.radians(SMEAR_DEG) * (s / (SMEAR_SAMPLES - 1) - 0.5)
            smear += sample(theta + off)
        smear /= SMEAR_SAMPLES
        out = sharp * SHARP_WEIGHT + smear * (1 - SHARP_WEIGHT)
        out[hub] = original[hub]
        out[..., 3] = np.where(inside, 255.0, 0.0)
        frames.append(Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA"))

    # Local offset of the frame centre from the body's pivot, in world units (y up).
    pivot_px = (img.width * BODY_PIVOT[0], img.height * (1 - BODY_PIVOT[1]))
    centre = (x0 + size / 2.0, y0 + size / 2.0)
    local = ((centre[0] - pivot_px[0]) / PPU, (pivot_px[1] - centre[1]) / PPU)
    return frames, local


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
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
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


def write_png(img, path):
    img.save(path)
    meta = path + ".meta"
    if not os.path.exists(meta):  # keep GUIDs stable across rebuilds
        with open(meta, "w", newline="\n") as f:
            f.write(META.format(guid=uuid.uuid4().hex, ppu=PPU))


def main():
    preview = "--preview" in sys.argv
    os.makedirs(OUT, exist_ok=True)
    sheet_rows = []
    for phase in (1, 2):
        body = Image.open(os.path.join(ART, f"VtolBoss_Phase{phase}.png")).convert("RGBA")
        for side, fan in FANS.items():
            frames, local = ellipse_frames(body, fan)
            if phase == 1:
                print(f"{side}: local offset ({local[0]:.3f}, {local[1]:.3f}), {frames[0].width}px")
            if preview:
                sheet_rows.append(frames)
                continue
            for i, fr in enumerate(frames):
                write_png(fr, os.path.join(OUT, f"Vtol_Fan_{side}_P{phase}_{i}.png"))

    if preview:
        w = max(sum(f.width + 6 for f in row) for row in sheet_rows)
        h = sum(row[0].height + 6 for row in sheet_rows)
        sheet = Image.new("RGBA", (w, h), (40, 40, 48, 255))
        y = 0
        for row in sheet_rows:
            x = 0
            for fr in row:
                sheet.alpha_composite(fr, (x, y))
                x += fr.width + 6
            y += row[0].height + 6
        path = os.path.join(os.environ.get("TEMP", "."), "vtol_fan_preview.png")
        sheet.resize((sheet.width * 3, sheet.height * 3), Image.NEAREST).save(path)
        print(path)


if __name__ == "__main__":
    main()
