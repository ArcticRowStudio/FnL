import os
import math
import numpy as np
import cv2
from PIL import Image, ImageDraw

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
    
    norm = np.sqrt(nx*nx + ny*ny + nz*nz) + 1e-6
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
# 1. High-Res Octagonal Phased Array Radar Face (2048 x 2048, PPU=128 -> 16.0 x 16.0)
# ==============================================================================
def make_highres_radar_face():
    w, h = 2048, 2048
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    cx, cy = w // 2, h // 2
    r_outer = 940
    r_inner = 880

    def get_oct_points(center_x, center_y, radius):
        pts = []
        for i in range(8):
            angle = math.pi / 8 + i * (math.pi / 4)
            pts.append((int(center_x + radius * math.cos(angle)), int(center_y + radius * math.sin(angle))))
        return pts

    oct_outer = get_oct_points(cx, cy, r_outer)
    oct_inner = get_oct_points(cx, cy, r_inner)

    # Outer Structural Rim (Dark military bronze-steel)
    draw.polygon(oct_outer, fill=(40, 44, 52, 255), outline=(15, 18, 22, 255))
    for i in range(8):
        p0 = oct_outer[i]
        p1 = oct_outer[(i + 1) % 8]
        draw.line([p0, p1], fill=(95, 105, 120, 255), width=5)
    cv2.fillPoly(height, [np.array(oct_outer, dtype=np.int32)], 0.75)

    # Inner Phased-Array Active Aperture (Rich Golden-Amber SBX Radar Reflector)
    # Base Golden-Amber fill with soft radial gradient
    GOLD_BASE = (212, 158, 48, 255)       # #D49E30
    GOLD_LIGHT = (245, 192, 75, 255)      # #F5C04B
    GOLD_DARK = (168, 120, 32, 255)       # #A87820
    draw.polygon(oct_inner, fill=GOLD_BASE, outline=(30, 34, 42, 255))
    cv2.fillPoly(height, [np.array(oct_inner, dtype=np.int32)], 0.88)

    # Thousands of Golden Phased-Array T/R Module Micro-Tiles
    grid_step = 28
    grid_layer = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    grid_draw = ImageDraw.Draw(grid_layer)
    for gx in range(cx - r_inner + 40, cx + r_inner - 40, grid_step):
        for gy in range(cy - r_inner + 40, cy + r_inner - 40, grid_step):
            dx = gx - cx
            dy = gy - cy
            if dx*dx + dy*dy < (r_inner - 45)**2:
                # Gold tile border with metallic highlight
                grid_draw.rectangle([gx, gy, gx + grid_step - 4, gy + grid_step - 4], fill=GOLD_LIGHT, outline=GOLD_DARK, width=1)
                # Central dipole micro-patch (subtle bronze amber)
                grid_draw.rectangle([gx + 7, gy + 7, gx + grid_step - 11, gy + grid_step - 11], fill=(195, 142, 38, 255))

    # Mask grid to oct_inner
    mask = Image.new('L', (w, h), 0)
    ImageDraw.Draw(mask).polygon(oct_inner, fill=255)
    grid_layer.putalpha(Image.composite(grid_layer.split()[3], Image.new('L', (w, h), 0), mask))
    img.alpha_composite(grid_layer)

    # Heavy Geodesic Radial Spokes & Cross Seams (Distinctive SBX-1 antenna face pattern)
    for i in range(8):
        angle = math.pi / 8 + i * (math.pi / 4)
        ex = int(cx + r_inner * math.cos(angle))
        ey = int(cy + r_inner * math.sin(angle))
        # Deep groove / structural divider
        draw.line([(cx, cy), (ex, ey)], fill=(65, 45, 12, 255), width=10)
        draw.line([(cx, cy), (ex, ey)], fill=(255, 215, 110, 255), width=3)
        cv2.line(height, (cx, cy), (ex, ey), 0.95, 8)

    # Secondary cross dividers (horizontal and vertical main axis)
    draw.line([(cx - r_inner + 20, cy), (cx + r_inner - 20, cy)], fill=(65, 45, 12, 255), width=8)
    draw.line([(cx - r_inner + 20, cy), (cx + r_inner - 20, cy)], fill=(255, 215, 110, 255), width=2)
    draw.line([(cx, cy - r_inner + 20), (cx, cy + r_inner - 20)], fill=(65, 45, 12, 255), width=8)
    draw.line([(cx, cy - r_inner + 20), (cx, cy + r_inner - 20)], fill=(255, 215, 110, 255), width=2)

    # Concentric Structural Rings (3 rings)
    for ring_r in [260, 520, 740]:
        ring_pts = get_oct_points(cx, cy, ring_r)
        draw.polygon(ring_pts, outline=(120, 85, 22, 255), width=6)
        # Ring highlight
        for i in range(8):
            draw.line([ring_pts[i], ring_pts[(i+1)%8]], fill=(255, 225, 130, 255), width=2)

    # Central Sensor Array (Dark Bronze/Gold feed)
    feed_r = 110
    draw.ellipse([cx - feed_r, cy - feed_r, cx + feed_r, cy + feed_r], fill=(42, 35, 24, 255), outline=(18, 16, 12, 255), width=6)
    draw.ellipse([cx - feed_r + 14, cy - feed_r + 14, cx + feed_r - 14, cy + feed_r - 14], fill=(160, 115, 30, 255), outline=(240, 195, 80, 255), width=3)
    # Bright Gold Center
    draw.ellipse([cx - 40, cy - 40, cx + 40, cy + 40], fill=(255, 210, 50, 255), outline=(130, 90, 15, 255), width=3)
    draw.ellipse([cx - 16, cy - 16, cx + 16, cy + 16], fill=(255, 240, 140, 255))
    cv2.circle(height, (cx, cy), feed_r, 0.98, -1)

    # Perimeter Heavy Clamps & Azimuth Lock Pins (16 clamps around outer rim)
    for i in range(16):
        angle = i * (math.pi / 8)
        px = int(cx + (r_outer - 25) * math.cos(angle))
        py = int(cy + (r_outer - 25) * math.sin(angle))
        draw.rectangle([px - 16, py - 16, px + 16, py + 16], fill=(30, 34, 42, 255), outline=(15, 18, 22, 255), width=2)
        draw.ellipse([px - 8, py - 8, px + 8, py + 8], fill=(160, 175, 195, 255))

    # Outer boundary outline
    draw.polygon(oct_outer, outline=(15, 18, 22, 255), width=6)

    alpha_mask = np.array(img)[:, :, 3]
    normal_img = generate_normal_from_height(height, alpha_mask, strength=3.5)

    img.save(os.path.join(out_dir, "XBand_Radar_Octagonal_Face.png"))
    normal_img.save(os.path.join(normal_dir, "XBand_Radar_Octagonal_Face_Normal.png"))
    print("Saved High-Res XBand_Radar_Octagonal_Face (2048x2048).")

