import os
import math
import numpy as np
import cv2
from PIL import Image, ImageDraw, ImageFilter

out_dir = r"C:\Users\mokos\My project\Assets\Environment\Sprites\Radome"
normal_dir = os.path.join(out_dir, "Normals")  # normal maps live in a Normals subfolder
os.makedirs(normal_dir, exist_ok=True)
os.makedirs(out_dir, exist_ok=True)

def generate_normal_from_height(height_map, alpha_mask, strength=3.5):
    h, w = height_map.shape
    dx = cv2.Sobel(height_map, cv2.CV_32F, 1, 0, ksize=3)
    dy = cv2.Sobel(height_map, cv2.CV_32F, 0, 1, ksize=3)
    
    nx = -dx * strength
    ny = dy * strength
    nz = np.ones_like(height_map)
    
    norm = np.sqrt(nx*nx + ny*ny + nz*nz)
    nx /= norm
    ny /= norm
    nz /= norm
    
    normal = np.zeros((h, w, 4), dtype=np.uint8)
    normal[:, :, 0] = np.clip((nx * 0.5 + 0.5) * 255, 0, 255).astype(np.uint8)
    normal[:, :, 1] = np.clip((ny * 0.5 + 0.5) * 255, 0, 255).astype(np.uint8)
    normal[:, :, 2] = np.clip((nz * 0.5 + 0.5) * 255, 0, 255).astype(np.uint8)
    normal[:, :, 3] = alpha_mask.astype(np.uint8)
    return Image.fromarray(normal, mode='RGBA')

# ==============================================================================
# 1. Radome Interior Dome Backdrop (2048 x 1024)
# ==============================================================================
def make_radome_interior_backdrop():
    w, h = 2048, 1024
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    cx, cy = w // 2, h - 80
    r_outer = 920
    r_inner = 880

    # Draw outer dome silhouette and paneling
    for y in range(h):
        for x in range(w):
            dx = x - cx
            dy = y - cy
            dist = math.sqrt(dx*dx + dy*dy)
            if dist <= r_outer and y <= cy:
                # Dome panel shading (spherical gradient)
                angle = math.atan2(-dy, dx)
                norm_d = dist / r_outer
                
                # Base panel tone (high-tech off-white/light metallic grey with radial seams)
                base_c = int(220 - norm_d * 50 + math.cos(angle * 16) * 6)
                
                # Geodesic / panel seam rings
                ring_mod = int(dist) % 110
                if ring_mod < 4:
                    base_c -= 25 # seam groove
                
                # Radial structural ribs
                if abs((angle * 180 / math.pi) % 15) < 0.8:
                    base_c -= 35
                
                # Vignette near ceiling apex
                if y < 200:
                    base_c = int(base_c * (0.8 + 0.2 * (y / 200.0)))
                
                c_val = max(30, min(245, base_c))
                img.putpixel((x, y), (c_val, c_val + 2, c_val + 5, 255))
                height[y, x] = 0.5 + 0.3 * (1.0 - norm_d)

    # Re-draw structural steel arch ribs along the dome
    for rib_deg in range(15, 175, 15):
        rad = math.radians(rib_deg)
        x_end = int(cx + r_outer * math.cos(rad))
        y_end = int(cy - r_outer * math.sin(rad))
        # Draw rib line
        draw.line([(cx, cy - 80), (x_end, y_end)], fill=(90, 95, 105, 255), width=6)
        draw.line([(cx, cy - 80), (x_end, y_end)], fill=(140, 150, 165, 255), width=2)

    # Perimeter ring girder at the dome base
    draw.rectangle([cx - r_outer - 10, cy - 10, cx + r_outer + 10, cy + 60], fill=(55, 60, 70, 255), outline=(30, 32, 38, 255), width=2)
    # Hazard yellow/black striping along base girder
    for sx in range(cx - r_outer, cx + r_outer, 30):
        draw.polygon([(sx, cy + 50), (sx + 15, cy + 50), (sx + 25, cy + 60), (sx + 10, cy + 60)], fill=(230, 180, 20, 255))

    # Base deck floor
    draw.rectangle([0, cy + 60, w, h], fill=(38, 42, 48, 255))
    height[cy + 60:h, :] = 0.9

    # Airlock doors at left (x=160..240) and right (x=w-240..w-160)
    for door_x in [cx - r_inner + 40, cx + r_inner - 120]:
        # Door frame
        draw.rectangle([door_x, cy - 120, door_x + 80, cy + 60], fill=(45, 50, 58, 255), outline=(20, 22, 26, 255), width=3)
        # Pressure door
        draw.rectangle([door_x + 10, cy - 110, door_x + 70, cy + 55], fill=(70, 75, 85, 255), outline=(110, 120, 135, 255), width=2)
        # Circular porthole
        draw.ellipse([door_x + 28, cy - 70, door_x + 52, cy - 46], fill=(30, 80, 120, 255), outline=(200, 210, 220, 255), width=2)
        # Status light above door
        draw.rectangle([door_x + 32, cy - 135, door_x + 48, cy - 125], fill=(40, 220, 60, 255), outline=(20, 22, 26, 255))
        height[cy - 120:cy + 60, door_x:door_x + 80] = 0.75

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr, strength=4.0)
    
    p1 = os.path.join(out_dir, "Radome_Interior_Backdrop.png")
    p2 = os.path.join(normal_dir, "Radome_Interior_Backdrop_Normal.png")
    img.save(p1)
    norm_img.save(p2)
    print("Saved Radome_Interior_Backdrop.png and Normal.")

