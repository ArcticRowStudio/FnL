import os
import math
import numpy as np
import cv2
from PIL import Image, ImageDraw

out_dir = r"C:\Users\mokos\My project\Assets\Environment\Sprites\Radome"
normal_dir = os.path.join(out_dir, "Normals")  # normal maps live in a Normals subfolder
os.makedirs(normal_dir, exist_ok=True)
os.makedirs(out_dir, exist_ok=True)

def generate_normal_from_height(height_map, alpha_mask, strength=3.8):
    dx = cv2.Sobel(height_map, cv2.CV_32F, 1, 0, ksize=3)
    dy = cv2.Sobel(height_map, cv2.CV_32F, 0, 1, ksize=3)
    
    nx = -dx * strength
    ny = dy * strength
    nz = np.ones_like(height_map)
    
    norm = np.sqrt(nx*nx + ny*ny + nz*nz)
    nx /= norm
    ny /= norm
    nz /= norm
    
    normal = np.zeros((height_map.shape[0], height_map.shape[1], 4), dtype=np.uint8)
    normal[:, :, 0] = np.clip((nx * 0.5 + 0.5) * 255, 0, 255).astype(np.uint8)
    normal[:, :, 1] = np.clip((ny * 0.5 + 0.5) * 255, 0, 255).astype(np.uint8)
    normal[:, :, 2] = np.clip((nz * 0.5 + 0.5) * 255, 0, 255).astype(np.uint8)
    normal[:, :, 3] = alpha_mask.astype(np.uint8)
    return Image.fromarray(normal, mode='RGBA')

# ==============================================================================
# 1. Military Naval Shipping Container (288 x 144)
# ==============================================================================
def make_naval_container():
    w, h = 288, 144
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    # Outer Frame (Corner castings and thick perimeter)
    draw.rectangle([4, 10, w - 5, h - 6], fill=(72, 78, 88, 255), outline=(24, 27, 32, 255), width=3)
    height[10:h-6, 4:w-5] = 0.65

    # 4 Corner Castings (ISO Twistlock Pockets)
    corners = [(4, 10), (w - 28, 10), (4, h - 28), (w - 28, h - 28)]
    for cx, cy in corners:
        draw.rectangle([cx, cy, cx + 24, cy + 22], fill=(50, 55, 62, 255), outline=(20, 22, 26, 255), width=2)
        draw.ellipse([cx + 6, cy + 6, cx + 18, cy + 16], fill=(15, 17, 20, 255))
        height[cy:cy+22, cx:cx+24] = 0.85

    # Corrugated Side Ribs
    rib_start_x = 32
    rib_end_x = w - 32
    for x in range(rib_start_x, rib_end_x, 14):
        # Recessed channel
        draw.rectangle([x, 18, x + 6, h - 14], fill=(58, 64, 72, 255))
        # Raised rib
        draw.rectangle([x + 6, 18, x + 13, h - 14], fill=(85, 92, 104, 255))
        draw.line([(x + 6, 18), (x + 6, h - 14)], fill=(120, 130, 145, 255), width=1) # highlight edge
        height[18:h-14, x+6:x+13] = 0.75
        height[18:h-14, x:x+6] = 0.55

    # Center Door Seam & Locking Bars (Right side container doors)
    door_seam_x = w - 80
    draw.line([(door_seam_x, 14), (door_seam_x, h - 10)], fill=(20, 22, 26, 255), width=2)
    # Dual Vertical Lock Rods & Handles
    for rx in [door_seam_x - 12, door_seam_x + 12]:
        draw.line([(rx, 12), (rx, h - 8)], fill=(180, 190, 205, 255), width=3)
        draw.line([(rx - 1, 12), (rx - 1, h - 8)], fill=(255, 255, 255, 220), width=1)
        # Cam lock hinges
        for hy in [22, h // 2, h - 22]:
            draw.rectangle([rx - 4, hy - 4, rx + 4, hy + 4], fill=(40, 44, 50, 255))
        height[12:h-8, rx-2:rx+3] = 0.95

    # Stencil Markings & Danger High Voltage Chevron
    # Caution yellow/black hatch on left
    draw.rectangle([12, 24, 38, 50], fill=(230, 180, 20, 255), outline=(30, 32, 38, 255))
    draw.polygon([(14, 24), (26, 24), (38, 36), (38, 48), (26, 48), (14, 36)], fill=(30, 32, 38, 255))
    # Military Code Stencil
    draw.line([(48, 28), (80, 28)], fill=(210, 215, 225, 255), width=2)
    draw.line([(48, 34), (72, 34)], fill=(210, 215, 225, 255), width=2)

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr, strength=4.0)

    p1 = os.path.join(out_dir, "Container_Military_NavyGrey.png")
    p2 = os.path.join(normal_dir, "Container_Military_NavyGrey_Normal.png")
    img.save(p1)
    norm_img.save(p2)
    print("Saved Container_Military_NavyGrey.png and Normal.")

