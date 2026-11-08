import os
import math
import numpy as np
from PIL import Image, ImageDraw

out_dir = r"c:\Users\mokos\My project\Assets\Environment\Sprites\Furniture"
normal_dir = os.path.join(out_dir, "Normals")  # normal maps live in a Normals subfolder
os.makedirs(normal_dir, exist_ok=True)
os.makedirs(out_dir, exist_ok=True)

def generate_normal_from_height(height_map, alpha_mask, strength=3.5):
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

# -------------------------------------------------------------
# 1. Table_Tactical_Briefing_384x192 (Room B: Strategic Command)
# -------------------------------------------------------------
def make_briefing_table():
    w, h = 384, 192
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    # Heavy steel angled legs (y=80 to 186)
    # Left leg: x=40 to 70; Right leg: x=314 to 344; Center brace: y=130 to 145, x=65 to 319
    draw.polygon([(40, 186), (65, 80), (85, 80), (60, 186)], fill=(35, 40, 48, 255))
    draw.polygon([(344, 186), (319, 80), (299, 80), (324, 186)], fill=(35, 40, 48, 255))
    draw.rectangle([65, 130, 319, 142], fill=(45, 50, 60, 255), outline=(20, 25, 30, 255))
    height[80:187, 40:86] = 0.5
    height[80:187, 299:345] = 0.5
    height[130:143, 65:320] = 0.6

    # Table slab: y=55 to 80, x=30 to 354
    for y in range(55, 80):
        shade = int(50 + (y - 55) * 0.8)
        draw.line([(30, y), (354, y)], fill=(shade, shade + 5, shade + 10, 255))
        height[y, 30:355] = 0.85
    draw.rectangle([30, 55, 354, 80], outline=(20, 24, 30, 255), width=2)

    # Glowing tactical holographic table surface / backlit glass (y=56 to 68, x=60 to 324)
    draw.rectangle([60, 56, 324, 68], fill=(10, 35, 45, 255), outline=(40, 120, 150, 255))
    # Grid lines & holographic sector radar markings
    for gx in range(70, 320, 20):
        draw.line([(gx, 57), (gx, 67)], fill=(20, 160, 180, 180))
    draw.ellipse([180, 57, 204, 67], outline=(80, 240, 255, 255))
    draw.line([(192, 57), (192, 67)], fill=(255, 100, 80, 255))
    height[56:69, 60:325] = 0.9

    # Tabletop accessories:
    # 1. Commander's radio transceiver on left (x=38 to 68, y=35 to 55)
    draw.rectangle([38, 35, 68, 55], fill=(30, 35, 42, 255), outline=(15, 20, 25, 255))
    draw.line([(44, 20), (44, 35)], fill=(180, 190, 200, 255), width=2) # Radio antenna
    draw.rectangle([42, 40, 52, 46], fill=(20, 60, 40, 255)) # Green LCD
    height[35:56, 38:69] = 0.85

    # 2. Coffee mug & paper dossiers (x=325 to 345, y=42 to 55)
    draw.rectangle([325, 48, 345, 55], fill=(210, 205, 190, 255), outline=(140, 130, 110, 255)) # Folders
    draw.rectangle([335, 38, 345, 48], fill=(220, 80, 50, 255), outline=(100, 25, 15, 255)) # Red mug
    height[38:56, 325:346] = 0.8

    # 3. Hologram projector emitter puck in center (x=184 to 200, y=50 to 56)
    draw.ellipse([184, 50, 200, 56], fill=(50, 180, 220, 255))

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr)
    img.save(os.path.join(out_dir, "Table_Tactical_Briefing_384x192.png"))
    norm_img.save(os.path.join(normal_dir, "Table_Tactical_Briefing_384x192_NormalMap.png"))
    print("Saved Table_Tactical_Briefing_384x192.png & NormalMap")

