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

def make_suspended_dual_wire_fluorescent():
    w, h = 256, 192
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    left_cable_x = 44
    right_cable_x = 212

    # 1. Top Ceiling Brackets (y=0 to 12)
    for bx in [left_cable_x, right_cable_x]:
        for y in range(0, 8):
            c = int(50 + y * 2)
            draw.line([(bx - 10, y), (bx + 10, y)], fill=(c, c + 3, c + 6, 255))
            height[y, bx - 10:bx + 11] = 0.85
        draw.rectangle([bx - 10, 0, bx + 10, 8], outline=(20, 22, 26, 255), width=1)
        # Bolts
        draw.point((bx - 7, 4), fill=(180, 190, 200, 255))
        draw.point((bx + 7, 4), fill=(180, 190, 200, 255))
        # Eye-hook loop
        draw.ellipse([bx - 4, 8, bx + 4, 16], outline=(70, 75, 85, 255), width=2)
        height[8:17, bx - 4:bx + 5] = 0.9

    # Center ceiling conduit connection (y=0 to 10, x=124 to 132)
    draw.rectangle([123, 0, 133, 8], fill=(45, 50, 58, 255), outline=(20, 22, 26, 255))
    height[0:9, 123:134] = 0.8

    # 2. Suspension Wires (y=16 to 132)
    # Dual steel suspension cables
    for cx in [left_cable_x, right_cable_x]:
        for y in range(16, 132):
            draw.point((cx - 1, y), fill=(55, 60, 68, 255))
            draw.point((cx, y), fill=(140, 150, 165, 255)) # Steel cable glint
            draw.point((cx + 1, y), fill=(30, 34, 40, 255))
            height[y, cx - 1:cx + 2] = 0.85

    # Loose curled black power cord from center ceiling snaking down to ballast box
    for y in range(8, 132):
        # snaking sine wave
        cur_x = int(128 + math.sin(y * 0.1) * 8.0 * (1.0 - math.exp(-y * 0.03)))
        draw.point((cur_x - 1, y), fill=(20, 22, 26, 255))
        draw.point((cur_x, y), fill=(45, 50, 58, 255))
        draw.point((cur_x + 1, y), fill=(15, 18, 22, 255))
        height[y, cur_x - 1:cur_x + 2] = 0.8

    # Cable attachment eyes on luminaire top (y=126 to 134)
    for bx in [left_cable_x, right_cable_x]:
        draw.ellipse([bx - 4, 126, bx + 4, 134], outline=(75, 80, 90, 255), width=2)
        height[126:135, bx - 4:bx + 5] = 0.95

    # 3. Horizontal Luminaire Metal Body (y=132 to 148, x=24 to 232)
    # Upper ballast spine housing
    for y in range(132, 144):
        c = int(48 + math.sin((y - 132) * 0.25) * 18)
        draw.line([(26, y), (230, y)], fill=(c, c + 3, c + 5, 255))
        height[y, 26:231] = 0.85
    draw.rectangle([26, 132, 230, 144], outline=(20, 24, 28, 255), width=1)
    # Top highlight line
    draw.line([(27, 133), (229, 133)], fill=(110, 120, 130, 255))

    # Center ballast box / control panel (x=114 to 142, y=128 to 144)
    draw.rectangle([114, 128, 142, 144], fill=(36, 40, 48, 255), outline=(18, 20, 24, 255), width=1)
    # Status indicator light (green/amber dot)
    draw.point((128, 136), fill=(60, 255, 120, 255))
    draw.point((129, 136), fill=(180, 255, 200, 255))
    height[128:145, 114:143] = 0.92

    # Luminaire end caps (heavy cast metal ends)
    for ex in [26, 226]:
        draw.rectangle([ex, 130, ex + 4, 172], fill=(32, 36, 42, 255), outline=(15, 18, 22, 255))
        draw.point((ex + 2, 136), fill=(160, 170, 180, 255)) # bolt
        draw.point((ex + 2, 166), fill=(160, 170, 180, 255)) # bolt
        height[130:173, ex:ex + 5] = 0.95

    # 4. Angled Reflector Hood (y=144 to 154, x=22 to 234)
    for y in range(144, 154):
        flare = int((y - 144) * 0.8)
        c = int(40 + (y - 144) * 3)
        draw.line([(24 - flare, y), (232 + flare, y)], fill=(c, c + 2, c + 4, 255))
        height[y, 24 - flare:233 + flare] = 0.75
    # Rolled bottom reflector edge
    draw.line([(16, 154), (240, 154)], fill=(85, 95, 105, 255), width=1)
    height[154, 16:241] = 0.9

    # 5. Dual Glowing Fluorescent Tubes (y=154 to 168)
    # Tube 1 (upper tube y=156 to 160)
    for y in range(156, 161):
        for x in range(32, 224):
            dist = abs(y - 158)
            alpha = int(255 - dist * 40)
            glow_col = (235, 250, 255, alpha) if dist == 0 else (190, 230, 245, alpha)
            draw.point((x, y), fill=glow_col)
            height[y, x] = 0.7 + (1.0 - dist / 3.0) * 0.25

    # Tube 2 (lower tube y=163 to 167)
    for y in range(163, 168):
        for x in range(32, 224):
            dist = abs(y - 165)
            alpha = int(255 - dist * 40)
            glow_col = (245, 255, 255, alpha) if dist == 0 else (200, 235, 250, alpha)
            draw.point((x, y), fill=glow_col)
            height[y, x] = 0.7 + (1.0 - dist / 3.0) * 0.25

    # Tube sockets / bi-pin holders at ends
    for sx in [30, 222]:
        draw.rectangle([sx, 155, sx + 3, 168], fill=(60, 65, 75, 255), outline=(25, 28, 32, 255))
        height[155:169, sx:sx + 4] = 0.85

    # 6. Protective Wire Guard Ribs (vertical grill bars every 28px)
    for rx in range(44, 220, 28):
        draw.line([(rx, 150), (rx, 172)], fill=(75, 82, 92, 255), width=2)
        draw.point((rx, 150), fill=(130, 140, 155, 255))
        draw.point((rx, 172), fill=(130, 140, 155, 255))
        height[150:173, rx:rx + 2] = 0.95

    # Bottom longitudinal guard wire (y=172, x=26 to 230)
    draw.line([(26, 172), (230, 172)], fill=(70, 75, 85, 255), width=1)
    height[172, 26:231] = 0.95

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr, strength=4.5)

    p1 = os.path.join(out_dir, "Lamp_Suspended_Dual_Wire_Fluorescent_256x192.png")
    p2 = os.path.join(normal_dir, "Lamp_Suspended_Dual_Wire_Fluorescent_256x192_NormalMap.png")
    img.save(p1)
    norm_img.save(p2)
    print("Saved Lamp_Suspended_Dual_Wire_Fluorescent_256x192.png & NormalMap")

