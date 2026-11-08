import os
import math
import numpy as np
from PIL import Image, ImageDraw

out_dir = r"c:\Users\mokos\My project\Assets\Environment\Sprites\Architecture"
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

# -------------------------------------------------------------------------
# Type 2: Lamp_Fluorescent_Tube_Dual_256x64 (천장 현수형 듀얼 산업용 형광등)
# -------------------------------------------------------------------------
def make_fluorescent_dual():
    w, h = 256, 64
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    # Suspension chains from ceiling (y=0 to 18, x=45 and x=210)
    for y in range(0, 18):
        c_shade = 90 if (y % 4 < 2) else 140
        draw.line([(45, y), (46, y)], fill=(c_shade, c_shade, c_shade, 255))
        draw.line([(210, y), (211, y)], fill=(c_shade, c_shade, c_shade, 255))
        height[y, 44:48] = 0.7
        height[y, 209:213] = 0.7

    # Metal housing reflector fixture (y=18 to 36, x=20 to 235)
    for y in range(18, 36):
        shade = int(50 + (y - 18) * 2.5)
        draw.line([(20, y), (235, y)], fill=(shade, shade + 3, shade + 6, 255))
        height[y, 20:236] = 0.85
    draw.rectangle([20, 18, 235, 36], outline=(20, 24, 28, 255), width=2)
    # Reflector angled hood bevel
    draw.polygon([(16, 36), (20, 20), (235, 20), (239, 36)], outline=(30, 35, 42, 255))
    # Ballast / Starter housing in center (x=115 to 140, y=14 to 22)
    draw.rectangle([115, 14, 140, 22], fill=(35, 40, 48, 255), outline=(15, 18, 22, 255))
    draw.ellipse([124, 16, 131, 20], fill=(220, 70, 50, 255)) # Red glow starter bulb
    height[14:23, 115:141] = 0.95

    # Dual glowing fluorescent tubes (y=36 to 48, x=25 to 230)
    # Tube 1 (top): y=38 to 41
    # Tube 2 (bottom): y=44 to 47
    for ty in [38, 44]:
        # Tube socket ends (x=24 to 28, and x=227 to 231)
        draw.rectangle([24, ty-1, 28, ty+3], fill=(80, 85, 95, 255), outline=(20, 24, 28, 255))
        draw.rectangle([227, ty-1, 231, ty+3], fill=(80, 85, 95, 255), outline=(20, 24, 28, 255))
        # Glowing phosphor tube body
        draw.rectangle([29, ty, 226, ty+2], fill=(255, 255, 250, 255))
        draw.line([(29, ty+1), (226, ty+1)], fill=(255, 255, 255, 255), width=1)
        height[ty-1:ty+4, 24:232] = 0.9

    # Wire guard clips (every 40 pixels)
    for cx in range(50, 220, 42):
        draw.line([(cx, 34), (cx, 50)], fill=(70, 75, 85, 255), width=2)
        height[34:51, cx:cx+2] = 1.0

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr, strength=4.0)

    img.save(os.path.join(out_dir, "Lamp_Fluorescent_Tube_Dual_256x64.png"))
    norm_img.save(os.path.join(normal_dir, "Lamp_Fluorescent_Tube_Dual_256x64_NormalMap.png"))
    print("Saved Lamp_Fluorescent_Tube_Dual_256x64.png & NormalMap")

# -------------------------------------------------------------------------
# Type 3: Lamp_Industrial_Swivel_Floodlight_128x128 (각도 조절형 중공업 투광등)
# -------------------------------------------------------------------------
def make_swivel_floodlight():
    w, h = 128, 128
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    # Ceiling / Wall Mounting Flange plate (y=0 to 22, x=46 to 82)
    for y in range(0, 22):
        draw.line([(46, y), (82, y)], fill=(40, 45, 52, 255))
        height[y, 46:83] = 0.7
    draw.rectangle([46, 0, 82, 22], outline=(18, 20, 25, 255), width=2)
    # Hex mounting bolts
    draw.ellipse([50, 4, 56, 10], fill=(160, 170, 180, 255))
    draw.ellipse([72, 4, 78, 10], fill=(160, 170, 180, 255))

    # Swivel Yoke / Pivot Arm (U-bracket from y=22 to 55)
    # Left arm: x=38 to 46; Right arm: x=82 to 90; Center stem: x=58 to 70
    draw.rectangle([58, 22, 70, 36], fill=(50, 56, 65, 255), outline=(20, 24, 30, 255))
    draw.line([(64, 36), (42, 48)], fill=(60, 68, 78, 255), width=6)
    draw.line([(64, 36), (86, 48)], fill=(60, 68, 78, 255), width=6)
    # Heavy pivot pivot bolt with tightening handle
    draw.ellipse([36, 44, 48, 56], fill=(180, 150, 40, 255), outline=(30, 25, 10, 255), width=2) # Brass swivel knob
    draw.ellipse([80, 44, 92, 56], fill=(180, 150, 40, 255), outline=(30, 25, 10, 255), width=2)
    height[22:57, 36:93] = 0.8

    # Lamp Bell Housing / Angled Parabolic Canister (pointing downwards-right or downward)
    # Center bell: y=50 to 95, x=42 to 86
    for y in range(50, 95):
        rad = int(14 + (y - 50) * 0.35)
        for x in range(64 - rad, 64 + rad + 1):
            shade = int(55 + (x - (64 - rad)) * 1.5)
            draw.point((x, y), fill=(shade, shade + 3, shade + 6, 255))
            height[y, x] = 0.85
    draw.rectangle([48, 50, 80, 95], outline=(20, 24, 28, 255), width=2)

    # Cooling heatsink fins on back of housing (y=54 to 88)
    for fy in range(54, 88, 6):
        draw.line([(44, fy), (84, fy)], fill=(30, 34, 40, 255), width=2)
        height[fy:fy+2, 44:85] = 0.95

    # Parabolic Reflector Hood & Glowing Front Lens (y=92 to 116, x=34 to 94)
    # Flared outer reflector rim
    draw.polygon([(34, 114), (46, 92), (82, 92), (94, 114)], fill=(120, 130, 142, 255), outline=(25, 28, 32, 255), width=2)
    # Visor / Barn door shield along top rim
    draw.line([(30, 116), (98, 116)], fill=(35, 40, 48, 255), width=3)

    # Intense Halogen Lens Glow inside reflector
    for y in range(96, 114):
        lw = int((y - 96) * 1.8 + 10)
        for x in range(64 - lw, 64 + lw + 1):
            dist = math.sqrt((x - 64)**2 + (y - 102)**2)
            c_val = int(max(180, 255 - dist * 4))
            draw.point((x, y), fill=(c_val, c_val, int(c_val * 0.85), 255))
            height[y, x] = 1.0

    # Bright central quartz halogen filament emitter
    draw.ellipse([60, 98, 68, 106], fill=(255, 255, 255, 255))

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr, strength=4.5)

    img.save(os.path.join(out_dir, "Lamp_Industrial_Swivel_Floodlight_128x128.png"))
    norm_img.save(os.path.join(normal_dir, "Lamp_Industrial_Swivel_Floodlight_128x128_NormalMap.png"))
    print("Saved Lamp_Industrial_Swivel_Floodlight_128x128.png & NormalMap")

if __name__ == "__main__":
    make_fluorescent_dual()
    make_swivel_floodlight()
    print("All 3 distinct lighting fixture types ready!")
