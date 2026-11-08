"""
SYNOPSIS
    Splits layered PSD art into one trimmed PNG per layer.

DESCRIPTION
    Reads every pixel layer out of a PSD with its real alpha and its bounding box on the
    canvas, writes each one trimmed to its own content, and records the boxes in a manifest.
    Because the box is kept, the pieces can be put back exactly where the artist drew them:
    placement costs nothing and needs no matching.

    Layers whose pixels are byte for byte identical to one already written are collapsed to a
    single file and noted in the manifest as duplicates. The same rack drawn into three
    different PSDs becomes one asset.

    Use PSD, not .clip. A .clip only gives up its low mipmaps to code; its full resolution
    layers are tiled in Clip Studio's own codec. Export PSD from Clip Studio instead, or use
    "레이어 -> 모두 내보내기" to write every layer as a full canvas PNG.

    Do NOT reach for PIL here. PIL reports the right layer count and names but decodes every
    layer past the first to garbage, and its convert("RGBA") fills alpha opaque, so the
    result looks plausible until you actually view it.

REQUIRES
    pip install --user psd-tools

EXAMPLE
    Split every prop PSD in a folder, skipping the opaque paper layers Clip Studio adds.

    python Tools/split-psd-layers.py --source "C:/Users/mokos/Desktop/FNL/배치할 것들" \
        --out ArtSource/Props

EXAMPLE
    A single file, keeping layers the artist had hidden.

    python Tools/split-psd-layers.py --source "Level 0.psd" --out ArtSource/Level0 \
        --include-hidden
"""
import argparse
import hashlib
import io
import json
import os
import sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

try:
    from psd_tools import PSDImage
except ImportError:
    sys.exit("psd-tools is required:  pip install --user psd-tools")

from PIL import Image

Image.MAX_IMAGE_PIXELS = None

# Clip Studio writes an opaque full-canvas paper layer into every export. It is not art.
DEFAULT_SKIP = "배경,용지"


def safe(name):
    return "".join(c if c.isalnum() or c in " _-()" else "_" for c in name).strip() or "layer"


def psd_files(source):
    if os.path.isfile(source):
        return [source]
    return [os.path.join(source, f) for f in sorted(os.listdir(source))
            if f.lower().endswith((".psd", ".psb"))]


def main():
    ap = argparse.ArgumentParser(description="Split PSD layers into trimmed PNGs.")
    ap.add_argument("--source", required=True, help="A .psd/.psb file, or a folder of them.")
    ap.add_argument("--out", required=True, help="Folder to write the PNGs and manifest into.")
    ap.add_argument("--skip-names", default=DEFAULT_SKIP,
                    help="Comma separated layer names to ignore (default: %s)." % DEFAULT_SKIP)
    ap.add_argument("--min-size", type=int, default=8,
                    help="Drop layers smaller than this on either side (default: 8).")
    ap.add_argument("--include-hidden", action="store_true",
                    help="Also export layers the artist left switched off.")
    ap.add_argument("--keep-duplicates", action="store_true",
                    help="Write repeated art once per occurrence instead of collapsing it.")
    args = ap.parse_args()

    skip = {s.strip() for s in args.skip_names.split(",") if s.strip()}
    os.makedirs(args.out, exist_ok=True)

    manifest = []
    seen = {}

    for path in psd_files(args.source):
        fname = os.path.basename(path)
        stem = os.path.splitext(fname)[0]
        try:
            psd = PSDImage.open(path)
        except Exception as e:
            print("%-34s OPEN FAILED %s" % (fname, e))
            continue

        kept = 0
        for i, layer in enumerate(psd.descendants()):
            if layer.kind != "pixel" or layer.name in skip:
                continue
            if not layer.visible and not args.include_hidden:
                continue
            try:
                img = layer.composite()
            except Exception as e:
                print("   %s / %s composite failed: %s" % (fname, layer.name, e))
                continue
            if img is None:
                continue

            img = img.convert("RGBA")
            inner = img.getbbox()
            if inner is None:
                continue
            img = img.crop(inner)
            if img.width < args.min_size or img.height < args.min_size:
                continue

            digest = hashlib.md5(img.tobytes()).hexdigest()
            if not args.keep_duplicates and digest in seen:
                manifest.append(dict(source=fname, layer=layer.name, index=i,
                                     duplicate_of=seen[digest]))
                continue

            box = layer.bbox
            name = "%s__%02d_%s" % (safe(stem), i, safe(layer.name))
            seen[digest] = name
            img.save(os.path.join(args.out, name + ".png"))
            manifest.append(dict(
                source=fname, layer=layer.name, index=i, file=name + ".png",
                canvas=list(psd.size),
                # Absolute position on the original canvas, after the inner trim.
                bbox=[box[0] + inner[0], box[1] + inner[1],
                      box[0] + inner[2], box[1] + inner[3]],
                size=[img.width, img.height]))
            kept += 1

        print("%-34s -> %2d layers" % (fname, kept))

    with open(os.path.join(args.out, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=1)

    written = [m for m in manifest if "file" in m]
    dupes = [m for m in manifest if "duplicate_of" in m]
    area = sum(m["size"][0] * m["size"][1] for m in written)
    print()
    print("%d layers written, %d duplicates collapsed" % (len(written), len(dupes)))
    print("%.1f M pixels total -> manifest.json in %s" % (area / 1e6, args.out))


if __name__ == "__main__":
    main()