# ==============================================================================
# 2. Power Generator & HVAC Container (288 x 176)
# ==============================================================================
def make_power_generator_container():
    w, h = 288, 176
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    cont_y_top = 42

    # Container Main Body
    draw.rectangle([4, cont_y_top, w - 5, h - 6], fill=(65, 72, 82, 255), outline=(22, 25, 30, 255), width=3)
    height[cont_y_top:h-6, 4:w-5] = 0.65

    # Large Heavy Intake & Exhaust Louvers (Generator Ventilation)
    for louver_box in [(16, cont_y_top + 14, 110, h - 18), (124, cont_y_top + 14, 218, h - 18)]:
        x1, y1, x2, y2 = louver_box
        draw.rectangle([x1, y1, x2, y2], fill=(30, 34, 40, 255), outline=(15, 18, 22, 255), width=2)
        for ly in range(y1 + 8, y2 - 6, 9):
            draw.line([(x1 + 4, ly), (x2 - 4, ly)], fill=(55, 60, 70, 255), width=4)
            draw.line([(x1 + 4, ly - 2), (x2 - 4, ly - 2)], fill=(110, 120, 135, 255), width=2)
            height[ly-2:ly+3, x1+4:x2-4] = 0.8

    # Operator Control Panel on right
    draw.rectangle([228, cont_y_top + 16, w - 16, h - 24], fill=(42, 46, 54, 255), outline=(18, 20, 25, 255), width=2)
    # Gauges and LEDs
    draw.ellipse([236, cont_y_top + 26, 250, cont_y_top + 40], fill=(220, 230, 240, 255), outline=(20, 22, 26, 255))
    draw.ellipse([256, cont_y_top + 26, 270, cont_y_top + 40], fill=(220, 230, 240, 255), outline=(20, 22, 26, 255))
    # Green and Red Status Lights
    draw.ellipse([240, cont_y_top + 52, 248, cont_y_top + 60], fill=(40, 230, 70, 255))
    draw.ellipse([258, cont_y_top + 52, 266, cont_y_top + 60], fill=(240, 40, 30, 255))

    # --- Roof Equipment (HVAC Cooling Fans & Exhaust Stacks) ---
    # Dual Cylindrical Cooling Fan Cowlings
    for fx in [54, 140]:
        # Cowling box
        draw.rectangle([fx - 28, cont_y_top - 28, fx + 28, cont_y_top], fill=(50, 56, 65, 255), outline=(20, 24, 28, 255), width=2)
        # Protective wire mesh dome
        draw.arc([fx - 24, cont_y_top - 38, fx + 24, cont_y_top - 20], 0, 180, fill=(160, 170, 185, 255), width=3)
        height[cont_y_top-38:cont_y_top, fx-28:fx+29] = 0.9

    # Stainless Diesel Exhaust Muffler Stack on right
    draw.rectangle([216, 6, 244, cont_y_top], fill=(130, 138, 150, 255), outline=(40, 44, 52, 255), width=2)
    draw.line([(220, 6), (220, cont_y_top)], fill=(240, 245, 255, 230), width=3) # chrome shine
    # Rain cap
    draw.polygon([(210, 6), (250, 6), (240, 0), (220, 0)], fill=(70, 75, 85, 255))
    height[0:cont_y_top, 210:250] = 0.95

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr, strength=4.2)

    p1 = os.path.join(out_dir, "Container_PowerGenerator_HVAC.png")
    p2 = os.path.join(normal_dir, "Container_PowerGenerator_HVAC_Normal.png")
    img.save(p1)
    norm_img.save(p2)
    print("Saved Container_PowerGenerator_HVAC.png and Normal.")

