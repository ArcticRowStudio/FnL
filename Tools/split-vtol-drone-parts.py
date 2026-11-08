"""Splits the VTOL boss's escort drone (Vtol_EscortDrone_P1/P2.png) into rig parts, the same way
Tools/split-vtol-parts.py splits the boss, and renders spin frames for its two fans.

  body    - everything left over, same canvas and pivot as the original sprite
  fan_l   - far fan pod + flame, BEHIND the body
  fan_r   - near fan pod + flame, IN FRONT of the body (the body side it hid is inpainted)
  gun_l   - far twin gatling, behind the body
  gun_r   - near twin gatling, in front (the body underside it hid is inpainted)
  antenna - whip antenna on the top

Writes Assets/Art/Sprites/Boss/Vtol/Drone/Drone_<part>_P<1|2>.png and
Drone_Fan_<L|R>_P<1|2>_<n>.png (+ .meta), and prints every joint and point as offsets from the
drone's pivot for BossSupportDrone.

    python Tools/split-vtol-drone-parts.py            # build
    python Tools/split-vtol-drone-parts.py --preview  # rest pose + swung pose in TEMP
"""
import importlib.util
import os
import sys

import cv2
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))


def _load(name, file):
    spec = importlib.util.spec_from_file_location(name, os.path.join(HERE, file))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


split_tool = _load("split_vtol_parts", "split-vtol-parts.py")
fan_tool = _load("build_vtol_fan_frames", "build-vtol-fan-frames.py")

ROOT = os.path.dirname(HERE)
ART = os.path.join(ROOT, "Assets", "Art", "Sprites", "Boss", "Vtol")
OUT = os.path.join(ART, "Drone")
PPU = 330
PIVOT = (0.5, 0.5)

FAN_L = [(5, 200), (25, 160), (70, 138), (130, 130), (185, 135), (232, 150), (250, 180), (248, 205),
         (212, 215), (206, 300), (205, 390), (198, 470), (160, 545), (95, 545), (65, 470), (40, 395),
         (10, 330), (5, 270)]
FAN_R = [(552, 335), (600, 316), (720, 316), (800, 328), (824, 360), (824, 480), (800, 540), (790, 640),
         (770, 782), (700, 782), (640, 700), (600, 620), (585, 555), (556, 520), (550, 420)]
ANTENNA = [(405, 0), (495, 0), (495, 30), (440, 135), (425, 152), (405, 152)]
# Guns are picked by solid component: a component belongs to a gun when its centre is inside.
GUN_L = [(40, 520), (270, 515), (300, 600), (385, 610), (385, 690), (40, 700)]
GUN_R = [(240, 640), (400, 560), (470, 545), (575, 535), (575, 640), (540, 720), (500, 782), (240, 782)]

JOINTS = {
    "fan_l": (205.0, 280.0),
    "fan_r": (575.0, 420.0),
    "gun_l": (240.0, 545.0),
    "gun_r": (480.0, 560.0),
    "antenna": (428.0, 148.0),
}
POINTS = {
    "nozzle_l": (125.0, 380.0),
    "nozzle_r": (690.0, 560.0),
    "muzzle_l": (48.0, 658.0),
    "muzzle_r": (248.0, 735.0),
}
FANS = {
    "L": dict(center=(125.0, 207.0), a=86.0, b=48.0, angle=-6.0, cone=((128.0, 183.0), 16.0, 26.0)),
    "R": dict(center=(688.0, 395.0), a=98.0, b=46.0, angle=-4.0, cone=((680.0, 372.0), 16.0, 24.0)),
}
BLADES = 6