# ==============================================================================
# 2. Octagonal Phased Array Radar Antenna Face (1024 x 1024)
# ==============================================================================
def make_xband_radar_antenna():
    w, h = 1024, 1024
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    cx, cy = w // 2, h // 2
    r_outer = 440
    r_inner = 410

    # Calculate 8 vertices of regular octagon
    def get_octagon_points(radius, center_x, center_y, angle_offset=22.5):
        pts = []
        for i in range(8):
            a = math.radians(i * 45 + angle_offset)
            pts.append((center_x + radius * math.cos(a), center_y + radius * math.sin(a)))
        return pts

    oct_outer = get_octagon_points(r_outer, cx, cy)
    oct_inner = get_octagon_points(r_inner, cx, cy)

    # 1. Outer Heavy Flange Frame (Dark Navy/Charcoal steel)
    draw.polygon(oct_outer, fill=(48, 54, 62, 255), outline=(20, 24, 28, 255))
    # 2. Inner Array Face (Segmented Light Grey Radome Panels)
    draw.polygon(oct_inner, fill=(235, 238, 242, 255), outline=(70, 78, 88, 255))

    # Height map base
    mask_poly = Image.new('L', (w, h), 0)
    ImageDraw.Draw(mask_poly).polygon(oct_outer, fill=255)
    mask_arr = np.array(mask_poly) > 0
    height[mask_arr] = 0.7

    mask_inner = Image.new('L', (w, h), 0)
    ImageDraw.Draw(mask_inner).polygon(oct_inner, fill=255)
    inner_arr = np.array(mask_inner) > 0
    height[inner_arr] = 0.85

    # 3. Phased Array Facets (8 major triangular segments radiating from center)
    for i in range(8):
        p1 = oct_inner[i]
        p2 = oct_inner[(i + 1) % 8]
        # Alternate facet shading slightly to create the 3D umbrella/cone faceted look from Image 2
        tint = 230 if i % 2 == 0 else 242
        draw.polygon([(cx, cy), p1, p2], fill=(tint, tint, tint + 2, 255), outline=(130, 140, 150, 255))
        # Radial structural spine
        draw.line([(cx, cy), p1], fill=(100, 110, 125, 255), width=3)
        draw.line([(cx, cy), p1], fill=(255, 255, 255, 200), width=1)

    # 4. Concentric Phased-Array Ring Sub-panels
    for sub_r in [120, 220, 320]:
        sub_pts = get_octagon_points(sub_r, cx, cy)
        draw.polygon(sub_pts, outline=(140, 150, 165, 255), width=2)

    # 5. Center Feed Horn / Calibrator Cap (Image 2 apex center)
    draw.ellipse([cx - 45, cy - 45, cx + 45, cy + 45], fill=(60, 68, 78, 255), outline=(30, 35, 42, 255), width=3)
    draw.ellipse([cx - 30, cy - 30, cx + 30, cy + 30], fill=(200, 210, 225, 255), outline=(100, 110, 125, 255), width=2)
    draw.ellipse([cx - 12, cy - 12, cx + 12, cy + 12], fill=(240, 245, 250, 255))
    height[cy-45:cy+46, cx-45:cx+46] = 0.95

    # 6. Perimeter Perimeter Bolts & Latching Blocks
    for pt in oct_outer:
        px, py = int(pt[0]), int(pt[1])
        draw.rectangle([px - 10, py - 10, px + 10, py + 10], fill=(80, 88, 100, 255), outline=(20, 22, 26, 255), width=2)
        draw.ellipse([px - 4, py - 4, px + 4, py + 4], fill=(200, 210, 225, 255))

    # Top Lifting Lug A-Frame Brackets (as seen in Image 2 crane rigging)
    top_y = min([p[1] for p in oct_outer])
    draw.polygon([(cx - 100, top_y + 40), (cx - 70, top_y - 30), (cx - 40, top_y + 40)], fill=(220, 160, 20, 255), outline=(40, 30, 5, 255), width=3)
    draw.polygon([(cx + 40, top_y + 40), (cx + 70, top_y - 30), (cx + 100, top_y + 40)], fill=(220, 160, 20, 255), outline=(40, 30, 5, 255), width=3)
    draw.ellipse([cx - 78, top_y - 25, cx - 62, top_y - 9], fill=(30, 30, 30, 255))
    draw.ellipse([cx + 62, top_y - 25, cx + 78, top_y - 9], fill=(30, 30, 30, 255))

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr, strength=4.5)

    p1 = os.path.join(out_dir, "XBand_Radar_Octagonal_Face.png")
    p2 = os.path.join(normal_dir, "XBand_Radar_Octagonal_Face_Normal.png")
    img.save(p1)
    norm_img.save(p2)
    print("Saved XBand_Radar_Octagonal_Face.png and Normal.")

