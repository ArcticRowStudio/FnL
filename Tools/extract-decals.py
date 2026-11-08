"""Second pass. The first one extracted cleanly and produced decals nobody can see.

The mistake was the definition. A feature was "the part that deviates from its local
surroundings" - but put back on a surface that is equally busy and the same colour family, it
has nothing left to deviate from. Rust laid on rust is invisible. Dotting to 64 colours then
finishes the job.

So visibility is not assumed here, it is measured. Every candidate is composited onto real
crops of the base tile, put through the same downscale-and-quantise the artist will apply, and
scored on how much of the wall it actually changes. Anything that survives that is worth
shipping; anything that does not is thrown away no matter how clean the cut was.
"""
import io, os, shutil, sys
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage

D    = r"C:\Users\mokos\Downloads"
DEST = r"C:\Users\mokos\My project\ArtSource\Decals"
BASE = r"C:\Users\mokos\My project\ArtSource\Tileable\wall_tile_1024.png"
SOURCES = {"hull":   "선박 벽면.jpeg",
           "radar":  "X선_밴드_레이더_기지_벽_2K_202609061958.jpeg",
           "radar2": "X-ray_radar_base_wall_texture_2K_202609062000.jpeg"}

DOT_DIV, DOT_COLS = 4, 64     # the artist's pipeline: 1024 -> 256, then 64 colours
VIS_MIN  = 6.0                # mean channel change over the footprint, after dotting
MIN_AREA, PAD = 2200, 6
base_img = Image.open(BASE).convert("RGB")
base = np.asarray(base_img, np.float32)


def blur(a, s):
    return np.asarray(Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
                      .filter(ImageFilter.GaussianBlur(s)), np.float32)


def dot(rgb):
    """Exactly what the artist does to it: integer downscale, then quantise to 64 colours."""
    im = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8))
    im = im.resize((max(1, im.width // DOT_DIV), max(1, im.height // DOT_DIV)), Image.LANCZOS)
    return np.asarray(im.quantize(DOT_COLS, method=Image.MEDIANCUT).convert("RGB"), np.float32)


def visibility(px):
    """Mean per-channel change the decal makes to the wall, measured after dotting."""
    h, w = px.shape[:2]
    if h < 24 or w < 24 or h > base.shape[0] or w > base.shape[1]:
        return 0.0
    rgb, a = px[:, :, :3], px[:, :, 3:4] / 255.0
    scores = []
    for oy, ox in [(40, 40), (300, 520), (620, 180)]:
        oy = min(oy, base.shape[0] - h); ox = min(ox, base.shape[1] - w)
        bg = base[oy:oy+h, ox:ox+w]
        over = bg * (1 - a) + rgb * a
        d0, d1 = dot(bg), dot(over)
        n = min(d0.shape[0], d1.shape[0]), min(d0.shape[1], d1.shape[1])
        scores.append(np.abs(d0[:n[0], :n[1]] - d1[:n[0], :n[1]]).mean())
    return float(np.mean(scores))


def detectors(rgb):
    R, G, B = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    lum = 0.299 * R + 0.587 * G + 0.114 * B
    bg = blur(lum, 70)
    sat = rgb.max(axis=2) - rgb.min(axis=2)
    # Only strong value contrast survives dotting, so the gates are absolute, not percentile.
    return {"Hole":  (bg - lum) > 38,                       # deep shadow, gouges, dark holes
            "Paint": (sat > 55) & (R - B > 55) & (lum > 95),  # bright peeling paint, the loud bits
            "Scar":  (lum - bg) > 34}                        # bare bright metal, scrapes


def cut(rgb, mask, idx_mask, name, out):
    ys, xs = np.where(idx_mask)
    h, w = mask.shape
    x0, x1 = max(0, xs.min()-PAD), min(w, xs.max()+1+PAD)
    y0, y1 = max(0, ys.min()-PAD), min(h, ys.max()+1+PAD)
    soft = blur(idx_mask[y0:y1, x0:x1].astype(np.float32)*255, 2.5) / 255.0
    px = np.dstack([rgb[y0:y1, x0:x1], np.clip(soft*1.15, 0, 1)*255]).astype(np.uint8)
    v = visibility(px.astype(np.float32))
    if v < VIS_MIN:
        return None, v
    Image.fromarray(px, "RGBA").save(os.path.join(out, name + ".png"))
    return (x1-x0, y1-y0), v


def main():
    for c in ["Rust", "Stain", "Drip"]:
        p = os.path.join(DEST, c)
        if os.path.isdir(p):
            shutil.rmtree(p)
    for c in ["Hole", "Paint", "Scar"]:
        p = os.path.join(DEST, c)
        if os.path.isdir(p):
            shutil.rmtree(p)
        os.makedirs(p)

    kept = {c: [] for c in ["Hole", "Paint", "Scar"]}
    rejected = 0
    for tag, fn in SOURCES.items():
        path = os.path.join(D, fn)
        if not os.path.exists(path):
            continue
        rgb = np.asarray(Image.open(path).convert("RGB"), np.float32)
        h, w = rgb.shape[:2]
        print("=== %s ===" % tag)
        for cat, m in detectors(rgb).items():
            m = ndimage.binary_opening(m, np.ones((3, 3)))
            m = ndimage.binary_closing(m, np.ones((11, 11)))
            lab, n = ndimage.label(m)
            if n == 0:
                print("   %-6s no candidates" % cat); continue
            sizes = ndimage.sum(np.ones_like(lab), lab, range(1, n+1))
            made = 0
            for idx in np.argsort(sizes)[::-1] + 1:
                if len(kept[cat]) >= 12:
                    break
                area = sizes[idx-1]
                if area < MIN_AREA or area > 0.05 * h * w:
                    continue
                nm = "%s_%s_%02d" % (tag, cat.lower(), len(kept[cat]) + 1)
                size, v = cut(rgb, m, lab == idx, nm, os.path.join(DEST, cat))
                if size is None:
                    rejected += 1
                    continue
                kept[cat].append((nm, size, v)); made += 1
            print("   %-6s kept %d" % (cat, made))

    print("\nrejected as invisible after dotting: %d" % rejected)
    for c, rows in kept.items():
        if rows:
            print("%-6s %2d  visibility %.1f-%.1f (threshold %.1f)"
                  % (c, len(rows), min(r[2] for r in rows), max(r[2] for r in rows), VIS_MIN))
        else:
            print("%-6s  none survived" % c)


main()