# -------------------------------------------------------------
# 2. Bed_Medical_Infirmary_IV_320x256 (Room C: Field Infirmary)
# -------------------------------------------------------------
def make_infirmary_bed():
    w, h = 320, 256
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    # Bed Frame: Tubular hospital bed legs (y=160 to 248)
    # Left caster leg: x=50 to 60; Right caster leg: x=240 to 250
    draw.rectangle([50, 160, 60, 246], fill=(130, 138, 148, 255), outline=(40, 45, 52, 255))
    draw.rectangle([240, 160, 250, 246], fill=(130, 138, 148, 255), outline=(40, 45, 52, 255))
    # Wheels / Casters
    draw.ellipse([48, 240, 62, 252], fill=(30, 32, 38, 255))
    draw.ellipse([238, 240, 252, 252], fill=(30, 32, 38, 255))
    height[160:253, 48:63] = 0.6
    height[160:253, 238:253] = 0.6

    # Lower shelf / frame stretcher brace (y=210 to 220, x=55 to 245)
    draw.rectangle([55, 212, 245, 218], fill=(90, 98, 108, 255), outline=(30, 35, 42, 255))
    # Oxygen cylinder on lower shelf
    draw.rectangle([80, 204, 140, 214], fill=(40, 140, 160, 255), outline=(20, 60, 70, 255))
    draw.rectangle([76, 206, 80, 212], fill=(180, 190, 200, 255)) # Valve
    height[204:219, 76:141] = 0.7

    # Mattress & White/Grey medical bedding (y=140 to 165, x=45 to 255)
    # Tilted headrest on left (x=45 to 90, y=125 to 155)
    draw.polygon([(45, 135), (90, 148), (90, 162), (45, 150)], fill=(215, 222, 228, 255))
    draw.polygon([(45, 135), (75, 125), (90, 148), (45, 135)], fill=(235, 240, 245, 255)) # Pillow
    # Main flat mattress
    draw.rectangle([90, 148, 255, 165], fill=(200, 208, 215, 255), outline=(50, 55, 65, 255))
    # Wrinkled blanket
    draw.rectangle([110, 144, 253, 162], fill=(85, 120, 115, 255), outline=(40, 65, 60, 255)) # Military drab hospital blanket
    height[125:166, 45:256] = 0.75

    # Side guard rails
    draw.rectangle([100, 135, 190, 140], fill=(160, 170, 180, 255), outline=(40, 45, 52, 255))
    for rx in range(110, 185, 22):
        draw.line([(rx, 140), (rx, 155)], fill=(140, 150, 160, 255), width=2)

    # IV Drip Stand on left (x=25 to 40, y=40 to 248)
    draw.line([(32, 45), (32, 248)], fill=(190, 200, 210, 255), width=3)
    # IV curved hooks
    draw.line([(24, 45), (40, 45)], fill=(190, 200, 210, 255), width=2)
    draw.line([(24, 45), (24, 52)], fill=(190, 200, 210, 255), width=2)
    draw.line([(40, 45), (40, 52)], fill=(190, 200, 210, 255), width=2)
    # Hanging Saline/Blood IV bags
    draw.rectangle([21, 52, 27, 72], fill=(220, 240, 255, 220), outline=(120, 150, 180, 255)) # Clear saline bag
    draw.rectangle([37, 52, 43, 72], fill=(180, 30, 30, 220), outline=(100, 15, 15, 255)) # Blood transfusion bag
    # IV tube trailing down
    draw.line([(24, 72), (28, 120), (55, 140)], fill=(200, 230, 255, 180), width=1)
    height[45:249, 21:44] = 0.85

    # Vitals ECG Heart Monitor on right cart (x=262 to 308, y=70 to 248)
    # Cart pole & base
    draw.line([(285, 125), (285, 248)], fill=(100, 110, 120, 255), width=3)
    draw.ellipse([275, 242, 295, 252], fill=(30, 35, 42, 255)) # Cart base
    # Monitor screen box (x=265 to 305, y=70 to 125)
    draw.rectangle([265, 70, 305, 125], fill=(35, 40, 48, 255), outline=(15, 20, 25, 255), width=2)
    # CRT monitor screen
    draw.rectangle([270, 75, 300, 105], fill=(10, 28, 20, 255), outline=(25, 60, 45, 255))
    # Glowing ECG heartbeat pulse wave (Green line)
    draw.line([(272, 90), (278, 90), (281, 80), (284, 100), (287, 85), (290, 90), (298, 90)], fill=(50, 255, 130, 255), width=1)
    # Control knobs
    draw.ellipse([272, 112, 278, 118], fill=(220, 60, 50, 255))
    draw.ellipse([282, 112, 288, 118], fill=(60, 160, 220, 255))
    height[70:126, 265:306] = 0.9

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr)
    img.save(os.path.join(out_dir, "Bed_Medical_Infirmary_IV_320x256.png"))
    norm_img.save(os.path.join(normal_dir, "Bed_Medical_Infirmary_IV_320x256_NormalMap.png"))
    print("Saved Bed_Medical_Infirmary_IV_320x256.png & NormalMap")