# ==============================================================================
# 3. Radar Elevation Gantry & Pedestal Superstructure (896 x 640)
# ==============================================================================
def make_xband_radar_pedestal_gantry():
    w, h = 896, 640
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    cx = w // 2

    # Left & Right Heavy Support Trunnion A-Frames (Elevation Pivots)
    left_x = cx - 240
    right_x = cx + 240

    # 1. Rear Transmitter Box (Transmitter equipment module behind antenna)
    draw.rectangle([cx - 200, 120, cx + 200, 380], fill=(62, 68, 78, 255), outline=(25, 28, 34, 255), width=3)
    # Cooling louver vents on transmitter box
    for vy in range(160, 340, 18):
        draw.line([(cx - 160, vy), (cx + 160, vy)], fill=(35, 38, 45, 255), width=5)
        draw.line([(cx - 160, vy - 2), (cx + 160, vy - 2)], fill=(90, 100, 115, 255), width=2)
    height[120:380, cx-200:cx+200] = 0.75

    # 2. Hydraulic Elevation Actuators (Massive dual pistons)
    for px in [cx - 120, cx + 120]:
        # Piston cylinder (Yellow industrial)
        draw.rectangle([px - 22, 280, px + 22, 540], fill=(230, 175, 25, 255), outline=(40, 30, 5, 255), width=3)
        # Chrome piston rod
        draw.rectangle([px - 14, 180, px + 14, 280], fill=(225, 235, 245, 255), outline=(70, 80, 95, 255), width=2)
        # Highlight
        draw.line([(px - 4, 180), (px - 4, 540)], fill=(255, 255, 255, 220), width=4)
        # Mounting Cleve & Pin
        draw.ellipse([px - 26, 160, px + 26, 200], fill=(70, 78, 88, 255), outline=(20, 22, 26, 255), width=3)
        draw.ellipse([px - 10, 170, px + 10, 190], fill=(20, 22, 26, 255))
        height[160:540, px-26:px+27] = 0.85

    # 3. Main Support Pylons (Angled heavy steel columns)
    for pylon_x, sign in [(left_x, -1), (right_x, 1)]:
        pts = [
            (pylon_x, 140),
            (pylon_x + 60 * sign, 140),
            (pylon_x + 120 * sign, 580),
            (pylon_x - 30 * sign, 580)
        ]
        draw.polygon(pts, fill=(50, 56, 65, 255), outline=(20, 22, 26, 255), width=3)
        # Main Elevation Trunnion Bearing Hub (Center pivot where antenna rotates in pitch)
        draw.ellipse([pylon_x - 45, 110, pylon_x + 45, 190], fill=(75, 84, 98, 255), outline=(25, 28, 34, 255), width=4)
        draw.ellipse([pylon_x - 25, 130, pylon_x + 25, 170], fill=(130, 142, 160, 255), outline=(30, 35, 42, 255), width=2)
        draw.ellipse([pylon_x - 10, 140, pylon_x + 10, 160], fill=(25, 28, 34, 255))
        height[110:580, min(pylon_x-45, pylon_x+120*sign):max(pylon_x+45, pylon_x+120*sign)] = 0.9

    # Cross-bracing girder between pylons
    draw.rectangle([left_x, 500, right_x, 560], fill=(45, 50, 60, 255), outline=(20, 24, 28, 255), width=3)
    # Inspection ladder on left pylon
    for ly in range(200, 560, 20):
        draw.line([(left_x - 15, ly), (left_x + 15, ly)], fill=(230, 180, 30, 255), width=3)

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr, strength=4.5)

    p1 = os.path.join(out_dir, "XBand_Radar_Pedestal_Gantry.png")
    p2 = os.path.join(normal_dir, "XBand_Radar_Pedestal_Gantry_Normal.png")
    img.save(p1)
    norm_img.save(p2)
    print("Saved XBand_Radar_Pedestal_Gantry.png and Normal.")