def make_ceiling_cable_bundle_droop():
    w, h = 256, 96
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    # 1. Ceiling Mount Clamps / Conduit Hangers (left x=16, mid x=128, right x=240)
    clamps = [16, 128, 240]
    for cx in clamps:
        # Steel unistrut clamp bracket
        draw.rectangle([cx - 8, 0, cx + 8, 14], fill=(50, 55, 62, 255), outline=(20, 22, 26, 255), width=1)
        draw.point((cx - 5, 4), fill=(180, 190, 200, 255)) # hex bolt
        draw.point((cx + 5, 4), fill=(180, 190, 200, 255))
        # Saddle strap holding cables
        draw.rectangle([cx - 6, 14, cx + 6, 28], fill=(40, 44, 50, 255), outline=(18, 20, 24, 255))
        height[0:29, cx - 8:cx + 9] = 0.9

    # 2. Main High-Voltage Cables (4 cables with catenary sags between clamps)
    # Cable 1: Heavy black insulated power feeder (thick 4px, moderate sag)
    # Cable 2: Ribbed conduit flex hose (grey, medium sag)
    # Cable 3: High-voltage warning orange/red cable (slight droop)
    # Cable 4: Loose drooping signal cable (deep sag with dangling loop!)

    # Helper function for catenary curve between x0 and x1 with sag S and base Y
    def catenary_points(x0, x1, y0, y1, sag):
        pts = []
        for x in range(x0, x1 + 1):
            t = (x - x0) / float(x1 - x0)
            y = (1.0 - t) * y0 + t * y1 + math.sin(t * math.pi) * sag
            pts.append((x, y))
        return pts

    # Left span: 16 to 128, Right span: 128 to 240
    spans = [(16, 128), (128, 240)]

    for (x0, x1) in spans:
        # Cable 1: Heavy Black Feeder (Sag = 16px)
        pts1 = catenary_points(x0, x1, 16, 16, 18)
        for (x, y) in pts1:
            iy = int(round(y))
            draw.point((x, iy - 1), fill=(18, 20, 24, 255))
            draw.point((x, iy), fill=(48, 54, 62, 255)) # highlight
            draw.point((x, iy + 1), fill=(30, 34, 40, 255))
            draw.point((x, iy + 2), fill=(12, 14, 18, 255))
            height[iy - 1:iy + 3, x] = 0.8

        # Cable 2: Industrial Ribbed Conduit (Grey, Sag = 24px)
        pts2 = catenary_points(x0, x1, 20, 20, 26)
        for (x, y) in pts2:
            iy = int(round(y))
            # ribbed texture: alternate dark and light every 3px
            rib = 15 if (x // 3) % 2 == 0 else -10
            c = int(60 + rib)
            draw.point((x, iy - 1), fill=(c - 20, c - 20, c - 18, 255))
            draw.point((x, iy), fill=(c + 20, c + 22, c + 25, 255))
            draw.point((x, iy + 1), fill=(c, c, c + 4, 255))
            draw.point((x, iy + 2), fill=(20, 22, 25, 255))
            height[iy - 1:iy + 3, x] = 0.85

        # Cable 3: Orange/Red Hazard Cable (Sag = 32px)
        pts3 = catenary_points(x0, x1, 22, 22, 34)
        for (x, y) in pts3:
            iy = int(round(y))
            draw.point((x, iy - 1), fill=(120, 30, 15, 255))
            draw.point((x, iy), fill=(220, 75, 30, 255)) # orange core
            draw.point((x, iy + 1), fill=(160, 45, 20, 255))
            draw.point((x, iy + 2), fill=(60, 15, 10, 255))
            height[iy - 1:iy + 3, x] = 0.82

    # Cable 4: Extra loose, deeply sagging bundle (droops down deeply in left span, hangs a loop in right span)
    # Left deep sag (sag = 48px, down to y=72!)
    pts4_left = catenary_points(16, 128, 24, 24, 46)
    for (x, y) in pts4_left:
        iy = int(round(y))
        draw.point((x, iy), fill=(22, 26, 32, 255))
        draw.point((x, iy + 1), fill=(42, 48, 56, 255))
        draw.point((x, iy + 2), fill=(14, 16, 20, 255))
        height[iy:iy + 3, x] = 0.78

    # Right span has a loose loop hanging down from x=165 to 195 (droops to y=82!)
    for t in range(0, 100):
        rad = t / 100.0 * math.pi
        lx = int(180 + math.cos(rad) * 16)
        ly = int(45 + math.sin(rad) * 36)
        draw.point((lx, ly), fill=(25, 28, 34, 255))
        draw.point((lx, ly + 1), fill=(50, 56, 66, 255))
        height[ly:ly + 2, lx] = 0.88

    # 3. Yellow / White Industrial Cable Zip-Ties holding the bundle together
    tie_xs = [40, 72, 104, 150, 185, 215]
    for tx in tie_xs:
        # Find cable bundle Y at this X
        # Draw bright yellow zip-tie band
        # Approximate bundle center Y
        span_idx = 0 if tx < 128 else 1
        t_span = (tx - 16 if tx < 128 else tx - 128) / 112.0
        cen_y = int(22 + math.sin(t_span * math.pi) * 26)
        draw.rectangle([tx - 2, cen_y - 8, tx + 2, cen_y + 8], fill=(220, 190, 40, 255), outline=(80, 70, 10, 255))
        # Small tie buckle tab
        draw.rectangle([tx - 1, cen_y + 8, tx + 3, cen_y + 12], fill=(240, 210, 50, 255))
        height[cen_y - 8:cen_y + 13, tx - 2:tx + 4] = 0.95

    # 4. Loose hanging wire tails / dangling cut wire ends (at x=88, y drops to 78)
    for dy in range(48, 76):
        wx = int(88 + math.sin(dy * 0.2) * 2)
        draw.point((wx, dy), fill=(35, 120, 180, 255)) # blue wire
        height[dy, wx] = 0.85
    draw.rectangle([87, 75, 91, 79], fill=(240, 180, 30, 255)) # yellow wire cap / nut

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr, strength=4.5)

    p1 = os.path.join(out_dir, "Deco_Ceiling_CableBundle_Droop_256x96.png")
    p2 = os.path.join(normal_dir, "Deco_Ceiling_CableBundle_Droop_256x96_NormalMap.png")
    img.save(p1)
    norm_img.save(p2)
    print("Saved Deco_Ceiling_CableBundle_Droop_256x96.png & NormalMap")

