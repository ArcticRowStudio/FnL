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

def make_cage_lamp():
    w, h = 128, 128
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    # Ceiling mounting plate & conduit box (y=0 to 24, x=44 to 84)
    # Flanged junction box
    for y in range(0, 24):
        shade = int(45 + y * 0.8)
        draw.line([(44, y), (84, y)], fill=(shade, shade + 4, shade + 8, 255))
        height[y, 44:85] = 0.7
    draw.rectangle([44, 0, 84, 24], outline=(20, 22, 26, 255), width=2)
    # Hex bolts on ceiling plate
    draw.ellipse([46, 4, 52, 10], fill=(160, 170, 180, 255))
    draw.ellipse([76, 4, 82, 10], fill=(160, 170, 180, 255))
    height[4:11, 46:53] = 0.9
    height[4:11, 76:83] = 0.9

    # Steel collar / socket cap (y=24 to 38, x=48 to 80)
    for y in range(24, 38):
        c_shade = int(55 + math.cos((y - 30)*0.2) * 15)
        draw.line([(48, y), (80, y)], fill=(c_shade, c_shade + 3, c_shade + 6, 255))
        height[y, 48:81] = 0.8
    draw.rectangle([48, 24, 80, 38], outline=(20, 22, 26, 255), width=2)

    # Thick protective glass globe (y=38 to 92, x=42 to 86)
    # Glass inner bulb & filament
    for y in range(38, 92):
        gw = int(22 * math.sin((y - 38) / 54.0 * math.pi))
        # Inner warm/white glass glow gradient
        for x in range(64 - gw, 64 + gw + 1):
            dist = math.sqrt((x - 64)**2 + (y - 62)**2)
            alpha = int(max(40, 255 - dist * 4))
            draw.point((x, y), fill=(255, 245, 200, alpha))
            height[y, x] = 0.65 + (1.0 - dist / 30.0) * 0.35

    # Glowing filament in center (y=50 to 74, x=60 to 68)
    draw.line([(60, 52), (64, 68), (68, 52)], fill=(255, 255, 255, 255), width=2)
    draw.ellipse([61, 58, 67, 64], fill=(255, 255, 220, 255))

    # Protective Heavy Steel Cage (Grid / Grille surrounding the glass)
    # Outer cage ring at top (y=40), middle (y=64), bottom (y=88)
    for ry in [40, 64, 88]:
        draw.line([(40, ry), (88, ry)], fill=(60, 65, 75, 255), width=3)
        draw.line([(41, ry - 1), (87, ry - 1)], fill=(120, 130, 142, 255), width=1) # Specular rim highlight
        height[ry-1:ry+2, 40:89] = 0.95

    # Vertical steel cage bars (5 bars around the perimeter)
    cage_xs = [42, 53, 64, 75, 86]
    for cx in cage_xs:
        for y in range(38, 96):
            draw.line([(cx, y), (cx, y)], fill=(70, 75, 85, 255), width=3)
            draw.point((cx - 1, y), fill=(130, 140, 150, 255)) # highlight
            height[y, cx-1:cx+2] = 1.0

    # Bottom cage cap / conical shield base (y=92 to 108, x=50 to 78)
    draw.polygon([(46, 92), (82, 92), (72, 106), (56, 106)], fill=(45, 50, 58, 255))
    draw.polygon([(46, 92), (82, 92), (72, 106), (56, 106)], outline=(20, 24, 28, 255))
    draw.ellipse([58, 104, 70, 110], fill=(30, 34, 40, 255), outline=(15, 18, 22, 255), width=1)
    height[92:111, 46:83] = 0.85

    # Hanging power conduit cable trailing from top left
    for ty in range(0, 28):
        c_x = int(48 - math.sin(ty * 0.15) * 5)
        draw.point((c_x, ty), fill=(25, 28, 32, 255))
        draw.point((c_x + 1, ty), fill=(40, 45, 52, 255))
        height[ty, c_x:c_x+2] = 0.75

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr, strength=4.5)

    img.save(os.path.join(out_dir, "Lamp_Bunker_Cage_Bulkhead_128x128.png"))
    norm_img.save(os.path.join(normal_dir, "Lamp_Bunker_Cage_Bulkhead_128x128_NormalMap.png"))
    print("Saved Lamp_Bunker_Cage_Bulkhead_128x128.png & NormalMap")

if __name__ == "__main__":
    make_cage_lamp()