# -------------------------------------------------------------
# 3. GunRack_Armoury_Wall_320x256 (Room A: Armoury Checkpoint)
# -------------------------------------------------------------
def make_armoury_rack():
    w, h = 320, 256
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    # Heavy steel frame bolted to wall (x=20 to 300, y=20 to 240)
    for y in range(20, 240):
        # Steel mesh grid texture
        mesh_shade = 38 if (y % 8 < 2) else 24
        draw.line([(20, y), (300, y)], fill=(mesh_shade, mesh_shade + 3, mesh_shade + 6, 255))
        height[y, 20:301] = 0.4
    # Vertical mesh lines
    for x in range(20, 301, 8):
        draw.line([(x, 20), (x, 240)], fill=(45, 50, 58, 255))

    # Heavy outer frame with yellow/black hazard diagonal stripes on header
    draw.rectangle([18, 18, 302, 242], outline=(15, 18, 22, 255), width=3)
    # Header hazard bar (y=20 to 42)
    for y in range(20, 42):
        for x in range(20, 300):
            c = (230, 180, 20, 255) if ((x + y) // 10 % 2 == 0) else (25, 28, 32, 255)
            draw.point((x, y), fill=c)
    draw.text((80, 25), "ARMOURY SEC-4", fill=(10, 15, 20, 255))
    height[20:43, 20:301] = 0.85

    # Weapon Racks (Horizontal locking bars at y=80, 130, 180)
    for y_bar in [80, 130, 180]:
        draw.rectangle([25, y_bar, 295, y_bar + 10], fill=(60, 68, 78, 255), outline=(20, 24, 30, 255))
        height[y_bar:y_bar+11, 25:296] = 0.7

    # Mounted Assault Rifles / Sniper Rifles on rack (3 rifles mounted vertically)
    for rx in [70, 130, 190, 250]:
        # Gun barrel & flash hider pointing up (y=48 to 180)
        draw.line([(rx, 48), (rx, 180)], fill=(75, 80, 88, 255), width=3)
        draw.line([(rx-1, 48), (rx-1, 60)], fill=(120, 130, 140, 255), width=1) # muzzle brake
        # Receiver & Magazine (y=110 to 145)
        draw.rectangle([rx-4, 110, rx+5, 145], fill=(35, 38, 44, 255), outline=(15, 18, 22, 255))
        # Curved 30-round magazine sticking out
        draw.polygon([(rx+4, 125), (rx+14, 145), (rx+10, 150), (rx+2, 132)], fill=(45, 50, 58, 255))
        # Scope / Optical sight
        draw.rectangle([rx-6, 90, rx+6, 102], fill=(25, 28, 34, 255), outline=(60, 70, 80, 255))
        draw.ellipse([rx+4, 92, rx+8, 100], fill=(40, 140, 180, 255)) # Glass lens glint
        # Buttstock (y=150 to 195)
        draw.polygon([(rx-5, 150), (rx+4, 150), (rx+6, 195), (rx-8, 195)], fill=(20, 22, 26, 255))
        height[48:196, rx-8:rx+15] = 0.85

    # Bottom ammo crate compartment (y=195 to 240, x=30 to 290)
    # Olive drab military ammo tin cans
    for ax in [40, 105, 170, 235]:
        draw.rectangle([ax, 202, ax+55, 238], fill=(65, 80, 50, 255), outline=(25, 32, 20, 255), width=2)
        # Yellow stencil text & latch
        draw.line([(ax+6, 218), (ax+48, 218)], fill=(210, 190, 40, 255), width=2)
        draw.rectangle([ax+24, 204, ax+32, 212], fill=(160, 170, 180, 255)) # Latch
        height[202:239, ax:ax+56] = 0.9

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr)
    img.save(os.path.join(out_dir, "GunRack_Armoury_Wall_320x256.png"))
    norm_img.save(os.path.join(normal_dir, "GunRack_Armoury_Wall_320x256_NormalMap.png"))
    print("Saved GunRack_Armoury_Wall_320x256.png & NormalMap")

