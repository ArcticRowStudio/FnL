import os
import math
import numpy as np
import cv2
from PIL import Image, ImageDraw, ImageFont, ImageFilter

out_dir = r"C:\Users\mokos\My project\Assets\Environment\Sprites\Radome"
normal_dir = os.path.join(out_dir, "Normals")  # normal maps live in a Normals subfolder
os.makedirs(normal_dir, exist_ok=True)
os.makedirs(out_dir, exist_ok=True)

def generate_normal_from_height(height_map, alpha_mask, strength=3.5):
    h, w = height_map.shape
    # Sobel gradients
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
# 1. High-Res Military Naval Container (1536 x 768, PPU=256 -> 6.0 x 3.0 units)
# ==============================================================================
def make_highres_military_container():
    w, h = 1536, 768
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    margin_x, margin_y = 16, 24
    frame_w = w - 2 * margin_x
    frame_h = h - 2 * margin_y
    x0, y0 = margin_x, margin_y
    x1, y1 = x0 + frame_w, y0 + frame_h

    # Container Body Background (Navy/Charcoal steel with vertical gradient)
    for y in range(y0, y1):
        ratio = (y - y0) / frame_h
        # Slate naval blue-grey: top slightly lighter, bottom darker
        r = int(58 * (1.0 - ratio * 0.25))
        g = int(66 * (1.0 - ratio * 0.25))
        b = int(76 * (1.0 - ratio * 0.25))
        draw.line([(x0, y), (x1, y)], fill=(r, g, b, 255))
    height[y0:y1, x0:x1] = 0.60

    # Corrugated Panels (Left side: 18 deep corrugation ribs)
    # Corrugation pitch = 56 px
    corrugation_end = int(w * 0.68)
    for cx in range(x0 + 80, corrugation_end, 56):
        # Recessed valley (shadow)
        draw.rectangle([cx, y0 + 36, cx + 22, y1 - 36], fill=(42, 48, 56, 255))
        height[y0+36:y1-36, cx:cx+22] = 0.50

        # Slope up (highlight line)
        draw.line([(cx + 22, y0 + 36), (cx + 22, y1 - 36)], fill=(95, 108, 124, 255), width=2)

        # Raised rib face (with subtle horizontal gradient)
        for rx in range(cx + 23, cx + 52):
            draw.line([(rx, y0 + 36), (rx, y1 - 36)], fill=(74, 84, 98, 255))
        height[y0+36:y1-36, cx+23:cx+52] = 0.75

        # Right slope down (shadow line)
        draw.line([(cx + 52, y0 + 36), (cx + 52, y1 - 36)], fill=(32, 36, 44, 255), width=2)

    # Right side: Double Cargo Doors (x0 + corrugation_end to x1 - 60)
    door_left = corrugation_end + 20
    door_right = x1 - 40
    door_w = door_right - door_left
    door_mid = door_left + door_w // 2

    # Door Frame Recess
    draw.rectangle([door_left, y0 + 32, door_right, y1 - 32], fill=(46, 52, 60, 255), outline=(26, 30, 36, 255), width=4)
    # Left door panel
    draw.rectangle([door_left + 6, y0 + 38, door_mid - 4, y1 - 38], fill=(62, 70, 82, 255), outline=(32, 36, 44, 255), width=3)
    # Right door panel
    draw.rectangle([door_mid + 4, y0 + 38, door_right - 6, y1 - 38], fill=(60, 68, 80, 255), outline=(32, 36, 44, 255), width=3)
    height[y0+38:y1-38, door_left+6:door_right-6] = 0.65

    # Center Door Seam (Black weather gasket)
    draw.rectangle([door_mid - 4, y0 + 32, door_mid + 4, y1 - 32], fill=(20, 22, 26, 255))
    height[y0+32:y1-32, door_mid-4:door_mid+4] = 0.45

    # Vertical Stainless Steel Lock Rods (2 per door = 4 rods)
    lock_rod_xs = [door_left + 70, door_mid - 70, door_mid + 70, door_right - 70]
    for rx in lock_rod_xs:
        # Shadow
        draw.line([(rx + 5, y0 + 26), (rx + 5, y1 - 26)], fill=(20, 22, 28, 255), width=4)
        # Rod body (chrome metallic gradient)
        draw.line([(rx, y0 + 24), (rx, y1 - 24)], fill=(170, 182, 196, 255), width=10)
        draw.line([(rx - 2, y0 + 24), (rx - 2, y1 - 24)], fill=(235, 242, 250, 255), width=3) # specular streak
        draw.line([(rx + 3, y0 + 24), (rx + 3, y1 - 24)], fill=(110, 120, 134, 255), width=3)
        height[y0+24:y1-24, rx-5:rx+6] = 0.95

        # Cam lock keepers (top & bottom)
        for ky in [y0 + 26, y1 - 38]:
            draw.rectangle([rx - 14, ky, rx + 14, ky + 16], fill=(50, 56, 66, 255), outline=(18, 20, 24, 255), width=2)
            draw.ellipse([rx - 6, ky + 4, rx + 6, ky + 12], fill=(130, 140, 155, 255))
            height[ky:ky+16, rx-14:rx+14] = 0.98

        # Lever Handles & Latches at waist level
        hy = y0 + frame_h // 2 + 30
        draw.rectangle([rx - 18, hy - 14, rx + 18, hy + 14], fill=(40, 46, 54, 255), outline=(18, 20, 24, 255), width=2)
        # Handle lever extending down
        draw.line([(rx, hy), (rx + 32, hy + 50)], fill=(190, 200, 215, 255), width=8)
        draw.line([(rx + 32, hy + 50), (rx + 32, hy + 75)], fill=(220, 120, 30, 255), width=9) # Rubber safety grip (orange)
        height[hy-14:hy+75, rx-18:rx+36] = 0.96

    # Heavy Heavy Outer Steel Perimeter Box & Corner Castings
    # Top & Bottom Heavy Rails
    draw.rectangle([x0, y0, x1, y0 + 32], fill=(68, 76, 88, 255), outline=(24, 28, 34, 255), width=3)
    draw.line([(x0 + 4, y0 + 3), (x1 - 4, y0 + 3)], fill=(120, 135, 155, 255), width=2) # Top highlight
    draw.rectangle([x0, y1 - 32, x1, y1], fill=(48, 54, 64, 255), outline=(22, 24, 30, 255), width=3)
    height[y0:y0+32, x0:x1] = 0.88
    height[y1-32:y1, x0:x1] = 0.88

    # Left & Right Heavy Corner Posts
    draw.rectangle([x0, y0, x0 + 48, y1], fill=(62, 70, 82, 255), outline=(24, 28, 34, 255), width=3)
    draw.rectangle([x1 - 48, y0, x1, y1], fill=(56, 62, 74, 255), outline=(24, 28, 34, 255), width=3)
    height[y0:y1, x0:x0+48] = 0.88
    height[y0:y1, x1-48:x1] = 0.88

    # 4 Heavy ISO Twistlock Corner Castings
    corners = [(x0, y0), (x1 - 96, y0), (x0, y1 - 96), (x1 - 96, y1 - 96)]
    for cx, cy in corners:
        draw.rectangle([cx, cy, cx + 96, cy + 96], fill=(44, 50, 60, 255), outline=(18, 20, 25, 255), width=4)
        draw.line([(cx + 4, cy + 4), (cx + 92, cy + 4)], fill=(100, 115, 135, 255), width=2)
        # Twistlock oval pocket aperture
        draw.rounded_rectangle([cx + 24, cy + 24, cx + 72, cy + 72], radius=16, fill=(12, 14, 18, 255), outline=(65, 75, 88, 255), width=3)
        height[cy:cy+96, cx:cx+96] = 0.95
        height[cy+24:cy+72, cx+24:cx+72] = 0.30

    # Forklift Pockets at bottom rail
    fork_pockets = [x0 + 440, x0 + 740]
    for fx in fork_pockets:
        draw.rectangle([fx, y1 - 28, fx + 140, y1 - 4], fill=(16, 18, 22, 255), outline=(75, 85, 100, 255), width=2)
        height[y1-28:y1-4, fx:fx+140] = 0.25

    # Decals, Stencils & Caution Badges
    # 1. Yellow/Black Hazard Warning Stripe Badge (Left corner)
    hx0, hy0, hx1, hy1 = x0 + 80, y0 + 64, x0 + 200, y0 + 130
    draw.rectangle([hx0, hy0, hx1, hy1], fill=(235, 180, 20, 255), outline=(20, 22, 26, 255), width=3)
    # Diagonal stripes
    stripe_w = 20
    for sx in range(hx0 - 60, hx1 + 60, stripe_w * 2):
        pts = [(sx, hy1), (sx + stripe_w, hy1), (sx + stripe_w + 35, hy0), (sx + 35, hy0)]
        clipped = []
        draw.polygon(pts, fill=(24, 26, 30, 255))
    # Redraw frame border over stripes
    draw.rectangle([hx0, hy0, hx1, hy1], outline=(20, 22, 26, 255), width=3)
    height[hy0:hy1, hx0:hx1] = 0.82

    # 2. Military Stencil Lettering (High-res simulated bold font)
    # "SBX-1" stencil block
    st_x, st_y = x0 + 230, y0 + 70
    draw.rectangle([st_x, st_y, st_x + 320, st_y + 44], fill=(36, 42, 50, 180)) # background placard
    # Draw stenciled text simulated blocks
    # "S B X - 1"
    draw.text((st_x + 16, st_y + 4), "SBX-1 / RADOME 01", fill=(225, 232, 242, 240))
    draw.text((st_x + 16, st_y + 24), "US MDA - BALLISTIC RADAR", fill=(175, 190, 205, 220))

    # Weight / Spec Stencil Placard
    sp_x, sp_y = x0 + 80, y0 + 160
    draw.text((sp_x, sp_y), "MAX GROSS:  30,480 KG", fill=(190, 200, 212, 210))
    draw.text((sp_x, sp_y + 18), "TARE WT:     3,750 KG", fill=(190, 200, 212, 210))
    draw.text((sp_x, sp_y + 36), "PAYLOAD:    26,730 KG", fill=(190, 200, 212, 210))

    # Clean crisp ink-line black outline around entire container
    draw.rectangle([x0, y0, x1, y1], outline=(15, 17, 21, 255), width=4)

    # Rivets along top and bottom structural beams
    for rx in range(x0 + 60, x1 - 60, 32):
        draw.ellipse([rx - 3, y0 + 14, rx + 3, y0 + 20], fill=(120, 132, 148, 255), outline=(20, 24, 30, 255))
        draw.ellipse([rx - 3, y1 - 20, rx + 3, y1 - 14], fill=(90, 100, 115, 255), outline=(18, 22, 28, 255))
        height[y0+14:y0+20, rx-3:rx+3] = 0.96
        height[y1-20:y1-14, rx-3:rx+3] = 0.96

    alpha_mask = np.array(img)[:, :, 3]
    normal_img = generate_normal_from_height(height, alpha_mask, strength=3.8)

    img.save(os.path.join(out_dir, "Container_Military_NavyGrey.png"))
    normal_img.save(os.path.join(normal_dir, "Container_Military_NavyGrey_Normal.png"))
    print("Saved High-Res Container_Military_NavyGrey (1536x768).")

