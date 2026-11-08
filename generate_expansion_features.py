import os
import math
import numpy as np
from PIL import Image, ImageDraw

out_dir = r"c:\Users\mokos\My project\Assets\Environment\Sprites\Architecture"
normal_dir = os.path.join(out_dir, "Normals")  # normal maps live in a Normals subfolder
os.makedirs(normal_dir, exist_ok=True)
os.makedirs(out_dir, exist_ok=True)

def generate_normal_from_height(height_map, alpha_mask, strength=3.0):
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

def make_hydroponics_pod():
    w, h = 256, 256
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    for y in range(180, 250):
        shade = int(35 + (y - 180) * 0.3)
        draw.line([(30, y), (226, y)], fill=(shade, shade + 8, shade + 15, 255))
        height[y, 30:227] = 0.6 + (0.1 if (y % 16 < 2) else 0.0)

    draw.rectangle([32, 182, 224, 248], outline=(20, 25, 32, 255), width=2)
    draw.rectangle([34, 185, 120, 245], outline=(25, 32, 42, 255), width=2)
    draw.rectangle([136, 185, 222, 245], outline=(25, 32, 42, 255), width=2)
    draw.rectangle([112, 195, 144, 215], fill=(15, 20, 25, 255), outline=(50, 60, 75, 255), width=1)
    draw.rectangle([115, 198, 141, 212], fill=(10, 45, 35, 255))
    draw.text((117, 200), "94%", fill=(80, 255, 180, 255))

    draw.ellipse([45, 195, 60, 210], fill=(60, 65, 75, 255), outline=(100, 110, 120, 255))
    draw.line([(52, 202), (58, 198)], fill=(220, 60, 40, 255), width=2)

    for x in range(30, 46):
        draw.line([(x, 50), (x, 180)], fill=(45, 52, 62, 255))
        height[50:181, x] = 0.8
    for x in range(211, 227):
        draw.line([(x, 50), (x, 180)], fill=(45, 52, 62, 255))
        height[50:181, x] = 0.8

    for y in range(35, 55):
        draw.line([(25, y), (231, y)], fill=(40, 46, 56, 255))
        height[y, 25:232] = 0.85
    draw.rectangle([25, 35, 231, 55], outline=(20, 25, 30, 255), width=2)
    for x in range(50, 205, 14):
        draw.rectangle([x, 52, x+8, 56], fill=(255, 80, 255, 255))
        draw.rectangle([x+2, 57, x+6, 60], fill=(220, 60, 240, 200))

    glass_overlay = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    gdraw = ImageDraw.Draw(glass_overlay)
    for y in range(56, 180):
        alpha = int(45 + math.sin(y * 0.1) * 10)
        gdraw.line([(46, y), (210, y)], fill=(30, 80, 100, alpha))
        height[y, 46:211] = 0.3

    for y in range(165, 180):
        draw.line([(46, y), (210, y)], fill=(15, 60, 70, 210))
        height[y, 46:211] = 0.45
    draw.line([(46, 165), (210, 165)], fill=(80, 180, 210, 220), width=1)
    draw.rectangle([46, 160, 210, 165], fill=(35, 42, 50, 255), outline=(60, 70, 80, 255))

    np.random.seed(42)
    for cx in range(55, 205, 18):
        plant_h = np.random.randint(45, 95)
        plant_base_y = 160
        draw.line([(cx, plant_base_y), (cx + np.random.randint(-4, 5), plant_base_y - plant_h)], fill=(40, 140, 60, 255), width=3)
        for leaf_i in range(5):
            lx = cx + np.random.randint(-15, 16)
            ly = plant_base_y - np.random.randint(15, plant_h)
            leaf_color = (
                np.random.randint(50, 110),
                np.random.randint(180, 255),
                np.random.randint(60, 140),
                255
            )
            draw.ellipse([lx-5, ly-4, lx+5, ly+4], fill=leaf_color)
            height[max(0, ly-4):min(h, ly+5), max(0, lx-5):min(w, lx+6)] = 0.7
            if np.random.random() > 0.5:
                draw.ellipse([lx-2, ly-6, lx+2, ly-2], fill=(220, 100, 255, 255))

    img = Image.alpha_composite(img, glass_overlay)
    draw = ImageDraw.Draw(img)

    draw.line([(70, 60), (120, 175)], fill=(200, 240, 255, 45), width=6)
    draw.line([(150, 60), (180, 175)], fill=(200, 240, 255, 35), width=4)

    draw.rectangle([30, 246, 226, 250], fill=(20, 20, 25, 255))
    for t in range(50, 170):
        tx = int(38 + math.sin(t * 0.08) * 4)
        draw.point((tx, t), fill=(40, 190, 220, 220))
        draw.point((tx+1, t), fill=(20, 120, 150, 200))
        height[t, max(0, tx-1):min(w, tx+2)] = 0.85

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr, strength=4.0)

    img.save(os.path.join(out_dir, "Hydroponics_Grow_Pod_256x256.png"))
    norm_img.save(os.path.join(normal_dir, "Hydroponics_Grow_Pod_256x256_NormalMap.png"))
    print("Saved Hydroponics_Grow_Pod_256x256.png & NormalMap")

