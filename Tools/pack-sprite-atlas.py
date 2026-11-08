"""
SYNOPSIS
    Packs loose PNGs into one sprite sheet plus a rect table Unity can slice from.

DESCRIPTION
    Takes the output of split-psd-layers.py and lays it into a single square sheet, writing
    the sheet next to a CSV of "name,x,y,w,h" rows. One texture asset then expands into as
    many named sprites in Unity, each of which can be dragged straight into a scene.

    The CSV measures y downward from the top of the sheet. Unity sprite rects measure upward
    from the bottom, so whatever consumes this has to mirror the row:

        rect = new Rect(x, atlas - y - h, w, h)

    Getting that backwards produces sprites that look sliced apart rather than obviously
    broken, so check a handful across the whole sheet, not just the first row.

    Scale exists because layer art is usually drawn far denser than the background it will
    sit against. This project's level art is capped to 2048 from a 15360 source, so props
    authored on a 4096 canvas lose nothing visible at half size.

    Full canvas layers - wall textures, backdrops - and animation frame sequences do not
    belong in a prop sheet. --max-size and --exclude keep them out.

EXAMPLE
    Pack the props, leaving out full canvas walls and any VFX frame sequences. The two
    Korean named PSDs need a Latin sprite name, or every layer from them lands as "_02".
    This is the command that produced Assets/Art/Props/Props.png.

    python Tools/pack-sprite-atlas.py --source ArtSource/Props \
        --out Assets/Art/Props/Props.png --scale 0.5 --max-size 4000x1700 --exclude Effect \
        --name-map "조종실 장비=CockpitGear,탁자=Table"

EXAMPLE
    Let it find the largest scale that still fits.

    python Tools/pack-sprite-atlas.py --source ArtSource/Props \
        --out Assets/Art/Props/Props.png --scale auto
"""
import argparse
import io
import json
import os
import sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from PIL import Image

Image.MAX_IMAGE_PIXELS = None


def load_manifest(folder):
    path = os.path.join(folder, "manifest.json")
    if not os.path.exists(path):
        return None
    with open(path, encoding="utf-8") as f:
        return [m for m in json.load(f) if "file" in m]


def wanted(entry, args):
    if args.exclude and any(x in entry["source"] for x in args.exclude):
        return False
    if args.max_size:
        mw, mh = args.max_size
        if entry["size"][0] >= mw and entry["size"][1] >= mh:
            return False
    return True


def ascii_name(entry, taken, name_map, warned):
    """Sprite names go into asset metadata, so keep them ASCII and readable.

    Stripping non-ASCII on its own is not enough: a wholly Korean filename reduces to
    nothing and every such layer ends up named "_02", "_03". Pass --name-map to give those
    files a Latin name; anything still left bare falls back to a generic one loudly rather
    than silently producing a sheet full of near-identical names.
    """
    base = os.path.splitext(entry["source"])[0].replace("(1)", "")
    for src, dst in name_map.items():
        base = base.replace(src, dst)

    clean = "".join(c for c in base if c.isascii() and (c.isalnum() or c == "_"))
    if not any(c.isalnum() for c in clean):
        if base not in warned:
            warned.add(base)
            print("  warning: '%s' has no ASCII name, using 'prop' - pass --name-map" % base)
        clean = "prop"

    name = "%s_%02d" % (clean.strip("_"), entry["index"])
    if name in taken:
        taken[name] += 1
        name = "%s_%d" % (name, taken[name])
    else:
        taken[name] = 0
    return name


def shelf_pack(boxes, width, pad):
    """boxes: (key, w, h) tallest first. Returns placements and the height used."""
    placed = {}
    x = y = shelf = 0
    for key, w, h in boxes:
        if x + w > width:
            x = 0
            y += shelf + pad
            shelf = 0
        placed[key] = (x, y)
        x += w + pad
        shelf = max(shelf, h)
    return placed, y + shelf


