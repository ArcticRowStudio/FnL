"""Re-dot coarse pixel art onto a finer grid without losing what was painted over it.

The Level -4 pipes were mosaicked at 25 source px per dot on the 100 px/unit level canvas,
i.e. 4 dots per world unit, eight times coarser than the 32 px/unit level-art grid. There is
no high resolution original (backup_original holds the same 25 px mosaic), so the finer dots
have to be made from the coarse ones:

1. Fit the dot grid (pitch + phase) on the clean mosaic in backup_original and read one
   colour per dot from the cell centre, never the blended boundary pixels.
2. The shipped PNGs were colour-adjusted after that backup and had details painted on at
   full resolution (junction box, gauge, text, meters). Map every backup colour to the colour
   it became, which recolours the dots, and treat whatever the map does not explain as
   painted detail.
3. Upscale the dots 8x with Scale2x three times: edges and diagonals get finer steps, flat
   colours stay flat, and no new colours are invented.
4. Shrink the detail layer onto the same grid (premultiplied box filter) and lay it on top.

PPU is scaled by the same factor as the pixels and sprite rects are scaled in the .meta, so
nothing moves in the world: only the dot size changes.

Scale2x cannot know what a curve was, so round things (pipe bends) keep the bumps the mosaic
gave them. When the un-mosaicked painting is available, --painting rebuilds the base from it
instead (steps 1 and 3 are replaced): `Desktop/FNL/배치할 것들/Level -4_1.psd`, layer
"레이어 1 복사", whose sprite-named layers carry the exact crop boxes. Tracing or blurring the
dots was tried and either faceted the bends or melted the shapes.

    python Tools/refine-dot-art.py Pipe01 Pipe02 ...          (names in Level -4)
    python Tools/refine-dot-art.py --dry-run Pipe06
"""
import argparse, datetime, os, re, shutil, sys
import cv2
import numpy as np
from PIL import Image
from scipy import ndimage

Image.MAX_IMAGE_PIXELS = None
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FOLDER = os.path.join(ROOT, "Assets", "Environment", "Sprites", "Level -4")
NORMALS = os.path.join(FOLDER, "Normals")  # *_Normal.png live here, not beside the art
CLEAN = os.path.join(FOLDER, "backup_original")
BACKUP_ROOT = os.path.join(ROOT, "ArtSource", "DotRefine_backup")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from generate_normal_maps import create_normal_map  # noqa: E402

UPSCALE = 8              # 4 dots/unit * 8 = 32 px/unit
DETAIL_THRESHOLD = 48    # summed RGBA difference that counts as painted detail
SOURCE_PPU = 100.0
SOURCE_NORMAL_BEVEL = 8.0  # what generate_normal_maps.py used for pipes at 100 px/unit


# ── dot grid ────────────────────────────────────────────────────────────────

def transitions(a, axis):
    d = np.abs(np.diff(a.astype(np.int32), axis=axis)).sum(axis=2)
    return d.sum(axis=1 - axis)


def fit_pitch(signal, lo=20.0, hi=30.0, step=0.002):
    """Boundaries sit at phase + k*pitch; pick the pitch whose phases agree best."""
    x = np.arange(len(signal)) + 1.0
    w = signal.astype(np.float64)
    best = (0.0, 0.0, -1.0)
    for p in np.arange(lo, hi, step):
        ang = 2 * np.pi * x / p
        c, s = (w * np.cos(ang)).sum(), (w * np.sin(ang)).sum()
        r = np.hypot(c, s)
        if r > best[2]:
            best = (p, (np.arctan2(s, c) / (2 * np.pi) * p) % p, r)
    return best[0], best[1], best[2] / max(w.sum(), 1)


def dot_grid(a):
    px, phx, cx = fit_pitch(transitions(a, 1))
    py, phy, cy = fit_pitch(transitions(a, 0))
    pitch = (px + py) / 2
    h, w = a.shape[:2]
    x0 = phx - pitch * np.ceil(phx / pitch)   # start of the first, partial dot (<= 0)
    y0 = phy - pitch * np.ceil(phy / pitch)
    nx = int(np.ceil((w - x0) / pitch))
    ny = int(np.ceil((h - y0) / pitch))
    return pitch, x0, y0, nx, ny, min(cx, cy)


