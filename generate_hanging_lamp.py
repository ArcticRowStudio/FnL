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

def make_hanging_wire_hood_lamp():
    w, h = 128, 256
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    # 1. Ceiling mount bracket (y=0 to 12, x=52 to 76)
    for y in range(0, 10):
        c = int(45 + y * 1.5)
        draw.line([(52, y), (76, y)], fill=(c, c + 3, c + 6, 255))
        height[y, 52:77] = 0.8
    draw.rectangle([52, 0, 76, 10], outline=(20, 22, 26, 255), width=1)
    # Hex bolts
    draw.point((55, 5), fill=(180, 190, 200, 255))
    draw.point((73, 5), fill=(180, 190, 200, 255))
    height[5, 55] = 0.95
    height[5, 73] = 0.95

    # Cable gland / conical strain relief collar (y=10 to 18, x=61 to 67)
    for y in range(10, 18):
        gw = int(4 - (y - 10) * 0.25)
        draw.line([(64 - gw, y), (64 + gw, y)], fill=(32, 35, 40, 255))
        height[y, 64 - gw:64 + gw + 1] = 0.85
    draw.line([(63, 10), (63, 17)], fill=(65, 70, 80, 255)) # highlight

    # 2. Hanging Black Electrical Wire / Drop Cord (y=18 to 145)
    # Give the cable a tiny realistic subtle sway/catenary curve
    for y in range(18, 145):
        # subtle curve amplitude ~1.2px
        cx = 64.0 + math.sin((y - 18) * 0.04) * 1.2
        ix = int(round(cx))
        # Cable thickness: 3px wide [ix-1, ix, ix+1]
        draw.point((ix - 1, y), fill=(22, 24, 28, 255))
        draw.point((ix, y), fill=(48, 52, 60, 255)) # specular highlight on rounded cord
        draw.point((ix + 1, y), fill=(18, 20, 24, 255))
        height[y, ix - 1:ix + 2] = 0.75
        height[y, ix] = 0.85

    # Cable clamp / tape detail at y=80
    draw.line([(62, 79), (66, 79)], fill=(120, 100, 50, 255)) # brass clamp band
    draw.line([(62, 80), (66, 80)], fill=(150, 130, 60, 255))
    height[79:81, 62:67] = 0.9

    # 3. Socket top strain relief boot & brass coupler (y=145 to 162)
    for y in range(145, 154):
        bw = int(2 + (y - 145) * 0.5)
        draw.line([(64 - bw, y), (64 + bw, y)], fill=(30, 32, 38, 255))
        draw.point((64, y), fill=(55, 60, 70, 255))
        height[y, 64 - bw:64 + bw + 1] = 0.85

    # Brass / Bronze heavy industrial socket cylinder (y=154 to 168, x=58 to 70)
    for y in range(154, 168):
        for x in range(58, 71):
            t = (x - 58) / 12.0
            # Brass shading
            r = int(90 + math.sin(t * math.pi) * 80)
            g = int(70 + math.sin(t * math.pi) * 60)
            b = int(25 + math.sin(t * math.pi) * 20)
            draw.point((x, y), fill=(r, g, b, 255))
            height[y, x] = 0.8 + math.sin(t * math.pi) * 0.15
    draw.rectangle([58, 154, 70, 168], outline=(25, 20, 10, 255), width=1)
    # Socket knurling / ridges
    for ry in [158, 163]:
        draw.line([(59, ry), (69, ry)], fill=(190, 160, 80, 255))
        height[ry, 59:70] = 0.95

    # 4. Flared Industrial Metal Shade (Enamel Bell Hood) (y=168 to 198)
    # Starts narrow at y=168 (width 26, x=51 to 77), flares down to y=192 (width 74, x=27 to 101)
    for y in range(168, 194):
        prog = (y - 168) / 25.0
        # curved flare
        hw = int(12 + 25 * (prog ** 1.3))
        left = 64 - hw
        right = 64 + hw
        for x in range(left, right + 1):
            norm_x = (x - left) / float(right - left)
            # Exterior dark industrial enamel slate/green-grey
            base_r = int(35 + math.sin(norm_x * math.pi) * 35)
            base_g = int(45 + math.sin(norm_x * math.pi) * 45)
            base_b = int(48 + math.sin(norm_x * math.pi) * 48)
            draw.point((x, y), fill=(base_r, base_g, base_b, 255))
            height[y, x] = 0.75 + math.sin(norm_x * math.pi) * 0.2

    # Outline and metallic rim
    for y in range(168, 194):
        prog = (y - 168) / 25.0
        hw = int(12 + 25 * (prog ** 1.3))
        draw.point((64 - hw, y), fill=(18, 22, 25, 255))
        draw.point((64 + hw, y), fill=(18, 22, 25, 255))
        # Specular glint on left curve
        if 174 <= y <= 188:
            draw.point((64 - hw + 2, y), fill=(100, 120, 130, 255))

    # Shade bottom lip / rolled edge (y=193 to 197, width x=25 to 103)
    draw.line([(25, 194), (103, 194)], fill=(80, 95, 105, 255), width=2)
    draw.line([(26, 193), (102, 193)], fill=(140, 160, 175, 255), width=1) # rim highlight
    draw.line([(25, 196), (103, 196)], fill=(20, 24, 28, 255), width=1)
    height[193:197, 25:104] = 0.95

    # White enamel reflector underside visible beneath rim
    for y in range(196, 202):
        hw = int(37 - (y - 196) * 4)
        for x in range(64 - hw, 64 + hw + 1):
            draw.point((x, y), fill=(180, 175, 160, 255))
            height[y, x] = 0.6

    # 5. Glowing Bulb & Protective Wire Cage below the shade (y=198 to 238)
    # Glass globe
    for y in range(198, 230):
        gw = int(13 * math.sin((y - 198) / 32.0 * math.pi))
        for x in range(64 - gw, 64 + gw + 1):
            dist = math.sqrt((x - 64)**2 + (y - 212)**2)
            alpha = int(max(70, 255 - dist * 7))
            draw.point((x, y), fill=(255, 242, 190, alpha))
            height[y, x] = 0.65 + max(0.0, 1.0 - dist / 16.0) * 0.35

    # Glowing filament loop (y=206 to 218)
    draw.line([(60, 206), (63, 216), (65, 216), (68, 206)], fill=(255, 255, 255, 255), width=2)
    draw.ellipse([61, 210, 67, 216], fill=(255, 255, 220, 255))

    # Protective Wire Cage around the bulb
    # Cage horizontal hoops
    for ry in [210, 224, 233]:
        hw = int(15 * math.sin((ry - 198) / 38.0 * math.pi))
        draw.line([(64 - hw, ry), (64 + hw, ry)], fill=(65, 70, 80, 255), width=2)
        draw.line([(64 - hw, ry - 1), (64 + hw, ry - 1)], fill=(120, 130, 140, 255), width=1)
        height[ry - 1:ry + 2, 64 - hw:64 + hw + 1] = 0.95

    # Cage vertical struts (4 wires)
    for fx in [-12, -4, 4, 12]:
        for y in range(198, 235):
            cur_hw = 15 * math.sin((y - 198) / 38.0 * math.pi)
            cx = int(64 + fx * (cur_hw / 15.0))
            draw.point((cx, y), fill=(70, 75, 85, 255))
            draw.point((cx - 1, y), fill=(130, 140, 150, 255))
            height[y, cx - 1:cx + 1] = 0.98

    # Cage bottom finial / junction ring (y=234 to 240)
    draw.ellipse([61, 233, 67, 239], fill=(50, 55, 62, 255), outline=(20, 22, 25, 255), width=1)
    height[233:240, 61:68] = 0.9

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr, strength=4.5)

    p1 = os.path.join(out_dir, "Lamp_Hanging_Wire_Industrial_128x256.png")
    p2 = os.path.join(normal_dir, "Lamp_Hanging_Wire_Industrial_128x256_NormalMap.png")
    img.save(p1)
    norm_img.save(p2)
    print("Saved Lamp_Hanging_Wire_Industrial_128x256.png & NormalMap")

