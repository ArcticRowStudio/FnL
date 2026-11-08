"""Normal maps for the X-band radome and its radar.

The older maps (written by generate_highres_*.py) only embossed outlines and came out ~98% flat,
so the dome had no curvature for a light to catch. These build the large-scale shape from the
geometry instead, then add the painted detail on top:

  * dome shells      - convex hemisphere about the hinge (dome centre at the base line)
  * interior wall    - the same hemisphere seen from inside, so concave
  * turntable silo   - vertical cylinder
  * antenna, gantry  - bevelled silhouette
  * rim / floor strips, seam strip - bevelled bands
  * everything       - fine relief from luminance (panel seams sink, bolts rise)

Convention: tangent-space, OpenGL style (green = +Y up), which is what URP 2D lights read.
Note image rows run DOWN, so the up-gradient is minus the row gradient.

Run from the repo root:  python Tools/generate_radome_normals.py
"""
import os
import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt, gaussian_filter, sobel

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIR = os.path.join(ROOT, "Assets", "Environment", "Sprites", "Radome")
OUT = os.path.join(DIR, "Normals")

# Shared dome geometry (texture px, rows from the top). The shutter halves and the interior
# backdrop are drawn on the same 4096-wide layout: base line at row 1868, radius ~1918 x 1868.
BASE_ROW = 1868
RX, RY = 1918.0, 1868.0
SEAM_W = 48           # hazard seam strip width at the inner edge of each shutter half
CURVE = 0.85          # 1 = true hemisphere; a little less keeps the rim from going black
# The painted radial seams all converge just above the base line. Their relief piles up there
# into a dark fan under a light, so line detail fades out within FAN_R of that point.
FAN_ROW, FAN_R = 1720, 650.0


def load(name):
    a = np.asarray(Image.open(os.path.join(DIR, name + ".png")).convert("RGBA")).astype(np.float32)
    return a[..., :3] / 255.0, a[..., 3] > 16


def luminance(rgb, sigma):
    lum = rgb[..., 0] * 0.299 + rgb[..., 1] * 0.587 + rgb[..., 2] * 0.114
    return gaussian_filter(lum, sigma)


def height_normal(h, strength):
    """Normal (x, y) from a height field, +Y up."""
    dhdx = sobel(h, axis=1) / 8.0
    dhdy_up = -sobel(h, axis=0) / 8.0
    return -dhdx * strength, -dhdy_up * strength


def bevel(mask, radius):
    d = np.clip(distance_transform_edt(mask) / radius, 0.0, 1.0)
    return np.sqrt(1.0 - (1.0 - d) ** 2)


def sphere(shape, cx, cy, rx, ry, sign):
    """Hemisphere normal (x, y, z); sign +1 bulges toward the viewer, -1 is the inside."""
    ys, xs = np.mgrid[0:shape[0], 0:shape[1]].astype(np.float32)
    dx = (xs - cx) / rx
    dy = (cy - ys) / ry
    r2 = np.clip(dx * dx + dy * dy, 0.0, 0.999)
    return sign * dx * CURVE, sign * dy * CURVE, np.sqrt(1.0 - r2)


def cylinder_x(shape, cx, r):
    xs = np.mgrid[0:shape[0], 0:shape[1]][1].astype(np.float32)
    dx = np.clip((xs - cx) / r, -0.999, 0.999)
    return dx * CURVE, np.zeros(shape, np.float32), np.sqrt(1.0 - dx * dx)


def compose(base, detail, mask):
    """Add detail slopes onto a base normal and pack to RGBA."""
    bx, by, bz = base
    nx, ny, nz = bx + detail[0], by + detail[1], bz
    n = np.sqrt(nx * nx + ny * ny + nz * nz)
    nx, ny, nz = nx / n, ny / n, nz / n
    out = np.stack([nx * 0.5 + 0.5, ny * 0.5 + 0.5, nz * 0.5 + 0.5], -1)
    out[~mask] = (0.5, 0.5, 1.0)
    rgba = np.concatenate([out, mask[..., None].astype(np.float32)], -1)
    return Image.fromarray(np.round(rgba * 255).astype(np.uint8), "RGBA")


