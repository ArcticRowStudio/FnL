import os
import math
import numpy as np
from PIL import Image, ImageDraw

out_dir = r"C:\Users\mokos\My project\Assets\Environment\Sprites\Architecture"
normal_dir = os.path.join(out_dir, "Normals")  # normal maps live in a Normals subfolder
os.makedirs(normal_dir, exist_ok=True)
os.makedirs(out_dir, exist_ok=True)

def generate_normal_from_height(height_map, alpha_mask, strength=4.0):
    h, w = height_map.shape
    normal = np.zeros((h, w, 4), dtype=np.uint8)
    dx = np.zeros_like(height_map)
    dy = np.zeros_like(height_map)
    dx[:, 1:-1] = (height_map[:, 2:] - height_map[:, :-2]) * 0.5
    dx[:, 0] = (height_map[:, 1] - height_map[:, 0])
    dx[:, -1] = (height_map[:, -1] - height_map[:, -2])

    dy_img = np.zeros_like(height_map)
    dy_img[1:-1, :] = (height_map[2:, :] - height_map[:-2, :]) * 0.5
    dy_img[0, :] = (height_map[1, :] - height_map[0, :])
    dy_img[-1, :] = (height_map[-1, :] - height_map[-2, :])
    dy = -dy_img

    nx = -dx * strength
    ny = -dy * strength
    nz = np.ones_like(height_map)

    norm = np.sqrt(nx*nx + ny*ny + nz*nz)
    nx /= norm
    ny /= norm
    nz /= norm

    normal[:, :, 0] = np.clip((nx * 0.5 + 0.5) * 255, 0, 255).astype(np.uint8)
    normal[:, :, 1] = np.clip((ny * 0.5 + 0.5) * 255, 0, 255).astype(np.uint8)
    normal[:, :, 2] = np.clip((nz * 0.5 + 0.5) * 255, 0, 255).astype(np.uint8)
    normal[:, :, 3] = alpha_mask.astype(np.uint8)
    return Image.fromarray(normal, mode='RGBA')