def make_hanging_wire_cage_drop_lamp():
    w, h = 128, 256
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    # 1. Industrial Pipe Flange & Conduit Box at ceiling (y=0 to 14, x=48 to 80)
    for y in range(0, 12):
        c = int(40 + y * 1.8)
        draw.line([(48, y), (80, y)], fill=(c, c + 3, c + 5, 255))
        height[y, 48:81] = 0.8
    draw.rectangle([48, 0, 80, 12], outline=(18, 20, 24, 255), width=1)
    # Hex bolt heads on flange
    draw.ellipse([51, 4, 57, 10], fill=(160, 170, 180, 255))
    draw.ellipse([71, 4, 77, 10], fill=(160, 170, 180, 255))
    height[4:11, 51:58] = 0.95
    height[4:11, 71:78] = 0.95

    # 2. Hanging Twisted-Pair / Heavy Cord (y=12 to 160)
    # Shows braided/twisted texture for classic industrial bunker look
    for y in range(12, 160):
        # gentle organic sway
        center_x = 64.0 + math.sin((y - 12) * 0.035) * 1.5
        phase = (y * 0.3) % (2 * math.pi)
        strand1_x = int(round(center_x + math.sin(phase) * 1.2))
        strand2_x = int(round(center_x - math.sin(phase) * 1.2))

        # Strand 1
        draw.point((strand1_x, y), fill=(38, 42, 48, 255))
        draw.point((strand1_x + 1, y), fill=(20, 22, 26, 255))
        # Strand 2
        draw.point((strand2_x, y), fill=(52, 58, 66, 255))
        draw.point((strand2_x - 1, y), fill=(24, 26, 30, 255))

        min_x = min(strand1_x - 1, strand2_x - 1)
        max_x = max(strand1_x + 1, strand2_x + 1)
        height[y, min_x:max_x + 1] = 0.8

    # Industrial rubber strain relief boot (y=160 to 174, x=60 to 68)
    for y in range(160, 174):
        bw = int(3 + (y - 160) * 0.4)
        draw.line([(64 - bw, y), (64 + bw, y)], fill=(32, 34, 40, 255))
        draw.point((64 - bw + 1, y), fill=(60, 65, 75, 255))
        height[y, 64 - bw:64 + bw + 1] = 0.85

    # Heavy Brass / Steel Threaded Socket & Paddle Switch (y=174 to 196, x=54 to 74)
    for y in range(174, 196):
        for x in range(54, 75):
            t = (x - 54) / 20.0
            r = int(85 + math.sin(t * math.pi) * 75)
            g = int(72 + math.sin(t * math.pi) * 60)
            b = int(30 + math.sin(t * math.pi) * 20)
            draw.point((x, y), fill=(r, g, b, 255))
            height[y, x] = 0.8 + math.sin(t * math.pi) * 0.15
    draw.rectangle([54, 174, 74, 196], outline=(22, 20, 14, 255), width=1)
    # Socket threads
    for ry in [178, 184, 190]:
        draw.line([(55, ry), (73, ry)], fill=(180, 155, 75, 255))
        height[ry, 55:74] = 0.95
    # Little turn-key / paddle switch on the right side
    draw.rectangle([74, 182, 80, 187], fill=(160, 140, 70, 255), outline=(30, 25, 15, 255))
    height[182:188, 74:81] = 0.9

    # 3. Cage Collar & Upper Ring (y=196 to 204, x=48 to 80)
    for y in range(196, 204):
        c = int(50 + math.sin((y - 196) * 0.4) * 20)
        draw.line([(48, y), (80, y)], fill=(c, c + 4, c + 8, 255))
        height[y, 48:81] = 0.88
    draw.rectangle([48, 196, 80, 204], outline=(20, 22, 26, 255), width=1)

    # 4. Large Teardrop Edison Bulb (y=204 to 242, x=44 to 84)
    for y in range(204, 242):
        gw = int(18 * math.sin((y - 204) / 38.0 * math.pi))
        for x in range(64 - gw, 64 + gw + 1):
            dist = math.sqrt((x - 64)**2 + (y - 222)**2)
            alpha = int(max(60, 255 - dist * 6))
            draw.point((x, y), fill=(255, 235, 170, alpha))
            height[y, x] = 0.65 + max(0.0, 1.0 - dist / 22.0) * 0.35

    # Golden spiral / loop filament (y=210 to 230)
    draw.line([(60, 210), (62, 224), (66, 224), (68, 210)], fill=(255, 255, 255, 255), width=2)
    draw.ellipse([61, 216, 67, 223], fill=(255, 245, 190, 255))

    # 5. Heavy Steel Bunker Cage enclosing the bulb (y=204 to 248)
    # Horizontal cage rings
    for ry in [214, 228, 242]:
        hw = int(21 * math.sin((ry - 200) / 48.0 * math.pi))
        draw.line([(64 - hw, ry), (64 + hw, ry)], fill=(65, 70, 80, 255), width=2)
        draw.line([(64 - hw, ry - 1), (64 + hw, ry - 1)], fill=(125, 135, 145, 255), width=1)
        height[ry - 1:ry + 2, 64 - hw:64 + hw + 1] = 0.95

    # Vertical cage struts (6 struts around)
    for angle in [-20, -12, -4, 4, 12, 20]:
        for y in range(204, 246):
            cur_hw = 21 * math.sin((y - 200) / 48.0 * math.pi)
            cx = int(64 + angle * (cur_hw / 21.0))
            draw.point((cx, y), fill=(70, 75, 85, 255))
            draw.point((cx - 1, y), fill=(130, 140, 150, 255))
            height[y, cx - 1:cx + 1] = 0.98

    # Bottom cage cup & hook (y=245 to 252)
    draw.ellipse([60, 244, 68, 251], fill=(45, 50, 58, 255), outline=(18, 20, 24, 255), width=1)
    height[244:252, 60:69] = 0.9

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr, strength=4.5)

    p1 = os.path.join(out_dir, "Lamp_Hanging_Wire_DropBulb_128x256.png")
    p2 = os.path.join(normal_dir, "Lamp_Hanging_Wire_DropBulb_128x256_NormalMap.png")
    img.save(p1)
    norm_img.save(p2)
    print("Saved Lamp_Hanging_Wire_DropBulb_128x256.png & NormalMap")

if __name__ == "__main__":
    make_hanging_wire_hood_lamp()
    make_hanging_wire_cage_drop_lamp()