def sample_centres(a, pitch, x0, y0, nx, ny):
    h, w = a.shape[:2]
    cx = np.clip(np.round(x0 + (np.arange(nx) + 0.5) * pitch).astype(int), 0, w - 1)
    cy = np.clip(np.round(y0 + (np.arange(ny) + 0.5) * pitch).astype(int), 0, h - 1)
    return a[cy][:, cx]


# ── colour handling ─────────────────────────────────────────────────────────

def pack(a):
    a = a.astype(np.uint32)
    return a[..., 0] | (a[..., 1] << 8) | (a[..., 2] << 16) | (a[..., 3] << 24)


def unpack(p):
    return np.stack([(p >> s) & 255 for s in (0, 8, 16, 24)], -1).astype(np.uint8)


def colour_map(clean, current):
    """For each colour in the clean mosaic, the colour those pixels most often became."""
    k = (pack(clean).astype(np.uint64) << np.uint64(32)) | pack(current).astype(np.uint64)
    pairs, counts = np.unique(k.ravel(), return_counts=True)
    src = (pairs >> np.uint64(32)).astype(np.uint32)
    dst = (pairs & np.uint64(0xFFFFFFFF)).astype(np.uint32)
    order = np.lexsort((-counts, src))           # by source, most frequent first
    src, dst = src[order], dst[order]
    first = np.ones(len(src), bool); first[1:] = src[1:] != src[:-1]
    return dict(zip(src[first].tolist(), dst[first].tolist()))


def apply_map(a, mapping):
    p = pack(a)
    keys = np.array(list(mapping.keys()), np.uint32)
    vals = np.array(list(mapping.values()), np.uint32)
    idx = np.searchsorted(keys, p)               # keys come out of np.unique sorted
    idx = np.clip(idx, 0, len(keys) - 1)
    out = np.where(keys[idx] == p, vals[idx], p)
    return unpack(out)


# ── upscale ─────────────────────────────────────────────────────────────────

def contrast(p, q):
    a, b = unpack(p).astype(np.int32), unpack(q).astype(np.int32)
    return np.abs(a - b).sum(-1)


def scale2x(p, min_contrast):
    """Scale2x (EPX). With min_contrast > 0 a corner is only rounded where the colour taking
    it over differs strongly from the dot itself: outlines and silhouettes get the finer
    steps, while low-contrast surface texture keeps square dots instead of melting into
    camouflage-like blobs."""
    a = np.pad(p, 1, mode="edge")
    up, down = a[:-2, 1:-1], a[2:, 1:-1]
    left, right = a[1:-1, :-2], a[1:-1, 2:]

    def ok(cond, repl):
        return cond if min_contrast <= 0 else cond & (contrast(repl, p) >= min_contrast)

    o = np.empty((p.shape[0] * 2, p.shape[1] * 2), p.dtype)
    o[0::2, 0::2] = np.where(ok((left == up) & (left != down) & (up != right), up), up, p)
    o[0::2, 1::2] = np.where(ok((up == right) & (up != left) & (right != down), right), right, p)
    o[1::2, 0::2] = np.where(ok((down == left) & (down != right) & (left != up), left), left, p)
    o[1::2, 1::2] = np.where(ok((right == down) & (right != up) & (down != left), down), down, p)
    return o


def upscale_dots(dots, factor, min_contrast=0):
    p = pack(dots)
    while factor > 1:
        p = scale2x(p, min_contrast); factor //= 2
    return unpack(p)


_psd_cache = {}


def psd_original(psd_path, layer_name, sprite_name):
    """Crop of the un-mosaicked painting under one sprite. The sprite layers in the PSD
    share the level canvas, so their bbox is exactly where the shipped PNG was cut from."""
    from psd_tools import PSDImage
    if psd_path not in _psd_cache:
        psd = PSDImage.open(psd_path)
        layers = {l.name: l for l in psd.descendants()}
        src = layers[layer_name]
        _psd_cache[psd_path] = (layers, np.asarray(src.composite().convert("RGBA")), src.bbox)
    layers, img, (ox, oy, _, _) = _psd_cache[psd_path]
    x0, y0, x1, y1 = layers[sprite_name].bbox
    crop = np.zeros((y1 - y0, x1 - x0, 4), np.uint8)
    sy0, sx0 = max(0, y0 - oy), max(0, x0 - ox)
    sy1, sx1 = min(img.shape[0], y1 - oy), min(img.shape[1], x1 - ox)
    crop[sy0 + oy - y0:sy1 + oy - y0, sx0 + ox - x0:sx1 + ox - x0] = img[sy0:sy1, sx0:sx1]
    return crop