# ==============================================================================
# 2. High-Res Power Generator & HVAC Container (1536 x 938, PPU=256 -> 6.0 x 3.66)
# ==============================================================================
def make_highres_power_hvac_container():
    w, h = 1536, 938
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    margin_x = 16
    body_y0 = 170 # lower container body starts at y=170, top 170px is HVAC cowls/chimneys
    body_y1 = h - 24
    x0, x1 = margin_x, w - margin_x

    # --- TOP ROOF APPARATUS (y: 10 to 170) ---
    # Dual Heavy Industrial Fan Turbines
    fans = [(x0 + 280, 110), (x0 + 720, 110)]
    fan_radius_x, fan_radius_y = 150, 60
    for fcx, fcy in fans:
        # Fan Cowl Base (Octagonal / Cylindrical housing)
        draw.rectangle([fcx - fan_radius_x, fcy - 20, fcx + fan_radius_x, body_y0], fill=(48, 54, 64, 255), outline=(20, 24, 30, 255), width=3)
        draw.line([(fcx - fan_radius_x + 4, fcy - 16), (fcx + fan_radius_x - 4, fcy - 16)], fill=(110, 122, 138, 255), width=2)
        height[fcy-20:body_y0, fcx-fan_radius_x:fcx+fan_radius_x] = 0.85

        # Curved Aerodynamic Fan Shroud
        draw.ellipse([fcx - fan_radius_x, fcy - fan_radius_y, fcx + fan_radius_x, fcy + fan_radius_y], fill=(32, 36, 44, 255), outline=(18, 20, 26, 255), width=4)
        # Interior Fan Recess (Dark tunnel)
        draw.ellipse([fcx - (fan_radius_x - 16), fcy - (fan_radius_y - 12), fcx + (fan_radius_x - 16), fcy + (fan_radius_y - 12)], fill=(14, 16, 20, 255))
        height[fcy-fan_radius_y:fcy+fan_radius_y, fcx-fan_radius_x:fcx+fan_radius_x] = 0.92

        # 6 Turbine Blades inside
        for angle in range(0, 180, 30):
            rad = math.radians(angle)
            dx = math.cos(rad) * (fan_radius_x - 28)
            dy = math.sin(rad) * (fan_radius_y - 20)
            draw.line([(fcx - dx, fcy - dy), (fcx + dx, fcy + dy)], fill=(85, 95, 110, 255), width=6)
            draw.line([(fcx - dx, fcy - dy), (fcx + dx, fcy + dy)], fill=(150, 165, 185, 255), width=2)

        # Center Spinner Cone
        draw.ellipse([fcx - 30, fcy - 18, fcx + 30, fcy + 18], fill=(58, 66, 78, 255), outline=(22, 26, 32, 255), width=2)
        draw.ellipse([fcx - 12, fcy - 8, fcx + 12, fcy + 8], fill=(130, 145, 165, 255))
        height[fcy-18:fcy+18, fcx-30:fcx+30] = 0.98

        # Protective Mesh Screen (Steel Wire Crossbars)
        for mx in range(fcx - fan_radius_x + 20, fcx + fan_radius_x - 10, 28):
            draw.line([(mx, fcy - fan_radius_y + 12), (mx, fcy + fan_radius_y - 12)], fill=(120, 130, 145, 160), width=2)
        for my in range(fcy - fan_radius_y + 16, fcy + fan_radius_y - 10, 18):
            draw.line([(fcx - fan_radius_x + 18, my), (fcx + fan_radius_x - 18, my)], fill=(120, 130, 145, 160), width=2)

    # Exhaust Chimney Stacks (Right side of roof)
    ex_x0, ex_w = x0 + 1040, 110
    draw.rectangle([ex_x0, 30, ex_x0 + ex_w, body_y0], fill=(62, 70, 80, 255), outline=(22, 26, 32, 255), width=3)
    draw.line([(ex_x0 + 4, 34), (ex_x0 + 4, body_y0 - 4)], fill=(110, 125, 145, 255), width=3) # highlight
    # Rain Cap on Chimney
    draw.polygon([(ex_x0 - 24, 30), (ex_x0 + ex_w // 2, 8), (ex_x0 + ex_w + 24, 30)], fill=(75, 85, 98, 255), outline=(18, 20, 26, 255))
    height[8:body_y0, ex_x0-24:ex_x0+ex_w+24] = 0.88

    # --- MAIN CONTAINER BODY (y: body_y0 to body_y1) ---
    for y in range(body_y0, body_y1):
        ratio = (y - body_y0) / (body_y1 - body_y0)
        r = int(50 * (1.0 - ratio * 0.22))
        g = int(56 * (1.0 - ratio * 0.22))
        b = int(66 * (1.0 - ratio * 0.22))
        draw.line([(x0, y), (x1, y)], fill=(r, g, b, 255))
    height[body_y0:body_y1, x0:x1] = 0.65

    # Heavy Outer Frame & Posts
    draw.rectangle([x0, body_y0, x1, body_y0 + 34], fill=(65, 72, 84, 255), outline=(20, 24, 30, 255), width=3)
    draw.rectangle([x0, body_y1 - 34, x1, body_y1], fill=(42, 48, 58, 255), outline=(18, 22, 28, 255), width=3)
    draw.rectangle([x0, body_y0, x0 + 48, body_y1], fill=(58, 66, 78, 255), outline=(20, 24, 30, 255), width=3)
    draw.rectangle([x1 - 48, body_y0, x1, body_y1], fill=(52, 60, 72, 255), outline=(20, 24, 30, 255), width=3)
    height[body_y0:body_y0+34, x0:x1] = 0.88
    height[body_y1-34:body_y1, x0:x1] = 0.88

    # Large Heat Exchanger / Louver Grill Banks (Left & Center)
    # Bank 1: x0 + 80 to x0 + 520
    # Bank 2: x0 + 560 to x0 + 1000
    banks = [(x0 + 80, x0 + 520), (x0 + 560, x0 + 1000)]
    for bx0, bx1 in banks:
        by0 = body_y0 + 60
        by1 = body_y1 - 60
        # Deep recess frame
        draw.rectangle([bx0, by0, bx1, by1], fill=(22, 26, 32, 255), outline=(16, 18, 22, 255), width=4)
        draw.rectangle([bx0 + 6, by0 + 6, bx1 - 6, by1 - 6], fill=(12, 14, 18, 255))
        height[by0:by1, bx0:bx1] = 0.40

        # Fine Angled Louver Slats (32 horizontal slats)
        slat_step = 18
        for sy in range(by0 + 14, by1 - 14, slat_step):
            # Slat Face
            draw.rectangle([bx0 + 10, sy, bx1 - 10, sy + 11], fill=(62, 70, 82, 255))
            # Slat Top Highlight edge
            draw.line([(bx0 + 10, sy), (bx1 - 10, sy)], fill=(115, 128, 145, 255), width=2)
            # Slat Bottom Shadow
            draw.line([(bx0 + 10, sy + 11), (bx1 - 10, sy + 11)], fill=(18, 20, 25, 255), width=2)
            height[sy:sy+11, bx0+10:bx1-10] = 0.72

    # Operator Control & Diagnostic Panel (Right side: x0 + 1040 to x1 - 70)
    pan_x0 = x0 + 1040
    pan_x1 = x1 - 80
    pan_y0 = body_y0 + 60
    pan_y1 = body_y1 - 60
    draw.rectangle([pan_x0, pan_y0, pan_x1, pan_y1], fill=(36, 42, 50, 255), outline=(20, 24, 30, 255), width=3)
    height[pan_y0:pan_y1, pan_x0:pan_x1] = 0.78

    # 1. Digital Status Screen (Cyan backlit LCD)
    lcd_x0, lcd_y0 = pan_x0 + 30, pan_y0 + 30
    lcd_w, lcd_h = pan_x1 - pan_x0 - 60, 110
    draw.rectangle([lcd_x0, lcd_y0, lcd_x0 + lcd_w, lcd_y0 + lcd_h], fill=(12, 38, 48, 255), outline=(8, 18, 24, 255), width=3)
    draw.text((lcd_x0 + 16, lcd_y0 + 12), "DIESEL GEN #3: RUNNING", fill=(80, 240, 255, 255))
    draw.text((lcd_x0 + 16, lcd_y0 + 38), "OUTPUT: 4160V / 60Hz 2.4MW", fill=(60, 210, 230, 230))
    draw.text((lcd_x0 + 16, lcd_y0 + 64), "TURBINE RPM: 1800  TEMP: 74C", fill=(60, 210, 230, 230))
    draw.text((lcd_x0 + 16, lcd_y0 + 86), "COOLING PRESSURE: NORMAL", fill=(70, 255, 160, 240))
    height[lcd_y0:lcd_y0+lcd_h, lcd_x0:lcd_x0+lcd_w] = 0.70

    # 2. Dual Analogue Dial Pressure & Temp Gauges
    gauges = [(pan_x0 + 90, pan_y0 + 220), (pan_x0 + 230, pan_y0 + 220)]
    for gx, gy in gauges:
        gr = 48
        draw.ellipse([gx - gr, gy - gr, gx + gr, gy + gr], fill=(225, 230, 238, 255), outline=(28, 32, 40, 255), width=4)
        draw.ellipse([gx - gr + 6, gy - gr + 6, gx + gr - 6, gy + gr - 6], fill=(240, 244, 250, 255), outline=(160, 170, 185, 255), width=1)
        # Red warning arc
        draw.arc([gx - gr + 10, gy - gr + 10, gx + gr - 10, gy + gr - 10], start=-30, end=45, fill=(220, 40, 30, 255), width=4)
        # Needle pointer
        draw.line([(gx, gy), (gx + 22, gy - 26)], fill=(200, 20, 20, 255), width=3)
        draw.ellipse([gx - 6, gy - 6, gx + 6, gy + 6], fill=(30, 35, 45, 255))
        height[gy-gr:gy+gr, gx-gr:gx+gr] = 0.90

    # 3. Status LEDs & Emergency Stop
    led_y = pan_y0 + 330
    leds = [(pan_x0 + 70, (40, 240, 60, 255)), (pan_x0 + 130, (255, 200, 30, 255)), (pan_x0 + 190, (255, 40, 30, 255))]
    for lx, col in leds:
        draw.ellipse([lx - 14, led_y - 14, lx + 14, led_y + 14], fill=(28, 32, 38, 255), outline=(60, 68, 78, 255), width=2)
        draw.ellipse([lx - 9, led_y - 9, lx + 9, led_y + 9], fill=col)
        # Specular glint
        draw.ellipse([lx - 4, led_y - 6, lx, led_y - 2], fill=(255, 255, 255, 230))
        height[led_y-14:led_y+14, lx-14:lx+14] = 0.92

    # Big Red Mushroom Emergency Stop
    es_x, es_y = pan_x0 + 260, led_y
    draw.rectangle([es_x - 22, es_y - 22, es_x + 22, es_y + 22], fill=(240, 210, 20, 255), outline=(20, 22, 28, 255), width=3) # yellow bezel
    draw.ellipse([es_x - 16, es_y - 16, es_x + 16, es_y + 16], fill=(220, 25, 20, 255), outline=(100, 15, 15, 255), width=2)
    height[es_y-22:es_y+22, es_x-22:es_x+22] = 0.96

    # Clean border lines and rivets
    draw.rectangle([x0, body_y0, x1, body_y1], outline=(16, 18, 22, 255), width=4)

    alpha_mask = np.array(img)[:, :, 3]
    normal_img = generate_normal_from_height(height, alpha_mask, strength=3.8)

    img.save(os.path.join(out_dir, "Container_PowerGenerator_HVAC.png"))
    normal_img.save(os.path.join(normal_dir, "Container_PowerGenerator_HVAC_Normal.png"))
    print("Saved High-Res Container_PowerGenerator_HVAC (1536x938).")

# ==============================================================================
# 3. High-Res Auxiliary Radar Dome (854 x 1024, PPU=256 -> 3.33 x 4.0 units)
# ==============================================================================
def make_highres_auxiliary_radome():
    w, h = 854, 1024
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    cx, cy = w // 2, 420
    radius = 360

    # Spherical Radome with 3D Directional Lighting & Geodesic Seams
    for y in range(cy - radius, cy + radius + 1):
        for x in range(cx - radius, cx + radius + 1):
            dx = (x - cx) / radius
            dy = (y - cy) / radius
            d2 = dx*dx + dy*dy
            if d2 <= 1.0:
                dz = math.sqrt(max(0.0, 1.0 - d2))
                height[y, x] = 0.40 + 0.55 * dz

                # Directional lighting vector (from top-left forward: -0.4, -0.6, 0.7)
                light_dot = -0.38 * dx - 0.58 * dy + 0.72 * dz
                intensity = np.clip(light_dot * 0.45 + 0.65, 0.25, 1.0)

                # Crisp off-white / light naval grey fabric tone
                r = int(np.clip(220 * intensity, 80, 250))
                g = int(np.clip(226 * intensity, 85, 252))
                b = int(np.clip(235 * intensity, 95, 255))
                img.putpixel((x, y), (r, g, b, 255))

    # Geodesic Seam Lines (Overlay subtle panel lines within sphere mask)
    seam_layer = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    seam_draw = ImageDraw.Draw(seam_layer)

    # Latitude arcs
    for angle in range(-60, 75, 25):
        lat_y = cy + int(radius * math.sin(math.radians(angle)) * 0.75)
        arc_w = int(radius * math.cos(math.radians(angle)))
        if arc_w > 20:
            seam_draw.arc([cx - arc_w, lat_y - 60, cx + arc_w, lat_y + 60], start=10, end=170, fill=(140, 150, 168, 200), width=3)

    # Vertical Longitude Arcs
    for lon in [-0.65, -0.32, 0.0, 0.32, 0.65]:
        lx = cx + int(radius * lon)
        seam_draw.arc([lx - 100, cy - radius, lx + 100, cy + radius], start=270, end=90, fill=(140, 150, 168, 180), width=3)

    # Mask seam layer to sphere interior only
    circle_mask = Image.new('L', (w, h), 0)
    ImageDraw.Draw(circle_mask).ellipse([cx - radius + 2, cy - radius + 2, cx + radius - 2, cy + radius - 2], fill=255)
    seam_layer.putalpha(Image.composite(seam_layer.split()[3], Image.new('L', (w, h), 0), circle_mask))
    img.alpha_composite(seam_layer)

    # Outer perimeter outline of dome
    draw.ellipse([cx - radius, cy - radius, cx + radius, cy + radius], outline=(30, 35, 45, 255), width=5)

    # --- MOUNTING COLLAR & SUPPORT PEDESTAL (y: cy + radius - 40 to h - 20) ---
    collar_y0 = cy + radius - 40
    collar_y1 = collar_y0 + 70
    collar_w = int(radius * 1.5)
    # Steel Flange Collar
    draw.rectangle([cx - collar_w // 2, collar_y0, cx + collar_w // 2, collar_y1], fill=(52, 60, 72, 255), outline=(22, 26, 32, 255), width=4)
    draw.line([(cx - collar_w // 2 + 4, collar_y0 + 4), (cx + collar_w // 2 - 4, collar_y0 + 4)], fill=(120, 135, 155, 255), width=3)
    height[collar_y0:collar_y1, cx-collar_w//2:cx+collar_w//2] = 0.82

    # Bolt Row along Collar
    for bx in range(cx - collar_w // 2 + 30, cx + collar_w // 2 - 20, 36):
        draw.ellipse([bx - 6, collar_y0 + 26, bx + 6, collar_y0 + 42], fill=(130, 142, 160, 255), outline=(20, 24, 30, 255), width=2)
        height[collar_y0+26:collar_y0+42, bx-6:bx+6] = 0.95

    # Octagonal Pedestal Trunk (collar_y1 to h - 40)
    ped_y0 = collar_y1
    ped_y1 = h - 60
    ped_w = int(radius * 1.25)
    draw.rectangle([cx - ped_w // 2, ped_y0, cx + ped_w // 2, ped_y1], fill=(42, 48, 58, 255), outline=(20, 24, 30, 255), width=4)
    height[ped_y0:ped_y1, cx-ped_w//2:cx+ped_w//2] = 0.75

    # Access Maintenance Hatch
    hx0, hy0, hx1, hy1 = cx - 70, ped_y0 + 30, cx + 70, ped_y1 - 30
    draw.rectangle([hx0, hy0, hx1, hy1], fill=(30, 34, 42, 255), outline=(15, 18, 22, 255), width=3)
    draw.rectangle([hx0 + 10, hy0 + 10, hx1 - 10, hy1 - 10], fill=(46, 52, 62, 255), outline=(18, 22, 28, 255), width=2)
    # Wheel latch
    draw.ellipse([cx - 16, hy0 + 55, cx + 16, hy0 + 87], fill=(160, 172, 190, 255), outline=(20, 24, 30, 255), width=3)
    height[hy0:hy1, hx0:hx1] = 0.70

    # Deck Floor Flange Base (h - 60 to h - 20)
    base_w = int(radius * 1.7)
    draw.rectangle([cx - base_w // 2, ped_y1, cx + base_w // 2, h - 20], fill=(34, 40, 48, 255), outline=(16, 18, 24, 255), width=4)
    draw.line([(cx - base_w // 2 + 4, ped_y1 + 4), (cx + base_w // 2 - 4, ped_y1 + 4)], fill=(100, 115, 135, 255), width=2)
    height[ped_y1:h-20, cx-base_w//2:cx+base_w//2] = 0.85

    alpha_mask = np.array(img)[:, :, 3]
    normal_img = generate_normal_from_height(height, alpha_mask, strength=3.5)

    img.save(os.path.join(out_dir, "Deck_Auxiliary_Radome.png"))
    normal_img.save(os.path.join(normal_dir, "Deck_Auxiliary_Radome_Normal.png"))
    print("Saved High-Res Deck_Auxiliary_Radome (854x1024).")

# ==============================================================================
# 4. High-Res Articulated Deck Crane (1366 x 1366, PPU=256 -> 5.33 x 5.33 units)
# ==============================================================================
def make_highres_deck_crane():
    w, h = 1366, 1366
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    # Palette
    YELLOW_MAIN = (225, 170, 18, 255)
    YELLOW_LIGHT = (255, 205, 50, 255)
    YELLOW_DARK = (165, 120, 12, 255)
    STEEL_DARK = (40, 45, 54, 255)
    STEEL_MID = (70, 78, 90, 255)
    STEEL_LIGHT = (160, 175, 195, 255)
    OUTLINE = (18, 20, 25, 255)

    # 1. Base Pedestal & Slewing Ring (Bottom center-left: cx=450, y: 1100 to 1340)
    bcx = 420
    # Turntable Foundation Column
    draw.rectangle([bcx - 140, 1180, bcx + 140, 1340], fill=STEEL_DARK, outline=OUTLINE, width=4)
    draw.line([(bcx - 134, 1184), (bcx + 134, 1184)], fill=STEEL_MID, width=3)
    height[1180:1340, bcx-140:bcx+140] = 0.70

    # Slewing Ring Gear & Roller Bearing Assembly
    draw.rectangle([bcx - 170, 1120, bcx + 170, 1180], fill=(58, 64, 76, 255), outline=OUTLINE, width=4)
    # Gear teeth pattern
    for gx in range(bcx - 160, bcx + 160, 20):
        draw.line([(gx, 1125), (gx, 1175)], fill=(28, 32, 40, 255), width=6)
        draw.line([(gx + 3, 1125), (gx + 3, 1175)], fill=(130, 140, 155, 255), width=2)
    height[1120:1180, bcx-170:bcx+170] = 0.82

    # 2. Slewing Turret Base Platform (y: 960 to 1120)
    turret_pts = [(bcx - 220, 1120), (bcx + 190, 1120), (bcx + 160, 960), (bcx - 180, 960)]
    draw.polygon(turret_pts, fill=YELLOW_MAIN, outline=OUTLINE)
    draw.line([(bcx - 176, 964), (bcx + 156, 964)], fill=YELLOW_LIGHT, width=4)
    height[960:1120, bcx-220:bcx+190] = 0.80

    # Rear Counterweight (Left side: bcx - 300 to bcx - 140)
    cw_pts = [(bcx - 340, 1080), (bcx - 170, 1080), (bcx - 170, 900), (bcx - 310, 900)]
    draw.polygon(cw_pts, fill=STEEL_DARK, outline=OUTLINE)
    # Hazard Stripes on Counterweight
    for sx in range(bcx - 380, bcx - 120, 40):
        s_pts = [(sx, 1080), (sx + 20, 1080), (sx + 45, 900), (sx + 25, 900)]
        draw.polygon(s_pts, fill=(230, 180, 20, 255))
    draw.polygon(cw_pts, outline=OUTLINE, width=4)
    height[900:1080, bcx-340:bcx-170] = 0.88

    # 3. Operator Cabin (Sitting on turret front: bcx - 60 to bcx + 140, y: 760 to 960)
    cab_x0, cab_y0, cab_x1, cab_y1 = bcx - 40, 760, bcx + 150, 960
    draw.rectangle([cab_x0, cab_y0, cab_x1, cab_y1], fill=YELLOW_MAIN, outline=OUTLINE, width=4)
    draw.line([(cab_x0 + 4, cab_y0 + 4), (cab_x1 - 4, cab_y0 + 4)], fill=YELLOW_LIGHT, width=3)

    # Tinted Panoramic Windows
    win_x0, win_y0, win_x1, win_y1 = cab_x0 + 16, cab_y0 + 20, cab_x1 - 16, cab_y0 + 110
    draw.rectangle([win_x0, win_y0, win_x1, win_y1], fill=(24, 48, 64, 255), outline=OUTLINE, width=3)
    # Glass reflection slash
    draw.polygon([(win_x0 + 10, win_y1 - 6), (win_x0 + 35, win_y1 - 6), (win_x0 + 65, win_y0 + 6), (win_x0 + 40, win_y0 + 6)], fill=(120, 210, 255, 140))
    # Operator Silhouette inside
    draw.ellipse([win_x0 + 30, win_y0 + 35, win_x0 + 55, win_y0 + 65], fill=(16, 22, 30, 255)) # Head
    draw.rectangle([win_x0 + 24, win_y0 + 65, win_x0 + 64, win_y1 - 4], fill=(22, 28, 38, 255)) # Body
    height[cab_y0:cab_y1, cab_x0:cab_x1] = 0.85

    # 4. Heavy Main Boom Arm (Reaching from turret pivot (bcx + 40, 880) up-right to (1150, 480))
    p0 = (bcx + 30, 860)
    p1 = (1180, 440)
    # Boom Pivot Pin Assembly
    draw.ellipse([p0[0] - 36, p0[1] - 36, p0[0] + 36, p0[1] + 36], fill=STEEL_DARK, outline=OUTLINE, width=4)
    draw.ellipse([p0[0] - 18, p0[1] - 18, p0[0] + 18, p0[1] + 18], fill=STEEL_LIGHT)

    # Telescopic Boom Box Girder
    boom_poly = [
        (p0[0] - 25, p0[1] - 45),
        (p1[0] - 15, p1[1] - 25),
        (p1[0] + 15, p1[1] + 25),
        (p0[0] + 25, p0[1] + 45)
    ]
    draw.polygon(boom_poly, fill=YELLOW_MAIN, outline=OUTLINE)
    # Boom Highlight edge
    draw.line([(boom_poly[0][0] + 3, boom_poly[0][1] + 3), (boom_poly[1][0] + 3, boom_poly[1][1] + 3)], fill=YELLOW_LIGHT, width=5)
    # Boom Bottom Shadow
    draw.line([(boom_poly[3][0] - 3, boom_poly[3][1] - 3), (boom_poly[2][0] - 3, boom_poly[2][1] - 3)], fill=YELLOW_DARK, width=5)

    # Hydraulic Elevation Cylinder (Runs from bcx + 110, 960 up to boom midpoint)
    cyl_base = (bcx + 110, 970)
    cyl_target = (p0[0] + (p1[0] - p0[0]) * 0.45, p0[1] + (p1[1] - p0[1]) * 0.45 + 20)
    # Barrel (Black/Dark steel)
    draw.line([cyl_base, (cyl_base[0] + (cyl_target[0] - cyl_base[0]) * 0.55, cyl_base[1] + (cyl_target[1] - cyl_base[1]) * 0.55)], fill=STEEL_DARK, width=28)
    draw.line([cyl_base, (cyl_base[0] + (cyl_target[0] - cyl_base[0]) * 0.55, cyl_base[1] + (cyl_target[1] - cyl_base[1]) * 0.55)], fill=OUTLINE, width=32)
    # Chrome Rod (Specular silver)
    draw.line([(cyl_base[0] + (cyl_target[0] - cyl_base[0]) * 0.45, cyl_base[1] + (cyl_target[1] - cyl_base[1]) * 0.45), cyl_target], fill=STEEL_LIGHT, width=18)
    draw.line([(cyl_base[0] + (cyl_target[0] - cyl_base[0]) * 0.45, cyl_base[1] + (cyl_target[1] - cyl_base[1]) * 0.45), cyl_target], fill=(245, 250, 255, 255), width=4)

    # 5. Boom Tip Sheave (Pulley Wheel) & Hoist Cable & Hook Block
    tip_x, tip_y = p1[0], p1[1]
    draw.ellipse([tip_x - 28, tip_y - 28, tip_x + 28, tip_y + 28], fill=STEEL_DARK, outline=OUTLINE, width=3)
    draw.ellipse([tip_x - 14, tip_y - 14, tip_x + 14, tip_y + 14], fill=STEEL_MID)

    # Steel Cable running down to Hook Block
    hook_y = tip_y + 360
    draw.line([(tip_x, tip_y), (tip_x, hook_y)], fill=(180, 190, 205, 255), width=5)
    draw.line([(tip_x + 8, tip_y), (tip_x + 8, hook_y)], fill=(120, 130, 145, 255), width=4)

    # Heavy Crane Hook Block (tip_x - 45 to tip_x + 45, y: hook_y to hook_y + 120)
    hk_x0, hk_y0 = tip_x - 45, hook_y
    draw.rectangle([hk_x0, hk_y0, hk_x0 + 90, hk_y0 + 100], fill=YELLOW_MAIN, outline=OUTLINE, width=4)
    # Hazard stripes on hook block
    draw.polygon([(hk_x0 + 5, hk_y0 + 35), (hk_x0 + 35, hk_y0 + 5), (hk_x0 + 55, hk_y0 + 5), (hk_x0 + 5, hk_y0 + 55)], fill=(20, 22, 26, 255))
    draw.polygon([(hk_x0 + 35, hk_y0 + 95), (hk_x0 + 85, hk_y0 + 45), (hk_x0 + 85, hk_y0 + 65), (hk_x0 + 55, hk_y0 + 95)], fill=(20, 22, 26, 255))

    # Forged Steel Crane Hook (Curved claw)
    hook_stem_y = hk_y0 + 100
    draw.rectangle([tip_x - 10, hook_stem_y, tip_x + 10, hook_stem_y + 30], fill=STEEL_DARK, outline=OUTLINE, width=2)
    # Hook curve
    draw.arc([tip_x - 45, hook_stem_y + 15, tip_x + 25, hook_stem_y + 95], start=0, end=200, fill=STEEL_DARK, width=18)
    draw.arc([tip_x - 45, hook_stem_y + 15, tip_x + 25, hook_stem_y + 95], start=0, end=200, fill=STEEL_MID, width=8)

    # Generate Heightmap
    height[1120:1340, bcx-170:bcx+170] = 0.75
    height[960:1120, bcx-220:bcx+190] = 0.82
    height[760:960, cab_x0:cab_x1] = 0.88
    # Boom line in heightmap
    cv2.line(height, p0, p1, 0.85, 45)
    cv2.circle(height, (tip_x, hook_y + 50), 40, 0.90, -1)

    alpha_mask = np.array(img)[:, :, 3]
    normal_img = generate_normal_from_height(height, alpha_mask, strength=3.5)

    img.save(os.path.join(out_dir, "Deck_Crane_Articulated.png"))
    normal_img.save(os.path.join(normal_dir, "Deck_Crane_Articulated_Normal.png"))
    print("Saved High-Res Deck_Crane_Articulated (1366x1366).")

# ==============================================================================
# 5. High-Res Deck Weather Plating (1366 x 170, PPU=256 -> 5.33 x 0.66 units)
# ==============================================================================
def make_highres_deck_plating():
    w, h = 1366, 170
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    # Steel Plating Base (Dark non-skid naval grey)
    draw.rectangle([0, 0, w, h], fill=(44, 48, 56, 255))
    height[:, :] = 0.60

    # Top Edge Highlight (where sunlight/ambient light strikes the deck edge)
    draw.line([(0, 4), (w, 4)], fill=(115, 126, 142, 255), width=4)
    draw.line([(0, 8), (w, 8)], fill=(75, 84, 98, 255), width=2)
    height[0:12, :] = 0.85

    # Raised Diamond Tread / Anti-Skid Crosshatch Texture
    step = 28
    for x in range(0, w + h, step):
        draw.line([(x, 14), (x - h, h - 8)], fill=(58, 64, 76, 180), width=3)
        draw.line([(x - h, 14), (x, h - 8)], fill=(58, 64, 76, 180), width=3)
        # Height highlights
        cv2.line(height, (x, 14), (max(0, x - h), h - 8), 0.72, 2)
        cv2.line(height, (max(0, x - h), 14), (min(w - 1, x), h - 8), 0.72, 2)

    # Structural I-Beam Stiffeners & Expansion Seams (Vertical seam every 340px)
    for sx in range(0, w, 341):
        draw.rectangle([sx - 8, 0, sx + 8, h], fill=(28, 32, 38, 255), outline=(16, 18, 22, 255), width=2)
        draw.line([(sx - 4, 2), (sx - 4, h - 2)], fill=(90, 100, 115, 255), width=2)
        height[:, max(0, sx-8):min(w, sx+8)] = 0.78

        # Flush Tie-Down Pad-Eyes (Recessed cloverleaf lashing rings)
        pex, pey = sx + 170, h // 2 + 10
        if pex + 25 < w:
            draw.ellipse([pex - 24, pey - 24, pex + 24, pey + 24], fill=(22, 26, 32, 255), outline=(70, 78, 92, 255), width=3)
            draw.ellipse([pex - 12, pey - 12, pex + 12, pey + 12], fill=(120, 132, 148, 255))
            height[pey-24:pey+24, pex-24:pex+24] = 0.50

    # Bottom Deck Lip & Shadow
    draw.rectangle([0, h - 14, w, h], fill=(20, 22, 26, 255))
    height[h-14:h, :] = 0.40

    alpha_mask = np.array(img)[:, :, 3]
    normal_img = generate_normal_from_height(height, alpha_mask, strength=3.5)

    img.save(os.path.join(out_dir, "Deck_Weather_Plating.png"))
    normal_img.save(os.path.join(normal_dir, "Deck_Weather_Plating_Normal.png"))
    print("Saved High-Res Deck_Weather_Plating (1366x170).")

# ==============================================================================
# 6. High-Res Deck Safety Railing (682 x 256, PPU=256 -> 2.66 x 1.0 unit)
# ==============================================================================
def make_highres_safety_railing():
    w, h = 682, 256
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    YELLOW = (235, 185, 22, 255)
    YELLOW_HIGHLIGHT = (255, 225, 80, 255)
    YELLOW_SHADOW = (170, 130, 12, 255)
    OUTLINE = (18, 20, 25, 255)

    # 1. Kickplate / Toe-Board along bottom (h - 40 to h - 10)
    draw.rectangle([0, h - 42, w, h - 10], fill=YELLOW, outline=OUTLINE, width=3)
    draw.line([(0, h - 39), (w, h - 39)], fill=YELLOW_HIGHLIGHT, width=2)
    draw.line([(0, h - 13), (w, h - 13)], fill=YELLOW_SHADOW, width=2)
    height[h-42:h-10, :] = 0.80

    # 2. Heavy Vertical Stanchions (Posts at x=20, x=240, x=460, x=660)
    stanchion_xs = [20, 230, 450, 660]
    for sx in stanchion_xs:
        # Base Gusset Flange
        draw.polygon([(sx - 26, h - 10), (sx + 26, h - 10), (sx + 10, h - 55), (sx - 10, h - 55)], fill=(65, 72, 85, 255), outline=OUTLINE)
        # Mounting hex bolts
        draw.ellipse([sx - 20, h - 18, sx - 12, h - 10], fill=(140, 150, 165, 255))
        draw.ellipse([sx + 12, h - 18, sx + 20, h - 10], fill=(140, 150, 165, 255))

        # Vertical Post Tube (diameter 16px)
        draw.rectangle([sx - 8, 24, sx + 8, h - 40], fill=YELLOW, outline=OUTLINE, width=3)
        draw.line([(sx - 4, 26), (sx - 4, h - 42)], fill=YELLOW_HIGHLIGHT, width=3) # specular ridge
        draw.line([(sx + 5, 26), (sx + 5, h - 42)], fill=YELLOW_SHADOW, width=3)
        height[24:h-10, sx-8:sx+8] = 0.88

    # 3. Horizontal Rails
    # Top Handrail (y: 20 to 42, diameter 22px)
    draw.rectangle([0, 20, w, 42], fill=YELLOW, outline=OUTLINE, width=3)
    draw.line([(0, 24), (w, 24)], fill=YELLOW_HIGHLIGHT, width=4) # top highlight
    draw.line([(0, 39), (w, 39)], fill=YELLOW_SHADOW, width=3)
    height[20:42, :] = 0.95

    # Intermediate Rail (y: 110 to 126, diameter 16px)
    draw.rectangle([0, 110, w, 126], fill=YELLOW, outline=OUTLINE, width=3)
    draw.line([(0, 113), (w, 113)], fill=YELLOW_HIGHLIGHT, width=3)
    draw.line([(0, 124), (w, 124)], fill=YELLOW_SHADOW, width=2)
    height[110:126, :] = 0.88

    alpha_mask = np.array(img)[:, :, 3]
    normal_img = generate_normal_from_height(height, alpha_mask, strength=3.5)

    img.save(os.path.join(out_dir, "Deck_Safety_Railing.png"))
    normal_img.save(os.path.join(normal_dir, "Deck_Safety_Railing_Normal.png"))
    print("Saved High-Res Deck_Safety_Railing (682x256).")

if __name__ == "__main__":
    print("Generating High-Resolution SBX-1 Container Deck Sprites...")
    make_highres_military_container()
    make_highres_power_hvac_container()
    make_highres_auxiliary_radome()
    make_highres_deck_crane()
    make_highres_deck_plating()
    make_highres_safety_railing()
    print("All High-Res Container Sprites & Normal Maps generated successfully!")