def attempt(items, scale, args):
    loaded, taken, warned = [], {}, set()
    for entry, path in items:
        im = Image.open(path).convert("RGBA")
        w = max(1, int(im.width * scale))
        h = max(1, int(im.height * scale))
        if w > args.size or h > args.size:
            return None
        loaded.append((ascii_name(entry, taken, args.name_map, warned),
                       im.resize((w, h), Image.LANCZOS), entry))

    boxes = sorted([(n, im.width, im.height) for n, im, _ in loaded], key=lambda b: -b[2])
    placed, used = shelf_pack(boxes, args.size, args.padding)
    if used > args.size:
        return None
    return loaded, placed, used


def main():
    ap = argparse.ArgumentParser(description="Pack PNGs into one Unity sprite sheet.")
    ap.add_argument("--source", required=True, help="Folder written by split-psd-layers.py.")
    ap.add_argument("--out", required=True, help="Sheet path, e.g. Assets/Art/Props/Props.png.")
    ap.add_argument("--size", type=int, default=4096, help="Sheet edge in pixels (default 4096).")
    ap.add_argument("--scale", default="1.0", help="Scale factor, or 'auto' to fit (default 1.0).")
    ap.add_argument("--padding", type=int, default=4, help="Gap between sprites (default 4).")
    ap.add_argument("--max-size", help="Drop layers at least this big, e.g. 4000x1700.")
    ap.add_argument("--exclude", nargs="*", default=[],
                    help="Skip entries whose source filename contains any of these.")
    ap.add_argument("--name-map", default="",
                    help="Rename source stems for sprite naming, e.g. '탁자=Table,의자=Chair'. "
                         "Needed for filenames with no ASCII in them at all.")
    args = ap.parse_args()

    if args.max_size:
        w, h = args.max_size.lower().split("x")
        args.max_size = (int(w), int(h))

    args.name_map = dict(
        pair.split("=", 1) for pair in args.name_map.split(",") if "=" in pair)

    manifest = load_manifest(args.source)
    if manifest is None:
        sys.exit("no manifest.json in %s - run split-psd-layers.py first" % args.source)

    items = [(m, os.path.join(args.source, m["file"]))
             for m in manifest if wanted(m, args)]
    items.sort(key=lambda t: (t[0]["source"], t[0]["index"]))
    if not items:
        sys.exit("nothing left to pack after filtering")
    print("packing %d of %d layers" % (len(items), len(manifest)))

    if args.scale == "auto":
        result = None
        for s in [1.0, 0.85, 0.75, 0.65, 0.5, 0.4, 0.35, 0.3, 0.25]:
            result = attempt(items, s, args)
            if result:
                print("auto scale settled on %.2f" % s)
                args.scale = s
                break
        if not result:
            sys.exit("does not fit even at 0.25 - raise --size or split the set")
    else:
        args.scale = float(args.scale)
        result = attempt(items, args.scale, args)
        if not result:
            sys.exit("does not fit at scale %.2f - lower it or raise --size" % args.scale)

    loaded, placed, used = result
    sheet = Image.new("RGBA", (args.size, args.size), (0, 0, 0, 0))
    rows = []
    for name, im, _ in loaded:
        x, y = placed[name]
        sheet.paste(im, (x, y), im)
        rows.append((name, x, y, im.width, im.height))

    os.makedirs(os.path.dirname(os.path.abspath(args.out)) or ".", exist_ok=True)
    sheet.save(args.out)

    csv_path = os.path.splitext(args.out)[0] + "-rects.csv"
    with open(csv_path, "w", encoding="ascii") as f:
        f.write("%d\n" % args.size)
        for r in rows:
            f.write("%s,%d,%d,%d,%d\n" % r)

    fill = 100.0 * sum(r[3] * r[4] for r in rows) / (args.size * args.size)
    print("%d sprites, %.1f%% of the sheet used, %.1f MB" %
          (len(rows), fill, os.path.getsize(args.out) / 1e6))
    print("sheet: %s" % args.out)
    print("rects: %s   (y is from the top - mirror it for Unity)" % csv_path)


if __name__ == "__main__":
    main()