def pipe_mask(orig, mosaic_alpha, pitch):
    """Where the pipe is in the painting. Well inside the mosaic silhouette it is pipe; in
    the uncertain band along that blocky edge, the painting decides: yellow paint and the
    black outline belong to the pipe, white background and grey walls do not."""
    rgb = orig[..., :3].astype(np.float32)
    lum = rgb @ np.array([0.299, 0.587, 0.114], np.float32)
    mx, mn = rgb.max(-1), rgb.min(-1)
    sat = (mx - mn) / np.maximum(mx, 1)
    opaque = orig[..., 3] > 128
    paint = opaque & (((sat > 0.3) & (mx > 80)) | (lum < 30))
    # white is background even where a mosaic dot covered it
    background = (sat < 0.12) & (lum > 200)
    r = int(round(pitch * 0.55))
    core = ndimage.binary_erosion(mosaic_alpha, iterations=r)
    reach = ndimage.binary_dilation(mosaic_alpha, iterations=r)
    disk = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7)).astype(bool)
    m = (ndimage.binary_closing(paint, structure=disk) | (core & ~background)) & reach
    comps, n = ndimage.label(m)
    keep = np.unique(comps[core & m])
    return np.isin(comps, keep[keep > 0])


def object_mask(orig, mosaic_alpha, pitch):
    """Where a machine is in the painting, for art that is not one paint colour (turbines:
    browns and greys, standing in front of grey walls and touching other machines). Colour
    cannot separate it, the drawn outline can: grow outward from well inside the mosaic
    silhouette through anything that is not background and not dark, stop at the dark
    outline, then take that outline too. The growth never leaves the uncertain band."""
    rgb = orig[..., :3].astype(np.float32)
    lum = rgb @ np.array([0.299, 0.587, 0.114], np.float32)
    mx, mn = rgb.max(-1), rgb.min(-1)
    sat = (mx - mn) / np.maximum(mx, 1)
    opaque = orig[..., 3] > 128
    background = ~opaque | ((sat < 0.12) & (lum > 200))
    barrier = opaque & (lum < 45)
    r = int(round(pitch * 0.55))
    core = ndimage.binary_erosion(mosaic_alpha, iterations=r) & ~background
    reach = ndimage.binary_dilation(mosaic_alpha, iterations=r)
    free = reach & ~background & ~barrier
    comps, _ = ndimage.label(free | core)
    keep = np.unique(comps[core])
    grown = np.isin(comps, keep[keep > 0])
    outline = barrier & reach & ndimage.binary_dilation(grown, iterations=4)
    m = grown | outline
    return ndimage.binary_closing(m, structure=np.ones((3, 3), bool)) & reach & ~background


MASKS = {"pipe": pipe_mask, "object": object_mask}


def from_painting(orig, mosaic_alpha, pitch, size, palette, outline_px=2.0, mask="pipe"):
    """Painting -> target grid: masked, area-downscaled, alpha cut hard, and every pixel
    snapped to the sprite's own shipped colours so it stays flat pixel art. The painting's
    thin black outline mostly averages away at this size, so a dark rim is redrawn along the
    silhouette in the darkest shipped colour, as the rest of the level art has."""
    m = MASKS[mask](orig, mosaic_alpha, pitch)
    src = orig.copy()
    src[..., 3] = np.where(m, 255, 0)
    small = box_down(src, size)
    solid = small[..., 3] >= 128
    pal = palette.reshape(-1, 1, 3).astype(np.uint8)
    pal_lab = cv2.cvtColor(pal, cv2.COLOR_RGB2LAB).reshape(-1, 3).astype(np.float32)
    rgb = np.clip(small[..., :3], 0, 255).astype(np.uint8)
    lab = cv2.cvtColor(rgb, cv2.COLOR_RGB2LAB).reshape(-1, 3).astype(np.float32)
    idx = np.empty(len(lab), np.int32)
    for i in range(0, len(lab), 200000):
        d = ((lab[i:i + 200000, None, :] - pal_lab[None]) ** 2).sum(-1)
        idx[i:i + 200000] = d.argmin(1)
    out = np.zeros(small.shape, np.uint8)
    out[..., :3] = palette[idx].reshape(small.shape[0], small.shape[1], 3)
    out[..., 3] = np.where(solid, 255, 0)
    if outline_px > 0:
        dark = palette[(palette.astype(np.float32) @ np.array([0.299, 0.587, 0.114], np.float32)).argmin()]
        rim = solid & (ndimage.distance_transform_edt(solid) <= outline_px)
        out[rim, :3] = dark
    return out