# ==============================================================================
# 4. Circular Turntable Base & Silo Well (Image 2 Silo Foundation) (1280 x 384)
# ==============================================================================
def make_xband_radar_turntable_silo():
    w, h = 1280, 384
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    cx = w // 2

    # Outer Silo Well Wall (Circular concrete/armored rim seen in Image 2)
    silo_left = cx - 520
    silo_right = cx + 520

    # 1. Silo Foundation Bulkhead (Concrete/Armored Base)
    draw.rectangle([silo_left, 120, silo_right, 380], fill=(68, 74, 84, 255), outline=(28, 32, 38, 255), width=3)
    height[120:380, silo_left:silo_right] = 0.7

    # Vertical stiffener ribs around the silo perimeter (exact feature from Image 2!)
    for sx in range(silo_left + 40, silo_right - 30, 48):
        draw.rectangle([sx, 125, sx + 22, 375], fill=(52, 58, 66, 255), outline=(20, 24, 28, 255), width=2)
        draw.line([(sx + 5, 125), (sx + 5, 375)], fill=(110, 120, 135, 255), width=2)
        height[125:375, sx:sx+22] = 0.85

    # 2. Bull-Gear Azimuth Rotation Ring (The massive geared track)
    gear_y = 60
    draw.rectangle([silo_left + 20, gear_y, silo_right - 20, 120], fill=(85, 92, 105, 255), outline=(30, 34, 40, 255), width=3)
    # Gear teeth
    for gx in range(silo_left + 30, silo_right - 30, 16):
        draw.rectangle([gx, gear_y + 8, gx + 8, 112], fill=(40, 44, 52, 255))
        draw.point((gx + 2, gear_y + 12), fill=(180, 190, 205, 255))
    height[gear_y:120, silo_left+20:silo_right-20] = 0.9

    # 3. Top Turntable Platform (Rotating carriage floor)
    draw.rectangle([silo_left + 50, 10, silo_right - 50, 60], fill=(48, 54, 62, 255), outline=(20, 24, 28, 255), width=2)
    # Yellow hazard safety border
    for hy in range(silo_left + 50, silo_right - 50, 25):
        draw.polygon([(hy, 45), (hy + 12, 45), (hy + 22, 58), (hy + 10, 58)], fill=(235, 185, 20, 255))
    height[10:60, silo_left+50:silo_right-50] = 0.95

    # 4. Circular Access Ring Catwalk around the Silo
    catwalk_y = 120
    draw.rectangle([silo_left - 60, catwalk_y, silo_left, catwalk_y + 16], fill=(30, 35, 42, 255))
    draw.rectangle([silo_right, catwalk_y, silo_right + 60, catwalk_y + 16], fill=(30, 35, 42, 255))
    # Railing
    for rx in [silo_left - 60, silo_right + 50]:
        draw.line([(rx, catwalk_y - 45), (rx + 50, catwalk_y - 45)], fill=(220, 170, 20, 255), width=3)
        draw.line([(rx, catwalk_y - 20), (rx + 50, catwalk_y - 20)], fill=(220, 170, 20, 255), width=2)
        for st in range(rx, rx + 55, 25):
            draw.line([(st, catwalk_y - 45), (st, catwalk_y)], fill=(120, 130, 145, 255), width=3)

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr, strength=4.5)

    p1 = os.path.join(out_dir, "XBand_Radar_Turntable_Silo.png")
    p2 = os.path.join(normal_dir, "XBand_Radar_Turntable_Silo_Normal.png")
    img.save(p1)
    norm_img.save(p2)
    print("Saved XBand_Radar_Turntable_Silo.png and Normal.")