# -------------------------------------------------------------
# 4. Station_Lab_Analysis_320x192 (Room D: Hydroponics & Research)
# -------------------------------------------------------------
def make_lab_station():
    w, h = 320, 192
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    # Stainless steel lab counter (y=75 to 186, x=30 to 290)
    for y in range(75, 186):
        shade = int(55 + (y - 75) * 0.4)
        draw.line([(30, y), (290, y)], fill=(shade, shade + 3, shade + 6, 255))
        height[y, 30:291] = 0.65
    draw.rectangle([30, 75, 290, 186], outline=(20, 24, 30, 255), width=2)

    # Cabinet drawers and doors with chrome handles
    draw.rectangle([35, 82, 110, 130], outline=(30, 35, 42, 255), width=2)
    draw.rectangle([35, 134, 110, 182], outline=(30, 35, 42, 255), width=2)
    draw.line([(65, 104), (80, 104)], fill=(180, 190, 200, 255), width=3) # Handle
    draw.line([(65, 156), (80, 156)], fill=(180, 190, 200, 255), width=3)

    # Centrifuge chamber door in center cabinet
    draw.ellipse([135, 95, 185, 145], fill=(35, 40, 48, 255), outline=(70, 80, 95, 255), width=2)
    draw.ellipse([155, 115, 165, 125], fill=(80, 220, 255, 255)) # Center viewing port

    # Right cabinet: Specimen rack
    draw.rectangle([210, 82, 285, 182], outline=(30, 35, 42, 255), width=2)

    # Tabletop Equipment:
    # 1. High-precision optical microscope on left (x=45 to 85, y=25 to 75)
    # Heavy base
    draw.polygon([(48, 75), (82, 75), (78, 68), (52, 68)], fill=(30, 35, 42, 255))
    # Arm & eyepiece
    draw.line([(65, 68), (75, 42), (65, 30)], fill=(35, 40, 48, 255), width=5)
    draw.line([(65, 30), (55, 24)], fill=(180, 190, 200, 255), width=4) # Eyepiece
    # Objective turret & stage
    draw.rectangle([(56, 52), (72, 55)], fill=(160, 170, 180, 255)) # Stage
    draw.line([(62, 45), (60, 52)], fill=(200, 180, 40, 255), width=3) # Brass objective lens
    height[24:76, 45:86] = 0.9

    # 2. Test tube rack with glowing reagents (x=105 to 155, y=42 to 75)
    draw.rectangle([105, 62, 155, 75], fill=(120, 90, 60, 255), outline=(40, 30, 20, 255)) # Wood rack
    # 5 test tubes
    colors = [(60, 255, 140), (40, 200, 255), (255, 60, 120), (220, 255, 60), (180, 60, 255)]
    for idx, tx in enumerate(range(110, 155, 9)):
        draw.rectangle([tx, 38, tx+5, 68], fill=(220, 240, 255, 120), outline=(150, 180, 210, 200))
        c_reagent = colors[idx % len(colors)]
        draw.rectangle([tx+1, 50, tx+4, 67], fill=c_reagent)
        height[38:69, tx:tx+6] = 0.85

    # 3. Cryogenic cylindrical specimen containment jar (x=225 to 275, y=20 to 75)
    # Metal base & cap
    draw.rectangle([228, 70, 272, 75], fill=(70, 80, 90, 255))
    draw.rectangle([228, 20, 272, 28], fill=(70, 80, 90, 255), outline=(20, 25, 30, 255))
    # Glass cylinder
    draw.rectangle([232, 28, 268, 70], fill=(20, 80, 100, 160), outline=(80, 180, 220, 255))
    # Suspended mutated glowing polar flora specimen inside
    draw.ellipse([244, 40, 256, 56], fill=(140, 255, 100, 240))
    draw.line([(250, 48), (250, 65)], fill=(60, 180, 80, 255), width=2)
    height[20:76, 228:273] = 0.95

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr)
    img.save(os.path.join(out_dir, "Station_Lab_Analysis_320x192.png"))
    norm_img.save(os.path.join(normal_dir, "Station_Lab_Analysis_320x192_NormalMap.png"))
    print("Saved Station_Lab_Analysis_320x192.png & NormalMap")