def make_crane_hoist():
    w, h = 128, 256
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    draw.rectangle([20, 4, 42, 22], fill=(45, 50, 58, 255), outline=(20, 22, 25, 255), width=2)
    draw.rectangle([86, 4, 108, 22], fill=(45, 50, 58, 255), outline=(20, 22, 25, 255), width=2)
    height[4:23, 20:43] = 0.9
    height[4:23, 86:109] = 0.9

    for y in range(20, 65):
        diag = (y + 18) // 8
        c = (235, 185, 20, 255) if (diag % 2 == 0) else (30, 32, 35, 255)
        draw.line([(18, y), (110, y)], fill=c)
        height[y, 18:111] = 0.8
    draw.rectangle([18, 20, 110, 65], outline=(20, 22, 25, 255), width=2)

    for y in range(65, 115):
        shade = int(50 + math.cos((y - 90) * 0.1) * 20)
        draw.line([(32, y), (96, y)], fill=(shade, shade + 4, shade + 8, 255))
        height[y, 32:97] = 0.95
    draw.rectangle([32, 65, 96, 115], outline=(25, 28, 32, 255), width=2)

    for y in range(72, 108, 4):
        draw.line([(36, y), (92, y)], fill=(160, 165, 175, 255), width=2)

    for y in range(115, 195):
        c_shade = 180 + (y % 4) * 20
        draw.line([(52, y), (53, y)], fill=(c_shade, c_shade, c_shade, 255))
        draw.line([(75, y), (76, y)], fill=(c_shade, c_shade, c_shade, 255))
        height[y, 51:55] = 0.7
        height[y, 74:78] = 0.7

    draw.rectangle([44, 195, 84, 225], fill=(210, 160, 15, 255), outline=(30, 32, 35, 255), width=2)
    height[195:226, 44:85] = 0.85
    draw.ellipse([54, 202, 74, 218], fill=(60, 65, 75, 255), outline=(30, 32, 35, 255), width=2)
    height[202:219, 54:75] = 1.0

    draw.rectangle([60, 225, 68, 235], fill=(130, 135, 145, 255), outline=(30, 35, 40, 255), width=1)
    height[225:236, 60:69] = 0.75
    draw.arc([48, 228, 80, 252], start=30, end=270, fill=(160, 165, 175, 255), width=5)
    draw.polygon([(48, 236), (53, 230), (56, 236)], fill=(160, 165, 175, 255))
    height[228:253, 48:81] = 0.8

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr, strength=4.5)

    img.save(os.path.join(out_dir, "Crane_Overhead_Hoist_128x256.png"))
    norm_img.save(os.path.join(normal_dir, "Crane_Overhead_Hoist_128x256_NormalMap.png"))
    print("Saved Crane_Overhead_Hoist_128x256.png & NormalMap")

