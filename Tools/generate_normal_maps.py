import os
import sys
import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt, sobel, gaussian_filter

def create_normal_map(img_path, out_path, bevel_radius=6.0, shape_weight=0.65, detail_weight=0.35, strength=2.2):
    try:
        img = Image.open(img_path).convert('RGBA')
    except Exception as e:
        print(f"Error reading {img_path}: {e}")
        return False

    arr = np.array(img, dtype=np.float32)
    r, g, b, a = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2], arr[:, :, 3]
    
    mask = a > 16.0
    if not np.any(mask):
        print(f"Skipping empty sprite: {img_path}")
        return False

    lum = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0
    lum_smooth = gaussian_filter(lum, sigma=0.6)

    dist = distance_transform_edt(mask)
    dist_norm = np.clip(dist / max(bevel_radius, 1.0), 0.0, 1.0)
    curve = np.sqrt(np.maximum(0.0, 1.0 - (1.0 - dist_norm) ** 2))

    h = curve * shape_weight + lum_smooth * detail_weight

    # Tangent space is +Y up (green = up, verified in Unity URP 2D 2026-10-05). Image rows run
    # down, so the up-slope is the negated row derivative and the normal tilts against it.
    dx = sobel(h, axis=1) / 8.0
    dy_up = -sobel(h, axis=0) / 8.0

    nx = -dx * strength
    ny = -dy_up * strength
    nz = np.ones_like(nx)

    norm = np.sqrt(nx ** 2 + ny ** 2 + nz ** 2)
    nx /= norm
    ny /= norm
    nz /= norm

    r_out = np.clip((nx * 0.5 + 0.5) * 255.0, 0, 255).astype(np.uint8)
    g_out = np.clip((ny * 0.5 + 0.5) * 255.0, 0, 255).astype(np.uint8)
    b_out = np.clip((nz * 0.5 + 0.5) * 255.0, 0, 255).astype(np.uint8)
    a_out = a.astype(np.uint8)

    r_out[~mask] = 128
    g_out[~mask] = 128
    b_out[~mask] = 255

    res = np.stack([r_out, g_out, b_out, a_out], axis=-1)
    Image.fromarray(res).save(out_path)
    print(f"Generated: {out_path}")
    return True

def process_targets():
    base_dir = r"C:\Users\mokos\My project"
    targets = [
        ("Assets/Environment/Sprites/Level -4", [
            "Pipe01.png", "Pipe02.png", "Pipe03.png", "Pipe04.png", 
            "Pipe05.png", "Pipe06.png", "Pipe07.png", "Pipe08.png", "Pipe09.png",
            "Turbine01.png", "Turbine02.png", "Turbine03.png", 
            "Turbine04.png", "Turbine05.png", "Turbine06.png",
            "Fan.png", "Lamp.png", "Fence.png", "Ladder.png", 
            "Chair and Table.png", "Frame.png"
        ], 6.0, 2.2),
        ("Assets/Environment/Sprites/Level 0", [
            "Level-1__관리 컴퓨터01_dot_302x81.png",
            "Level-1__관리 컴퓨터01 복사_dot_302x81.png",
            "Level0__Bridge_dot_679x42.png",
            "Level0__Bridge 복사_dot_679x42.png",
            "Level1__Laser Beam Projector 복사_dot_693x316.png",
            "Level1__Laser Beam Projector 복사 2_dot_693x316.png",
            "Level1__X 기둥_dot_45x793.png"
        ], 5.0, 2.0),
        ("Assets/Environment/Sprites/Level -2", [
            "조종실 장비__05_삽입한 이미지_dot_275x170.png"
        ], 5.0, 2.0)
    ]

    generated_count = 0
    for folder, files, bevel, strength in targets:
        full_folder = os.path.join(base_dir, folder)
        if not os.path.exists(full_folder):
            continue
        for filename in files:
            src_path = os.path.join(full_folder, filename)
            if not os.path.exists(src_path):
                continue
            base_name, _ = os.path.splitext(filename)
            out_filename = f"{base_name}_Normal.png"
            # Normal maps live in a Normals subfolder next to the art they belong to.
            normal_folder = os.path.join(full_folder, "Normals")
            os.makedirs(normal_folder, exist_ok=True)
            out_path = os.path.join(normal_folder, out_filename)
            
            b_rad = bevel
            if "Pipe" in filename:
                b_rad = 8.0
            
            if create_normal_map(src_path, out_path, bevel_radius=b_rad, strength=strength):
                generated_count += 1

    print(f"\nTotal normal maps successfully generated: {generated_count}")

if __name__ == "__main__":
    process_targets()