def make_long_waterproof_cable_fluorescent():
    # 256 wide x 384 high (3.0 Unity units tall) - REALLY LONG waterproof cables!
    w, h = 256, 384
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    left_cx = 48
    right_cx = 208

    # 1. Heavy Industrial Ceiling Anchor Plates & IP68 Waterproof Cable Glands (y=0 to 18)
    for bx in [left_cx, right_cx]:
        # Steel mounting base plate (x - 12 to x + 12, y=0 to 8)
        for y in range(0, 8):
            c = int(42 + y * 2.5)
            draw.line([(bx - 12, y), (bx + 12, y)], fill=(c, c + 3, c + 6, 255))
            height[y, bx - 12:bx + 13] = 0.85
        draw.rectangle([bx - 12, 0, bx + 12, 8], outline=(18, 20, 24, 255), width=1)
        # Hex bolts on flange
        draw.point((bx - 9, 4), fill=(180, 190, 200, 255))
        draw.point((bx + 9, 4), fill=(180, 190, 200, 255))

        # Heavy brass / black nylon waterproof compression gland (y=8 to 18, x - 6 to x + 6)
        for y in range(8, 18):
            for x in range(bx - 5, bx + 6):
                t = (x - (bx - 5)) / 10.0
                r = int(50 + math.sin(t * math.pi) * 40)
                g = int(45 + math.sin(t * math.pi) * 35)
                b = int(25 + math.sin(t * math.pi) * 15)
                draw.point((x, y), fill=(r, g, b, 255))
                height[y, x] = 0.9 + math.sin(t * math.pi) * 0.1
        draw.rectangle([bx - 5, 8, bx + 5, 18], outline=(15, 18, 22, 255), width=1)
        # Gland hex flats
        draw.line([(bx - 4, 13), (bx + 4, 13)], fill=(160, 140, 60, 255))
        # Rubber compression cone boot (y=18 to 22)
        draw.polygon([(bx - 4, 18), (bx + 4, 18), (bx + 3, 22), (bx - 3, 22)], fill=(20, 22, 26, 255))
        height[18:23, bx - 4:bx + 5] = 0.85

    # 2. REALLY LONG Heavy Black Waterproof Rubber Cables (y=22 to 320, over 300 pixels long!)
    # Thickness: 5px wide [cx-2, cx-1, cx, cx+1, cx+2]
    # Matte black rubber with subtle organic flexibility and cylindrical highlight
    for cable_idx, base_x in enumerate([left_cx, right_cx]):
        sway_dir = -1.0 if cable_idx == 0 else 1.0
        for y in range(22, 322):
            # Gentle organic flex/sway (max sway ~2.0px)
            cx = base_x + math.sin((y - 22) * 0.02) * 2.2 * sway_dir
            ix = int(round(cx))

            # 5-pixel thick cylindrical rubber cable
            # ix-2: dark outline shadow
            draw.point((ix - 2, y), fill=(12, 14, 16, 255))
            # ix-1: soft highlight on rounded rubber jacket
            draw.point((ix - 1, y), fill=(55, 62, 70, 255))
            # ix: main body black rubber
            draw.point((ix, y), fill=(38, 42, 48, 255))
            # ix+1: core shadow
            draw.point((ix + 1, y), fill=(24, 26, 30, 255))
            # ix+2: outer edge
            draw.point((ix + 2, y), fill=(10, 12, 14, 255))

            height[y, ix - 2:ix + 3] = 0.8
            height[y, ix - 1:ix + 1] = 0.9

            # Occasional yellow cable markers / warning wrap bands every 70px
            if (y - 30) % 75 in [0, 1, 2]:
                draw.line([(ix - 2, y), (ix + 2, y)], fill=(215, 185, 30, 255))
                height[y, ix - 2:ix + 3] = 0.95

    # Center secondary coiled black waterproof power cord snaking down from ceiling
    draw.rectangle([123, 0, 133, 10], fill=(35, 38, 44, 255), outline=(15, 18, 22, 255))
    height[0:11, 123:134] = 0.85
    for y in range(10, 320):
        cur_x = int(128 + math.sin(y * 0.08) * 9.0 * (1.0 - math.exp(-y * 0.02)))
        draw.point((cur_x - 1, y), fill=(14, 16, 20, 255))
        draw.point((cur_x, y), fill=(42, 46, 54, 255))
        draw.point((cur_x + 1, y), fill=(18, 20, 24, 255))
        height[y, cur_x - 1:cur_x + 2] = 0.82

    # 3. Waterproof Cable Glands & Strain Relief at luminaire top (y=316 to 326)
    for bx in [left_cx, right_cx]:
        draw.rectangle([bx - 5, 316, bx + 5, 324], fill=(45, 48, 55, 255), outline=(15, 18, 22, 255))
        draw.line([(bx - 4, 320), (bx + 4, 320)], fill=(170, 150, 70, 255)) # brass collar
        height[316:325, bx - 5:bx + 6] = 0.92

    # Center power entry gland (y=318 to 326, x=124 to 132)
    draw.rectangle([123, 318, 133, 326], fill=(30, 32, 38, 255), outline=(15, 18, 22, 255))
    height[318:327, 123:134] = 0.92

    # 4. Heavy Industrial Luminaire Body (y=324 to 365, x=24 to 232)
    # Ballast housing
    for y in range(324, 336):
        c = int(45 + math.sin((y - 324) * 0.28) * 18)
        draw.line([(26, y), (230, y)], fill=(c, c + 3, c + 5, 255))
        height[y, 26:231] = 0.85
    draw.rectangle([26, 324, 230, 336], outline=(18, 22, 26, 255), width=1)
    # Upper rim glint
    draw.line([(27, 325), (229, 325)], fill=(110, 120, 132, 255))

    # Center junction box with green status LED
    draw.rectangle([114, 320, 142, 336], fill=(32, 36, 44, 255), outline=(15, 18, 22, 255), width=1)
    draw.point((128, 328), fill=(60, 255, 120, 255))
    draw.point((129, 328), fill=(190, 255, 210, 255))
    height[320:337, 114:143] = 0.93

    # Heavy cast metal endcaps
    for ex in [26, 226]:
        draw.rectangle([ex, 322, ex + 4, 364], fill=(28, 32, 38, 255), outline=(12, 15, 18, 255))
        draw.point((ex + 2, 328), fill=(160, 170, 180, 255))
        draw.point((ex + 2, 358), fill=(160, 170, 180, 255))
        height[322:365, ex:ex + 5] = 0.96

    # 5. Angled Reflector Hood (y=336 to 346, x=22 to 234)
    for y in range(336, 346):
        flare = int((y - 336) * 0.8)
        c = int(38 + (y - 336) * 3)
        draw.line([(24 - flare, y), (232 + flare, y)], fill=(c, c + 2, c + 4, 255))
        height[y, 24 - flare:233 + flare] = 0.75
    draw.line([(16, 346), (240, 346)], fill=(85, 95, 105, 255), width=1)
    height[346, 16:241] = 0.9

    # 6. Dual Glowing Fluorescent Tubes (y=348 to 360)
    for y in range(348, 353):
        for x in range(32, 224):
            dist = abs(y - 350)
            alpha = int(255 - dist * 40)
            glow_col = (235, 250, 255, alpha) if dist == 0 else (190, 230, 245, alpha)
            draw.point((x, y), fill=glow_col)
            height[y, x] = 0.7 + (1.0 - dist / 3.0) * 0.25

    for y in range(355, 360):
        for x in range(32, 224):
            dist = abs(y - 357)
            alpha = int(255 - dist * 40)
            glow_col = (245, 255, 255, alpha) if dist == 0 else (200, 235, 250, alpha)
            draw.point((x, y), fill=glow_col)
            height[y, x] = 0.7 + (1.0 - dist / 3.0) * 0.25

    # End sockets
    for sx in [30, 222]:
        draw.rectangle([sx, 347, sx + 3, 360], fill=(55, 60, 70, 255), outline=(20, 24, 28, 255))
        height[347:361, sx:sx + 4] = 0.85

    # 7. Heavy Wire Guard (vertical bars every 28px)
    for rx in range(44, 220, 28):
        draw.line([(rx, 342), (rx, 364)], fill=(75, 82, 92, 255), width=2)
        draw.point((rx, 342), fill=(130, 140, 155, 255))
        draw.point((rx, 364), fill=(130, 140, 155, 255))
        height[342:365, rx:rx + 2] = 0.95
    draw.line([(26, 364), (230, 364)], fill=(70, 75, 85, 255), width=1)
    height[364, 26:231] = 0.95

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr, strength=4.5)

    p1 = os.path.join(out_dir, "Lamp_Suspended_Waterproof_Fluorescent_256x384.png")
    p2 = os.path.join(normal_dir, "Lamp_Suspended_Waterproof_Fluorescent_256x384_NormalMap.png")
    img.save(p1)
    norm_img.save(p2)
    print("Saved Lamp_Suspended_Waterproof_Fluorescent_256x384.png & NormalMap")