def make_ceiling_cable_bundle_hanging():
    w, h = 128, 128
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    # 1. Industrial Ceiling Junction Box / Conduit Hub (y=0 to 18, x=46 to 82)
    for y in range(0, 16):
        c = int(45 + y * 1.5)
        draw.line([(46, y), (82, y)], fill=(c, c + 3, c + 6, 255))
        height[y, 46:83] = 0.85
    draw.rectangle([46, 0, 82, 16], outline=(18, 20, 24, 255), width=1)
    # Hex screws on junction cover
    draw.point((50, 4), fill=(170, 180, 190, 255))
    draw.point((78, 4), fill=(170, 180, 190, 255))
    draw.point((50, 12), fill=(170, 180, 190, 255))
    draw.point((78, 12), fill=(170, 180, 190, 255))
    # Warning hazard stripe sticker
    draw.rectangle([56, 6, 72, 11], fill=(220, 180, 30, 255), outline=(20, 20, 20, 255))
    draw.line([(58, 6), (62, 11)], fill=(20, 20, 20, 255), width=1)
    draw.line([(64, 6), (68, 11)], fill=(20, 20, 20, 255), width=1)
    height[0:17, 46:83] = 0.9

    # Lateral conduit pipes entering junction box (left and right)
    draw.rectangle([8, 5, 46, 11], fill=(60, 65, 72, 255), outline=(22, 25, 28, 255))
    draw.rectangle([82, 5, 120, 11], fill=(60, 65, 72, 255), outline=(22, 25, 28, 255))
    height[5:12, 8:121] = 0.88

    # Heavy rubber strain relief collar at box bottom (y=16 to 24, x=54 to 74)
    for y in range(16, 24):
        c = int(32 + (y - 16) * 2)
        draw.line([(54, y), (74, y)], fill=(c, c + 2, c + 5, 255))
        height[y, 54:75] = 0.92
    draw.rectangle([54, 16, 74, 24], outline=(15, 18, 22, 255), width=1)

    # 2. Main Cable Bundle Core (y=24 to 68) wrapped with black rubber tape & zip-ties
    for y in range(24, 64):
        bw = int(7 - (y - 24) * 0.05)
        for x in range(64 - bw, 64 + bw + 1):
            t = (x - (64 - bw)) / float(bw * 2)
            c = int(25 + math.sin(t * math.pi) * 30)
            draw.point((x, y), fill=(c, c + 3, c + 6, 255))
            height[y, x] = 0.85

    # Yellow cable tie bands around main bundle (at y=32, 48)
    for ty in [32, 48]:
        draw.rectangle([56, ty - 2, 72, ty + 2], fill=(225, 195, 35, 255), outline=(60, 50, 10, 255))
        draw.point((73, ty), fill=(245, 215, 45, 255))
        height[ty - 2:ty + 3, 56:74] = 0.96

    # 3. Cascading & Dangling Individual Wires branching out from y=50 downwards
    # Wire 1: Thick Black Power Cable with curled tail (y=50 to 118)
    for y in range(50, 118):
        cur_x = int(60 + math.sin(y * 0.09) * 8)
        draw.point((cur_x - 1, y), fill=(18, 20, 24, 255))
        draw.point((cur_x, y), fill=(48, 54, 62, 255)) # highlight
        draw.point((cur_x + 1, y), fill=(28, 32, 38, 255))
        height[y, cur_x - 1:cur_x + 2] = 0.85

    # Wire 2: Orange/Red High Voltage Wire with orange wire nut (y=50 to 92)
    for y in range(50, 92):
        cur_x = int(68 + math.cos(y * 0.12) * 5)
        draw.point((cur_x - 1, y), fill=(140, 35, 15, 255))
        draw.point((cur_x, y), fill=(225, 75, 25, 255))
        draw.point((cur_x + 1, y), fill=(90, 20, 10, 255))
        height[y, cur_x - 1:cur_x + 2] = 0.82
    # Orange wire nut
    draw.rectangle([66, 91, 72, 97], fill=(240, 110, 20, 255), outline=(60, 20, 5, 255))
    height[91:98, 66:73] = 0.95

    # Wire 3: Blue Signal Control Wire with loose loop (y=50 to 104)
    for y in range(50, 104):
        cur_x = int(54 - math.sin(y * 0.08) * 6)
        draw.point((cur_x, y), fill=(40, 120, 200, 255))
        draw.point((cur_x + 1, y), fill=(70, 160, 240, 255))
        height[y, cur_x:cur_x + 2] = 0.8
    # Blue wire end taped
    draw.rectangle([53, 103, 57, 107], fill=(20, 22, 26, 255))

    # Wire 4: Grey ribbed flex conduit hanging in a loop from y=55 down to 110 and curving right
    for y in range(55, 108):
        cur_x = int(72 + math.sin((y - 55) * 0.06) * 12)
        rib = 18 if (y // 3) % 2 == 0 else -10
        c = int(65 + rib)
        draw.point((cur_x - 1, y), fill=(c - 15, c - 15, c - 10, 255))
        draw.point((cur_x, y), fill=(c + 20, c + 20, c + 25, 255))
        draw.point((cur_x + 1, y), fill=(c, c, c + 5, 255))
        height[y, cur_x - 1:cur_x + 2] = 0.88

    # Wire 5: Thin Yellow/Green ground wire curling off left (y=50 to 82)
    for y in range(50, 82):
        cur_x = int(48 - math.sin((y - 50) * 0.15) * 4)
        col = (210, 220, 40, 255) if (y // 3) % 2 == 0 else (45, 160, 65, 255)
        draw.point((cur_x, y), fill=col)
        height[y, cur_x] = 0.8
    draw.point((48, 82), fill=(200, 200, 200, 255)) # metal lug

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr, strength=4.5)

    p1 = os.path.join(out_dir, "Deco_Ceiling_CableBundle_Hanging_128x128.png")
    p2 = os.path.join(normal_dir, "Deco_Ceiling_CableBundle_Hanging_128x128_NormalMap.png")
    img.save(p1)
    norm_img.save(p2)
    print("Saved Deco_Ceiling_CableBundle_Hanging_128x128.png & NormalMap")

if __name__ == "__main__":
    make_suspended_dual_wire_fluorescent()
    make_ceiling_cable_bundle_droop()
    make_ceiling_cable_bundle_hanging()