# ==============================================================================
# 2. High-Res Radar Pedestal & Gantry (2048 x 1755, PPU=128 -> 16.0 x 13.7)
# ==============================================================================
def make_highres_radar_pedestal():
    w, h = 2048, 1755
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    cx = w // 2

    # Left & Right Massive Box-Truss Support A-Frames
    legs = [
        # Left Leg: base (cx - 720, h - 60) to pivot (cx - 360, 240)
        ((cx - 740, h - 60), (cx - 560, h - 60), (cx - 320, 240), (cx - 440, 240)),
        # Right Leg: base (cx + 560, h - 60) to pivot (cx + 360, 240)
        ((cx + 560, h - 60), (cx + 740, h - 60), (cx + 440, 240), (cx + 320, 240))
    ]

    for leg_pts in legs:
        draw.polygon(leg_pts, fill=(52, 60, 74, 255), outline=(18, 22, 28, 255), width=5)
        # Highlight along outside edge
        draw.line([leg_pts[0], leg_pts[3]], fill=(115, 130, 150, 255), width=5)
        # Shadow along inside edge
        draw.line([leg_pts[1], leg_pts[2]], fill=(28, 34, 42, 255), width=5)
        cv2.fillPoly(height, [np.array(leg_pts, dtype=np.int32)], 0.80)

        # Cross Lattice Bracing inside the leg
        p_base_mid = ((leg_pts[0][0] + leg_pts[1][0]) // 2, (leg_pts[0][1] + leg_pts[1][1]) // 2)
        p_top_mid = ((leg_pts[2][0] + leg_pts[3][0]) // 2, (leg_pts[2][1] + leg_pts[3][1]) // 2)
        for t in np.linspace(0.15, 0.85, 6):
            lx0 = int(leg_pts[0][0] + (leg_pts[3][0] - leg_pts[0][0]) * t)
            ly0 = int(leg_pts[0][1] + (leg_pts[3][1] - leg_pts[0][1]) * t)
            lx1 = int(leg_pts[1][0] + (leg_pts[2][0] - leg_pts[1][0]) * (t + 0.12))
            ly1 = int(leg_pts[1][1] + (leg_pts[2][1] - leg_pts[1][1]) * (t + 0.12))
            draw.line([(lx0, ly0), (lx1, ly1)], fill=(75, 86, 102, 255), width=8)
            draw.line([(lx1, ly1), (lx0, int(ly0 + 120))], fill=(75, 86, 102, 255), width=8)

    # Elevation Trunnion Bearings (Pivots at top of legs)
    pivots = [(cx - 380, 240), (cx + 380, 240)]
    for px, py in pivots:
        # Housing Collar
        draw.ellipse([px - 90, py - 90, px + 90, py + 90], fill=(42, 48, 60, 255), outline=(18, 20, 26, 255), width=6)
        # Chrome Pivot Pin
        draw.ellipse([px - 50, py - 50, px + 50, py + 50], fill=(160, 175, 195, 255), outline=(22, 26, 32, 255), width=3)
        draw.ellipse([px - 20, py - 20, px + 20, py + 20], fill=(240, 245, 255, 255))
        cv2.circle(height, (px, py), 90, 0.95, -1)

    # Dual Giant Hydraulic Elevation Actuators (Connecting base center to trunnions)
    cylinders = [
        # Left actuator: base (cx - 240, h - 220) to (cx - 300, 360)
        ((cx - 240, h - 220), (cx - 320, 360)),
        # Right actuator: base (cx + 240, h - 220) to (cx + 320, 360)
        ((cx + 240, h - 220), (cx + 320, 360))
    ]

    for p_base, p_target in cylinders:
        # Barrel (Dark blue-grey cylinder)
        mid_p = (int(p_base[0] + (p_target[0] - p_base[0]) * 0.55), int(p_base[1] + (p_target[1] - p_base[1]) * 0.55))
        draw.line([p_base, mid_p], fill=(36, 42, 52, 255), width=48)
        draw.line([p_base, mid_p], fill=(18, 22, 28, 255), width=54)
        draw.line([p_base, mid_p], fill=(90, 105, 125, 255), width=10) # specular highlight
        # Chrome Rod (Bright silver mirror finish)
        draw.line([mid_p, p_target], fill=(180, 195, 215, 255), width=30)
        draw.line([mid_p, p_target], fill=(255, 255, 255, 255), width=8) # glint
        cv2.line(height, p_base, p_target, 0.90, 48)

    # Heavy Cross-Deck Torque Tube & Horizontal Support Girder (h - 260 to h - 140)
    gy0, gy1 = h - 260, h - 140
    gw = 1200
    draw.rectangle([cx - gw // 2, gy0, cx + gw // 2, gy1], fill=(46, 54, 66, 255), outline=(18, 20, 26, 255), width=5)
    draw.line([(cx - gw // 2 + 6, gy0 + 6), (cx + gw // 2 - 6, gy0 + 6)], fill=(110, 125, 145, 255), width=4)
    height[gy0:gy1, cx-gw//2:cx+gw//2] = 0.85

    # Hazard Stripe Chevron on Center Torque Tube
    hx0, hy0, hx1, hy1 = cx - 180, gy0 + 20, cx + 180, gy1 - 20
    draw.rectangle([hx0, hy0, hx1, hy1], fill=(235, 185, 20, 255), outline=(18, 22, 28, 255), width=3)
    for sx in range(hx0 - 60, hx1 + 60, 40):
        draw.polygon([(sx, hy1), (sx + 20, hy1), (sx + 45, hy0), (sx + 25, hy0)], fill=(24, 28, 34, 255))
    draw.rectangle([hx0, hy0, hx1, hy1], outline=(18, 22, 28, 255), width=3)

    # Massive Foundation Footing Flanges (Bolted to Turntable)
    for bx in [cx - 650, cx + 650]:
        draw.rectangle([bx - 180, h - 80, bx + 180, h - 20], fill=(36, 42, 52, 255), outline=(16, 18, 24, 255), width=4)
        for fx in range(bx - 150, bx + 150, 45):
            draw.ellipse([fx - 8, h - 60, fx + 8, h - 40], fill=(140, 152, 168, 255), outline=(20, 24, 30, 255), width=2)
        height[h-80:h-20, bx-180:bx+180] = 0.90

    alpha_mask = np.array(img)[:, :, 3]
    normal_img = generate_normal_from_height(height, alpha_mask, strength=3.5)

    img.save(os.path.join(out_dir, "XBand_Radar_Pedestal_Gantry.png"))
    normal_img.save(os.path.join(normal_dir, "XBand_Radar_Pedestal_Gantry_Normal.png"))
    print("Saved High-Res XBand_Radar_Pedestal_Gantry (2048x1755).")

# ==============================================================================
# 3. High-Res Turntable Silo Base (2048 x 768, PPU=128 -> 16.0 x 6.0)
# ==============================================================================
def make_highres_turntable_silo():
    w, h = 2048, 768
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    cx = w // 2

    # Circular Azimuth Slewing Ring Gear (Top section: y: 40 to 220)
    ring_w = 1760
    draw.rectangle([cx - ring_w // 2, 40, cx + ring_w // 2, 220], fill=(52, 60, 72, 255), outline=(18, 20, 26, 255), width=5)
    draw.line([(cx - ring_w // 2 + 6, 46), (cx + ring_w // 2 - 6, 46)], fill=(125, 140, 160, 255), width=4)
    height[40:220, cx-ring_w//2:cx+ring_w//2] = 0.85

    # Precision Machined Azimuth Gear Teeth (110 teeth)
    for gx in range(cx - ring_w // 2 + 30, cx + ring_w // 2 - 30, 24):
        draw.rectangle([gx, 65, gx + 12, 185], fill=(30, 36, 44, 255))
        draw.line([(gx + 3, 65), (gx + 3, 185)], fill=(160, 175, 195, 255), width=3) # chrome teeth edge

    # 4 Azimuth Pinion Drive Motors with planetary reduction gearboxes
    motor_xs = [cx - 620, cx - 220, cx + 220, cx + 620]
    for mx in motor_xs:
        draw.rectangle([mx - 55, 30, mx + 55, 230], fill=(38, 44, 54, 255), outline=(16, 20, 25, 255), width=4)
        draw.line([(mx - 48, 36), (mx + 48, 36)], fill=(90, 105, 125, 255), width=3)
        # Cooling ribs on motor
        for ry in range(55, 210, 20):
            draw.line([(mx - 45, ry), (mx + 45, ry)], fill=(65, 75, 90, 255), width=4)
        height[30:230, mx-55:mx+55] = 0.95

    # Silo Well Cylindrical Trunk (y: 220 to h - 40)
    trunk_w = 1680
    for y in range(220, h - 40):
        ratio = (y - 220) / (h - 260)
        # Cylindrical curved shading
        r = int(48 * (1.0 - ratio * 0.25))
        g = int(54 * (1.0 - ratio * 0.25))
        b = int(66 * (1.0 - ratio * 0.25))
        draw.line([(cx - trunk_w // 2, y), (cx + trunk_w // 2, y)], fill=(r, g, b, 255))
    height[220:h-40, cx-trunk_w//2:cx+trunk_w//2] = 0.75

    # Heavy Vertical Stiffener Ribs on Silo Wall (Every 160px)
    for rx in range(cx - trunk_w // 2 + 80, cx + trunk_w // 2, 160):
        draw.rectangle([rx - 12, 220, rx + 12, h - 40], fill=(32, 38, 48, 255), outline=(16, 18, 24, 255), width=2)
        draw.line([(rx - 6, 222), (rx - 6, h - 42)], fill=(90, 105, 125, 255), width=3)
        height[220:h-40, rx-12:rx+12] = 0.88

    # Access Maintenance Doors & Safety Interlocks
    door_w, door_h = 160, 240
    door_x0, door_y0 = cx - door_w // 2, h - 40 - door_h - 20
    draw.rectangle([door_x0, door_y0, door_x0 + door_w, door_y0 + door_h], fill=(28, 34, 42, 255), outline=(16, 18, 22, 255), width=4)
    draw.rectangle([door_x0 + 12, door_y0 + 12, door_x0 + door_w - 12, door_y0 + door_h - 12], fill=(45, 52, 64, 255), outline=(100, 115, 135, 255), width=2)
    # Circular porthole
    draw.ellipse([cx - 28, door_y0 + 40, cx + 28, door_y0 + 96], fill=(20, 60, 90, 255), outline=(180, 195, 215, 255), width=3)
    # Caution High Voltage badge on door
    draw.rectangle([cx - 35, door_y0 + 130, cx + 35, door_y0 + 175], fill=(235, 185, 20, 255), outline=(18, 20, 26, 255), width=2)
    height[door_y0:door_y0+door_h, door_x0:door_x0+door_w] = 0.78

    # Bottom Foundation Flange Bolted to Hull Deck (h - 50 to h - 10)
    base_w = 1860
    draw.rectangle([cx - base_w // 2, h - 50, cx + base_w // 2, h - 10], fill=(30, 36, 44, 255), outline=(14, 16, 20, 255), width=4)
    draw.line([(cx - base_w // 2 + 6, h - 46), (cx + base_w // 2 - 6, h - 46)], fill=(90, 105, 125, 255), width=3)
    for bx in range(cx - base_w // 2 + 40, cx + base_w // 2 - 20, 48):
        draw.ellipse([bx - 6, h - 36, bx + 6, h - 22], fill=(130, 142, 160, 255))
    height[h-50:h-10, cx-base_w//2:cx+base_w//2] = 0.90

    # Outer outline
    draw.rectangle([cx - trunk_w // 2, 220, cx + trunk_w // 2, h - 40], outline=(15, 18, 22, 255), width=4)

    alpha_mask = np.array(img)[:, :, 3]
    normal_img = generate_normal_from_height(height, alpha_mask, strength=3.5)

    img.save(os.path.join(out_dir, "XBand_Radar_Turntable_Silo.png"))
    normal_img.save(os.path.join(normal_dir, "XBand_Radar_Turntable_Silo_Normal.png"))
    print("Saved High-Res XBand_Radar_Turntable_Silo (2048x768).")

# ==============================================================================
# 4. High-Res Catwalk Platform (2048 x 512, PPU=128 -> 16.0 x 4.0)
# ==============================================================================
def make_highres_catwalk_platform():
    w, h = 2048, 512
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    YELLOW = (235, 185, 20, 255)
    YELLOW_LIGHT = (255, 225, 70, 255)
    OUTLINE = (18, 20, 25, 255)

    # Deck level runs at y: 240
    # Top Section: Safety Yellow Handrail (y: 60 to 240)
    # Handrail Top Tube (y: 60 to 86)
    draw.rectangle([0, 60, w, 86], fill=YELLOW, outline=OUTLINE, width=4)
    draw.line([(0, 65), (w, 65)], fill=YELLOW_LIGHT, width=4)
    height[60:86, :] = 0.95

    # Mid Handrail Tube (y: 140 to 160)
    draw.rectangle([0, 140, w, 160], fill=YELLOW, outline=OUTLINE, width=3)
    draw.line([(0, 143), (w, 143)], fill=YELLOW_LIGHT, width=3)
    height[140:160, :] = 0.88

    # Vertical Posts every 180px
    for sx in range(40, w, 180):
        draw.rectangle([sx - 10, 60, sx + 10, 240], fill=YELLOW, outline=OUTLINE, width=3)
        draw.line([(sx - 4, 62), (sx - 4, 238)], fill=YELLOW_LIGHT, width=3)
        height[60:240, sx-10:sx+10] = 0.92

    # Kickplate Toe-board (y: 216 to 240)
    draw.rectangle([0, 216, w, 240], fill=YELLOW, outline=OUTLINE, width=3)
    height[216:240, :] = 0.85

    # Catwalk Structural Deck Floor (y: 240 to 320)
    draw.rectangle([0, 240, w, 320], fill=(48, 54, 66, 255), outline=OUTLINE, width=4)
    draw.line([(0, 244), (w, 244)], fill=(110, 125, 145, 255), width=3)
    height[240:320, :] = 0.80

    # Grated Mesh / Diamond Floor Texture
    for gx in range(0, w, 24):
        draw.line([(gx, 246), (gx + 40, 316)], fill=(32, 38, 48, 200), width=2)
        draw.line([(gx + 40, 246), (gx, 316)], fill=(32, 38, 48, 200), width=2)

    # Cantilever Diagonal Steel Brackets underneath (y: 320 to 500)
    for bx in range(120, w, 360):
        b_pts = [(bx - 40, 320), (bx + 40, 320), (bx - 120, 500), (bx - 180, 500)]
        draw.polygon(b_pts, fill=(38, 44, 54, 255), outline=OUTLINE, width=4)
        draw.line([b_pts[0], b_pts[3]], fill=(95, 110, 130, 255), width=4)
        cv2.fillPoly(height, [np.array(b_pts, dtype=np.int32)], 0.70)

    alpha_mask = np.array(img)[:, :, 3]
    normal_img = generate_normal_from_height(height, alpha_mask, strength=3.5)

    img.save(os.path.join(out_dir, "Radome_Catwalk_Platform.png"))
    normal_img.save(os.path.join(normal_dir, "Radome_Catwalk_Platform_Normal.png"))
    print("Saved High-Res Radome_Catwalk_Platform (2048x512).")

# ==============================================================================
# 5. High-Res Radome Interior Geodesic Dome Backdrop (4096 x 2048, PPU=48 -> 85.3 x 42.6)
# ==============================================================================
def make_highres_radome_backdrop():
    w, h = 4096, 2048
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    cx, cy = w // 2, h - 160
    r_outer = 1920
    r_inner = 1840

    # Spherical Dome Surface with Radial Panel Shading
    for y in range(0, h, 2): # step 2 for performance, then smooth
        for x in range(0, w, 2):
            dx = x - cx
            dy = y - cy
            dist = math.sqrt(dx*dx + dy*dy)
            if dist <= r_outer and y <= cy:
                norm_d = dist / r_outer
                angle = math.atan2(-dy, dx)
                
                # Base tone (off-white high-tech radar membrane)
                base = 225 - norm_d * 45 + math.cos(angle * 24) * 4
                
                # Geodesic rings
                ring_mod = int(dist) % 180
                if ring_mod < 6:
                    base -= 25
                
                # Vignette at top apex
                if y < 400:
                    base *= (0.82 + 0.18 * (y / 400.0))
                    
                c_val = int(np.clip(base, 40, 245))
                # 2x2 block
                img.putpixel((x, y), (c_val, c_val + 2, c_val + 5, 255))
                if x + 1 < w: img.putpixel((x + 1, y), (c_val, c_val + 2, c_val + 5, 255))
                if y + 1 < h: img.putpixel((x, y + 1), (c_val, c_val + 2, c_val + 5, 255))
                if x + 1 < w and y + 1 < h: img.putpixel((x + 1, y + 1), (c_val, c_val + 2, c_val + 5, 255))
                height[y:y+2, x:x+2] = 0.55 + 0.3 * (1.0 - norm_d)

    # Crisp Structural Arch Ribs (Every 12 degrees)
    for deg in range(12, 170, 12):
        rad = math.radians(deg)
        x_end = int(cx + r_outer * math.cos(rad))
        y_end = int(cy - r_outer * math.sin(rad))
        draw.line([(cx, cy - 140), (x_end, y_end)], fill=(75, 82, 95, 255), width=8)
        draw.line([(cx, cy - 140), (x_end, y_end)], fill=(130, 142, 160, 255), width=3)
        cv2.line(height, (cx, cy - 140), (x_end, y_end), 0.85, 8)

    # Perimeter Base Ring Girder (cy - 20 to cy + 120)
    draw.rectangle([cx - r_outer - 20, cy - 20, cx + r_outer + 20, cy + 120], fill=(50, 56, 68, 255), outline=(18, 20, 26, 255), width=5)
    draw.line([(cx - r_outer, cy - 14), (cx + r_outer, cy - 14)], fill=(110, 125, 145, 255), width=4)
    # Yellow/Black Hazard band
    for sx in range(cx - r_outer, cx + r_outer, 50):
        draw.polygon([(sx, cy + 80), (sx + 25, cy + 80), (sx + 45, cy + 115), (sx + 20, cy + 115)], fill=(230, 180, 20, 255))
    height[cy-20:cy+120, cx-r_outer:cx+r_outer] = 0.90

    # Base Deck Floor Plating (cy + 120 to h)
    draw.rectangle([0, cy + 120, w, h], fill=(36, 40, 48, 255))
    height[cy+120:h, :] = 0.92

    # Left & Right Pressurized Airlock Gates
    for door_x in [cx - r_inner + 80, cx + r_inner - 260]:
        # Outer Portal Frame
        draw.rectangle([door_x, cy - 220, door_x + 180, cy + 120], fill=(42, 48, 58, 255), outline=(16, 18, 24, 255), width=6)
        # Heavy Steel Blast Door
        draw.rectangle([door_x + 18, cy - 200, door_x + 162, cy + 110], fill=(68, 76, 88, 255), outline=(110, 125, 145, 255), width=4)
        # Porthole window
        draw.ellipse([door_x + 55, cy - 120, door_x + 125, cy - 50], fill=(24, 60, 90, 255), outline=(200, 215, 235, 255), width=5)
        # Emergency indicator light
        draw.rectangle([door_x + 65, cy - 250, door_x + 115, cy - 230], fill=(40, 230, 80, 255), outline=(16, 18, 22, 255), width=3)
        height[cy-220:cy+120, door_x:door_x+180] = 0.85

    alpha_mask = np.array(img)[:, :, 3]
    normal_img = generate_normal_from_height(height, alpha_mask, strength=3.5)

    img.save(os.path.join(out_dir, "Radome_Interior_Backdrop.png"))
    normal_img.save(os.path.join(normal_dir, "Radome_Interior_Backdrop_Normal.png"))
    print("Saved High-Res Radome_Interior_Backdrop (4096x2048).")

if __name__ == "__main__":
    print("Generating High-Resolution SBX-1 Radome Chamber Sprites...")
    make_highres_radar_face()
    make_highres_radar_pedestal()
    make_highres_turntable_silo()
    make_highres_catwalk_platform()
    make_highres_radome_backdrop()
    print("All High-Res Radome Sprites & Normal Maps generated successfully!")