# -------------------------------------------------------------
# 5. Generator_Diesel_Heavy_384x256 (Room F: Backup Generator Bay)
# -------------------------------------------------------------
def make_diesel_generator():
    w, h = 384, 256
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    # Concrete mounting base pad / steel skids (y=220 to 252, x=25 to 359)
    for y in range(220, 252):
        shade = int(45 + (y - 220) * 0.4)
        draw.line([(25, y), (359, y)], fill=(shade, shade + 3, shade + 5, 255))
        height[y, 25:360] = 0.5
    draw.rectangle([25, 220, 359, 252], outline=(15, 18, 22, 255), width=2)

    # Vibration springs / mounting dampeners
    for vx in [45, 120, 260, 335]:
        draw.rectangle([vx, 214, vx+20, 222], fill=(180, 190, 200, 255), outline=(30, 35, 40, 255))
        height[214:223, vx:vx+21] = 0.65

    # Main Engine Block & Alternator housing (y=80 to 216, x=35 to 349)
    # Heavy industrial olive-yellow machinery
    for y in range(80, 216):
        eng_c = (int(160 - (y-80)*0.25), int(130 - (y-80)*0.2), 25, 255)
        draw.line([(35, y), (349, y)], fill=eng_c)
        height[y, 35:350] = 0.8
    draw.rectangle([35, 80, 349, 216], outline=(20, 22, 18, 255), width=3)

    # Radiator cooling louvers on left (x=45 to 110, y=95 to 200)
    for ly in range(95, 200, 8):
        draw.rectangle([45, ly, 110, ly+4], fill=(25, 28, 30, 255), outline=(10, 12, 15, 255))
        height[ly:ly+5, 45:111] = 0.7

    # Center engine cylinder head covers & fuel injection pipes (x=120 to 240, y=85 to 135)
    for cx in range(130, 235, 26):
        draw.rectangle([cx, 88, cx+20, 130], fill=(45, 50, 58, 255), outline=(20, 24, 28, 255), width=2)
        draw.line([(cx+10, 72), (cx+10, 88)], fill=(190, 150, 50, 255), width=3) # Brass fuel line
        height[88:131, cx:cx+21] = 0.95

    # Alternator cylindrical housing on right (x=245 to 335, y=95 to 205)
    draw.rectangle([245, 95, 335, 205], fill=(70, 75, 82, 255), outline=(25, 28, 32, 255), width=2)
    # Air vent vents
    for ax in range(255, 325, 14):
        draw.line([(ax, 105), (ax, 195)], fill=(30, 32, 36, 255), width=4)

    # Electrical control panel with analog pressure dials & emergency stop mushroom button
    draw.rectangle([140, 145, 220, 205], fill=(25, 30, 36, 255), outline=(60, 70, 80, 255), width=2)
    # 2 round analog gauges (RPM & Oil Pressure)
    draw.ellipse([148, 155, 172, 179], fill=(225, 230, 235, 255), outline=(40, 45, 52, 255), width=2)
    draw.line([(160, 167), (167, 160)], fill=(220, 40, 30, 255), width=2) # Needle
    draw.ellipse([185, 155, 209, 179], fill=(225, 230, 235, 255), outline=(40, 45, 52, 255), width=2)
    draw.line([(197, 167), (192, 160)], fill=(220, 40, 30, 255), width=2)
    # Red Emergency STOP knob
    draw.ellipse([175, 186, 189, 200], fill=(240, 30, 30, 255), outline=(100, 15, 15, 255), width=2)
    height[145:206, 140:221] = 0.9

    # Massive heavy exhaust muffler & stack pipe on top (y=20 to 80, x=75 to 115)
    draw.rectangle([75, 45, 115, 80], fill=(85, 75, 70, 255), outline=(30, 25, 22, 255)) # Muffler canister
    draw.line([(95, 20), (95, 45)], fill=(120, 110, 100, 255), width=8) # Exhaust stack
    # Flapper rain cap on exhaust tip
    draw.line([(86, 18), (104, 20)], fill=(160, 150, 140, 255), width=3)
    height[20:81, 75:116] = 0.9

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr)
    img.save(os.path.join(out_dir, "Generator_Diesel_Heavy_384x256.png"))
    norm_img.save(os.path.join(normal_dir, "Generator_Diesel_Heavy_384x256_NormalMap.png"))
    print("Saved Generator_Diesel_Heavy_384x256.png & NormalMap")