# ==============================================================================
# 3. Auxiliary Satellite Communication Radome (160 x 192)
# ==============================================================================
def make_auxiliary_radome():
    w, h = 160, 192
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    cx = w // 2
    dome_cy = 76
    radius = 64

    # 1. Spherical Radome (The white satellite dome seen in Image 1)
    for y in range(h):
        for x in range(w):
            dx = x - cx
            dy = y - dome_cy
            dist = math.sqrt(dx*dx + dy*dy)
            if dist <= radius:
                # 3D spherical shading with top-left specular highlight
                norm_d = dist / radius
                nx = dx / radius
                ny = -dy / radius
                nz = math.sqrt(max(0.0, 1.0 - norm_d * norm_d))
                # Light from (-0.4, 0.6, 0.7)
                light = nx * (-0.4) + ny * 0.6 + nz * 0.7
                light = max(0.0, light)
                
                c = int(185 + light * 65)
                c = min(250, max(40, c))
                
                # Geodesic seam lines
                angle = math.atan2(-dy, dx)
                if abs((angle * 180 / math.pi) % 45) < 1.8 or abs(dist - 38) < 1.2:
                    c -= 20
                    
                img.putpixel((x, y), (c, c + 2, c + 4, 255))
                height[y, x] = 0.5 + nz * 0.45

    # 2. Cylindrical Pedestal / Riser
    ped_top = dome_cy + radius - 6
    ped_bot = h - 16
    draw.rectangle([cx - 40, ped_top, cx + 40, ped_bot], fill=(55, 60, 70, 255), outline=(20, 22, 26, 255), width=2)
    # Pedestal ribbing
    draw.line([(cx - 20, ped_top), (cx - 20, ped_bot)], fill=(85, 95, 110, 255), width=3)
    draw.line([(cx + 20, ped_top), (cx + 20, ped_bot)], fill=(85, 95, 110, 255), width=3)
    height[ped_top:ped_bot, cx-40:cx+41] = 0.75

    # 3. Flanged Deck Mounting Base Plate
    draw.rectangle([cx - 56, ped_bot, cx + 56, h - 4], fill=(42, 46, 54, 255), outline=(18, 20, 24, 255), width=2)
    # Heavy mounting bolts
    for bx in [cx - 48, cx - 28, cx + 28, cx + 48]:
        draw.rectangle([bx - 3, h - 12, bx + 3, h - 6], fill=(180, 190, 205, 255))
    height[ped_bot:h-4, cx-56:cx+57] = 0.85

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr, strength=4.5)

    p1 = os.path.join(out_dir, "Deck_Auxiliary_Radome.png")
    p2 = os.path.join(normal_dir, "Deck_Auxiliary_Radome_Normal.png")
    img.save(p1)
    norm_img.save(p2)
    print("Saved Deck_Auxiliary_Radome.png and Normal.")

# ==============================================================================
# 4. Articulated Deck Crane (Yellow Industrial Marine Crane) (256 x 256)
# ==============================================================================
def make_deck_crane():
    w, h = 256, 256
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    base_x = 70
    base_y = h - 20

    # 1. Turntable Pedestal Base
    draw.rectangle([base_x - 30, base_y - 45, base_x + 30, base_y], fill=(45, 50, 60, 255), outline=(18, 20, 25, 255), width=2)
    draw.rectangle([base_x - 24, base_y - 70, base_x + 24, base_y - 45], fill=(225, 170, 20, 255), outline=(35, 28, 5, 255), width=2)
    # Slewing ring gear
    draw.rectangle([base_x - 28, base_y - 48, base_x + 28, base_y - 42], fill=(70, 75, 85, 255))
    height[base_y-70:base_y, base_x-30:base_x+31] = 0.8

    # 2. Main Crane Mast & Operator Cab
    mast_top_y = base_y - 120
    draw.rectangle([base_x - 18, mast_top_y, base_x + 18, base_y - 70], fill=(235, 180, 20, 255), outline=(35, 28, 5, 255), width=2)
    # Operator window
    draw.rectangle([base_x - 12, mast_top_y + 12, base_x + 12, mast_top_y + 36], fill=(30, 80, 120, 255), outline=(20, 22, 26, 255))

    # 3. Main Articulated Boom Arm (Reaching right toward deck)
    boom_end_x = w - 24
    boom_end_y = mast_top_y - 30
    draw.polygon([
        (base_x, mast_top_y),
        (base_x + 16, mast_top_y - 12),
        (boom_end_x, boom_end_y - 4),
        (boom_end_x, boom_end_y + 8),
        (base_x, mast_top_y + 16)
    ], fill=(240, 185, 20, 255), outline=(35, 28, 5, 255), width=2)
    # Hydraulic Lift Cylinder
    draw.line([(base_x + 12, mast_top_y + 25), (base_x + 80, mast_top_y - 5)], fill=(190, 200, 215, 255), width=5)
    draw.line([(base_x + 80, mast_top_y - 5), (base_x + 115, boom_end_y + 4)], fill=(215, 160, 15, 255), width=7)

    # 4. Hoist Cable & Hook Block
    hook_y = boom_end_y + 70
    draw.line([(boom_end_x - 6, boom_end_y), (boom_end_x - 6, hook_y)], fill=(30, 32, 38, 255), width=2)
    # Hook block (Yellow with stripes)
    draw.rectangle([boom_end_x - 14, hook_y, boom_end_x + 2, hook_y + 20], fill=(225, 175, 20, 255), outline=(25, 20, 5, 255))
    # Forged steel hook
    draw.arc([boom_end_x - 16, hook_y + 14, boom_end_x + 4, hook_y + 34], 0, 180, fill=(60, 65, 75, 255), width=3)

    height[mast_top_y-30:base_y, 40:w-15] = 0.85

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr, strength=4.0)

    p1 = os.path.join(out_dir, "Deck_Crane_Articulated.png")
    p2 = os.path.join(normal_dir, "Deck_Crane_Articulated_Normal.png")
    img.save(p1)
    norm_img.save(p2)
    print("Saved Deck_Crane_Articulated.png and Normal.")

if __name__ == "__main__":
    make_naval_container()
    make_power_generator_container()
    make_auxiliary_radome()
    make_deck_crane()