def split(img):
    rgba = np.asarray(img.convert("RGBA")).copy()
    h, w = rgba.shape[:2]
    alpha = rgba[..., 3] > 0
    luminance = rgba[..., :3].astype(np.int32).sum(axis=2) / 3
    fringe = rgba[..., 3] < 250
    poly = lambda pts: split_tool.poly_mask(alpha.shape, pts)

    def with_ink(mask, grow=6):
        grown = cv2.dilate(mask.astype(np.uint8), np.ones((2 * grow + 1, 2 * grow + 1), np.uint8)) > 0
        return mask | (grown & alpha & ((luminance < 90) | fringe))

    solid = (rgba[..., 3] >= 128).astype(np.uint8)
    n, lab, stats, cents = cv2.connectedComponentsWithStats(solid, connectivity=4)

    def by_component(area_poly, max_area=60000):
        inside = poly(area_poly)
        m = np.zeros_like(alpha)
        for i in range(1, n):
            cx, cy = cents[i]
            if stats[i][4] < max_area and inside[int(round(cy)), int(round(cx))]:
                m |= lab == i
        return with_ink(m, 4)

    gun_l = by_component(GUN_L)
    gun_r = by_component(GUN_R) & ~gun_l
    fan_r = with_ink(poly(FAN_R) & alpha) & ~gun_r
    fan_l = with_ink(poly(FAN_L) & alpha) & ~gun_l
    antenna = poly(ANTENNA) & alpha

    def layer(mask):
        out = np.zeros_like(rgba)
        out[mask] = rgba[mask]
        return out

    parts = {name: layer(m) for name, m in
             (("fan_l", fan_l), ("fan_r", fan_r), ("gun_l", gun_l), ("gun_r", gun_r), ("antenna", antenna))}

    body = rgba.copy()
    for m in (fan_l, fan_r, gun_l, gun_r, antenna):
        body[m] = 0

    # What the near fan and gun covered on the body: fill the hole where it sits against the body.
    body_alpha = body[..., 3] > 0
    near_body = cv2.dilate(body_alpha.astype(np.uint8), np.ones((17, 17), np.uint8)) > 0
    ys, xs = np.mgrid[0:h, 0:w]
    hole = ((fan_r & (xs < 700) & (ys < 500)) | (gun_r & (ys < 620))) & near_body & ~body_alpha
    body = split_tool.inpaint_rgba(body, hole, 6)

    parts["body"] = body
    return parts


def local(px, size):
    pivot_px = (size[0] * PIVOT[0], size[1] * (1 - PIVOT[1]))
    return ((px[0] - pivot_px[0]) / PPU, (pivot_px[1] - px[1]) / PPU)


def preview(parts, size):
    def compose(ang_fan, ang_gun, ang_ant):
        canvas = Image.new("RGBA", size, (40, 44, 52, 255))
        for name, ang in (("fan_l", ang_fan), ("gun_l", ang_gun), ("body", 0), ("antenna", ang_ant),
                          ("gun_r", ang_gun), ("fan_r", ang_fan)):
            layer = Image.fromarray(parts[name], "RGBA")
            if ang:
                layer = layer.rotate(ang, resample=Image.NEAREST, center=JOINTS[name])
            canvas.alpha_composite(layer)
        return canvas

    shots = [compose(0, 0, 0), compose(25, -12, 15), compose(-25, 12, -15)]
    sheet = Image.new("RGBA", (size[0] * 3, size[1]))
    for i, s in enumerate(shots):
        sheet.paste(s, (i * size[0], 0))
    path = os.path.join(os.environ.get("TEMP", "."), "vtol_drone_preview.png")
    sheet.save(path)
    print(path)


def main():
    os.makedirs(OUT, exist_ok=True)
    fan_tool.BLADES = BLADES
    fan_tool.PPU = PPU
    fan_tool.BODY_PIVOT = PIVOT

    for phase in (1, 2):
        src = Image.open(os.path.join(ART, f"Vtol_EscortDrone_P{phase}.png"))
        size = src.size
        parts = split(src)
        if "--preview" in sys.argv:
            if phase == 1:
                preview(parts, size)
            continue

        for name, arr in parts.items():
            if name == "body":
                img, pivot = Image.fromarray(arr, "RGBA"), PIVOT
            else:
                img, pivot = split_tool.crop_with_pivot(arr, JOINTS[name])
            split_tool.PPU = PPU
            split_tool.write_sprite(img, pivot, os.path.join(OUT, f"Drone_{name}_P{phase}.png"))

        for side, fan in FANS.items():
            frames, fan_local = fan_tool.ellipse_frames(src.convert("RGBA"), fan)
            for i, fr in enumerate(frames):
                fan_tool.write_png(fr, os.path.join(OUT, f"Drone_Fan_{side}_P{phase}_{i}.png"))
            if phase == 1:
                print(f"  fan_{side.lower()} centre: ({fan_local[0]:.3f}f, {fan_local[1]:.3f}f)")

    if "--preview" in sys.argv:
        return
    print("Offsets from the drone pivot (units):")
    for name, p in list(JOINTS.items()) + list(POINTS.items()):
        x, y = local(p, size)
        print(f"  {name}: ({x:.3f}f, {y:.3f}f)")


if __name__ == "__main__":
    main()