def make_antenna_mast():
    w, h = 128, 384
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    draw.polygon([(30, 380), (45, 350), (55, 350), (40, 380)], fill=(60, 68, 78, 255))
    draw.polygon([(98, 380), (83, 350), (73, 350), (88, 380)], fill=(60, 68, 78, 255))
    draw.rectangle([35, 375, 93, 383], fill=(45, 52, 60, 255))
    height[350:384, 30:99] = 0.7

    for y in range(70, 350):
        t = (y - 70) / (350 - 70)
        lx = int(58 - t * 16)
        rx = int(70 + t * 16)
        draw.line([(lx, y), (lx+2, y)], fill=(120, 130, 142, 255))
        draw.line([(rx-2, y), (rx, y)], fill=(140, 150, 162, 255))
        height[y, lx:lx+3] = 0.8
        height[y, rx-2:rx+1] = 0.8

    for y_seg in range(70, 340, 28):
        t1 = (y_seg - 70) / 280.0
        t2 = (y_seg + 28 - 70) / 280.0
        lx1, rx1 = int(58 - t1 * 16), int(70 + t1 * 16)
        lx2, rx2 = int(58 - t2 * 16), int(70 + t2 * 16)
        draw.line([(lx1, y_seg), (rx1, y_seg)], fill=(100, 110, 120, 255), width=2)
        draw.line([(lx1, y_seg), (rx2, y_seg + 28)], fill=(90, 100, 110, 255), width=1)
        draw.line([(rx1, y_seg), (lx2, y_seg + 28)], fill=(85, 95, 105, 255), width=1)

    draw.rectangle([70, 158, 86, 166], fill=(50, 55, 65, 255), outline=(25, 28, 32, 255))
    draw.arc([82, 130, 124, 194], start=260, end=100, fill=(200, 210, 220, 255), width=5)
    for dy in range(133, 192):
        dx_val = int(math.sin((dy - 130) / 64.0 * math.pi) * 14)
        draw.line([(86, dy), (86 + dx_val, dy)], fill=(170, 180, 190, 255))
        height[dy, 86:86+dx_val+1] = 0.75
    draw.line([(96, 162), (116, 162)], fill=(40, 45, 52, 255), width=3)
    draw.polygon([(116, 157), (124, 162), (116, 167)], fill=(220, 80, 50, 255))
    height[157:168, 96:125] = 0.9

    draw.line([(38, 95), (58, 95)], fill=(60, 65, 75, 255), width=2)
    draw.line([(38, 88), (38, 102)], fill=(60, 65, 75, 255), width=2)
    draw.arc([26, 82, 38, 94], start=0, end=180, fill=(220, 225, 235, 255), width=3)
    draw.arc([26, 96, 38, 108], start=180, end=360, fill=(220, 225, 235, 255), width=3)
    height[80:110, 25:58] = 0.7

    draw.line([(64, 15), (64, 70)], fill=(200, 210, 220, 255), width=3)
    draw.line([(63, 15), (63, 70)], fill=(240, 245, 255, 255), width=1)
    height[15:70, 62:66] = 0.9

    draw.ellipse([58, 6, 70, 18], fill=(255, 40, 40, 255), outline=(120, 20, 20, 255), width=1)
    draw.ellipse([61, 9, 67, 15], fill=(255, 180, 180, 255))
    height[6:19, 58:71] = 1.0

    for y_ice in range(120, 360, 35):
        for ix in [50, 64, 78]:
            ice_len = (ix * 7 + y_ice * 11) % 12 + 6
            for step in range(ice_len):
                w_ice = max(1, 3 - step // 4)
                draw.line([(ix - w_ice, y_ice + step), (ix + w_ice, y_ice + step)], fill=(220, 245, 255, 220))
                height[y_ice + step, ix - w_ice:ix + w_ice + 1] = 0.95

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr, strength=4.0)

    img.save(os.path.join(out_dir, "Antenna_Mast_Weather_128x384.png"))
    norm_img.save(os.path.join(normal_dir, "Antenna_Mast_Weather_128x384_NormalMap.png"))
    print("Saved Antenna_Mast_Weather_128x384.png & NormalMap")

def make_snow_drift_cap():
    w, h = 256, 64
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    top_contour = np.zeros(w, dtype=int)
    for x in range(w):
        angle = (x / float(w)) * 2 * math.pi
        y_val = 14 + math.sin(angle * 2) * 5 + math.cos(angle * 4) * 2.5
        top_contour[x] = int(y_val)

    for x in range(w):
        y_start = top_contour[x]
        for y in range(y_start, 42):
            depth = (y - y_start) / max(1, 42 - y_start)
            c_r = int(245 - depth * 45)
            c_g = int(250 - depth * 35)
            c_b = int(255 - depth * 15)
            draw.point((x, y), fill=(c_r, c_g, c_b, 255))
            height[y, x] = 0.85 - depth * 0.3

    for x in range(w):
        draw.point((x, top_contour[x]), fill=(255, 255, 255, 255))
        draw.point((x, top_contour[x] + 1), fill=(245, 250, 255, 255))
        height[top_contour[x], x] = 1.0

    for x in range(w):
        angle = (x / float(w)) * 2 * math.pi
        icicle_chance = math.sin(angle * 8) + math.cos(angle * 16) * 0.5
        if icicle_chance > 0.4:
            tip_y = min(62, int(42 + (icicle_chance - 0.4) * 24))
            for y in range(42, tip_y):
                fade = (tip_y - y) / (tip_y - 42.0)
                alpha = int(fade * 230)
                draw.point((x, y), fill=(210, 240, 255, alpha))
                height[y, x] = 0.9

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr, strength=4.0)

    img.save(os.path.join(out_dir, "Snow_Drift_Platform_Cap_256x64.png"))
    norm_img.save(os.path.join(normal_dir, "Snow_Drift_Platform_Cap_256x64_NormalMap.png"))
    print("Saved Snow_Drift_Platform_Cap_256x64.png & NormalMap")

if __name__ == "__main__":
    make_hydroponics_pod()
    make_crane_hoist()
    make_antenna_mast()
    make_snow_drift_cap()
    print("All 4 architectural expansion features generated successfully!")