def fan_fade(shape, cx):
    ys, xs = np.mgrid[0:shape[0], 0:shape[1]].astype(np.float32)
    t = np.clip(np.hypot(xs - cx, ys - FAN_ROW) / FAN_R, 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def flat(shape):
    return np.zeros(shape, np.float32), np.zeros(shape, np.float32), np.ones(shape, np.float32)


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + "_Normal.png")
    img.save(path)
    print("wrote", os.path.relpath(path, ROOT))


def shutter(name, left):
    rgb, mask = load(name)
    shape = mask.shape
    cx = shape[1] if left else 0
    bx, by, bz = sphere(shape, cx, BASE_ROW, RX, RY, +1)

    rows = np.arange(shape[0])[:, None]
    cols = np.arange(shape[1])[None, :]
    shell = mask & (rows < BASE_ROW)
    seam = shell & ((cols >= shape[1] - SEAM_W) if left else (cols < SEAM_W))
    band = mask & (rows >= BASE_ROW)

    # Seam and rim are bands bolted onto the shell: flat-ish with bevelled edges.
    for region in (seam, band):
        bx[region], by[region], bz[region] = 0.0, 0.0, 1.0
    lines = luminance(rgb, 1.2) * 0.3 * np.where(shell & ~seam, fan_fade(shape, cx), 1.0)
    h = bevel(seam, 10) * 0.6 + bevel(band, 10) * 0.6 + lines
    detail = height_normal(h, 2.2)
    save(compose((bx, by, bz), detail, mask), name)


def interior_backdrop():
    name = "Radome_Interior_Backdrop"
    rgb, mask = load(name)
    shape = mask.shape
    bx, by, bz = sphere(shape, shape[1] / 2.0, BASE_ROW, RX, RY, -1)
    floor = mask & (np.arange(shape[0])[:, None] >= BASE_ROW)
    bx[floor], by[floor], bz[floor] = 0.0, 0.0, 1.0
    lines = luminance(rgb, 1.2) * 0.3 * np.where(floor, 1.0, fan_fade(shape, shape[1] / 2.0))
    h = bevel(floor, 10) * 0.6 + lines
    save(compose((bx, by, bz), height_normal(h, 2.2), mask), name)


def silo():
    name = "XBand_Radar_Turntable_Silo"
    rgb, mask = load(name)
    base = cylinder_x(mask.shape, mask.shape[1] / 2.0, 900.0)
    h = bevel(mask, 14) * 0.5 + luminance(rgb, 1.2) * 0.35
    save(compose(base, height_normal(h, 2.2), mask), name)


def bevelled(name, radius, detail_sigma, bulge=0.0):
    """Bevelled silhouette; bulge > 0 also curves the whole face gently toward the viewer."""
    rgb, mask = load(name)
    base = flat(mask.shape)
    if bulge > 0.0:
        ys, xs = np.nonzero(mask)
        cx, cy = (xs.min() + xs.max()) / 2.0, (ys.min() + ys.max()) / 2.0
        r = max(xs.max() - xs.min(), ys.max() - ys.min()) / 2.0
        sx, sy, sz = sphere(mask.shape, cx, cy, r, r, +1)
        base = (sx * bulge, sy * bulge, sz)
    h = bevel(mask, radius) * 0.65 + luminance(rgb, detail_sigma) * 0.35
    save(compose(base, height_normal(h, 2.4), mask), name)


if __name__ == "__main__":
    shutter("Radome_Shutter_Left", left=True)
    shutter("Radome_Shutter_Right", left=False)
    interior_backdrop()
    silo()
    # The antenna's orange mesh is a fine dot pattern; blur it more so it reads as texture, not noise.
    bevelled("XBand_Radar_Octagonal_Face", radius=70, detail_sigma=2.0, bulge=0.35)
    bevelled("XBand_Radar_Pedestal_Gantry", radius=22, detail_sigma=1.2)