# ==============================================================================
# 5. Radome Modular Catwalk Platform (512 x 128)
# ==============================================================================
def make_radome_catwalk_platform():
    w, h = 512, 128
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    deck_y = 70

    # 1. Handrails (Yellow industrial safety rail)
    draw.line([(0, deck_y - 50), (w, deck_y - 50)], fill=(235, 185, 20, 255), width=4) # top rail
    draw.line([(0, deck_y - 25), (w, deck_y - 25)], fill=(235, 185, 20, 255), width=3) # mid rail
    # Stanchion posts every 64 px
    for px in range(0, w + 1, 64):
        draw.line([(px, deck_y - 50), (px, deck_y)], fill=(80, 88, 100, 255), width=4)
        draw.line([(px - 1, deck_y - 50), (px - 1, deck_y)], fill=(150, 160, 175, 255), width=2)

    # 2. Yellow Toe-board / Kickplate
    draw.rectangle([0, deck_y - 12, w, deck_y], fill=(210, 160, 15, 255), outline=(50, 40, 5, 255), width=1)

    # 3. Steel Grating Walkway Deck (Diamond grating with transparency)
    draw.rectangle([0, deck_y, w, deck_y + 18], fill=(50, 55, 65, 255), outline=(20, 22, 26, 255), width=2)
    # Grate mesh pattern
    for mx in range(0, w, 8):
        draw.line([(mx, deck_y + 2), (mx + 6, deck_y + 16)], fill=(25, 28, 32, 255), width=2)
        draw.line([(mx + 6, deck_y + 2), (mx, deck_y + 16)], fill=(90, 100, 115, 255), width=1)

    # 4. Under-deck Support Truss Brackets
    for bx in range(32, w, 128):
        pts = [(bx, deck_y + 18), (bx + 30, deck_y + 18), (bx, deck_y + 55)]
        draw.polygon(pts, fill=(40, 44, 52, 255), outline=(15, 18, 22, 255), width=2)
        pts2 = [(bx + 30, deck_y + 18), (bx + 60, deck_y + 18), (bx + 60, deck_y + 55)]
        draw.polygon(pts2, fill=(40, 44, 52, 255), outline=(15, 18, 22, 255), width=2)

    height[deck_y:deck_y+18, :] = 0.95
    height[deck_y-12:deck_y, :] = 0.9

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr, strength=4.0)

    p1 = os.path.join(out_dir, "Radome_Catwalk_Platform.png")
    p2 = os.path.join(normal_dir, "Radome_Catwalk_Platform_Normal.png")
    img.save(p1)
    norm_img.save(p2)
    print("Saved Radome_Catwalk_Platform.png and Normal.")

if __name__ == "__main__":
    make_radome_interior_backdrop()
    make_xband_radar_antenna()
    make_xband_radar_pedestal_gantry()
    make_xband_radar_turntable_silo()
    make_radome_catwalk_platform()
