import os
import cv2
import numpy as np

src_dir = r'C:\Users\mokos\Desktop\FNL\New_Sprites\Row\Parry_wind02'
dst_dir = r'Assets\Art\Pixel\Vfx\Parry_Wind'
os.makedirs(dst_dir, exist_ok=True)

files = sorted([f for f in os.listdir(src_dir) if f.endswith('.png')])
print(f'Found {len(files)} source files in {src_dir}')

# 28 frames
for idx, fn in enumerate(files):
    src_path = os.path.join(src_dir, fn)
    img = cv2.imread(src_path).astype(np.float32)
    
    # Background is pure white [255, 255, 255]
    diff = 255.0 - img
    # Red diff is highest for cyan wind
    intensity = np.maximum(diff[:, :, 2], np.maximum(diff[:, :, 1], diff[:, :, 0]))
    
    # Calculate smooth alpha
    alpha = np.clip(intensity / 60.0, 0.0, 1.0)
    alpha = np.power(alpha, 0.75) # enhance midtone wisps
    
    # Luminous cyan/white blade color
    # Core is bright luminous cyan-white
    color_b = np.full_like(alpha, 255.0)
    color_g = 225.0 + 30.0 * (1.0 - alpha)
    color_r = 150.0 + 95.0 * (1.0 - alpha)
    
    rgba = np.zeros((1080, 1920, 4), dtype=np.uint8)
    rgba[:, :, 0] = np.clip(color_b, 0, 255).astype(np.uint8)
    rgba[:, :, 1] = np.clip(color_g, 0, 255).astype(np.uint8)
    rgba[:, :, 2] = np.clip(color_r, 0, 255).astype(np.uint8)
    rgba[:, :, 3] = (alpha * 255.0).astype(np.uint8)
    
    # Crop to centered 1080x1080 square (center x=960 -> 420 to 1500)
    crop = rgba[:, 420:1500]
    
    dst_name = f'Parry_Wind_{idx:04d}.png'
    dst_path = os.path.join(dst_dir, dst_name)
    cv2.imwrite(dst_path, crop)
    print(f'Saved {dst_name} ({crop.shape[1]}x{crop.shape[0]})')

print('All 28 wind frames processed and saved!')