def box_down(rgba, size):
    """Premultiplied area downscale so transparent pixels do not darken the edges."""
    im = Image.fromarray(rgba, "RGBA").convert("RGBa").resize(size, Image.BOX)
    return np.asarray(im.convert("RGBA")).astype(np.float32)


# ── meta ────────────────────────────────────────────────────────────────────

def rewrite_meta(path, fx, fy, new_w, new_h, colour, source_meta=None):
    """Scale the sprite rects and PPU of source_meta (default: path itself) into path."""
    text = open(source_meta or path, encoding="utf-8").read()
    head, sep, sheet = text.partition("  spriteSheet:")

    def scale_rect(m):
        ind = m.group(1)
        x, y, w, h = (float(m.group(i)) for i in (2, 3, 4, 5))
        nx0, nx1 = round(x * fx), round((x + w) * fx)
        ny0, ny1 = round(y * fy), round((y + h) * fy)
        return (f"{ind}x: {nx0}\n{ind}y: {ny0}\n{ind}width: {max(1, nx1 - nx0)}\n"
                f"{ind}height: {max(1, ny1 - ny0)}\n")

    sheet = re.sub(r"( +)x: ([-\d.e]+)\n\1y: ([-\d.e]+)\n\1width: ([-\d.e]+)\n\1height: ([-\d.e]+)\n",
                   scale_rect, sheet)
    head = re.sub(r"spritePixelsToUnits: [\d.]+", f"spritePixelsToUnits: {SOURCE_PPU * fx:.5f}", head)
    head = re.sub(r"filterMode: \d+", "filterMode: 0", head)
    max_size = 2048 if max(new_w, new_h) <= 2048 else 4096
    head = re.sub(r"maxTextureSize: \d+", f"maxTextureSize: {max_size}", head)
    if colour:  # block compression smears flat pixel art; normals are smooth and keep it
        head = re.sub(r"textureCompression: \d+", "textureCompression: 0", head)
    open(path, "w", encoding="utf-8", newline="\n").write(head + sep + sheet)


# ── main ────────────────────────────────────────────────────────────────────