def make_deep_droop_waterproof_cables():
    # 256 wide x 160 high - thick black waterproof cables with DEEP dramatic sagging loops!
    w, h = 256, 160
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    # 3 Heavy Unistrut / Beam Clamps at ceiling (x=16, 128, 240)
    for cx in [16, 128, 240]:
        draw.rectangle([cx - 10, 0, cx + 10, 16], fill=(45, 50, 58, 255), outline=(18, 20, 24, 255), width=1)
        draw.point((cx - 6, 6), fill=(180, 190, 200, 255))
        draw.point((cx + 6, 6), fill=(180, 190, 200, 255))
        # Heavy-duty cable hanger saddle bracket
        draw.rectangle([cx - 8, 16, cx + 8, 32], fill=(36, 40, 48, 255), outline=(15, 18, 22, 255))
        height[0:33, cx - 10:cx + 11] = 0.92

    def catenary_thick(x0, x1, y0, y1, sag, thickness=4, col_core=(45, 50, 58, 255), col_hi=(70, 78, 88, 255)):
        for x in range(x0, x1 + 1):
            t = (x - x0) / float(x1 - x0)
            cen_y = int(round((1.0 - t) * y0 + t * y1 + math.sin(t * math.pi) * sag))
            half = thickness // 2
            # draw thick rounded cable
            for dy in range(-half, half + 1):
                py = cen_y + dy
                if 0 <= py < h:
                    if dy == -half:
                        draw.point((x, py), fill=(15, 18, 22, 255)) # top shadow
                    elif dy == -half + 1:
                        draw.point((x, py), fill=col_hi) # highlight
                    elif dy == half:
                        draw.point((x, py), fill=(10, 12, 15, 255)) # bottom shadow
                    else:
                        draw.point((x, py), fill=col_core)
                    height[py, x] = 0.85

    spans = [(16, 128), (128, 240)]
    for (x0, x1) in spans:
        # Cable 1: Ultra thick heavy industrial 3-phase black waterproof cable (thickness 5, sag 45px)
        catenary_thick(x0, x1, 20, 20, 45, thickness=5, col_core=(32, 36, 42, 255), col_hi=(60, 68, 76, 255))

        # Cable 2: Heavy black waterproof rubber cable 2 (thickness 4, sag 68px - DROOPING DEEP!)
        catenary_thick(x0, x1, 24, 24, 68, thickness=4, col_core=(26, 29, 34, 255), col_hi=(52, 58, 66, 255))

        # Cable 3: Heavy black waterproof cable 3 with orange identification stripe (thickness 4, sag 88px!)
        catenary_thick(x0, x1, 28, 28, 88, thickness=4, col_core=(22, 25, 30, 255), col_hi=(45, 50, 58, 255))
        # Orange identification spiral line
        for x in range(x0, x1 + 1):
            if (x // 6) % 3 == 0:
                t = (x - x0) / float(x1 - x0)
                cy = int(round(28 + math.sin(t * math.pi) * 88))
                if 0 <= cy < h:
                    draw.point((x, cy), fill=(225, 80, 20, 255))

    # Cable 4: Loose dangling loop in left span droops down to Y=125!
    for t in range(0, 120):
        rad = t / 120.0 * math.pi
        lx = int(72 + math.cos(rad) * 28)
        ly = int(50 + math.sin(rad) * 75) # reaches y=125!
        if 0 <= ly < h:
            draw.point((lx, ly - 1), fill=(12, 14, 18, 255))
            draw.point((lx, ly), fill=(48, 54, 62, 255))
            draw.point((lx, ly + 1), fill=(18, 20, 24, 255))
            height[ly - 1:ly + 2, lx] = 0.88

    # Heavy industrial yellow cable ties & black rubber bundle bands
    ties = [45, 75, 105, 155, 185, 215]
    for tx in ties:
        span_idx = 0 if tx < 128 else 1
        t_span = (tx - 16 if tx < 128 else tx - 128) / 112.0
        cen_y = int(24 + math.sin(t_span * math.pi) * 65)
        draw.rectangle([tx - 2, cen_y - 14, tx + 2, cen_y + 14], fill=(220, 190, 35, 255), outline=(60, 50, 10, 255))
        draw.rectangle([tx - 1, cen_y + 14, tx + 3, cen_y + 19], fill=(240, 210, 45, 255))
        height[cen_y - 14:cen_y + 20, tx - 2:tx + 4] = 0.96

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr, strength=4.5)

    p1 = os.path.join(out_dir, "Deco_Ceiling_CableBundle_Droop_Waterproof_256x160.png")
    p2 = os.path.join(normal_dir, "Deco_Ceiling_CableBundle_Droop_Waterproof_256x160_NormalMap.png")
    img.save(p1)
    norm_img.save(p2)
    print("Saved Deco_Ceiling_CableBundle_Droop_Waterproof_256x160.png & NormalMap")