# -------------------------------------------------------------
# 6. Barrels_Fuel_Oil_256x160 (Room F: Fuel Supply Storage)
# -------------------------------------------------------------
def make_fuel_barrels():
    w, h = 256, 160
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    # 3 Oil Drums stacked (2 bottom, 1 top on side or 3 standing)
    # Barrel 1 (Left standing): x=20 to 95, y=55 to 154
    # Barrel 2 (Right standing): x=155 to 230, y=55 to 154
    # Barrel 3 (Center standing slightly forward): x=85 to 165, y=65 to 156
    def draw_barrel(bx, by, bw, bh, color_main, color_rim, h_val):
        for y in range(by, by + bh):
            shade_factor = 1.0 - 0.25 * math.sin((y - by) / float(bh) * math.pi)
            c = (int(color_main[0] * shade_factor), int(color_main[1] * shade_factor), int(color_main[2] * shade_factor), 255)
            draw.line([(bx, y), (bx + bw, y)], fill=c)
            height[y, bx:bx+bw+1] = h_val
        # Metal reinforcement ribs (rings)
        draw.line([(bx, by + int(bh*0.33)), (bx + bw, by + int(bh*0.33))], fill=color_rim, width=3)
        draw.line([(bx, by + int(bh*0.66)), (bx + bw, by + int(bh*0.66))], fill=color_rim, width=3)
        draw.rectangle([bx, by, bx + bw, by + bh], outline=(20, 22, 25, 255), width=2)
        # Top rim lid ellipse
        draw.ellipse([bx, by - 6, bx + bw, by + 8], fill=color_rim, outline=(20, 22, 25, 255), width=2)
        # Bung cap
        draw.ellipse([bx + int(bw*0.3), by - 2, bx + int(bw*0.45), by + 4], fill=(180, 190, 200, 255))

    # Left barrel (Red hazard drum with FLAMMABLE diamond)
    draw_barrel(22, 52, 70, 102, (180, 45, 35), (120, 25, 20), 0.7)
    # Diamond hazard label
    draw.polygon([(48, 95), (60, 83), (72, 95), (60, 107)], fill=(240, 200, 30, 255))

    # Right barrel (Blue chemical drum)
    draw_barrel(160, 52, 70, 102, (35, 90, 160), (20, 55, 110), 0.7)

    # Center barrel (Olive drab military diesel drum) standing in front
    draw_barrel(88, 62, 75, 94, (80, 95, 60), (50, 62, 38), 0.9)
    # Hand rotary siphon pump attached to center barrel bung!
    draw.line([(115, 30), (115, 60)], fill=(160, 170, 180, 255), width=3)
    draw.ellipse([108, 22, 122, 36], fill=(40, 45, 52, 255))
    draw.line([(120, 26), (134, 22)], fill=(220, 60, 40, 255), width=3) # Crank handle
    draw.line([(112, 32), (100, 48), (95, 80)], fill=(30, 30, 35, 255), width=3) # Rubber discharge hose
    height[20:62, 95:135] = 0.95

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr)
    img.save(os.path.join(out_dir, "Barrels_Fuel_Oil_256x160.png"))
    norm_img.save(os.path.join(normal_dir, "Barrels_Fuel_Oil_256x160_NormalMap.png"))
    print("Saved Barrels_Fuel_Oil_256x160.png & NormalMap")