def refine(name, dry_run, backup_dir, close_px=0, min_contrast=0, source_dir=None, painting=None,
           mask="pipe"):
    cur_path = os.path.join(FOLDER, name + ".png")
    # Re-running on art this script already converted: read the full-resolution PNG and
    # its unscaled .meta from a DotRefine_backup folder instead of the converted ones.
    src_dir = source_dir or FOLDER
    clean_path = os.path.join(CLEAN, name + ".png")
    current = np.asarray(Image.open(os.path.join(src_dir, name + ".png")).convert("RGBA"))
    clean = np.asarray(Image.open(clean_path).convert("RGBA"))
    if current.shape != clean.shape:
        raise SystemExit(f"{name}: current {current.shape} and backup {clean.shape} differ in size")
    h, w = clean.shape[:2]

    pitch, x0, y0, nx, ny, conc = dot_grid(clean)
    mapping = colour_map(clean, current)
    dots = apply_map(sample_centres(clean, pitch, x0, y0, nx, ny), mapping)

    expected = apply_map(clean, mapping)
    diff = np.abs(current.astype(np.int32) - expected.astype(np.int32)).sum(2)
    detail = diff > DETAIL_THRESHOLD
    if close_px > 0:
        # A painted panel is one region. Per pixel, some of its pixels happen to match the
        # old base colour and would let the upscaled mosaic show through as blotches.
        st = np.ones((close_px, close_px), bool)
        detail = ndimage.binary_fill_holes(ndimage.binary_closing(detail, structure=st))
        detail &= current[..., 3] > 0

    s = pitch / UPSCALE                       # source px per output px
    new_w, new_h = int(round(w / s)), int(round(h / s))
    if painting:
        # Rebuild from the un-mosaicked painting: real curves instead of guessed ones.
        psd_path, layer_name = painting
        palette = np.unique(pack(dots[dots[..., 3] >= 128]))
        palette = unpack(palette)[:, :3]
        orig = psd_original(psd_path, layer_name, name)
        base = from_painting(orig, clean[..., 3] >= 128, pitch, (new_w, new_h), palette,
                             mask=mask).astype(np.float32)
    else:
        up = upscale_dots(dots, UPSCALE, min_contrast)
        ox, oy = int(round(-x0 / s)), int(round(-y0 / s))
        up = np.pad(up, ((0, max(0, oy + new_h - up.shape[0])), (0, max(0, ox + new_w - up.shape[1])), (0, 0)))
        base = up[oy:oy + new_h, ox:ox + new_w].astype(np.float32)

    layer = current.copy()
    layer[~detail, 3] = 0
    d = box_down(layer, (new_w, new_h))
    da = d[..., 3:4] / 255.0
    out = base.copy()
    out[..., :3] = d[..., :3] * da + base[..., :3] * (1 - da)
    out[..., 3] = np.maximum(base[..., 3], d[..., 3])
    out = np.clip(np.round(out), 0, 255).astype(np.uint8)

    fx, fy = new_w / w, new_h / h
    print(f"{name}: {w}x{h} -> {new_w}x{new_h}  pitch {pitch:.3f}px (fit {conc:.2f})  "
          f"dots {nx}x{ny}  colours {len(mapping)}  detail {detail.mean() * 100:.1f}%  "
          f"ppu {SOURCE_PPU * fx:.3f}")
    if dry_run:
        return out

    os.makedirs(backup_dir, exist_ok=True)
    for suffix in (".png", ".png.meta", "_Normal.png", "_Normal.png.meta"):
        src = os.path.join(NORMALS if suffix.startswith("_Normal") else FOLDER, name + suffix)
        if os.path.exists(src):
            shutil.copy2(src, os.path.join(backup_dir, name + suffix))

    Image.fromarray(out, "RGBA").save(cur_path)
    rewrite_meta(cur_path + ".meta", fx, fy, new_w, new_h, colour=True,
                 source_meta=os.path.join(src_dir, name + ".png.meta"))

    normal_path = os.path.join(NORMALS, name + "_Normal.png")
    # A DotRefine_backup source folder is flat; the live folder keeps normals in Normals.
    normal_src_dir = NORMALS if src_dir == FOLDER else src_dir
    if os.path.exists(normal_path):
        create_normal_map(cur_path, normal_path, bevel_radius=SOURCE_NORMAL_BEVEL * fx, strength=2.2)
        rewrite_meta(normal_path + ".meta", fx, fy, new_w, new_h, colour=False,
                     source_meta=os.path.join(normal_src_dir, name + "_Normal.png.meta"))
    return out


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("names", nargs="+")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--preview", help="save the dry-run result of the first name here")
    ap.add_argument("--close", type=int, default=0,
                    help="close gaps in the painted-detail mask (source px); use for art with big painted panels")
    ap.add_argument("--edge-contrast", type=int, default=0,
                    help="only round corners across colour steps at least this strong (summed RGBA); 0 = plain Scale2x")
    ap.add_argument("--source-dir", help="read the full-resolution PNG + .meta from here (a DotRefine_backup stamp)")
    ap.add_argument("--mask", choices=sorted(MASKS), default="pipe",
                    help="with --painting: how to cut the sprite out (pipe = yellow paint, object = inside the drawn outline)")
    ap.add_argument("--painting", nargs=2, metavar=("PSD", "LAYER"),
                    help="rebuild from the un-mosaicked painting in this PSD layer (sprite bboxes come from "
                         "the PSD layers named like the sprites) instead of upscaling the dots")
    args = ap.parse_args()
    stamp = datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
    backup_dir = os.path.join(BACKUP_ROOT, stamp)
    for i, n in enumerate(args.names):
        out = refine(n, args.dry_run, backup_dir, args.close, args.edge_contrast, args.source_dir, args.painting,
                     args.mask)
        if args.preview and i == 0:
            Image.fromarray(out, "RGBA").save(args.preview)
    if not args.dry_run:
        print(f"backups: {backup_dir}")