def make_long_hanging_waterproof_cables():
    # 128 wide x 256 high - VERY LONG dangling cluster of thick black waterproof cables!
    w, h = 128, 256
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    # 1. Cast Iron Waterproof Junction Box (y=0 to 22, x=42 to 86)
    for y in range(0, 20):
        c = int(40 + y * 1.5)
        draw.line([(42, y), (86, y)], fill=(c, c + 3, c + 5, 255))
        height[y, 42:87] = 0.88
    draw.rectangle([42, 0, 86, 20], outline=(15, 18, 22, 255), width=1)
    # Hex screws
    for sx in [46, 82]:
        for sy in [4, 16]:
            draw.point((sx, sy), fill=(170, 180, 190, 255))
    # IP68 waterproof embossed logo / hazard plate
    draw.rectangle([54, 7, 74, 13], fill=(210, 175, 30, 255), outline=(18, 18, 18, 255))
    draw.line([(56, 7), (60, 13)], fill=(18, 18, 18, 255), width=1)
    draw.line([(62, 7), (66, 13)], fill=(18, 18, 18, 255), width=1)
    draw.line([(68, 7), (72, 13)], fill=(18, 18, 18, 255), width=1)
    height[0:21, 42:87] = 0.92

    # Lateral conduit pipes (left and right)
    draw.rectangle([4, 7, 42, 14], fill=(55, 60, 68, 255), outline=(18, 22, 26, 255))
    draw.rectangle([86, 7, 124, 14], fill=(55, 60, 68, 255), outline=(18, 22, 26, 255))
    height[7:15, 4:125] = 0.88

    # Heavy rubber gland manifold at bottom (y=20 to 30, x=50 to 78)
    draw.rectangle([50, 20, 78, 30], fill=(28, 30, 36, 255), outline=(12, 15, 18, 255))
    height[20:31, 50:79] = 0.95

    # 2. Main Cable Bundle Core wrapped in heavy black rubber tape (y=30 to 80)
    for y in range(30, 80):
        bw = int(9 - (y - 30) * 0.04)
        for x in range(64 - bw, 64 + bw + 1):
            t = (x - (64 - bw)) / float(bw * 2)
            c = int(22 + math.sin(t * math.pi) * 28)
            draw.point((x, y), fill=(c, c + 3, c + 5, 255))
            height[y, x] = 0.88

    # Yellow cable tie bands around main bundle (at y=38, 58, 78)
    for ty in [38, 58, 78]:
        draw.rectangle([53, ty - 2, 75, ty + 2], fill=(225, 195, 35, 255), outline=(60, 50, 10, 255))
        draw.point((76, ty), fill=(245, 215, 45, 255))
        height[ty - 2:ty + 3, 53:77] = 0.98

    # 3. Cascading & REALLY LONG Dangling Waterproof Cables (y=75 all the way down to y=240!)
    # Cable 1: Extra-long thick black power cable dropping down to Y=238! (4px thick)
    for y in range(75, 238):
        cx = int(62 + math.sin(y * 0.05) * 10)
        draw.point((cx - 2, y), fill=(12, 14, 16, 255))
        draw.point((cx - 1, y), fill=(55, 62, 70, 255)) # highlight
        draw.point((cx, y), fill=(35, 38, 44, 255))
        draw.point((cx + 1, y), fill=(18, 20, 24, 255))
        height[y, cx - 2:cx + 2] = 0.88
    # Heavy rubber end cap
    draw.rectangle([cx - 3, 235, cx + 2, 242], fill=(18, 20, 24, 255), outline=(8, 10, 12, 255))

    # Cable 2: Long black waterproof cable 2 dropping to Y=210 with orange tape
    for y in range(75, 210):
        cx = int(70 + math.cos(y * 0.07) * 8)
        draw.point((cx - 1, y), fill=(15, 18, 22, 255))
        draw.point((cx, y), fill=(48, 54, 62, 255))
        draw.point((cx + 1, y), fill=(22, 25, 30, 255))
        height[y, cx - 1:cx + 2] = 0.85
        # Orange identification bands every 40px
        if (y - 80) % 45 in [0, 1]:
            draw.line([(cx - 1, y), (cx + 1, y)], fill=(220, 80, 25, 255))
    draw.rectangle([cx - 2, 208, cx + 2, 214], fill=(230, 90, 25, 255), outline=(50, 20, 5, 255)) # orange wire nut

    # Cable 3: Deep hanging loop (y=75 drops to y=190 and loops back up to y=150)
    for t in np.linspace(0, math.pi, 200):
        lx = int(round(52 + math.cos(t) * 12))
        ly = int(round(120 + math.sin(t) * 68))
        if 0 <= ly < h:
            for dy in [-1, 0, 1]:
                py = ly + dy
                if 0 <= py < h:
                    draw.point((lx - 1, py), fill=(14, 16, 20, 255))
                    draw.point((lx, py), fill=(48, 54, 62, 255))
                    draw.point((lx + 1, py), fill=(20, 22, 26, 255))
                    height[py, lx - 1:lx + 2] = 0.88

    # Cable 4: Thick black waterproof flex conduit curving right (y=75 to 165)
    for y in range(75, 166):
        cx = int(round(76 + math.sin((y - 75) * 0.05) * 14))
        for ox in [-2, -1, 0, 1, 2]:
            col = (18, 20, 24, 255) if abs(ox) == 2 else ((52, 58, 66, 255) if ox == -1 else (32, 36, 42, 255))
            draw.point((cx + ox, y), fill=col)
        height[y, cx - 2:cx + 3] = 0.88

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr, strength=4.5)

    p1 = os.path.join(out_dir, "Deco_Ceiling_CableBundle_Hanging_Waterproof_128x256.png")
    p2 = os.path.join(normal_dir, "Deco_Ceiling_CableBundle_Hanging_Waterproof_128x256_NormalMap.png")
    img.save(p1)
    norm_img.save(p2)
    print("Saved Deco_Ceiling_CableBundle_Hanging_Waterproof_128x256.png & NormalMap")

if __name__ == "__main__":
    make_long_waterproof_cable_fluorescent()
    make_deep_droop_waterproof_cables()
    make_long_hanging_waterproof_cables()
