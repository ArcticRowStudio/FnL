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

def make_radome_shutters():
    # Full dome is 4096 x 2048. Each shutter half is 2048 x 2048.
    w, h = 2048, 2048
    r_outer = 1920
    cy = h - 160  # Dome floor line at y = 1888

    # ==========================================
    # 1. Left Shutter (cx is at right edge: w = 2048)
    # ==========================================
    img_l = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height_l = np.zeros((h, w), dtype=np.float32)
    draw_l = ImageDraw.Draw(img_l)

    cx_l = w  # 2048

    for y in range(0, h, 2):
        for x in range(0, w, 2):
            dx = x - cx_l
            dy = y - cy
            dist = math.sqrt(dx*dx + dy*dy)
            if dist <= r_outer and y <= cy and x <= cx_l:
                norm_d = dist / r_outer
                angle = math.atan2(-dy, dx)
                
                # Exterior clean off-white / polar white radome fabric
                base = 230 - norm_d * 35 + math.cos(angle * 20) * 3
                
                # Geodesic ring seam grooves
                ring_mod = int(dist) % 180
                if ring_mod < 6:
                    base -= 20

                # Apex shading
                if y < 400:
                    base *= (0.88 + 0.12 * (y / 400.0))

                c_val = int(np.clip(base, 50, 250))
                for oy in range(2):
                    for ox in range(2):
                        if x + ox < w and y + oy < h:
                            img_l.putpixel((x + ox, y + oy), (c_val, c_val + 2, c_val + 5, 255))
                            height_l[y + oy, x + ox] = 0.60 + 0.3 * (1.0 - norm_d)

    # Arch Ribs on Left Shutter (angles 90 to 180 deg)
    for deg in range(96, 175, 12):
        rad = math.radians(deg)
        x_end = int(cx_l + r_outer * math.cos(rad))
        y_end = int(cy - r_outer * math.sin(rad))
        draw_l.line([(cx_l, cy - 140), (x_end, y_end)], fill=(120, 130, 145, 255), width=6)
        draw_l.line([(cx_l, cy - 140), (x_end, y_end)], fill=(200, 210, 225, 255), width=2)
        cv2.line(height_l, (cx_l, cy - 140), (x_end, y_end), 0.85, 6)

    # Base Guide Rail & Track Collar (cy - 20 to cy + 120)
    draw_l.rectangle([0, cy - 20, w, cy + 120], fill=(50, 56, 68, 255), outline=(18, 20, 26, 255), width=5)
    draw_l.line([(0, cy - 14), (w, cy - 14)], fill=(110, 125, 145, 255), width=4)
    # Heavy Roller Carriage Wheels at base
    for rx in range(120, w - 80, 240):
        draw_l.rectangle([rx - 45, cy + 20, rx + 45, cy + 100], fill=(36, 42, 52, 255), outline=(16, 18, 24, 255), width=3)
        draw_l.ellipse([rx - 30, cy + 30, rx + 30, cy + 90], fill=(140, 152, 170, 255), outline=(20, 24, 30, 255), width=3)
    height_l[cy-20:cy+120, :] = 0.90

    # Center Seam Interlocking Seal (Along right edge x = w - 40 to w)
    seam_x0 = w - 48
    draw_l.rectangle([seam_x0, 0, w, cy + 120], fill=(42, 48, 58, 255), outline=(18, 20, 25, 255), width=3)
    draw_l.line([(seam_x0 + 4, 0), (seam_x0 + 4, cy + 120)], fill=(120, 135, 155, 255), width=3)
    # Hazard yellow/black chevron stripe along the seam edge
    for sy in range(60, cy - 20, 60):
        draw_l.polygon([(seam_x0 + 6, sy), (w - 6, sy + 25), (w - 6, sy + 50), (seam_x0 + 6, sy + 25)], fill=(235, 185, 20, 255))
        draw_l.polygon([(seam_x0 + 6, sy + 25), (w - 6, sy + 50), (w - 6, sy + 75), (seam_x0 + 6, sy + 50)], fill=(24, 28, 34, 255))
    # Heavy Pneumatic Locking Latches
    for ly in range(180, cy - 60, 220):
        draw_l.rectangle([seam_x0 - 24, ly - 20, w, ly + 20], fill=(65, 75, 90, 255), outline=(18, 20, 26, 255), width=3)
        draw_l.ellipse([seam_x0 - 12, ly - 10, seam_x0 + 8, ly + 10], fill=(220, 40, 30, 255)) # red latch pin
        height_l[ly-20:ly+20, seam_x0-24:w] = 0.96

    # Mask Left Shutter to Dome Outer Silhouette
    mask_l = Image.new('L', (w, h), 0)
    ImageDraw.Draw(mask_l).ellipse([cx_l - r_outer, cy - r_outer, cx_l + r_outer, cy + r_outer], fill=255)
    ImageDraw.Draw(mask_l).rectangle([0, cy, w, cy + 120], fill=255)
    # Cut off strictly at seam x = w
    mask_l_arr = np.array(mask_l)
    alpha_l = np.array(img_l)[:, :, 3]
    alpha_l = np.minimum(alpha_l, mask_l_arr)
    img_l.putalpha(Image.fromarray(alpha_l))

    normal_l = generate_normal_from_height(height_l, alpha_l, strength=3.5)
    img_l.save(os.path.join(out_dir, "Radome_Shutter_Left.png"))
    normal_l.save(os.path.join(normal_dir, "Radome_Shutter_Left_Normal.png"))
    print("Saved Radome_Shutter_Left (2048x2048).")

    # ==========================================
    # 2. Right Shutter (Mirror of Left Shutter, cx is at left edge: x = 0)
    # ==========================================
    # We can mirror the Left Shutter horizontally for perfect mechanical alignment!
    img_r = img_l.transpose(Image.FLIP_LEFT_RIGHT)
    
    # Generate heightmap for Right Shutter by flipping height_l
    height_r = np.fliplr(height_l)
    alpha_r = np.array(img_r)[:, :, 3]
    normal_r = generate_normal_from_height(height_r, alpha_r, strength=3.5)

    img_r.save(os.path.join(out_dir, "Radome_Shutter_Right.png"))
    normal_r.save(os.path.join(normal_dir, "Radome_Shutter_Right_Normal.png"))
    print("Saved Radome_Shutter_Right (2048x2048).")

if __name__ == "__main__":
    make_radome_shutters()
    print("Radome Shutter Sprites & Normal Maps generated successfully!")