# -------------------------------------------------------------
# 7. Telescope_Observation_Tripod_128x192 (Room G: Outdoor Lookout)
# -------------------------------------------------------------
def make_telescope():
    w, h = 128, 192
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    height = np.zeros((h, w), dtype=np.float32)
    draw = ImageDraw.Draw(img)

    # Tripod legs (y=85 to 188)
    # Center mount hub at x=64, y=85
    # Left leg: (64, 85) to (24, 188)
    # Center leg: (64, 85) to (64, 188)
    # Right leg: (64, 85) to (104, 188)
    draw.line([(64, 85), (24, 188)], fill=(65, 72, 82, 255), width=4)
    draw.line([(64, 85), (64, 188)], fill=(50, 56, 64, 255), width=4)
    draw.line([(64, 85), (104, 188)], fill=(65, 72, 82, 255), width=4)
    # Leg spreaders / brace
    draw.line([(42, 140), (86, 140)], fill=(40, 45, 52, 255), width=2)
    height[85:189, 20:108] = 0.6

    # Mount gimbal & altitude elevation gear
    draw.ellipse([54, 76, 74, 96], fill=(160, 130, 40, 255), outline=(40, 32, 15, 255), width=2) # Brass gear
    height[76:97, 54:75] = 0.85

    # Main Telescope Tube angled pointing up and to the right toward distant glacier mountains
    # Eyepiece at (38, 88), Objective lens at (98, 48)
    # Draw angled barrel
    draw.line([(38, 88), (98, 48)], fill=(190, 160, 50, 255), width=12) # Brass telescope body
    draw.line([(42, 85), (96, 49)], fill=(230, 200, 80, 255), width=3)  # Top specular highlight
    draw.line([(94, 51), (102, 45)], fill=(60, 70, 80, 255), width=16)   # Dew shield / Objective hood
    draw.line([(100, 47), (104, 44)], fill=(80, 220, 255, 255), width=14) # Front cyan lens glint
    # Eyepiece & focuser knob on left
    draw.line([(30, 93), (38, 88)], fill=(40, 45, 52, 255), width=6)
    draw.line([(24, 97), (30, 93)], fill=(20, 22, 26, 255), width=8)    # Rubber eye cup
    height[40:100, 24:106] = 0.95

    # Polar frost & ice rime encrusted on the brass tube
    for ix in [50, 68, 86]:
        draw.line([(ix, 70), (ix-2, 82)], fill=(220, 245, 255, 220), width=2) # Icicle dripping from scope

    alpha_arr = np.array(img)[:, :, 3]
    norm_img = generate_normal_from_height(height, alpha_arr)
    img.save(os.path.join(out_dir, "Telescope_Observation_Tripod_128x192.png"))
    norm_img.save(os.path.join(normal_dir, "Telescope_Observation_Tripod_128x192_NormalMap.png"))
    print("Saved Telescope_Observation_Tripod_128x192.png & NormalMap")

if __name__ == "__main__":
    make_briefing_table()
    make_infirmary_bed()
    make_armoury_rack()
    make_lab_station()
    make_diesel_generator()
    make_fuel_barrels()
    make_telescope()
    print("All 7 room-defining furniture sprites generated successfully!")
